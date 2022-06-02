# LambdaSharp.Benchmark

This module is used to benchmark .NET Lambda functions with different compilation and deployment options. The measurements are then collected into a CSV file for analysis.

The measurements are captured in an interactive [Google Sheets dashboard](https://docs.google.com/spreadsheets/d/1ULCEIbXPXFWzv8m-FMnh6b0T4acgZDavJxwY7-NKGdo/edit?usp=sharing).

## Projects
1. [Minimal](Projects/Minimal/)
1. [AwsSdk](Projects/AwsSdk/)
1. [NewtonsoftJson](Projects/NewtonsoftJson/)
1. [SystemTextJson](Projects/SystemTextJson/)
1. [SourceGeneratorJson](Projects/SourceGeneratorJson/)

## Reports
1. [Tiered Compilation and ReadyToRun Options](Docs/Tiered-Ready2Run-Options.md)
1. TODO: .NET 6 vs. .NET 3.1
1. TODO: ARM64 vs. x86-64

## Measurements

1. [Minimal](Data/Minimal%20%5Bus-west-2%5D%20(2022-04-05).csv)
1. [AwsSdk](Data/AwsSdk%20%5Bus-west-2%5D%20(2022-04-05).csv)
1. [NewtonsoftJson](Data/NewtonsoftJson%20%5Bus-west-2%5D%20(2022-04-05).csv)
1. [SystemTextJson](Data/SystemTextJson%20%5Bus-west-2%5D%20(2022-04-05).csv)
1. [SourceGeneratorJson](Data/SourceGeneratorJson%20%5Bus-west-2%5D%20(2022-04-05).csv)

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
