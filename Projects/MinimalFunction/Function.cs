using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace LambdaPerformance.MinimalFunction {

    public sealed class Function {

        //--- Methods ---
        public async Task<Stream> ProcessAsync(Stream request) {

            // it doesn't get more minimal than this!
            return Stream.Null;
        }
    }
}
