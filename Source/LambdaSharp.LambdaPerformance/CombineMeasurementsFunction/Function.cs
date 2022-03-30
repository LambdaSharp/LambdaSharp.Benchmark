namespace LambdaSharp.LambdaPerformance.CombineMeasurementsFunction;

using System.Text;
using Amazon.S3;
using LambdaSharp;

public class FunctionRequest { }

public class FunctionResponse { }

public sealed class Function : ALambdaFunction<FunctionRequest, FunctionResponse> {

    //--- Fields ---
    private string? _buildBucketName;
    private string? _codeBuildProjectName;
    private IAmazonS3? _s3Client;

    //--- Constructors ---
    public Function() : base(new LambdaSharp.Serialization.LambdaSystemTextJsonSerializer()) { }

    //--- Properties ---
    private string BuildBucketName => _buildBucketName ?? throw new InvalidOperationException();
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
        await S3Client.PutObjectAsync(new() {
            BucketName = _buildBucketName,
            Key = $"{_codeBuildProjectName}/combined-measurements.csv",
            ContentBody = combinedCsv.ToString()
        });
        return new();
    }
}
