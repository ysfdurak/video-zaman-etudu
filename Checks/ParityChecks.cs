using System.Text.Json;
using MathNet.Numerics.Distributions;

namespace VideoZamanEtudu;

/// <summary>
/// Shared/esdeger-ornekler.json dosyasını okur. Aynı dosya Web/tests tarafından da okunur;
/// iki sürüm aynı girdiden aynı sonucu ve aynı paket kararını vermelidir.
/// </summary>
internal static class ParityChecks
{
    public static void Run(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var root = document.RootElement;
        int failures = 0, total = 0;
        void Fail(string message) { failures++; Console.WriteLine("EŞDEĞERLİK KALDI  " + message); }

        foreach (var item in root.GetProperty("Analysis").EnumerateArray())
        {
            total++;
            string name = item.GetProperty("Name").GetString()!;
            var input = item.GetProperty("Input");
            var study = new Study
            {
                Name = "E", Code = "K",
                Settings = input.GetProperty("Settings").Deserialize<Settings>()!,
                Observations = input.GetProperty("Observations").Deserialize<List<Observation>>()!,
                Frequency = input.GetProperty("Frequency").Deserialize<Dictionary<string, int>>()!
            };
            var steps = Analysis.Steps(study).ToDictionary(x => x.Step);
            var expectedSteps = item.GetProperty("Steps").EnumerateArray().ToList();
            if (steps.Count != expectedSteps.Count) Fail($"{name}: adım sayısı {steps.Count} ≠ {expectedSteps.Count}");
            foreach (var e in expectedSteps)
            {
                string step = e.GetProperty("Step").GetString()!;
                if (!steps.TryGetValue(step, out var r)) { Fail($"{name}/{step}: adım yok"); continue; }
                void Num(string field, double? actual)
                {
                    var x = e.GetProperty(field);
                    double? expected = x.ValueKind == JsonValueKind.Null ? null : x.GetDouble();
                    if (!Same(actual, expected)) Fail($"{name}/{step}/{field}: {actual} ≠ {expected}");
                }
                Num("Mean", r.Mean); Num("Median", r.Median); Num("Min", r.Min); Num("Max", r.Max);
                Num("Deviation", r.Deviation); Num("CriticalT", r.CriticalT); Num("ErrorPercent", r.ErrorPercent);
                Num("Base", r.Base); Num("Normal", r.Normal); Num("Standard", r.Standard); Num("Contribution", r.Contribution);
                if (r.Count != e.GetProperty("Count").GetInt32() || r.Excluded != e.GetProperty("Excluded").GetInt32() ||
                    r.Every != e.GetProperty("Every").GetInt32() || r.Sufficiency != e.GetProperty("Sufficiency").GetString() ||
                    r.Method != e.GetProperty("Method").GetString())
                    Fail($"{name}/{step}: sayım, sıklık, yeterlilik veya yöntem farklı");
                var groups = e.GetProperty("Groups").EnumerateArray().ToList();
                if (groups.Count != r.Groups.Count ||
                    groups.Zip(r.Groups).Any(p => p.First.GetProperty("Count").GetInt32() != p.Second.Count ||
                                                  !Same(p.Second.Seconds, p.First.GetProperty("Seconds").GetDouble())))
                    Fail($"{name}/{step}: süre grupları farklı");
            }
            var cycles = Analysis.Cycles(study);
            var expectedCycles = item.GetProperty("Cycles").EnumerateArray().ToList();
            if (cycles.Count != expectedCycles.Count ||
                cycles.Zip(expectedCycles).Any(p => p.First.Cycle != p.Second.GetProperty("Cycle").GetInt32() ||
                                                    !Same(p.First.Elapsed, p.Second.GetProperty("Elapsed").GetDouble()) ||
                                                    !Same(p.First.Workload, p.Second.GetProperty("Workload").GetDouble())))
                Fail($"{name}: çevrimler farklı");
        }

        foreach (var item in root.GetProperty("TTable").EnumerateArray())
        {
            total++;
            double c = item.GetProperty("Confidence").GetDouble();
            int df = item.GetProperty("Df").GetInt32();
            if (!Same(StudentT.InvCDF(0, 1, df, (1 + c / 100) / 2), item.GetProperty("T").GetDouble()))
                Fail($"t tablosu %{c}, sd {df}");
        }

        foreach (var item in root.GetProperty("Packages").EnumerateArray())
        {
            total++;
            string name = item.GetProperty("Name").GetString()!;
            string file = Path.Combine(Path.GetTempPath(), "vz-esdeger-" + Guid.NewGuid().ToString("N"));
            File.WriteAllBytes(file, Convert.FromBase64String(item.GetProperty("Base64").GetString()!));
            try
            {
                bool accept = item.GetProperty("Accept").GetBoolean();
                try
                {
                    var study = StudyTransfer.Import(file);
                    if (!accept) { Fail($"paket '{name}': kabul edildi, ret bekleniyordu"); continue; }
                    var e = item.GetProperty("Expected");
                    if (study.Name != e.GetProperty("Name").GetString() || study.Code != e.GetProperty("Code").GetString() ||
                        study.ExpectedVideoName != e.GetProperty("ExpectedVideoName").GetString() ||
                        study.Observations.Count != e.GetProperty("Observations").GetArrayLength() ||
                        study.Frequency.Count != e.GetProperty("Frequency").EnumerateObject().Count())
                        Fail($"paket '{name}': içe alınan değerler farklı");
                }
                catch (InvalidDataException)
                {
                    if (accept) Fail($"paket '{name}': reddedildi, kabul bekleniyordu");
                }
            }
            finally
            {
                File.Delete(file);
            }
        }

        Console.WriteLine($"Eşdeğerlik kontrolleri: {total - failures}/{total} geçti.");
        if (failures > 0) throw new Exception($"{failures} eşdeğerlik kontrolü başarısız.");
    }

    /// <summary>MathNet ile örnekleri üreten ortam arasında 1e-12 düzeyinde fark olabilir; 1e-9 göreli tolerans yeterli.</summary>
    private static bool Same(double? actual, double? expected) =>
        actual is null || expected is null
            ? actual is null && expected is null
            : Math.Abs(actual.Value - expected.Value) <= 1e-9 * Math.Max(1, Math.Abs(expected.Value));
}
