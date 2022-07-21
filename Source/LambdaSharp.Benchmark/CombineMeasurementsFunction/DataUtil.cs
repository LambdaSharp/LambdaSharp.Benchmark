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

    //--- Class Methods ---
    public static string GenerateCsv(IEnumerable<MeasurementSummary> measurements) {

        // combine measurements
        StringBuilder result = new();
        var warmStartSamplesCount = measurements.First().Samples.Count - 1;
        List<string> usedDurationColumns = new();
        for(var i = 1; i <= warmStartSamplesCount; ++i) {
            usedDurationColumns.Add($"Used-{i:00}");
        }
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
            "Total Warm Used",
            usedDurationColumns
        );
        foreach(var measurement in measurements) {
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
                measurement.Samples.Select(sample => sample.UsedDurations[0]).Average().ToString("0.###"),

                // sum of average warm invocation durations
                Enumerable.Range(1, warmStartSamplesCount).Select(index => measurement.Samples.Select(sample => sample.UsedDurations.ElementAt(index)).Average()).Sum().ToString("0.###"),

                // average by warm used duration
                Enumerable.Range(1, warmStartSamplesCount).Select(index => measurement.Samples.Select(sample => sample.UsedDurations.ElementAt(index)).Average().ToString("0.###"))
            );
        }
        return result.ToString();

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
            IEnumerable<string> usedWarmDurations
        )
            => result.AppendLine($"{project},{build},{runtime},{architecture},{tiered},{ready2run},{preJIT},{zipSize},{memory},{runs},{initDuration},{usedCold},{totalUsedWarm},{string.Join(",", usedWarmDurations)}");
    }
}