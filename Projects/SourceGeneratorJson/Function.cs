using System.Text.Json.Serialization;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Benchmark.SourceGeneratorJson;

[assembly: LambdaSerializer(typeof(SourceGeneratorLambdaJsonSerializer<FunctionSerializerContext>))]

namespace Benchmark.SourceGeneratorJson;

[JsonSerializable(typeof(Root))]
public partial class FunctionSerializerContext : JsonSerializerContext { }

public sealed class Function {

    //--- Methods ---
    public async Task<Stream> ProcessAsync(Root request) {
        return Stream.Null;
    }
}
