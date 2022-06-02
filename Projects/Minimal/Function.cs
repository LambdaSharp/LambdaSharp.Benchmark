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
