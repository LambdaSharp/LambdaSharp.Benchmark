using System.IO;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Amazon.Lambda.Core;

[assembly:LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.SourceGeneratorLambdaJsonSerializer<LambdaPerformance.SourceGeneratorJson.FunctionSerializerContext>))]

namespace LambdaPerformance.SourceGeneratorJson {

    [JsonSerializable(typeof(Root))]
    public partial class FunctionSerializerContext : JsonSerializerContext { }

    public sealed class Function {

        //--- Methods ---
        public async Task<Stream> ProcessAsync(Root request) {
            return Stream.Null;
        }
    }
}
