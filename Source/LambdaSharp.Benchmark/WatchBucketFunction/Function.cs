namespace LambdaSharp.Benchmark.WatchBucketFunction;

using Amazon.Lambda.S3Events;
using Amazon.StepFunctions;
using LambdaSharp;

public sealed class Function : ALambdaFunction<S3Event, string> {

    //--- Fields ---
    private string? _stepFunctionArn;
    private IAmazonStepFunctions? _stepFunctionsClient;

    //--- Constructors ---
    public Function() : base(new LambdaSharp.Serialization.LambdaSystemTextJsonSerializer()) { }

    //--- Properties ---
    private string StepFunctionArn => _stepFunctionArn ?? throw new InvalidOperationException();
    private IAmazonStepFunctions StepFunctionsClient => _stepFunctionsClient ?? throw new InvalidOperationException();

    //--- Methods ---
    public override async Task InitializeAsync(LambdaConfig config) {

        // read configuration settings
        _stepFunctionArn = config.ReadText("TestWorkflow::StepFunction");

        // initialize clients
        _stepFunctionsClient = new AmazonStepFunctionsClient();
    }

    public override async Task<string> ProcessMessageAsync(S3Event request) {
        LogInfo($"New Lambda zip package detected: {request.Records[0].S3.Object.Key}");

        // kick-off step-function to build and measure the Lambda zip package
        await StepFunctionsClient.StartExecutionAsync(new() {
            StateMachineArn = StepFunctionArn,
            Input = LambdaSerializer.Serialize(new {
                ProjectPath = request.Records[0].S3.Object.Key
            })
        });
        return "Ok";
    }
}
