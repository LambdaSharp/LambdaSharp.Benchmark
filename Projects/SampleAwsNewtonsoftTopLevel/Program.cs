using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.Json;
using Amazon.S3;
using Benchmark.SampleAwsNewtonsoftTopLevel;

// initialize client
IAmazonS3? _s3Client = new AmazonS3Client();

// register Lambda handler with Newtonsoft JSON serializer
await LambdaBootstrapBuilder.Create<Root>(Handler, new JsonSerializer())
    .Build()
    .RunAsync();

Task Handler(Root request, ILambdaContext context) => Task.CompletedTask;