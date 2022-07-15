/*
 * LambdaSharp (λ#)
 * Copyright (C) 2018-2022
 * lambdasharp.net
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

namespace LambdaSharp.Benchmark.CombineMeasurementsFunction;

using System.Text;
using System.Text.Json;
using Amazon.S3;
using LambdaSharp;
using LambdaSharp.Benchmark.Common;

public class FunctionRequest {

    //--- Properties ---
    public string? BuildId { get; set; }
}

public class FunctionResponse {

    //--- Properties ---
    public string? MeasurementFile { get; set; }
}

public sealed class Function : ALambdaFunction<FunctionRequest, FunctionResponse> {

    //--- Fields ---
    private string? _buildBucketName;
    private IAmazonS3? _s3Client;

    //--- Constructors ---
    public Function() : base(new LambdaSharp.Serialization.LambdaSystemTextJsonSerializer()) { }

    //--- Properties ---
    private string BuildBucketName => _buildBucketName ?? throw new InvalidOperationException();
    private IAmazonS3 S3Client => _s3Client ?? throw new InvalidOperationException();

    //--- Methods ---
    public override async Task InitializeAsync(LambdaConfig config) {

        // read configuration settings
        _buildBucketName = config.ReadS3BucketName("BuildBucket");

        // initialize clients
        _s3Client = new AmazonS3Client();
    }

    public override async Task<FunctionResponse> ProcessMessageAsync(FunctionRequest request) {
        ArgumentAssertException.Assert(request.BuildId is not null);
        ArgumentAssertException.Assert(request.BuildId.IndexOf(':') >= 0);
        var buildId = request.BuildId.Split(':', 2)[1];

        // find all measurements JSON files in S3 bucket
        LogInfo($"Process measurements for: {buildId}");
        var s3MeasurementPrefix = $"Measurements/{buildId}/";
        var listObjectsResponse = await S3Client.ListObjectsV2Async(new() {
            BucketName = BuildBucketName,
            Prefix = s3MeasurementPrefix,
            Delimiter = "/"
        });
        var foundMeasurementFiles = listObjectsResponse.S3Objects
            .Where(s3Object => s3Object.Key.EndsWith(".json", StringComparison.Ordinal))
            .ToList();
        LogInfo($"Found {foundMeasurementFiles.Count:N0} files to process");
        if(foundMeasurementFiles.Count == 0) {
            throw new ApplicationException($"Could not find any files to process for {buildId}");
        }

        // read all JSON measurement files
        var measurements = new List<MeasurementSummary>();
        foreach(var measurementFile in foundMeasurementFiles) {

            // read run-spec from S3 bucket
            var getObjectResponse = await S3Client.GetObjectAsync(new() {
                BucketName = BuildBucketName,
                Key = measurementFile.Key
            });

            // add ZipFile location
            using StreamReader reader = new(getObjectResponse.ResponseStream);
            var text = await reader.ReadToEndAsync();
            var measurement = JsonSerializer.Deserialize<MeasurementSummary>(text)
                ?? throw new ApplicationException($"S3 JSON file is not valid ({measurementFile.Key})");
            measurements.Add(measurement);
        }

        // combine measurements
        StringBuilder csv = new();
        var warmStartSamplesCount = measurements.First().Samples.Count - 1;
        List<string> usedDurationColumns = new() {
            "Used"
        };
        for(var i = 1; i <= warmStartSamplesCount; ++i) {
            usedDurationColumns.Add($"Used-{i:00}");
        }
        AppendCsvLine(
            nameof(MeasurementSummary.Project),
            nameof(MeasurementSummary.Build),
            nameof(MeasurementSummary.Runtime),
            nameof(MeasurementSummary.Architecture),
            nameof(MeasurementSummary.Tiered),
            nameof(MeasurementSummary.Ready2Run),
            nameof(MeasurementSummary.PreJIT),
            nameof(MeasurementSummary.ZipSize),
            nameof(MeasurementSummary.MemorySize),
            "Runs",
            "Init",
            "Cold Used",
            "Total Warm Used",
            usedDurationColumns
        );
        foreach(var measurement in measurements) {
            AppendCsvLine(
                measurement.Project,
                measurement.Build,
                measurement.Runtime,
                measurement.Architecture,
                measurement.Tiered,
                measurement.Ready2Run,
                measurement.PreJIT,
                measurement.ZipSize.ToString(),
                $"{measurement.MemorySize}MB",
                measurement.Samples.Count.ToString(),

                // average of all init durations
                measurement.Samples.Average(sample => sample.InitDuration)?.ToString("0.###"),

                // average of all cold used durations
                measurement.Samples.Average(sample => sample.UsedDurations[0]).ToString("0.###"),

                // sum of average warm invocation durations
                Enumerable.Range(1, warmStartSamplesCount).Select(index => measurement.Samples.Average(sample => sample.UsedDurations.ElementAt(index))).Sum().ToString("0.###"),

                // INDIVIDUAL WARM USED

                // average by warm used duration
                Enumerable.Range(1, warmStartSamplesCount).Select(index => measurement.Samples.Average(sample => sample.UsedDurations.ElementAt(index)).ToString("0.###"))
            );
        }

        // write combined CSV file back to be co-located with original project file
        var resultPath = $"Reports/{measurements.First().Project} ({DateTime.UtcNow:yyyy-MM-dd}).csv";
        LogInfo($"Writing combined measurement file to s3://{resultPath}");
        await S3Client.PutObjectAsync(new() {
            BucketName = _buildBucketName,
            Key = resultPath,
            ContentBody = csv.ToString()
        });
        return new() {
            MeasurementFile = resultPath
        };

        // local functions
        void AppendCsvLine(
            string? project,
            string? build,
            string? runtime,
            string? architecture,
            string? tiered,
            string? ready2run,
            string? preJIT,
            string? zipSize,
            string? memory,
            string? runs,
            string? initDuration,
            string? usedColdDuration,
            string? totalWarmUsed,
            IEnumerable<string> usedWarmDurations
        )
            => csv.AppendLine($"{project},{build},{runtime},{architecture},{tiered},{ready2run},{preJIT},{zipSize},{memory},{runs},{initDuration},{usedColdDuration},{totalWarmUsed},{string.Join(",", usedWarmDurations)}");
    }
}
