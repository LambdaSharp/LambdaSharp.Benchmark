# System.Text.Json Function

The _SystemTextJson_ project uses the `System.Text.Json` runtime JSON serializer.

The purpose of this project is compare the `System.Text` JSON serializer performance for cold- and warm- starts with alternatives.

## Code

```csharp
using System.IO;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;

[assembly:LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace Benchmark.SystemTextJson {

    public sealed class Function {

        //--- Methods ---
        public async Task<Stream> ProcessAsync(Root request) {
            return Stream.Null;
        }
    }
}
```