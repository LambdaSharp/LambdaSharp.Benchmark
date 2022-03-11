namespace LambdaPerformance.MinimalFunction;

using System.Text;

public sealed class Function {

    //--- Methods ---
    public async Task<Stream> ProcessMessageStreamAsync(Stream request) {
        // var responseStream = new MemoryStream();
        // responseStream.Write(Encoding.UTF8.GetBytes("Hello World!"));
        // responseStream.Position = 0;
        return Stream.Null;
    }
}
