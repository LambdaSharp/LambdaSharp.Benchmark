namespace LambdaSharp.Benchmark.ReadRunSpec;

using Amazon.S3;
using LambdaSharp;
using LambdaSharp.Benchmark.Common;

public class FunctionRequest {

    //--- Properties ---
    public string? RunSpec { get; set; }
}

public class FunctionResponse {

    //--- Properties ---
    public bool Success { get; set; }
    public string? Message { get; set; }
    public RunSpec? RunSpec { get; set; }
    public string? Build { get; set; }
}

public sealed class Function : ALambdaFunction<FunctionRequest, FunctionResponse> {

    //--- Fields ---
    private IAmazonS3? _s3Client;
    private string? _buildBucketName;

    //--- Constructors ---
    public Function() : base(new LambdaSharp.Serialization.LambdaSystemTextJsonSerializer()) { }

    //--- Properties ---
    private string AwsAccountId => CurrentContext.InvokedFunctionArn.Split(':')[4];
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

        // validate run-spec
        try {
            ArgumentAssertException.Assert(request.RunSpec is not null);
        } catch(ArgumentAssertException e) {
            return new() {
                Success = false,
                Message = $"Request validation failed: {e.Message}"
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
            ArgumentAssertException.Assert(runSpec.MemorySize is >= 128 and <= 10240);
            ArgumentAssertException.Assert(runSpec.Payload is not null);
        } catch(ArgumentAssertException e) {
            return new() {
                Success = false,
                Message = $"Request validation failed: {e.Message}"
            };
        }
        if(runSpec.Role is null) {
            runSpec = runSpec with {
                Role = $"arn:aws:iam::{AwsAccountId}:role/LambdaDefaultRole"
            };
        }
        return new() {
            Success = true,
            RunSpec = runSpec,
            Build = Path.GetFileNameWithoutExtension(request.RunSpec)
        };
    }
}
