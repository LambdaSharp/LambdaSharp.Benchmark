using System.IO;
using System.Text;
using System.Threading.Tasks;
using Amazon.S3;

namespace LambdaPerformance.Minimal {

    public sealed class Function {

        //--- Fields ---
        private IAmazonS3? _s3Client;

        //--- Methods ---
        public async Task<Stream> ProcessAsync(Stream request) {

            // initialize S3 client
            _s3Client = new AmazonS3Client();
            return Stream.Null;
        }
    }
}
