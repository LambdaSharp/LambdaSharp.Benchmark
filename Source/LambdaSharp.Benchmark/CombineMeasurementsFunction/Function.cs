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
using Amazon.S3;
using LambdaSharp;

public class FunctionRequest {

    //--- Properties ---
    public string? ProjectPath { get; set; }
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
        ArgumentAssertException.Assert(request.ProjectPath is not null);
        ArgumentAssertException.Assert(request.BuildId is not null);
        ArgumentAssertException.Assert(request.BuildId.IndexOf(':') >= 0);
        LogInfo($"Combine measurements for BuildId: {request.BuildId}");

        // return list of all build artifacts
        var buildId = request.BuildId.Split(':', 2)[1];
        var pathPrefix = $"Build/{buildId}/";
        LogInfo($"Finding all measurements at s3://{BuildBucketName}/{pathPrefix}");
        var listObjectsResponse = await S3Client.ListObjectsV2Async(new() {
            BucketName = BuildBucketName,
            Prefix = pathPrefix,
            Delimiter = "/"
        });

        // read all CSV measurement files and combine them
        StringBuilder combinedCsv = new();
        foreach(var runSpecObject in listObjectsResponse.S3Objects.Where(s3Object => s3Object.Key.EndsWith(".csv", StringComparison.Ordinal))) {

            // read run-spec from S3 bucket
            var getCsvObjectResponse = await S3Client.GetObjectAsync(new() {
                BucketName = BuildBucketName,
                Key = runSpecObject.Key
            });

            // add ZipFile location
            using StreamReader reader = new(getCsvObjectResponse.ResponseStream);
            var csv = await reader.ReadToEndAsync();
            if(combinedCsv.Length > 0) {
                combinedCsv.Append(string.Join('\n', csv.Split('\n').Skip(1)));
            } else {
                combinedCsv.Append(csv);
            }
        }

        // write combined CSV file back to be co-located with original project file
        var resultPath = $"Reports/{Path.GetFileNameWithoutExtension(request.ProjectPath)} ({DateTime.UtcNow:yyyy-MM-dd}) [{buildId}].csv";
        LogInfo($"Writing combined measurement file to s3://{resultPath}");
        await S3Client.PutObjectAsync(new() {
            BucketName = _buildBucketName,
            Key = resultPath,
            ContentBody = combinedCsv.ToString()
        });
        return new() {
            MeasurementFile = resultPath
        };
    }
}
