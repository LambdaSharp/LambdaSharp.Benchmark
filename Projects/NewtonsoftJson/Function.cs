using System.IO;
using System.Threading.Tasks;
using Amazon.Lambda.Core;

[assembly:LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace LambdaPerformance.NewtonsoftJson {

    public sealed class Function {

        //--- Methods ---
        public async Task<Stream> ProcessAsync(Root request) {
            return Stream.Null;
        }
    }
}
