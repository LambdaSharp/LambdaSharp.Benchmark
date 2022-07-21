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

// check parameters
using System.Text.Json;
using LambdaSharp.Benchmark.CombineMeasurementsFunction;
using LambdaSharp.Benchmark.Common;

if(args.Length < 1) {
    Console.WriteLine("ERROR: missing arguments <INPUT-FOLDER> [ <OUTPUT-FOLDER> ]");
    return;
}
var inputFolder = args[0];
var outputFolder = (args.Length == 2) ? args[1] : null;
if(!Directory.Exists(inputFolder)) {
    Console.WriteLine("ERROR: folder does not exist");
    return;
}

// enumerate contents of folder
var filenames = Directory.GetFiles(inputFolder, "*.json");
Console.WriteLine($"Found {filenames.Length:N0} files");
var measurements = new List<MeasurementSummary>();
foreach(var filename in filenames) {
    Console.WriteLine($"Parsing {filename}");
    var json = File.ReadAllText(filename);
    var data = JsonSerializer.Deserialize<MeasurementSummary>(json)
        ?? throw new ApplicationException($"Unable to deserialize file ({filename})");
    measurements.Add(data);
}

// generate CSV table
Console.WriteLine("Generate CSV");
var result = DataUtil.GenerateCsv(measurements);
if(outputFolder is null) {
    Console.WriteLine(result);
} else {
    File.WriteAllText(Path.Combine(outputFolder, result.Filename), result.Csv);
}
Console.WriteLine("DONE");