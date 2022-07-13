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

namespace LambdaSharp.Benchmark.CreateFunction;

using Amazon.Lambda;
using LambdaSharp;
using LambdaSharp.Benchmark.Common;

public class FunctionRequest {

    //--- Properties ---
    public RunSpec? RunSpec { get; set; }
}

public class FunctionResponse {

    //--- Properties ---
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? LambdaName { get; set; }
}

public sealed class Function : ALambdaFunction<FunctionRequest, FunctionResponse> {

    //--- Constants ---
    private const string CHARACTERS = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private const int MAX_LAMBDA_NAME_LENGTH = 64;

    //--- Class Fields ---
    private static Random _random = new();

    //--- Class Methods ---
    private static string RandomString(int length)
        => new string(Enumerable.Range(0, length).Select(_ => CHARACTERS[_random.Next(CHARACTERS.Length)]).ToArray());

    //--- Fields ---
    private string? _buildBucketName;
    private IAmazonLambda? _lambdaClient;

    //--- Constructors ---
    public Function() : base(new LambdaSharp.Serialization.LambdaSystemTextJsonSerializer()) { }

    //--- Properties ---
    private string BuildBucketName => _buildBucketName ?? throw new InvalidOperationException();
    private IAmazonLambda LambdaClient => _lambdaClient ?? throw new InvalidOperationException();

    //--- Methods ---
    public override async Task InitializeAsync(LambdaConfig config) {

        // read configuration settings
        _buildBucketName = config.ReadS3BucketName("BuildBucket");

        // initialize clients
        _lambdaClient = new AmazonLambdaClient();
    }

    public override async Task<FunctionResponse> ProcessMessageAsync(FunctionRequest request) {
        try {
            ArgumentAssertException.Assert(request.RunSpec is not null);
        } catch(ArgumentAssertException e) {
            return new() {
                Success = false,
                Message = $"Request validation failed: {e.Message.Replace("runSpec.", "")}"
            };
        }

        // create a unique Lambda name
        var lambdaNamePrefix = $"{Info.ModuleId}-Test-";
        var lambdaName = lambdaNamePrefix + RandomString(MAX_LAMBDA_NAME_LENGTH - lambdaNamePrefix.Length);

        // create Lambda function
        LogInfo($"Create Lambda function: {lambdaName}");
        await LambdaClient.CreateFunctionAsync(new() {
            Architectures = {
                request.RunSpec.Architecture
            },
            Timeout = 30,
            Runtime = request.RunSpec.Runtime,
            PackageType = PackageType.Zip,
            MemorySize = request.RunSpec.MemorySize,
            FunctionName = lambdaName,
            Description = $"Measuring {request.RunSpec.ZipFile} (Memory: {request.RunSpec.MemorySize})",
            Code = new() {
                S3Bucket = BuildBucketName,
                S3Key = request.RunSpec.ZipFile
            },
            Handler = request.RunSpec.Handler,
            Role = request.RunSpec.Role
        });
        return new() {
            Success = true,
            LambdaName = lambdaName
        };
    }
}
