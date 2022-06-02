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

namespace LambdaSharp.Benchmark.ListArtifactsFunction;

using Amazon.S3;
using LambdaSharp;
using LambdaSharp.Benchmark.Common;

public class FunctionRequest {

    //--- Properties ---
    public string? BuildId { get; set; }
}

public class FunctionResponse {

    //--- Properties ---
    public List<string> RunSpecs { get; set; } = new();
}

public enum YesNoBothOption {
    No,
    Yes,
    Both
}

public sealed class Function : ALambdaFunction<FunctionRequest, FunctionResponse> {

    //--- Fields ---
    private string? _buildBucketName;
    private IAmazonS3? _s3Client;
    private string[]? _architectures;
    private string[]? _runtimes;
    private int[]? _memoryConfigurations;
    private string[]? _projectNames;

    //--- Constructors ---
    public Function() : base(new LambdaSharp.Serialization.LambdaSystemTextJsonSerializer()) { }

    //--- Properties ---
    private string BuildBucketName => _buildBucketName ?? throw new InvalidOperationException();
    private IAmazonS3 S3Client => _s3Client ?? throw new InvalidOperationException();
    public IEnumerable<string> Architectures => _architectures ?? throw new InvalidOperationException();
    public IEnumerable<int> MemorySizes => _memoryConfigurations ?? throw new InvalidOperationException();
    public IEnumerable<string> ProjectNames => _projectNames ?? throw new InvalidOperationException();
    public IEnumerable<string> Runtimes => _runtimes ?? throw new InvalidOperationException();
    private YesNoBothOption TieredCompilation { get; set; }
    private YesNoBothOption Ready2RunCompilation { get; set; }

    //--- Methods ---
    public override async Task InitializeAsync(LambdaConfig config) {

        // read configuration settings
        _buildBucketName = config.ReadS3BucketName("BuildBucket");
        _architectures = config.ReadText("Architectures").Split(",", StringSplitOptions.RemoveEmptyEntries);
        _runtimes = config.ReadText("Runtimes").Split(",", StringSplitOptions.RemoveEmptyEntries);
        _memoryConfigurations = config.ReadText("MemorySizes").Split(",", StringSplitOptions.RemoveEmptyEntries).Select(value => int.Parse(value)).ToArray();
        _projectNames = config.ReadText("ProjectNames").Split(",", StringSplitOptions.RemoveEmptyEntries);
        TieredCompilation = Enum.Parse<YesNoBothOption>(config.ReadText("TieredOption"), ignoreCase: true);
        Ready2RunCompilation = Enum.Parse<YesNoBothOption>(config.ReadText("Ready2RunOption"), ignoreCase: true);

        // initialize clients
        _s3Client = new AmazonS3Client();
    }

    public override async Task<FunctionResponse> ProcessMessageAsync(FunctionRequest request) {
        ArgumentAssertException.Assert(request.BuildId is not null);
        ArgumentAssertException.Assert(request.BuildId.IndexOf(':') >= 0);
        LogInfo($"List artifacts for BuildId: {request.BuildId}");

        // return list of all build artifacts
        var pathPrefix = $"Build/{request.BuildId.Split(':', 2)[1]}/";
        LogInfo($"Finding all run-specs at s3://{BuildBucketName}/Build/{pathPrefix}");
        var listObjectsResponse = await S3Client.ListObjectsV2Async(new() {
            BucketName = BuildBucketName,
            Prefix = pathPrefix,
            Delimiter = "/"
        });
        List<RunSpec> runSpecs = new();
        var response = new FunctionResponse();
        foreach(var runSpecObject in listObjectsResponse.S3Objects.Where(s3Object => s3Object.Key.EndsWith(".json", StringComparison.Ordinal))) {

            // read run-spec from S3 bucket
            var getRunSpecObjectResponse = await S3Client.GetObjectAsync(new() {
                BucketName = BuildBucketName,
                Key = runSpecObject.Key
            });
            runSpecs.Add(LambdaSerializer.Deserialize<RunSpec>(getRunSpecObjectResponse.ResponseStream) with {
                ZipFile = Path.ChangeExtension(runSpecObject.Key, ".zip")
            });
        }
        LogInfo($"Found {runSpecs.Count:N0} run-specs");

        // read all run-spec JSON file and augment them with the zip file location
        var projectDidNotMatch = 0;
        var runtimeDidNotMatch = 0;
        var architectureDidNotMatch = 0;
        var tieredCompilationDidNotMatch = 0;
        var ready2RunDidNotMatch = 0;
        foreach(var runSpec in runSpecs) {

            // check if requested project settings are part of the measurements configuration
            if(ProjectNames.Any() && !ProjectNames.Contains(runSpec.Project)) {
                ++projectDidNotMatch;

                // skip this run-spec
                continue;
            }
            if(!Runtimes.Contains(runSpec.Runtime)) {
                ++runtimeDidNotMatch;

                // skip this run-spec
                continue;
            }
            if(!Architectures.Contains(runSpec.Architecture)) {
                ++architectureDidNotMatch;

                // skip this run-spec
                continue;
            }
            if(
                ((TieredCompilation == YesNoBothOption.Yes) && (runSpec.Tiered == "no"))
                || ((TieredCompilation == YesNoBothOption.No) && (runSpec.Tiered == "yes"))
            ) {
                ++tieredCompilationDidNotMatch;

                // skip this run-spec
                continue;
            }
            if(
                ((Ready2RunCompilation == YesNoBothOption.Yes) && (runSpec.Ready2Run == "no"))
                || ((Ready2RunCompilation == YesNoBothOption.No) && (runSpec.Ready2Run == "yes"))
            ) {
                ++ready2RunDidNotMatch;

                // skip this run-spec
                continue;
            }

            // generate a run-spec for each memory configuration
            foreach(var memorySize in MemorySizes) {
                var runSpecFileName = Path.ChangeExtension(runSpec.ZipFile, extension: null) + $"-{memorySize}.json";
                LogInfo($"Writing run-spec file to s3://{BuildBucketName}/{runSpecFileName}");
                await S3Client.PutObjectAsync(new() {
                    BucketName = BuildBucketName,
                    Key = runSpecFileName,
                    ContentBody = LambdaSerializer.Serialize(runSpec with {
                        MemorySize = memorySize
                    })
                });
                response.RunSpecs.Add(runSpecFileName);
            }
        }
        LogInfo($"Discarded mismatches: ProjectName={projectDidNotMatch}, Runtime={runtimeDidNotMatch}, Architecture={architectureDidNotMatch}, TieredCompilation={tieredCompilationDidNotMatch}, Ready2Run={ready2RunDidNotMatch}");
        LogInfo($"Generated {response.RunSpecs.Count:N0} run-specs for MemorySizes=[{string.Join(", ", MemorySizes.Select(memorySize => memorySize.ToString()))}]");
        return response;
    }
}
