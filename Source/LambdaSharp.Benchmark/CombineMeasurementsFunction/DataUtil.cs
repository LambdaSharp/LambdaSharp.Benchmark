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

namespace LambdaSharp.Benchmark.CombineMeasurementsFunction;

using System.Text;
using LambdaSharp.Benchmark.Common;

public static class DataUtil {

    //--- Constants ---
    private const decimal AWS_LAMBDA_X86_US_EAST_1_PER_SECOND_GB = 0.0000166667m;
    private const decimal AWS_LAMBDA_ARM_US_EAST_1_PER_SECOND_GB = 0.0000133334m;
    private const decimal AWS_LAMBDA_PER_MILLION_REQUESTS = 0.20m;
    private const decimal AWS_LAMBDA_X86_US_EAST_1_PER_MILLISECOND_MB = AWS_LAMBDA_X86_US_EAST_1_PER_SECOND_GB / 1000 / 1024;
    private const decimal AWS_LAMBDA_ARM_US_EAST_1_PER_MILLISECOND_MB = AWS_LAMBDA_ARM_US_EAST_1_PER_SECOND_GB / 1000 / 1024;
    private const decimal AWS_LAMBDA_PER_REQUEST = AWS_LAMBDA_PER_MILLION_REQUESTS / 1_000_000;

    //--- Class Methods ---
    public static (string Filename, string Csv)  GenerateCsv(IEnumerable<MeasurementSummary> measurements) {
        var usedDurationsCount = measurements.First().Samples.First().UsedDurations.Count;

        // create CSV header line
        StringBuilder result = new();
        AppendCsvLine(
            nameof(MeasurementSummary.Project),
            nameof(MeasurementSummary.Build),
            nameof(MeasurementSummary.Runtime),
            nameof(MeasurementSummary.Architecture),
            nameof(MeasurementSummary.Tiered),
            nameof(MeasurementSummary.Ready2Run),
            nameof(MeasurementSummary.PreJIT),
            nameof(MeasurementSummary.ZipSize),
            nameof(MeasurementSummary.MemorySize),
            "Runs",
            "Init",
            "Cold Used",
            $"Total Warm Used ({usedDurationsCount - 1})",
            "Cost (µ$)"
        );

        // combine measurements
        foreach(var measurement in measurements) {
            var averageUsed = Enumerable.Range(0, usedDurationsCount)
                .Select(index => measurement.Samples.Select(sample => sample.UsedDurations.ElementAt(index)).Average())
                .ToArray();
            AppendCsvLine(
                measurement.Project,
                measurement.Build,
                measurement.Runtime,
                measurement.Architecture,
                measurement.Tiered,
                measurement.Ready2Run,
                measurement.PreJIT,
                measurement.ZipSize.ToString(),
                $"{measurement.MemorySize}MB",
                measurement.Samples.Count.ToString(),

                // average of all init durations
                measurement.Samples.Select(sample => sample.InitDuration).Average()?.ToString("0.###"),

                // average of all cold used durations
                averageUsed[0].ToString("0.###"),

                // sum of average warm invocation durations
                averageUsed.Skip(1).Sum().ToString("0.###"),

                // compute cost based on architecture and memory configuration
                CalculateLambdaCost(averageUsed, measurement.Architecture, measurement.MemorySize).ToString(/*"0.###"*/)
            );
        }

        // extract date for measurement
        DateTime date;
        try {
            if(!DateTime.TryParse(measurements.First().Date, out date)) {
                date = DateTime.UtcNow;
            }
        } catch {

            // set a default date if need be
            date = DateTime.UtcNow;
        }

        // return filename and CSV tuple
        var filename = $"{measurements.First().Project} [{measurements.First().Region}] ({date:yyyy-MM-dd}).csv";
        return (filename, result.ToString());

        // local functions
        void AppendCsvLine(
            string? project,
            string? build,
            string? runtime,
            string? architecture,
            string? tiered,
            string? ready2run,
            string? preJIT,
            string? zipSize,
            string? memory,
            string? runs,
            string? initDuration,
            string? usedCold,
            string? totalUsedWarm,
            string? cost
        )
            => result.AppendLine($"{project},{build},{runtime},{architecture},{tiered},{ready2run},{preJIT},{zipSize},{memory},{runs},{initDuration},{usedCold},{totalUsedWarm},{cost}");
    }

    private static double CalculateLambdaCost(IEnumerable<double> averageUsed, string? architecture, int memorySize) {
        var totalBillableInvocationTime = averageUsed.Sum();
        var usedBillable = architecture switch {
            "arm64" => AWS_LAMBDA_ARM_US_EAST_1_PER_MILLISECOND_MB * (decimal)totalBillableInvocationTime * memorySize,
            "x86_64" => AWS_LAMBDA_X86_US_EAST_1_PER_MILLISECOND_MB * (decimal)totalBillableInvocationTime * memorySize,
            _ => throw new ApplicationException($"unrecognized architecture: {architecture}")
        };
        var requestsBillable = averageUsed.Count() * AWS_LAMBDA_PER_REQUEST;
        return (double)(usedBillable + requestsBillable) * 1_000_000;
    }
}