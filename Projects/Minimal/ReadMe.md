# Minimal Function

The _Minimal_ project consists of the absolute least amount of code required by an invocable Lambda function.

The purpose of this project is to establish a baseline for all other .NET Lambda functions. Measurements of this Lambda function represent the lower bound of the AWS Lambda runtime for .NET projects.

## Code

```csharp
using System.IO;
using System.Threading.Tasks;

namespace Benchmark.Minimal {

    public sealed class Function {

        //--- Methods ---
        public async Task<Stream> ProcessAsync(Stream request) {

            // it doesn't get more minimal than this!
            return Stream.Null;
        }
    }
}
```