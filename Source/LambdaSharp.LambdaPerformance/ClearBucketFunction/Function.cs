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

namespace LambdaSharp.LambdaPerformance.ClearBucketFunction;

using Amazon.S3;
using Amazon.S3.Model;
using LambdaSharp;

public class FunctionRequest { }

public class FunctionResponse { }

public sealed class Function : ALambdaFunction<FunctionRequest, FunctionResponse> {

    //--- Fields ---
    private string? _buildBucketName;
    private IAmazonS3? _s3Client;

    //--- Constructors ---
    public Function() : base(new LambdaSharp.Serialization.LambdaSystemTextJsonSerializer()) { }

    //--- Properties ---
    private IAmazonS3 S3Client => _s3Client ?? throw new InvalidOperationException();

    //--- Methods ---
    public override async Task InitializeAsync(LambdaConfig config) {

        // read configuration settings
        _buildBucketName = config.ReadS3BucketName("CodeBuild::ArtifactBucket");

        // initialize clients
        _s3Client = new AmazonS3Client();
    }

    public override async Task<FunctionResponse> ProcessMessageAsync(FunctionRequest request) {
        LogInfo($"Emptying bucket: {_buildBucketName}");

        // enumerate all S3 objects
        var s3Request = new ListObjectsV2Request {
            BucketName = _buildBucketName
        };
        var counter = 0;
        var deletions = new List<Task>();
        do {
            var response = await S3Client.ListObjectsV2Async(s3Request);

            // delete any objects found
            if(response.S3Objects.Any()) {
                deletions.Add(S3Client.DeleteObjectsAsync(new DeleteObjectsRequest {
                    BucketName = _buildBucketName,
                    Objects = response.S3Objects.Select(s3 => new KeyVersion {
                        Key = s3.Key
                    }).ToList(),
                    Quiet = true
                }));
                counter += response.S3Objects.Count;
            }

            // continue until no more objects can be fetched
            s3Request.ContinuationToken = response.NextContinuationToken;
        } while(s3Request.ContinuationToken != null);

        // wait for all deletions to complete
        await Task.WhenAll(deletions);
        LogInfo($"deleted {counter:N0} objects");
        return new();
    }
}
