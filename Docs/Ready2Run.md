# Ready-to-Run Option


## Overview

_ReadyToRun_ is a form of ahead-of-time (AOT) compilation to improve startup performance by reducing the amount of work the just-in-time (JIT) compiler needs to do as your application loads. [Click here to learn more about _ReadyToRun_.](https://docs.microsoft.com/en-us/dotnet/core/deploying/ready-to-run)


## Minimal API, .NET 6 Runtime, 1024MB, Tiered Compilation Off, ARM64

![Minimal API, Cold Init Duration](SampleMinimalApi-Net6-Arm64-NoTC-X-1024-ColdInit.png)
![Minimal API, Cold Used Duration](SampleMinimalApi-Net6-Arm64-NoTC-X-1024-ColdUsed.png)
![Minimal API, Total Warm Used Duration](SampleMinimalApi-Net6-Arm64-NoTC-X-1024-TotalWarmUsed.png)

## ARM64 vs. x64

![Minimal API, Cold Init Duration](SampleMinimalApi-Net6-X-NoTC-YesR2R-1024-ColdInit.png)
![Minimal API, Cold Used Duration](SampleMinimalApi-Net6-X-NoTC-YesR2R-1024-ColdUsed.png)
![Minimal API, Total Warm Used Duration](SampleMinimalApi-Net6-X-NoTC-YesR2R-1024-TotalWarmUsed.png)

## Memory Size: 128MB, 256MB, 512MB, 1024MB, vs. 1769MB

![Minimal API, Cold Init Duration](SampleMinimalApi-Net6-Arm64-NoTC-YesR2R-X-ColdInit.png)
![Minimal API, Cold Used Duration](SampleMinimalApi-Net6-Arm64-NoTC-YesR2R-X-ColdUsed.png)
![Minimal API, Total Warm Used Duration](SampleMinimalApi-Net6-Arm64-NoTC-YesR2R-X-TotalWarmUsed.png)

