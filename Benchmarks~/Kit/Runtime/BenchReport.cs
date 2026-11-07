using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace FastIoCBench
{
    public struct BenchMetric
    {
        public string Name;
        public double Value;
        public string Unit;
        public string Aggregation;
    }

    /// <summary>Writes one result file per launch, following Documentation~/benchmarks/benchmark-result.schema.json.</summary>
    public static class BenchReport
    {
        public static void Add(this List<BenchMetric> metrics, string name, double value, string unit, string aggregation = "single")
        {
            metrics.Add(new BenchMetric { Name = name, Value = value, Unit = unit, Aggregation = aggregation });
        }

        public static void AddStats(this List<BenchMetric> metrics, string name, List<double> values, string unit)
        {
            if (values.Count == 0)
                return;

            List<double> sorted = new(values);
            sorted.Sort();
            metrics.Add(name, Percentile(sorted, 0.5), unit, "median");
            metrics.Add(name, Percentile(sorted, 0.95), unit, "p95");
            metrics.Add(name, sorted[0], unit, "min");
            metrics.Add(name, sorted[sorted.Count - 1], unit, "max");
        }

        public static double Percentile(List<double> sorted, double q)
        {
            if (sorted.Count == 0)
                return 0;

            int index = (int)Math.Round(q * (sorted.Count - 1));
            return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
        }

        public static string Write(
            BenchBuildInfo info,
            IBenchFixture fixture,
            string variant,
            int runIndex,
            int totalRuns,
            string notes,
            List<BenchMetric> metrics)
        {
            CultureInfo inv = CultureInfo.InvariantCulture;
            StringBuilder sb = new();
            sb.Append("{\n");
            sb.Append("  \"tool\": \"fast-ioc\",\n");
            sb.Append("  \"schemaVersion\": \"1.0.0\",\n");
            sb.Append("  \"timestampUtc\": ").Append(Q(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", inv))).Append(",\n");
            sb.Append("  \"session\": {\n");
            sb.Append("    \"deviceName\": ").Append(Q(DeviceName())).Append(",\n");
            sb.Append("    \"deviceModel\": ").Append(Q(NonEmpty(SystemInfo.deviceModel))).Append(",\n");
            sb.Append("    \"osVersion\": ").Append(Q(NonEmpty(SystemInfo.operatingSystem))).Append(",\n");
            sb.Append("    \"unityVersion\": ").Append(Q(Application.unityVersion)).Append(",\n");
            sb.Append("    \"gitCommit\": ").Append(Q(info.gitCommit)).Append(",\n");
            sb.Append("    \"buildMode\": ").Append(Q(Debug.isDebugBuild ? "Development" : "Release")).Append(",\n");
#if ENABLE_IL2CPP
            sb.Append("    \"scriptingBackend\": \"IL2CPP\",\n");
#else
            sb.Append("    \"scriptingBackend\": \"Mono\",\n");
#endif
            sb.Append("    \"notes\": ").Append(Q(notes)).Append('\n');
            sb.Append("  },\n");
            sb.Append("  \"workload\": {\n");
            sb.Append("    \"profileOrScenario\": ").Append(Q(fixture.ProfileName)).Append(",\n");
            sb.Append("    \"variant\": ").Append(Q(variant)).Append(",\n");
            sb.Append("    \"runIndex\": ").Append(runIndex.ToString(inv)).Append(",\n");
            sb.Append("    \"totalRuns\": ").Append(totalRuns.ToString(inv)).Append(",\n");
            sb.Append("    \"seed\": ").Append(fixture.Seed.ToString(inv)).Append('\n');
            sb.Append("  },\n");
            sb.Append("  \"metrics\": [\n");
            for (int i = 0; i < metrics.Count; i++)
            {
                BenchMetric m = metrics[i];
                double value = double.IsFinite(m.Value) ? m.Value : -1;
                sb.Append("    { \"name\": ").Append(Q(m.Name))
                  .Append(", \"value\": ").Append(value.ToString("R", inv))
                  .Append(", \"unit\": ").Append(Q(m.Unit))
                  .Append(", \"aggregation\": ").Append(Q(m.Aggregation ?? "single"))
                  .Append(i < metrics.Count - 1 ? " },\n" : " }\n");
            }

            sb.Append("  ]\n");
            sb.Append("}\n");

            string device = Sanitize(SystemInfo.deviceModel);
            string build = info.buildId.Length > 8 ? info.buildId.Substring(0, 8) : info.buildId;
            string fileName = $"{device}_{fixture.ProfileName}_{variant}_r{runIndex:00}_{build}.json";
            Directory.CreateDirectory(BenchPlan.Folder);
            string path = Path.Combine(BenchPlan.Folder, fileName);
            File.WriteAllText(path, sb.ToString());
            return path;
        }

        // SystemInfo.deviceName is the name the owner gave the phone (it can include a person's or a company's name),
        // and results are published, so only the model is recorded.
        private static string DeviceName() => NonEmpty(SystemInfo.deviceModel);

        private static string NonEmpty(string value) => string.IsNullOrWhiteSpace(value) ? "unknown" : value;

        private static string Sanitize(string value)
        {
            StringBuilder sb = new();
            foreach (char c in NonEmpty(value))
                sb.Append(char.IsLetterOrDigit(c) ? c : '-');
            return sb.ToString();
        }

        private static string Q(string value)
        {
            StringBuilder sb = new("\"");
            foreach (char c in value ?? "")
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else
                            sb.Append(c);
                        break;
                }
            }

            return sb.Append('"').ToString();
        }
    }
}
