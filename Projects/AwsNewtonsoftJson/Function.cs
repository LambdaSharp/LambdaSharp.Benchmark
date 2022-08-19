using System.IO;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.Json;
using Amazon.S3;

[assembly:LambdaSerializer(typeof(JsonSerializer))]

namespace Benchmark.AwsNewtonsoftJson {

    public sealed class Function {

        //--- Fields ---
        private IAmazonS3? _s3Client;

        //--- Constructors ---
        public Function() {

            // initialize S3 client
            _s3Client = new AmazonS3Client();
        }

        //--- Methods ---
        public async Task<Stream> ProcessAsync(Root request) {
            return Stream.Null;
        }
    }
}
