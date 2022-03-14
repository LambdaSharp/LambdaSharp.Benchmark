namespace LambdaSharp.LambdaPerformance.DeployFunction;

using Amazon.Lambda;
using Amazon.CloudWatchLogs;
using LambdaSharp;

public class FunctionRequest {

    //--- Properties ---
    public string? FunctionName { get; set; }
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
}

public sealed class Function : ALambdaFunction<FunctionRequest, FunctionResponse> {

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

    //--- Methods ---
    public override async Task InitializeAsync(LambdaConfig config) {

        // read configuration settings
        _buildBucketName = config.ReadS3BucketName("CodeBuild::ArtifactBucket");

        // initialize clients
        _lambdaClient = new AmazonLambdaClient();
        _logsClient = new AmazonCloudWatchLogsClient();
    }

    public override async Task<FunctionResponse> ProcessMessageAsync(FunctionRequest request) {

        // validate request
        try {
            ArgumentAssertException.Assert(request.FunctionName is not null);
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
        var functionName = $"LambdaPerformance-{request.FunctionName}";
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
                Code = new() {
                    S3Bucket = BuildBucketName,
                    S3Key = request.ZipFile
                },
                Handler = request.Handler,
                Role = $"arn:aws:iam::{AwsAccountId}:role/LambdaDefaultRole"
            });

            // conduct cold-start performance expirement
            await PerformanceTestAsync(functionName, request.Payload, request.Runs);
        } catch(Exception e) {
            LogErrorAsInfo(e, "Lambda invocation failed; aborting peformance check");

            // return error response
            return new() {
                Success = false,
                Message = "Performance test failed (see log for more details)"
            };
        } finally {

            // delete Lambda function
            try {
                await LambdaClient.DeleteFunctionAsync(functionName);
            } catch(Exception e) {
                LogErrorAsInfo(e, $"Unable to delete function: {functionName}");
            }

            // delete log group, which is automatically created by Lambda invocation
            var logGroupName = $"/aws/lambda/{functionName}";
            try {
                await LogsClient.DeleteLogGroupAsync(new() {
                    LogGroupName = logGroupName
                });
            } catch(Exception e) {
                LogErrorAsInfo(e, $"Unable to delete log group: {logGroupName}");
            }
        }

        // return final response
        return new() {
            Success = true,
            Message = "Performance test complete"
        };
    }

    private async Task PerformanceTestAsync(string functionName, string payload, int runs) {
        for(var i = 1; i <= runs; ++i) {
            LogInfo($"LambdaPerformance iteration {i}");

            // update Lambda function configuration to force a cold start
            await LambdaClient.UpdateFunctionConfigurationAsync(new() {
                Environment = new() {
                    Variables = {
                        ["LAMBDAPERFORMANCE_RUN"] = i.ToString()
                    }
                }
            });

            // wait for Lambda configuration changes to propagate
            await Task.Delay(TimeSpan.FromSeconds(1));

            // invoke Lambda function
            var response = await LambdaClient.InvokeAsync(new() {
                FunctionName = functionName,
                Payload = payload,
                InvocationType = InvocationType.RequestResponse,
                LogType = LogType.Tail
            });
            LogInfo($"LogResult: {response.LogResult}");
        }
    }
}
