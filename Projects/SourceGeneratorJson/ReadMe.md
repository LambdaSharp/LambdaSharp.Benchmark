# Source Generator JSON Function

The _SourceGeneratorJson_ project uses the new compile-time generated JSON serializer capabilities of the .NET 6 compiler.

The purpose of this project is compare JSON serializer performance of source generators for cold- and warm- starts with alternatives.

## Code

```csharp
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
```