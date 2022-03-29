namespace LambdaSharp.LambdaPerformance.DeployFunction;

using Amazon.Lambda;
using Amazon.CloudWatchLogs;
using Amazon.S3;
using LambdaSharp;
using System.Text;
using System.Text.RegularExpressions;

public class FunctionRequest {

    //--- Properties ---
    public string? Project { get; set; }
    public string? Handler { get; set; }
    public string? ZipFile { get; set; }
    public string? Runtime { get; set; }
    public string? Architecture { get; set; }
    public int MemorySize { get; set; }
    public int Runs { get; set; }
    public string? Payload { get; set; }
}

public class FunctionResponse {

    //--- Properties ---
    public bool Success { get; set; }
    public string? Message { get; set; }
    public double InitDurationMax { get; set; }
    public double InitDurationMin { get; set; }
    public double InitDurationAverage { get; set; }
    public double InitDurationStdDev { get; set; }
    public double InitDurationMedian { get; set; }
    public double UsedDurationMax { get; set; }
    public double UsedDurationMin { get; set; }
    public double UsedDurationAverage { get; set; }
    public double UsedDurationStdDev { get; set; }
    public double UsedDurationMedian { get; set; }
    public List<TestResult>? Details { get; set; }
}

public class TestResult {

    //--- Properties ---
    public int Run { get; set; }
    public bool Success { get; set; }
    public double InitDuration { get; set; }
    public double UsedDuration { get; set; }
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
    private IAmazonLambda LambdaClient => _lambdaClient ?? throw new InvalidOperationException();
    private IAmazonCloudWatchLogs LogsClient => _logsClient ?? throw new InvalidOperationException();
    private string AwsAccountId => CurrentContext.InvokedFunctionArn.Split(':')[4];
    private string BuildBucketName => _buildBucketName ?? throw new InvalidOperationException();
    private IAmazonS3 S3Client => _s3Client ?? throw new InvalidOperationException();

    //--- Methods ---
    public override async Task InitializeAsync(LambdaConfig config) {

        // read configuration settings
        _buildBucketName = config.ReadS3BucketName("CodeBuild::ArtifactBucket");

        // initialize clients
        _lambdaClient = new AmazonLambdaClient();
        _logsClient = new AmazonCloudWatchLogsClient();
        _s3Client = new AmazonS3Client();
    }

    public override async Task<FunctionResponse> ProcessMessageAsync(FunctionRequest request) {

        // validate request
        try {
            ArgumentAssertException.Assert(request.Handler is not null);
            ArgumentAssertException.Assert(request.ZipFile is not null);
            ArgumentAssertException.Assert(request.Runtime is not null);
            ArgumentAssertException.Assert(request.Architecture is not null);
            ArgumentAssertException.Assert(request.MemorySize is >= 128 and <= 1769);
            ArgumentAssertException.Assert(request.Runs is >= 1 and <= 100);
            ArgumentAssertException.Assert(request.Payload is not null);
        } catch(ArgumentAssertException e) {
            return new() {
                Success = false,
                Message = $"Request validation failed: {e.Message.Replace("request.", "")}"
            };
        }

        // create Lambda function
        var functionNamePrefix = $"{Info.ModuleId}-Test-";
        var functionName = functionNamePrefix + RandomString(MAX_LAMBDA_NAME_LENGTH - functionNamePrefix.Length);
        List<TestResult>? results;
        try {

            // create Lambda function
            LogInfo($"Create Lambda function: {functionName}");
            await LambdaClient.CreateFunctionAsync(new() {
                Architectures = {
                    request.Architecture
                },
                Timeout = 30,
                Runtime = request.Runtime,
                PackageType = PackageType.Zip,
                MemorySize = request.MemorySize,
                FunctionName = functionName,
                Description = $"Testing {request.ZipFile} (Memory: {request.MemorySize}, Arch: {request.Architecture}, Runtime: {request.Runtime})",
                Code = new() {
                    S3Bucket = BuildBucketName,
                    S3Key = request.ZipFile
                },
                Handler = request.Handler,
                Role = $"arn:aws:iam::{AwsAccountId}:role/LambdaDefaultRole"
            });

            // conduct cold-start performance expirement
            try {
                results = await PerformanceTestAsync(functionName, request.Payload, request.Runs);
            } catch(Exception e) {
                LogErrorAsInfo(e, "Lambda invocation failed; aborting peformance check");

                // return error response
                return new() {
                    Success = false,
                    Message = "Performance test failed (see log for more details)"
                };
            }
        } catch(Exception e) {
            LogErrorAsInfo(e, "Lambda creation failed; aborting peformance check");

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
        var initDurationAverageAndStandardDeviation = AverageAndStandardDeviation(results.Select(result => result.InitDuration));
        var usedDurationAverageAndStandardDeviation = AverageAndStandardDeviation(results.Select(result => result.UsedDuration));
        FunctionResponse response = new() {
            Success = true,
            InitDurationMin = results.Min(result => result.InitDuration),
            InitDurationMax = results.Max(result => result.InitDuration),
            InitDurationAverage = initDurationAverageAndStandardDeviation.Average,
            InitDurationStdDev = initDurationAverageAndStandardDeviation.StandardDeviation,
            InitDurationMedian = Median(results.Select(result => result.InitDuration)),
            UsedDurationMin = results.Min(result => result.UsedDuration),
            UsedDurationMax = results.Max(result => result.UsedDuration),
            UsedDurationAverage = usedDurationAverageAndStandardDeviation.Average,
            UsedDurationStdDev = usedDurationAverageAndStandardDeviation.StandardDeviation,
            UsedDurationMedian = Median(results.Select(result => result.UsedDuration)),
            Details = results
        };

        // write measuremnts to S3 bucket
        var measurementFile = Path.ChangeExtension(Path.ChangeExtension(request.ZipFile, extension: null) + "-measurement", ".json");
        LogInfo($"Writing result file: {measurementFile}");
        await S3Client.PutObjectAsync(new() {
            BucketName = _buildBucketName,
            Key = measurementFile,
            ContentBody = LambdaSerializer.Serialize(response)
        });
        return response;
    }

    private async Task<List<TestResult>> PerformanceTestAsync(string functionName, string payload, int runs) {
        var results = new List<TestResult>();

        // wait for Lambda creation to complete
        await WaitForFunctionToBeReady(functionName);

        // run tests
        for(var i = 1; i <= runs; ++i) {
            LogInfo($"LambdaPerformance iteration {i}");

            // update Lambda function configuration to force a cold start
            await LambdaClient.UpdateFunctionConfigurationAsync(new() {
                FunctionName = functionName,
                Environment = new() {
                    Variables = {
                        ["LAMBDAPERFORMANCE_RUN"] = i.ToString()
                    }
                }
            });

            // wait for Lambda configuration changes to propagate
            await WaitForFunctionToBeReady(functionName);

            // invoke Lambda function
            var response = await LambdaClient.InvokeAsync(new() {
                FunctionName = functionName,
                Payload = payload,
                InvocationType = InvocationType.RequestResponse,
                LogType = LogType.Tail
            });
            var result = ParseLambdaReportFromLogResult(response.LogResult);
            if(result.InitDuration == 0.0) {

                // invocation didn't cause a cold-start; add an additional run
                ++runs;
                continue;
            }

            // add result
            result.Run = i;
            result.Success = string.IsNullOrEmpty(response.FunctionError);
            results.Add(result);
            LogInfo($"Result: Iteration={i}, InitDuration={result.InitDuration * 1000.0:0.###}ms, UsedDuration={result.UsedDuration * 1000.0:0.###}ms");
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

    private TestResult ParseLambdaReportFromLogResult(string logResult) {

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
                InitDuration = initDuration
            };
        }
        return new() {
            UsedDuration = 0.0,
            InitDuration = 0.0
        };
    }
}
