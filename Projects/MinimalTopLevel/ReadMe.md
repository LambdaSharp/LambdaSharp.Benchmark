# Minimal Top-Level Function

The _Minimal Top-Level_ project consists of the absolute least amount of code required by an invocable Lambda function using top-level statements.

The purpose of this project is to establish a baseline for other .NET Lambda functions. Measurements of this Lambda function represent the lower bound of the AWS Lambda runtime for .NET projects using top-level statements.

## Code

```csharp
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

await LambdaBootstrapBuilder.Create(Handler, new DefaultLambdaJsonSerializer())
    .Build()
    .RunAsync();

Task Handler(Stream request, ILambdaContext context) => Task.CompletedTask;
```