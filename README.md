# LambdaSharp.Benchmark

This module is used to benchmark .NET Lambda functions with different compilation and deployment options. The measurements are then collected into a CSV file for analysis.

The measurements are captured in an interactive [Google Sheets dashboard](https://docs.google.com/spreadsheets/d/1ULCEIbXPXFWzv8m-FMnh6b0T4acgZDavJxwY7-NKGdo/edit?usp=sharing).

## Projects
1. [AwsSdk](Projects/AwsSdk/): benchmark using AWS .NET SDK
1. [Minimal](Projects/Minimal/): minimal baseline project
1. [NewtonsoftJson](Projects/NewtonsoftJson/): benchmark using Newtonsoft JSON.NET
1. [SampleAwsNewtonsoftTopLevel](Projects/SampleAwsNewtonsoftTopLevel/)
1. [SampleAwsSystemTextJsonTopLevel](Projects/SampleAwsSystemTextJsonTopLevel/)
1. [SampleMinimalApi](Projects/SampleMinimalApi/)
1. [SourceGeneratorJson](Projects/SourceGeneratorJson/): benchmark using .NET 6+ source generators for JSON parsing
1. [SystemTextJson](Projects/SystemTextJson/): benchmark using System.Text.Json

## Benchmark Results (CSV)

1. [AwsSdk](Data/AwsSdk%20(2022-06-02).csv)
1. [Minimal](Data/Minimal%20(2022-06-02).csv)
1. [NewtonsoftJson](Data/NewtonsoftJson%20(2022-06-02).csv)
1. [SampleAwsNewtonsoftTopLevel](Data/SampleAwsNewtonsoftTopLevel%20(2022-06-03).csv)
1. TODO: [SampleAwsSystemTextJsonTopLevel](Data)
1. [SampleMinimalApi](Data/SampleMinimalApi%20(2022-06-03).csv)
1. [SourceGeneratorJson](Data/SourceGeneratorJson%20(2022-06-02).csv)
1. [SystemTextJson](Data/SystemTextJson%20(2022-06-02).csv)

## Reports
1. TODO: .NET 6 vs. .NET Core 3.1
    1. .NET 6
        1. [Tiered Compilation and ReadyToRun Options](Docs/Tiered-Ready2Run-Options.md)
        1. TODO: ARM64 vs. x86-64
        1. TODO: Newtonsoft vs. System.Text.Json vs. Source Generators
        1. TODO: Function vs. Custom Runtime vs. Top-Level Statements
    1. .NET Core 3.1

## Using LambdaSharp.Benchmark

TODO: how to deploy and analyze a project

[Install LambdaSharp tooling and create a deployment tier.](https://lambdasharp.net)

Deploy the LambdaSharp.Benchmark module to your AWS account.
```bash
lash deploy LambdaSharp.Benchmark@lambdasharp
```

Use the `measure.sh` script to package your project and upload it to the benchmarking S3 bucket.
```bash
measure.sh <PROJECT-TO-MEASURE> <NAME-OF-S3-BUCKET>
```

## License

> Copyright (c) 2018-2022 LambdaSharp (λ#)
>
> Licensed under the Apache License, Version 2.0 (the "License");
> you may not use this file except in compliance with the License.
> You may obtain a copy of the License at
>
> http://www.apache.org/licenses/LICENSE-2.0
>
> Unless required by applicable law or agreed to in writing, software
> distributed under the License is distributed on an "AS IS" BASIS,
> WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
> See the License for the specific language governing permissions and
> limitations under the License.
