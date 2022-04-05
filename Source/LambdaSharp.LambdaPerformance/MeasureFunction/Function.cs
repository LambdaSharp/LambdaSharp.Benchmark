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

namespace LambdaSharp.LambdaPerformance.DeployFunction;

using Amazon.Lambda;
using Amazon.CloudWatchLogs;
using Amazon.S3;
using LambdaSharp;
using System.Text;
using System.Text.RegularExpressions;
using LambdaSharp.LambdaPerformance.Common;

public class FunctionRequest {

    //--- Properties ---
    public string? RunSpec { get; set; }
}

public class FunctionResponse {

    //--- Properties ---
    public bool Success { get; set; }
    public string? Message { get; set; }
}

public class MeasurementSummary {

    //--- Properties ---
    public string? Project { get; set; }
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
    public int ColdStartSample { get; internal set; }
    public int WarmStartSample { get; internal set; }
    public bool Success { get; set; }
    public double InitDuration { get; set; }
    public double UsedDuration { get; set; }
    public double TotalDuration { get; set; }
}

public sealed class Function : ALambdaFunction<FunctionRequest, FunctionResponse> {

    //--- Constants ---
    private const string CHARACTERS = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private const int MAX_LAMBDA_NAME_LENGTH = 64;

    //--- Class Fields ---
    private static Random _random = new();
    private static Regex _lambdaReportPattern = new(@"REPORT RequestId: (?<RequestId>[\da-f\-]+)\s*Duration: (?<UsedDuration>[\d\.]+) ms\s*Billed Duration: (?<BilledDuration>[\d\.]+) ms\s*Memory Size: (?<MaxMemory>[\d\.]+) MB\s*Max Memory Used: (?<UsedMemory>[\d\.]+) MB\s*(Init Duration: (?<InitDuration>[\d\.]+) ms)?");
    private IAmazonS3? _s3Client;

    //--- Class Methods ---
    private static string RandomString(int length)
        => new string(Enumerable.Range(0, length).Select(_ => CHARACTERS[_random.Next(CHARACTERS.Length)]).ToArray());

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
    private IAmazonCloudWatchLogs? _logsClient;
    private string? _buildBucketName;

    //--- Constructors ---
    public Function() : base(new LambdaSharp.Serialization.LambdaSystemTextJsonSerializer()) { }

    //--- Properties ---
    public int ColdStartSamplesCount { get; set; }
    public int WarmStartSamplesCount { get; set; }
    private IAmazonLambda LambdaClient => _lambdaClient ?? throw new InvalidOperationException();
    private IAmazonCloudWatchLogs LogsClient => _logsClient ?? throw new InvalidOperationException();
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
        _logsClient = new AmazonCloudWatchLogsClient();
        _s3Client = new AmazonS3Client();
    }

    public override async Task<FunctionResponse> ProcessMessageAsync(FunctionRequest request) {

        // validate run-spec
        try {
            ArgumentAssertException.Assert(request.RunSpec is not null);
        } catch(ArgumentAssertException e) {
            return new() {
                Success = false,
                Message = $"Request validation failed: {e.Message.Replace("request.", "")}"
            };
        }

        // load run-spec from S3
        LogInfo($"Loading run-spec s3://{BuildBucketName}/{request.RunSpec}");
        var getRunSpecObjectResponse = await S3Client.GetObjectAsync(new() {
            BucketName = BuildBucketName,
            Key = request.RunSpec
        });
        var runSpec = LambdaSerializer.Deserialize<RunSpec>(getRunSpecObjectResponse.ResponseStream);
        try {
            ArgumentAssertException.Assert(runSpec.Project is not null);
            ArgumentAssertException.Assert(runSpec.Handler is not null);
            ArgumentAssertException.Assert(runSpec.ZipFile is not null);
            ArgumentAssertException.Assert(runSpec.Runtime is not null);
            ArgumentAssertException.Assert(runSpec.Architecture is not null);
            ArgumentAssertException.Assert(runSpec.MemorySize is >= 128 and <= 1769);
            ArgumentAssertException.Assert(runSpec.Payload is not null);
        } catch(ArgumentAssertException e) {
            return new() {
                Success = false,
                Message = $"Request validation failed: {e.Message.Replace("runSpec.", "")}"
            };
        }

        // create Lambda function
        var functionNamePrefix = $"{Info.ModuleId}-Test-";
        var functionName = functionNamePrefix + RandomString(MAX_LAMBDA_NAME_LENGTH - functionNamePrefix.Length);
        List<MeasurementSample>? samples;
        try {

            // create Lambda function
            LogInfo($"Create Lambda function: {functionName}");
            await LambdaClient.CreateFunctionAsync(new() {
                Architectures = {
                    runSpec.Architecture
                },
                Timeout = 30,
                Runtime = runSpec.Runtime,
                PackageType = PackageType.Zip,
                MemorySize = runSpec.MemorySize,
                FunctionName = functionName,
                Description = $"Measuring {runSpec.ZipFile} (Memory: {runSpec.MemorySize}, Arch: {runSpec.Architecture}, Runtime: {runSpec.Runtime}, Tiered: {runSpec.Tiered}, Ready2Run: {runSpec.Ready2Run})",
                Code = new() {
                    S3Bucket = BuildBucketName,
                    S3Key = runSpec.ZipFile
                },
                Handler = runSpec.Handler,
                Role = $"arn:aws:iam::{AwsAccountId}:role/LambdaDefaultRole"
            });

            // conduct cold-start performance measurement
            try {
                samples = await MeasureAsync(functionName, runSpec.Payload, ColdStartSamplesCount, WarmStartSamplesCount);
            } catch(Exception e) {
                LogErrorAsInfo(e, "Lambda invocation failed; aborting measurement check");

                // TODO: should we let the exception bubble up instead?

                // return error response
                return new() {
                    Success = false,
                    Message = "Performance test failed (see log for more details)"
                };
            }
        } catch(Exception e) {
            LogErrorAsInfo(e, "Lambda creation failed; aborting measurement check");

            // return error response
            return new() {
                Success = false,
                Message = "Performance test failed (see log for more details)"
            };
        } finally {

            // wait to ensure log groups have been created
            await Task.Delay(TimeSpan.FromSeconds(5));

            // delete Lambda function
            try {
                LogInfo($"Delete Lambda function: {functionName}");
                await LambdaClient.DeleteFunctionAsync(functionName);
            } catch(Exception e) {
                LogErrorAsInfo(e, $"Unable to delete function: {functionName}");
            }

            // delete log group, which is automatically created by Lambda invocation
            var logGroupName = $"/aws/lambda/{functionName}";
            try {
                LogInfo($"Delete log group: {logGroupName}");
                await LogsClient.DeleteLogGroupAsync(new() {
                    LogGroupName = logGroupName
                });
            } catch(Amazon.CloudWatchLogs.Model.ResourceNotFoundException) {

                // nothing to do
                LogInfo($"Log group does not exist: {logGroupName}");
            } catch(Exception e) {
                LogErrorAsInfo(e, $"Unable to delete log group: {logGroupName}");
            }
        }

        // create result file
        MeasurementSummary summary = new() {
            Project = runSpec.Project,
            Runtime = runSpec.Runtime,
            Architecture = runSpec.Architecture,
            MemorySize = runSpec.MemorySize,
            Tiered = runSpec.Tiered,
            Ready2Run = runSpec.Ready2Run,
            ZipSize = runSpec.ZipSize,
            Samples = samples
        };

        // write measurements JSON to S3 bucket
        await WriteToS3(Path.ChangeExtension(request.RunSpec, extension: null) + "-measurement.json", LambdaSerializer.Serialize(summary));

        // write measurements CSV to S3 bucket
        StringBuilder csv = new();
        AppendCsvLine(nameof(MeasurementSummary.Project), nameof(MeasurementSummary.Runtime), nameof(MeasurementSummary.Architecture), nameof(MeasurementSummary.Tiered), nameof(MeasurementSummary.Ready2Run), nameof(MeasurementSummary.ZipSize), nameof(MeasurementSummary.MemorySize), nameof(MeasurementSample.ColdStartSample), nameof(MeasurementSample.WarmStartSample), nameof(MeasurementSample.UsedDuration), nameof(MeasurementSample.InitDuration), nameof(MeasurementSample.TotalDuration));
        foreach(var sample in samples) {
            AppendCsvLine(summary.Project, summary.Runtime, summary.Architecture, summary.Tiered, summary.Ready2Run, runSpec.ZipSize.ToString(), summary.MemorySize.ToString(), sample.ColdStartSample.ToString(), sample.WarmStartSample.ToString(), sample.UsedDuration.ToString(), sample.InitDuration.ToString(), sample.TotalDuration.ToString());
        }
        await WriteToS3(Path.ChangeExtension(request.RunSpec, extension: null) + "-measurement.csv", csv.ToString());

        // return successfully
        return new() {
            Success = true
        };

        // local functions
        void AppendCsvLine(string? project, string? runtime, string? architecture, string? tiered, string? ready2run, string? zipSize, string? memory, string? coldStartSample, string? warmStartSample, string? usedDuration, string? initDuration, string? totalDuration)
            => csv.AppendLine($"{project},{runtime},{architecture},{tiered},{ready2run},{zipSize},{memory},{coldStartSample},{warmStartSample},{usedDuration},{initDuration},{totalDuration}");

        async Task WriteToS3(string key, string contents) {
            LogInfo($"Writing measurement file to s3://{BuildBucketName}/{key}");
            await S3Client.PutObjectAsync(new() {
                BucketName = BuildBucketName,
                Key = key,
                ContentBody = contents
            });
        }
    }

    private async Task<List<MeasurementSample>> MeasureAsync(string functionName, string payload, int coldStartSamplesCount, int warmStartSamplesCount) {
        var results = new List<MeasurementSample>();

        // wait for Lambda creation to complete
        await WaitForFunctionToBeReady(functionName);

        // collect cold-start samples
        for(var coldStartSample = 1; coldStartSample <= coldStartSamplesCount; ++coldStartSample) {
            LogInfo($"Iteration {coldStartSample}.0");

            // update Lambda function configuration to force a cold start
            await LambdaClient.UpdateFunctionConfigurationAsync(new() {
                FunctionName = functionName,
                Environment = new() {
                    Variables = {
                        ["COLDSTART_RUN"] = coldStartSample.ToString()
                    }
                }
            });

            // wait for Lambda configuration changes to propagate
            await WaitForFunctionToBeReady(functionName);

            // invoke Lambda function
            var lambdaResponse = await LambdaClient.InvokeAsync(new() {
                FunctionName = functionName,
                Payload = payload,
                InvocationType = InvocationType.RequestResponse,
                LogType = LogType.Tail
            });
            var result = ParseLambdaReportFromLogResult(lambdaResponse.LogResult);
            if(result.InitDuration == 0.0) {
                LogInfo($"Lambda invocation did not report a cold-start. Trying again.");

                // invocation didn't cause a cold-start; add an additional run
                ++coldStartSamplesCount;
                continue;
            }

            // add cold-start result
            result.ColdStartSample = coldStartSample;
            result.WarmStartSample = 0;
            result.Success = string.IsNullOrEmpty(lambdaResponse.FunctionError);
            results.Add(result);
            LogInfo($"Cold-Start: Iteration={coldStartSample}.0, InitDuration={result.InitDuration * 1000.0:0.###}ms, UsedDuration={result.UsedDuration * 1000.0:0.###}ms");

            // collect warm-start samples
            for(var warmStartSample = 1; warmStartSample <= warmStartSamplesCount; ++warmStartSample) {
                LogInfo($"Iteration {coldStartSample}.{warmStartSample}");

                // invoke Lambda function
                lambdaResponse = await LambdaClient.InvokeAsync(new() {
                    FunctionName = functionName,
                    Payload = payload,
                    InvocationType = InvocationType.RequestResponse,
                    LogType = LogType.Tail
                });
                result = ParseLambdaReportFromLogResult(lambdaResponse.LogResult);
                if(result.InitDuration > 0.0) {
                    LogInfo($"Lambda invocation reported a cold-start. Aborting warm sampling.");
                    break;
                }

                // add warm-start result
                result.ColdStartSample = coldStartSample;
                result.WarmStartSample = warmStartSample;
                result.Success = string.IsNullOrEmpty(lambdaResponse.FunctionError);
                results.Add(result);
                LogInfo($"Warm-Start: Iteration={coldStartSample}.{warmStartSample}, UsedDuration={result.UsedDuration * 1000.0:0.###}ms");
            }
        }
        return results;
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
            var usedDuration = double.Parse(match.Groups["UsedDuration"].Value) / 1000.0;
            var initDuration = match.Groups["InitDuration"].Success
                ? double.Parse(match.Groups["InitDuration"].Value) / 1000.0
                : 0.0;
            return new() {
                UsedDuration = usedDuration,
                InitDuration = initDuration,
                TotalDuration = usedDuration + initDuration
            };
        }
        return new() {
            UsedDuration = 0.0,
            InitDuration = 0.0,
            TotalDuration = 0.0
        };
    }
}
