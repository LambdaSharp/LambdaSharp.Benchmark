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

namespace LambdaSharp.Benchmark.Common;

public class MeasurementSummary {

    //--- Properties ---
    public string? Date { get; set; }
    public string? Region { get; set; }
    public string? Project { get; set; }
    public string? Build { get; set; }
    public string? Runtime { get; set; }
    public string? Architecture { get; set; }
    public int MemorySize { get; set; }
    public string? Tiered { get; set; }
    public string? PreJIT { get; set; }
    public string? Ready2Run { get; set; }
    public long ZipSize { get; set; }
    public List<MeasurementSample> Samples { get; set; } = new();
}

public class MeasurementSample {

    //--- Properties ---
    public int Sample { get; internal set; }
    public double? InitDuration { get; set; }
    public List<double> UsedDurations { get; set; } = new();
}