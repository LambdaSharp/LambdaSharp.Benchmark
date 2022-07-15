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

namespace LambdaSharp.Benchmark.MeasureFunction;

using Amazon.Lambda;
using Amazon.S3;
using LambdaSharp;
using LambdaSharp.Benchmark.Common;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

public class FunctionRequest {

    //--- Properties ---
    public string? LambdaName { get; set; }
    public RunSpec? RunSpec { get; set; }
    public string? Build { get; set; }
    public string? BuildId { get; set; }
}

public class FunctionResponse {

    //--- Properties ---
    public bool Success { get; set; }
    public string? Message { get; set; }
    public bool Continue { get; set; }
    public bool RateExceeded { get; set; }
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

        // validate request
        try {
            ArgumentAssertException.Assert(request.LambdaName is not null);
            ArgumentAssertException.Assert(request.Build is not null);
            ArgumentAssertException.Assert(request.BuildId is not null);
            ArgumentAssertException.Assert(request.BuildId.IndexOf(':') >= 0);
            ArgumentAssertException.Assert(request.RunSpec is not null);
            ArgumentAssertException.Assert(request.RunSpec.Project is not null);
            ArgumentAssertException.Assert(request.RunSpec.Payload is not null);
            ArgumentAssertException.Assert(request.RunSpec.Runtime is not null);
            ArgumentAssertException.Assert(request.RunSpec.Architecture is not null);
            ArgumentAssertException.Assert(request.RunSpec.Tiered is not null);
            ArgumentAssertException.Assert(request.RunSpec.Ready2Run is not null);
            ArgumentAssertException.Assert(request.RunSpec.PreJIT is not null);
        } catch(ArgumentAssertException e) {
            return new() {
                Success = false,
                Message = $"Request validation failed: {e.Message}"
            };
        }
        LogInfo($"Starting measurement for {request.LambdaName}");

        // check if a measurement file already exists
        var buildId = request.BuildId.Split(':', 2)[1];
        var s3MeasurementKey = $"Measurements/{buildId}/{request.Build}.json";
        MeasurementSummary summary;
        var existingMeasurementJson = await ReadFromS3(s3MeasurementKey);
        if(existingMeasurementJson is not null) {
            summary = JsonSerializer.Deserialize<MeasurementSummary>(existingMeasurementJson)
                ?? throw new ApplicationException($"S3 JSON file is not valid ({s3MeasurementKey})");
            LogInfo($"Restored measurements from s3://{BuildBucketName}/{s3MeasurementKey}");
        } else {
            summary = new() {
                Project = request.RunSpec.Project,
                Build = request.Build,
                Runtime = request.RunSpec.Runtime,
                Architecture = request.RunSpec.Architecture,
                MemorySize = request.RunSpec.MemorySize,
                Tiered = request.RunSpec.Tiered,
                Ready2Run = request.RunSpec.Ready2Run,
                PreJIT = request.RunSpec.PreJIT,
                ZipSize = request.RunSpec.ZipSize
            };
        }

        // conduct cold-start performance measurement
        var cancellationTokenSource = new CancellationTokenSource(CurrentContext.RemainingTime - TimeSpan.FromSeconds(60));
        var response = await MeasureColdStartAsync(
            request.LambdaName,
            request.RunSpec.Payload,
            ColdStartSamplesCount,
            WarmStartSamplesCount,
            cancellationTokenSource.Token
        );
        summary.Samples.AddRange(response.Measurements);

        // write measurements JSON to S3 bucket
        await WriteToS3(s3MeasurementKey, LambdaSerializer.Serialize(summary));

        // return successfully
        return new() {
            Success = true,
            Continue = summary.Samples.Count < ColdStartSamplesCount,
            RateExceeded = response.RateExceeded
        };

        // local functions
        async Task WriteToS3(string key, string contents) {
            LogInfo($"Writing measurement file to s3://{BuildBucketName}/{key}");
            await S3Client.PutObjectAsync(new() {
                BucketName = BuildBucketName,
                Key = key,
                ContentBody = contents
            });
        }

        async Task<string?> ReadFromS3(string key) {
            LogInfo($"Reading measurement file from s3://{BuildBucketName}/{key}");
            try {
                var response = await S3Client.GetObjectAsync(new() {
                    BucketName = BuildBucketName,
                    Key = key
                });
                using StreamReader reader = new(response.ResponseStream);
                return await reader.ReadToEndAsync();
            } catch(AmazonS3Exception) {
                return null;
            }
        }
    }

    private async Task<(List<MeasurementSample> Measurements, bool RateExceeded)> MeasureColdStartAsync(
        string functionName,
        string payload,
        int samplesCount,
        int warmStartSamplesCount,
        CancellationToken cancellationToken
    ) {
        var result = new List<MeasurementSample>();
        var failureCount = 0;

        // collect cold-start samples
        for(var iterationCounter = 0; result.Count < samplesCount; ++iterationCounter) {
            var sampleIndex = result.Count + 1;
            if(cancellationToken.IsCancellationRequested) {
                LogInfo($"Cold iteration {sampleIndex}: cancelled");
                return (Measurements: result, RateExceeded: false);
            }
            LogInfo($"Cold iteration {sampleIndex}: starting");

            // update Lambda function configuration to force a cold start
            try {
                var updateConfigurationResponse = await LambdaClient.UpdateFunctionConfigurationAsync(new() {
                    FunctionName = functionName,
                    Environment = new() {
                        Variables = {
                            ["COLD_START_RUN"] = iterationCounter.ToString()
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
                if(++failureCount >= 10) {
                    throw new ApplicationException("Too many failed attempts updating configuration");
                }
                continue;
            } catch(Amazon.Lambda.Model.TooManyRequestsException) {
                LogInfo($"Cold iteration {sampleIndex}: rate exceeded");
                return (Measurements: result, RateExceeded: true);
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
                if(++failureCount >= 10) {
                    throw new ApplicationException("Too many failed measurement attempts");
                }
                goto skip;
            }

            // add cold-start result
            coldStartMeasurement.Sample = sampleIndex;
            var coldStartUsedDuration = coldStartMeasurement.UsedDurations.First();
            LogInfo($"Cold iteration {sampleIndex}: InitDuration={coldStartMeasurement.InitDuration:0.###}ms, UsedDuration={coldStartUsedDuration:0.###}ms");

            // measure warm performance
            var warmUsedDurations = await MeasureWarmUsedDurationsAsync(functionName, payload, warmStartSamplesCount);
            if(warmUsedDurations is null) {

                // abort on too many consecutive failed attempts
                if(++failureCount >= 10) {
                    throw new ApplicationException("Too many failed measurement attempts");
                }
                goto skip;
            }

            // add completed measurement
            coldStartMeasurement.UsedDurations.AddRange(warmUsedDurations);
            result.Add(coldStartMeasurement);

            // reset failure count after successfully measuring the function
            failureCount = 0;
        skip:
            continue;
        }
        return (Measurements: result, RateExceeded: false);
    }

    private async Task<IEnumerable<double>?> MeasureWarmUsedDurationsAsync(string functionName, string payload, int warmStartSamplesCount) {

        // collect warm samples
        var results = new List<double>();
        for(var warmStartSampleIndex = 1; warmStartSampleIndex <= warmStartSamplesCount; ++warmStartSampleIndex) {
            LogInfo($"Warm iteration {warmStartSampleIndex}: starting");

            // invoke Lambda function
            var lambdaResponse = await LambdaClient.InvokeAsync(new() {
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
                return null;
            }

            // add warm result
            var warmStartUsedDuration = warmStartMeasurement.UsedDurations.First();
            results.Add(warmStartUsedDuration);
            LogInfo($"Warm iteration {warmStartSampleIndex}: UsedDuration={warmStartUsedDuration:0.###}ms");
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
