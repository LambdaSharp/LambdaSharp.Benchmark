using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.S3;
using Benchmark.SampleAwsSystemTextJsonTopLevel;

// initialize client
IAmazonS3? _s3Client = new AmazonS3Client();

// register Lambda handler with Newtonsoft JSON serializer
await LambdaBootstrapBuilder.Create<Root>(Handler, new DefaultLambdaJsonSerializer())
    .Build()
    .RunAsync();

Task Handler(Root request, ILambdaContext context) => Task.CompletedTask;