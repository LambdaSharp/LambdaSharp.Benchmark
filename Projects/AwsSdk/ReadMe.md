# AWS SDK Function

The _AwsSdk_ project includes the AWS SDK Core package and initializes an S3 client with it.

The purpose of this project is to measure the overhead of the AWS SDK on cold-start performance.

## Code

```csharp
using System.IO;
using System.Threading.Tasks;
using Amazon.S3;

namespace Benchmark.AwsSdk {

    public sealed class Function {

        //--- Fields ---
        private IAmazonS3? _s3Client;

        //--- Constructors ---
        public Function() {

            // initialize S3 client
            _s3Client = new AmazonS3Client();
        }

        //--- Methods ---
        public async Task<Stream> ProcessAsync(Stream request) {
            return Stream.Null;
        }
    }
}
```