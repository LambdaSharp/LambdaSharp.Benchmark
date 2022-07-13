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

namespace LambdaSharp.Benchmark.DeployFunction;

using Amazon.Lambda;
using Amazon.S3;
using LambdaSharp;
using LambdaSharp.Benchmark.Common;
using System.Text;
using System.Text.RegularExpressions;

public class FunctionRequest {

    //--- Properties ---
    public string? LambdaName { get; set; }
    public RunSpec? RunSpec { get; set; }
    public string? Build { get; set; }
    public int LastCount { get; set; }
    public int PreviousRuns { get; set; }
}

public class FunctionResponse {

    //--- Properties ---
    public bool Success { get; set; }
    public string? Message { get; set; }
    public int PreviousRuns { get; set; }
    public int LastCount { get; set; }
    public string? FunctionName { get; set; }
}

public class MeasurementSummary {

    //--- Properties ---
    public string? Project { get; set; }
    public string? Build { get; set; }
    public string? Runtime { get; set; }
    public string? Architecture { get; set; }
    public int MemorySize { get; set; }
    public string? Tiered { get; set; }
    public string? Ready2Run { get; set; }
    public long ZipSize { get; internal set; }
    public List<MeasurementSample>? Samples { get; set; }
}

public class MeasurementSample {

    //--- Properties ---
    public int Sample { get; internal set; }
    public double? InitDuration { get; set; }
    public List<double> UsedDurations { get; set; } = new();
}

public sealed class Function : ALambdaFunction<FunctionRequest, FunctionResponse> {

    //--- Constants ---
    private const string CHARACTERS = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private const int MAX_LAMBDA_NAME_LENGTH = 64;

    //--- Class Fields ---
    private static Regex _lambdaReportPattern = new(@"REPORT RequestId: (?<RequestId>[\da-f\-]+)\s*Duration: (?<UsedDuration>[\d\.]+) ms\s*Billed Duration: (?<BilledDuration>[\d\.]+) ms\s*Memory Size: (?<MaxMemory>[\d\.]+) MB\s*Max Memory Used: (?<UsedMemory>[\d\.]+) MB\s*(Init Duration: (?<InitDuration>[\d\.]+) ms)?");
    private IAmazonS3? _s3Client;

    //--- Class Methods ---
    private static double Median(IEnumerable<double> numbers) {
        ArgumentAssertException.Assert(numbers.Any());
        var orderedNumbers = numbers.OrderBy(number => number).ToArray();
        var middleIndex = orderedNumbers.Length / 2;
        return ((orderedNumbers.Length & 1) == 0)
            ? (orderedNumbers[middleIndex - 1] + orderedNumbers[middleIndex]) / 2.0
            : orderedNumbers[middleIndex];
    }

    private static (double Average, double StandardDeviation) AverageAndStandardDeviation(IEnumerable<double> numbers) {
        ArgumentAssertException.Assert(numbers.Any());
        var average = numbers.Average();
        var deltaSquaredSum = numbers.Sum(number => (number - average) * (number - average));
        var standardDeviation = Math.Sqrt(deltaSquaredSum / numbers.Count());
        return (average, standardDeviation);
    }

    //--- Fields ---
    private IAmazonLambda? _lambdaClient;
    private string? _buildBucketName;

    //--- Constructors ---
    public Function() : base(new LambdaSharp.Serialization.LambdaSystemTextJsonSerializer()) { }

    //--- Properties ---
    public int ColdStartSamplesCount { get; set; }
    public int WarmStartSamplesCount { get; set; }
    private IAmazonLambda LambdaClient => _lambdaClient ?? throw new InvalidOperationException();
    private string AwsAccountId => CurrentContext.InvokedFunctionArn.Split(':')[4];
    private string BuildBucketName => _buildBucketName ?? throw new InvalidOperationException();
    private IAmazonS3 S3Client => _s3Client ?? throw new InvalidOperationException();

    //--- Methods ---
    public override async Task InitializeAsync(LambdaConfig config) {

        // read configuration settings
        _buildBucketName = config.ReadS3BucketName("BuildBucket");
        ColdStartSamplesCount = int.Parse(config.ReadText("ColdStartSamplesCount"));
        WarmStartSamplesCount = int.Parse(config.ReadText("WarmStartSamplesCount"));

        // initialize clients
        _lambdaClient = new AmazonLambdaClient();
        _s3Client = new AmazonS3Client();
    }

    public override async Task<FunctionResponse> ProcessMessageAsync(FunctionRequest request) {

        // validate run-spec
        try {
            ArgumentAssertException.Assert(request.LambdaName is not null);
            ArgumentAssertException.Assert(request.RunSpec is not null);
        } catch(ArgumentAssertException e) {
            return new() {
                Success = false,
                Message = $"Request validation failed: {e.Message.Replace("request.", "")}"
            };
        }

        // check if Lambda function must be created
        var lambdaName = request.LambdaName;

        // conduct cold-start performance measurement
        var samples = await MeasureAsync(lambdaName, request.RunSpec.Payload, ColdStartSamplesCount - request.PreviousRuns, WarmStartSamplesCount);

        // wait to ensure log groups have been created
        await Task.Delay(TimeSpan.FromSeconds(5));

        // create result file
        MeasurementSummary summary = new() {
            Project = request.RunSpec.Project,
            Build = request.Build,
            Runtime = request.RunSpec.Runtime,
            Architecture = request.RunSpec.Architecture,
            MemorySize = request.RunSpec.MemorySize,
            Tiered = request.RunSpec.Tiered,
            Ready2Run = request.RunSpec.Ready2Run,
            ZipSize = request.RunSpec.ZipSize,
            Samples = samples
        };

        // write measurements JSON to S3 bucket
        var lastCount = request.LastCount + 1;
        await WriteToS3($"measurements/{request.Build}-measurement-{lastCount:00}.json", LambdaSerializer.Serialize(summary));

        // TODO: consider moving this logic to `CombineMeasurementFunction` instead

        // write measurements CSV to S3 bucket
        StringBuilder csv = new();
        List<string> usedDurationColumns = new() {
            "Used"
        };
        for(var i = 1; i <= WarmStartSamplesCount; ++i) {
            usedDurationColumns.Add($"Used-{i:00}");
        }
        AppendCsvLine(
            nameof(MeasurementSummary.Project),
            nameof(MeasurementSummary.Build),
            nameof(MeasurementSummary.Runtime),
            nameof(MeasurementSummary.Architecture),
            nameof(MeasurementSummary.Tiered),
            nameof(MeasurementSummary.Ready2Run),
            nameof(MeasurementSummary.ZipSize),
            nameof(MeasurementSummary.MemorySize),
            nameof(MeasurementSample.Sample),
            "Runs",
            "Init",
            usedDurationColumns,
            "Total Used"
        );
        AppendCsvLine(
            summary.Project,
            summary.Build,
            summary.Runtime,
            summary.Architecture,
            summary.Tiered,
            summary.Ready2Run,
            request.RunSpec.ZipSize.ToString(),
            $"{summary.MemorySize}MB",
            "AVERAGE",
            samples.Count.ToString(),

            // average of all init durations
            samples.Average(sample => sample.InitDuration)?.ToString("0.###"),

            // average by used duration, including cold start used duration
            Enumerable.Range(0, WarmStartSamplesCount + 1).Select(index => samples.Average(sample => sample.UsedDurations.ElementAt(index)).ToString("0.###")),

            // sum of average warm invocation durations
            Enumerable.Range(1, WarmStartSamplesCount).Select(index => samples.Average(sample => sample.UsedDurations.ElementAt(index))).Sum().ToString("0.###")
        );
        await WriteToS3($"measurements/{request.Build}-measurements-average.csv", csv.ToString());

        // return successfully
        return new() {
            Success = true,
            LastCount = lastCount,
            FunctionName = lambdaName,
            PreviousRuns = request.PreviousRuns + samples.Count
        };

        // local functions
        void AppendCsvLine(string? project, string? build, string? runtime, string? architecture, string? tiered, string? ready2run, string? zipSize, string? memory, string? sample, string? runs, string? initDuration, IEnumerable<string> usedDurations, string totalUsed)
            => csv.AppendLine($"{project},{build},{runtime},{architecture},{tiered},{ready2run},{zipSize},{memory},{sample},{runs},{initDuration},{string.Join(",", usedDurations)},{totalUsed}");

        async Task WriteToS3(string key, string contents) {
            LogInfo($"Writing measurement file to s3://{BuildBucketName}/{key}");
            await S3Client.PutObjectAsync(new() {
                BucketName = BuildBucketName,
                Key = key,
                ContentBody = contents
            });
        }
    }

    private async Task<List<MeasurementSample>> MeasureAsync(string functionName, string payload, int samplesCount, int warmStartSamplesCount) {
        var result = new List<MeasurementSample>();
        var failureCount = 0;

        // collect cold-start samples
        for(var sampleIndex = 1; sampleIndex <= samplesCount; ++sampleIndex) {
            LogInfo($"Iteration {sampleIndex}.0");

            // update Lambda function configuration to force a cold start
            try {
                var updateConfigurationResponse = await LambdaClient.UpdateFunctionConfigurationAsync(new() {
                    FunctionName = functionName,
                    Environment = new() {
                        Variables = {
                            ["COLDSTART_RUN"] = sampleIndex.ToString()
                        }
                    }
                });
                if(updateConfigurationResponse.LastUpdateStatus == LastUpdateStatus.Failed) {
                    throw new ApplicationException("Unable to update Lambda configuration");
                }
            } catch(Amazon.Lambda.Model.ResourceConflictException) {

                // Lambda function is not yet ready to be updated; wait and try again
                await Task.Delay(TimeSpan.FromSeconds(2));
                await WaitForFunctionToBeReady(functionName);
                ++samplesCount;
                if(++failureCount >= 10) {
                    throw new ApplicationException("Too many failed attempts updating configuration");
                }
                continue;
            }

            // wait for Lambda configuration changes to propagate
            await WaitForFunctionToBeReady(functionName);

            // invoke Lambda function
            var lambdaResponse = await LambdaClient.InvokeAsync(new() {
                FunctionName = functionName,
                Payload = payload,
                InvocationType = InvocationType.RequestResponse,
                LogType = LogType.Tail
            });
            if(!string.IsNullOrEmpty(lambdaResponse.FunctionError)) {
                throw new ApplicationException($"Lambda function start-up failed: {lambdaResponse.FunctionError}");
            }
            var coldStartMeasurement = ParseLambdaReportFromLogResult(lambdaResponse.LogResult);
            if(
                (coldStartMeasurement.InitDuration is null)
                || !coldStartMeasurement.UsedDurations.Any()
            ) {
                LogInfo($"Lambda invocation did not report a cold-start. Trying again.");

                // invocation didn't cause a cold-start; add an additional run
                ++samplesCount;
                if(++failureCount >= 10) {
                    throw new ApplicationException("Too many failed measurement attempts");
                }
                goto skip;
            }

            // add cold-start result
            coldStartMeasurement.Sample = sampleIndex;
            var coldStartUsedDuration = coldStartMeasurement.UsedDurations.First();
            LogInfo($"Cold-Start: Iteration={sampleIndex}.0, InitDuration={coldStartMeasurement.InitDuration:0.###}ms, UsedDuration={coldStartUsedDuration:0.###}ms");

            // collect warm-start samples
            for(var warmStartSampleIndex = 1; warmStartSampleIndex <= warmStartSamplesCount; ++warmStartSampleIndex) {
                LogInfo($"Iteration {sampleIndex}.{warmStartSampleIndex}");

                // invoke Lambda function
                lambdaResponse = await LambdaClient.InvokeAsync(new() {
                    FunctionName = functionName,
                    Payload = payload,
                    InvocationType = InvocationType.RequestResponse,
                    LogType = LogType.Tail
                });
                var warmStartMeasurement = ParseLambdaReportFromLogResult(lambdaResponse.LogResult);
                if(!string.IsNullOrEmpty(lambdaResponse.FunctionError)) {
                    throw new ApplicationException($"Lambda function invocation failed: {lambdaResponse.FunctionError}");
                }
                if(
                    (warmStartMeasurement.InitDuration is not null)
                    || !string.IsNullOrEmpty(lambdaResponse.FunctionError)
                    || !warmStartMeasurement.UsedDurations.Any()
                ) {
                    LogInfo($"Lambda invocation reported a cold-start. Aborting warm-start sampling.");

                    // invocation caused a cold-start; add an additional run
                    ++samplesCount;
                    if(++failureCount >= 10) {
                        throw new ApplicationException("Too many failed measurement attempts");
                    }
                    goto skip;
                }

                // add warm-start result
                var warmStartUsedDuration = warmStartMeasurement.UsedDurations.First();
                coldStartMeasurement.UsedDurations.Add(warmStartUsedDuration);
                LogInfo($"Warm-Start: Iteration={sampleIndex}.{warmStartSampleIndex}, UsedDuration={warmStartUsedDuration:0.###}ms");
            }

            // add completed measurement
            result.Add(coldStartMeasurement);

            // reset failure count after successfully measuring the function
            failureCount = 0;
        skip:
            continue;
        }
        return result;
    }

    private async Task WaitForFunctionToBeReady(string functionName) {
        do {

            // wait for function to stabilize
            await Task.Delay(TimeSpan.FromSeconds(1));

            // fetch current function state
            var getFunctionResponse = await LambdaClient.GetFunctionAsync(functionName);
            var state = getFunctionResponse.Configuration.State;
            if((state == State.Active) || (state == State.Inactive)) {

                // function is ready to go
                return;
            }
            if(state == State.Pending) {

                // try again
                continue;
            }
            if(state == State.Failed) {

                // function is in an unusable state
                throw new ApplicationException($"Lambda is in failed state");
            }
            throw new ApplicationException($"Unexpected Lambda state: {state}");
        } while(true);
    }

    private MeasurementSample ParseLambdaReportFromLogResult(string logResult) {

        // Sample Log Result: REPORT RequestId: 7234b561-1e51-45f4-a031-a71b9836f038	Duration: 327.16 ms	Billed Duration: 328 ms	Memory Size: 256 MB	Max Memory Used: 61 MB	Init Duration: 243.54 ms

        var decodedLogResult = Encoding.UTF8.GetString(Convert.FromBase64String(logResult));
        var match = _lambdaReportPattern.Match(decodedLogResult);
        if(match.Success) {
            var usedDuration = double.Parse(match.Groups["UsedDuration"].Value);
            double? initDuration = match.Groups["InitDuration"].Success
                ? double.Parse(match.Groups["InitDuration"].Value)
                : null;
            return new() {
                InitDuration = initDuration,
                UsedDurations = { usedDuration }
            };
        }
        return new();
    }
}
