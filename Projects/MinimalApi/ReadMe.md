# Minimal API Function

The _Minimal API_ project uses the ASP.NET Core minimal APIs as a Lambda function. The project is based on the sample provided in [Introducing the .NET 6 runtime for AWS Lambda](https://aws.amazon.com/blogs/compute/introducing-the-net-6-runtime-for-aws-lambda/) blog post.

## Code

```csharp
using Amazon.S3;
using Benchmark.MinimalApi;

var builder = WebApplication.CreateBuilder(args);

// Add Swagger/OpenAPI support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add S3 service client to dependency injection container
builder.Services.AddAWSService<IAmazonS3>();

// Add AWS Lambda support.
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);

// build app
var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();

// Example POST route
app.MapPost("/test", async (IAmazonS3 s3Client, Root root) => root.user.id);

app.Run();
```