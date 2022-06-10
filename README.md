# LambdaSharp.Benchmark

This module is used to benchmark .NET AWS Lambda functions with different compilation and deployment options. The measurements are then collected into a CSV file for analysis.

The measurements are captured in an interactive [Google Sheets dashboard](https://docs.google.com/spreadsheets/d/1ULCEIbXPXFWzv8m-FMnh6b0T4acgZDavJxwY7-NKGdo/edit?usp=sharing).

## Presentation

<a href="https://youtu.be/fzLIqliRGrE"><img title="Serverless .NET Pattern: What's New in .NET 6 and AWS Lambda" src="Docs/TitleSlide.png"></a>

### Links from Presentation

* [When is the Lambda Init Phase Free, and when is it Billed?](https://bitesizedserverless.com/bite/when-is-the-lambda-init-phase-free-and-when-is-it-billed/)
* [Profiling functions with AWS Lambda Power Tuning](https://docs.aws.amazon.com/lambda/latest/operatorguide/profile-functions.html)
* [AWS Lambda Memory Vs CPU configuration](https://stackoverflow.com/questions/66522916/aws-lambda-memory-vs-cpu-configuration)
* [Benchmark .NET Lambda Projects with LambdaSharp.Benchmark](https://github.com/LambdaSharp/LambdaSharp.Benchmark)
* [Explore the results from the benchmarks in this public Google Sheet (LambdaSharp.Benchmark Explorer)](https://docs.google.com/spreadsheets/d/1ULCEIbXPXFWzv8m-FMnh6b0T4acgZDavJxwY7-NKGdo/edit?usp=sharing)

## Benchmarked Projects

1. [AwsSdk](Projects/AwsSdk/): benchmark using AWS .NET SDK ([Results](Data/AwsSdk%20(2022-06-02).csv))
1. [Minimal](Projects/Minimal/): minimal baseline project ([Results](Data/Minimal%20(2022-06-02).csv))
1. [NewtonsoftJson](Projects/NewtonsoftJson/): benchmark using Newtonsoft JSON.NET ([Results](Data/NewtonsoftJson%20(2022-06-02).csv))
1. [SampleAwsNewtonsoftTopLevel](Projects/SampleAwsNewtonsoftTopLevel/) ([Results](Data/SampleAwsNewtonsoftTopLevel%20(2022-06-03).csv))
1. [SampleAwsSystemTextJsonTopLevel](Projects/SampleAwsSystemTextJsonTopLevel/) ([Results](Data/SampleAwsSystemTextJsonTopLevel%20(2022-06-04).csv))
1. [SampleMinimalApi](Projects/SampleMinimalApi/) ([Results](Data/SampleMinimalApi%20(2022-06-03).csv))
1. [SourceGeneratorJson](Projects/SourceGeneratorJson/): benchmark using .NET 6+ source generators for JSON parsing ([Results](Data/SourceGeneratorJson%20(2022-06-02).csv))
1. [SystemTextJson](Projects/SystemTextJson/): benchmark using System.Text.Json ([Results](Data/SystemTextJson%20(2022-06-02).csv))


## Using LambdaSharp.Benchmark

_LambdaSharp.Benchmark_ uses the [LambdaSharp Tool](https://lambdasharp.net) for deployment. Once _LambdaSharp_ is set up, deploy _LambdaSharp.Benchmark_ directly from its published module. (Alternatively, you can clone and deploy from a local copy.)
```bash
lash deploy LambdaSharp.Benchmark@lambdasharp
```

Make note of the S3 bucket name that was created. It is used to upload sample projects to benchmark and holds the measurements at the end.
```bash
...
TODO: tool output
```

Identify a project to measure. Now add a `RunSpec.json` file to the project folder. This file contains the AWS Lambda entry point name and the JSON payload to use for the Lambda invocation.
```json
{
    "Handler": "SAMPLE-PROJECT::LAMBDA-FUNCTION-CLASS-NAME::METHOD-NAME",
    "Payload": "{\"message\":\"Hello World!\"}"
}
```

Second [zip](https://stackoverflow.com/questions/38782928/how-to-add-man-and-zip-to-git-bash-installation-on-windows) the contents of the folder, omitting any build files:
```bash
zip -9 -r SAMPLE-PROJECT.zip /MY-PROJECTS/SAMPLE-PROJECT -x "**/bin/*" -x "**/obj/*"
```

Then upload the zip file to the S3 bucket using the [AWS CLI](https://aws.amazon.com/cli/):
```bash
aws s3 cp "$ZIP_FILE" "s3://S3-BUCKET-NAME/Projects/SAMPLE-PROJECT.zip"
```

This will automatically kick-off the a step-function to build and benchmark the code. Once completed, the resulting CSV can be find in `Reports/` folder on the S3 bucket.


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
