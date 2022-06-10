using System.IO;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.Json;

[assembly:LambdaSerializer(typeof(JsonSerializer))]

namespace Benchmark.NewtonsoftJson {

    public sealed class Function {

        //--- Methods ---
        public async Task<Stream> ProcessAsync(Root request) {
            return Stream.Null;
        }
    }
}
