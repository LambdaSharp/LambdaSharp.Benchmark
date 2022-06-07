using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

await LambdaBootstrapBuilder.Create(Handler, new DefaultLambdaJsonSerializer())
    .Build()
    .RunAsync();

Task Handler(Stream request, ILambdaContext context) => Task.CompletedTask;
