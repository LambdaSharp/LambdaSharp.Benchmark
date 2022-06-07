using System.IO;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;

[assembly:LambdaSerializer(typeof(SourceGeneratorLambdaJsonSerializer<Benchmark.SourceGeneratorJson.FunctionSerializerContext>))]

namespace Benchmark.SourceGeneratorJson {

    [JsonSerializable(typeof(Root))]
    public partial class FunctionSerializerContext : JsonSerializerContext { }

    public sealed class Function {

        //--- Methods ---
        public async Task<Stream> ProcessAsync(Root request) {
            return Stream.Null;
        }
    }
}
