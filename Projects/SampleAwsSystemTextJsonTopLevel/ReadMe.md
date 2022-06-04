# Sample AWS Top-Level Project using System.Text.Json

TODO: The _Minimal Top-Level_ project consists of the absolute least amount of code required by an invocable Lambda function using top-level statements.

The purpose of this project is to establish a baseline for other .NET Lambda functions. Measurements of this Lambda function represent the lower bound of the AWS Lambda runtime for .NET projects using top-level statements.

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