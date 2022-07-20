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

using Amazon.CloudWatchLogs;
using Amazon.Lambda;
using Amazon.Lambda.Model;

// initialize clients
var lambdaClient = new AmazonLambdaClient();
var logsClient = new AmazonCloudWatchLogsClient();

// enumerate all deployed Lambda functions by paging through all results
var foundLambdaNames = new List<string>();
var request = new ListFunctionsRequest();
do {
    var response = await lambdaClient.ListFunctionsAsync(request);
    foundLambdaNames.AddRange(response.Functions
        .Where(function => function.FunctionName.StartsWith("LambdaSharp-Benchmark-Test-"))
        .Select(function => function.FunctionName)
    );
    request.Marker = response.NextMarker;
} while(request.Marker is not null);
Console.WriteLine($"Found {foundLambdaNames.Count:N0} matching Lambda functions");

// delete matched functions
foreach(var lambdaName in foundLambdaNames) {

    // delete Lambda function
    try {
        Console.WriteLine($"Delete Lambda function: {lambdaName}");
        await lambdaClient.DeleteFunctionAsync(lambdaName);
    } catch(Amazon.Lambda.Model.ResourceNotFoundException) {

        // nothing to do
    } catch(Exception) {
        Console.WriteLine($"Unable to delete Lambda function: {lambdaName}");
    }

    // delete log group, which is automatically created by Lambda invocation
    var logGroupName = $"/aws/lambda/{lambdaName}";
    try {
        Console.WriteLine($"Delete log group: {logGroupName}");
        await logsClient.DeleteLogGroupAsync(new() {
            LogGroupName = logGroupName
        });
    } catch(Amazon.CloudWatchLogs.Model.ResourceNotFoundException) {

        // nothing to do
    } catch(Exception) {
        Console.WriteLine($"Unable to delete log group: {logGroupName}");
    }
}