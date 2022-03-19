namespace LambdaSharp.LambdaPerformance.ListArtifactsFunction;

using Amazon.S3;
using LambdaSharp;

public class FunctionRequest { }

public class FunctionResponse {

    //--- Properties ---
    public List<RunSpec> RunSpecs { get; set; } = new();
}

public class RunSpec {

    //--- Properties ---
    public string? Payload { get; set; }
    public string? Handler { get; set; }
    public string? Runtime { get; set; }
    public string? Architecture { get; set; }
    public string? ZipFile { get; set; }
}

public sealed class Function : ALambdaFunction<FunctionRequest, FunctionResponse> {

    //--- Fields ---
    private string? _buildBucketName;
    private string? _codeBuildProjectName;
    private IAmazonS3? _s3Client;

    //--- Constructors ---
    public Function() : base(new LambdaSharp.Serialization.LambdaSystemTextJsonSerializer()) { }

    //--- Properties ---
    private string BuildBucketName => _buildBucketName ?? throw new InvalidOperationException();
    private string CodeBuildProjectName => _codeBuildProjectName ?? throw new InvalidOperationException();
    private IAmazonS3 S3Client => _s3Client ?? throw new InvalidOperationException();

    //--- Methods ---
    public override async Task InitializeAsync(LambdaConfig config) {

        // read configuration settings
        _buildBucketName = config.ReadS3BucketName("CodeBuild::ArtifactBucket");
        _codeBuildProjectName = config.ReadText("CodeBuild::ProjectName");

        // initialize clients
        _s3Client = new AmazonS3Client();
    }

    public override async Task<FunctionResponse> ProcessMessageAsync(FunctionRequest request) {

        // return list of all build artifacts
        var listObjectsResponse = await S3Client.ListObjectsV2Async(new() {
            BucketName = BuildBucketName,
            Prefix = $"{_codeBuildProjectName}/",
            Delimiter = "/"
        });

        // read all run-spec JSON file and augment them with the zip file location
        var response = new FunctionResponse();
        foreach(var runSpecObject in listObjectsResponse.S3Objects.Where(s3Object => s3Object.Key.EndsWith(".json", StringComparison.Ordinal))) {

            // read run-spec from S3 bucket
            var getRunSpecObjectResponse = await S3Client.GetObjectAsync(new() {
                BucketName = BuildBucketName,
                Key = runSpecObject.Key
            });

            // add ZipFile location
            var runSpec = LambdaSerializer.Deserialize<RunSpec>(getRunSpecObjectResponse.ResponseStream);
            runSpec.ZipFile = Path.ChangeExtension(runSpecObject.Key, ".zip");
            response.RunSpecs.Add(runSpec);
        }
        return response;
    }
}
