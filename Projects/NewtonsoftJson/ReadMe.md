# Newtonsoft JSON Function

The _NewtonsoftJson_ project includes the popular [Json.NET](https://www.newtonsoft.com/json) package and uses it to deserialize a JSON payload.

The purpose of this project is compare Json.NET serializer performance for cold- and warm- starts with alternatives.

## Code

```csharp
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
```