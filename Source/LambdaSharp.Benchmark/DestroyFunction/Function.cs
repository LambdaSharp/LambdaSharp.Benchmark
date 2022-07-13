/*
 * LambdaSharp (λ#)
 * Copyright (C) 2018-2022
 * lambdasharp.net
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

namespace LambdaSharp.Benchmark.DestroyFunction;

using Amazon.CloudWatchLogs;
using Amazon.Lambda;
using LambdaSharp;

public class FunctionRequest {

    //--- Properties ---
    public string? LambdaName { get; set; }
}

public class FunctionResponse {

    //--- Properties ---
    public bool Success { get; set; }
    public string? Message { get; set; }
}

public sealed class Function : ALambdaFunction<FunctionRequest, FunctionResponse> {

    //--- Fields ---
    private IAmazonLambda? _lambdaClient;
    private IAmazonCloudWatchLogs? _logsClient;

    //--- Constructors ---
    public Function() : base(new LambdaSharp.Serialization.LambdaSystemTextJsonSerializer()) { }

    //--- Properties ---
    private IAmazonLambda LambdaClient => _lambdaClient ?? throw new InvalidOperationException();
    private IAmazonCloudWatchLogs LogsClient => _logsClient ?? throw new InvalidOperationException();

    //--- Methods ---
    public override async Task InitializeAsync(LambdaConfig config) {

        // initialize clients
        _lambdaClient = new AmazonLambdaClient();
        _logsClient = new AmazonCloudWatchLogsClient();
    }

    public override async Task<FunctionResponse> ProcessMessageAsync(FunctionRequest request) {;
        try {
            ArgumentAssertException.Assert(request.LambdaName is not null);
        } catch(ArgumentAssertException e) {
            return new() {
                Success = false,
                Message = $"Request validation failed: {e.Message.Replace("runSpec.", "")}"
            };
        }

        // delete Lambda function
        try {
            LogInfo($"Delete Lambda function: {request.LambdaName}");
            await LambdaClient.DeleteFunctionAsync(request.LambdaName);
        } catch(Amazon.Lambda.Model.ResourceNotFoundException) {

            // nothing to do
            LogInfo($"Lambda function does not exist: {request.LambdaName}");
        } catch(Exception e) {
            LogErrorAsWarning(e, $"Unable to delete Lambda function: {request.LambdaName}");
        }

        // delete log group, which is automatically created by Lambda invocation
        var logGroupName = $"/aws/lambda/{request.LambdaName}";
        try {
            LogInfo($"Delete log group: {logGroupName}");
            await LogsClient.DeleteLogGroupAsync(new() {
                LogGroupName = logGroupName
            });
        } catch(Amazon.CloudWatchLogs.Model.ResourceNotFoundException) {

            // nothing to do
            LogInfo($"Log group does not exist: {logGroupName}");
        } catch(Exception e) {
            LogErrorAsInfo(e, $"Unable to delete log group: {logGroupName}");
        }
        return new() {
            Success = true
        };
    }
}
