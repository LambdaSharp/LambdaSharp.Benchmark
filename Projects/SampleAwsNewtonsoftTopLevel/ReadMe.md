# Sample AWS Top-Level Project using Newtonsoft JSON.NET

The project uses top-level statements, an AWS client, and the Newtonsoft JSON.NET serializer.

## Code

```csharp
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.Json;
using Amazon.S3;

// initialize client
IAmazonS3? _s3Client = new AmazonS3Client();

// register Lambda handler with Newtonsoft JSON serializer
await LambdaBootstrapBuilder.Create(Handler, new JsonSerializer())
    .Build()
    .RunAsync();

Task Handler(Root request, ILambdaContext context) => Task.CompletedTask;
```