using System.Text.Json;
using System.Text.Json.Serialization;
using MathNet.Numerics.Distributions;

namespace VideoZamanEtudu;

public sealed class Observation
{
    public long StartMs { get; set; }
    public long EndMs { get; set; }
    public double Seconds => (EndMs - StartMs) / 1000d;
    public string Step { get; set; } = "Adım";
    public string Classification { get; set; } = "";
    public bool Excluded { get; set; }
    public int Cycle { get; set; } = 1;

    private string? note;
    /// <summary>Kayıt notu (v2, isteğe bağlı): boşsa yazılmaz; en fazla 200 karakter. Metin olmayan değer okunurken yok sayılır.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonConverter(typeof(LenientStringConverter))]
    public string? Note { get => note; set => note = StudyRules.CleanNote(value); }
}

/// <summary>Adım listesindeki bir iş adımı: ad, sınıf ve bitiş anı açıklaması (v2).</summary>
public sealed class StepDef
{
    public string Name { get; set; } = "";
    public string Classification { get; set; } = "";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Note { get; set; }
}

/// <summary>Metin bekleyen isteğe bağlı alan: başka türde değer gelirse null okunur (paketi reddettirmez).</summary>
public sealed class LenientStringConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String) return reader.GetString();
        reader.Skip();
        return null;
    }
    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue(); else writer.WriteStringValue(value);
    }
}

public sealed class Study
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string Video { get; set; } = "";
    public string ExpectedVideoName { get; set; } = "";
    public string VideoLabel => Video.Length > 0 ? System.IO.Path.GetFileName(Video) : ExpectedVideoName;
    public DateTime Updated { get; set; } = DateTime.Now;
    public List<Observation> Observations { get; set; } = new();
    public Settings Settings { get; set; } = new();
    public Dictionary<string, int> Frequency { get; set; } = new();

    // v2 (tarayıcı v1.5–v1.7 ile aynı): hepsi isteğe bağlı.
    /// <summary>Süreç sırasıyla adım listesi.</summary>
    public List<StepDef> Steps { get; set; } = new();
    /// <summary>Etüt bilgileri: Operator, Station, Shift, Date, Observer, Note.</summary>
    public Dictionary<string, string> Info { get; set; } = new();
    /// <summary>Adıma özel tempo (%). Yoksa genel tempo.</summary>
    public Dictionary<string, double> StepPace { get; set; } = new();
    /// <summary>Takt süresi (sn).</summary>
    public double? Takt { get; set; }
    /// <summary>Tolerans kalemleri (Personal, Fatigue, Delay, Other); toplamları Settings.Allowance'a eşittir.</summary>
    public Dictionary<string, double> AllowanceParts { get; set; } = new();
}

public sealed class Settings
{
    public double Confidence { get; set; } = 95;
    public double TargetError { get; set; } = 5;
    public double Pace { get; set; } = 100;
    public double Allowance { get; set; }
    public bool RepeatedLow { get; set; }
    public int GroupMs { get; set; } = 100;
    public bool Stable { get; set; }

    public void Validate()
    {
        if (!double.IsFinite(Confidence) || Confidence <= 0 || Confidence >= 100 ||
            !double.IsFinite(TargetError) || TargetError <= 0 || !double.IsFinite(Pace) || Pace <= 0 ||
            !double.IsFinite(Allowance) || Allowance < 0 || Allowance >= 100 || GroupMs <= 0)
            throw StudyRules.Keyed(new ArgumentException("Güven 0–100 arasında, tempo ve hedef hata pozitif, tolerans %0–%100 aralığında (%100 hariç) ve grup hassasiyeti pozitif olmalı."), "settings.invalid");
    }
}

public record DurationGroup(double Seconds, int Count);
public record StepResult(string Step, int Count, int Excluded, double? Mean, double? Median,
    double? Min, double? Max, double? Deviation, double? CriticalT, double? ErrorPercent,
    string Sufficiency, double? Base, double? Normal, double? Standard, int Every,
    double? Contribution, string Method, IReadOnlyList<DurationGroup> Groups, double Pace = 100);
public record CycleResult(int Cycle, double Elapsed, double Workload);

public static class Analysis
{
    public static IReadOnlyList<StepResult> Steps(Study study)
    {
        var settings = study.Settings;
        settings.Validate();
        return study.Observations.GroupBy(x => x.Step).OrderBy(g => g.Key).Select(group =>
        {
            var values = group.Where(x => !x.Excluded && x.EndMs > x.StartMs)
                .Select(x => x.EndMs - x.StartMs).Order().ToArray();
            int n = values.Length;
            double? mean = n == 0 ? null : values.Average() / 1000;
            double? median = n == 0 ? null : (values[(n - 1) / 2] / 2d + values[n / 2] / 2d) / 1000;
            double? sd = n < 2 ? null : Math.Sqrt(values.Sum(x => Math.Pow(x / 1000d - mean!.Value, 2)) / (n - 1));
            double? t = n < 2 ? null : StudentT.InvCDF(0, 1, n - 1, (1 + settings.Confidence / 100) / 2);
            double? error = t * sd / (Math.Sqrt(n) * mean) * 100;
            var groups = values.GroupBy(x => (long)Math.Round(x / (double)settings.GroupMs,
                    MidpointRounding.AwayFromZero) * settings.GroupMs)
                .Select(g => new DurationGroup(g.Average() / 1000, g.Count()))
                .OrderByDescending(g => g.Count).ThenBy(g => g.Seconds).ToArray();
            var repeated = groups.FirstOrDefault(g => g.Count >= 2);
            double? basis = settings.RepeatedLow ? repeated?.Seconds : mean;
            // Adıma özel tempo (v2) yoksa genel tempo.
            double pace = study.StepPace != null && study.StepPace.TryGetValue(group.Key, out var stepPace) ? stepPace : settings.Pace;
            double? normal = basis * pace / 100;
            double? standard = normal / (1 - settings.Allowance / 100);
            int every = Math.Max(1, study.Frequency.GetValueOrDefault(group.Key, 1));
            return new StepResult(group.Key, n, group.Count() - n, mean, median,
                n == 0 ? null : values[0] / 1000d, n == 0 ? null : values[^1] / 1000d,
                sd, t, error, error is null ? "Hesaplanamıyor" : error <= settings.TargetError ? "Yeterli" : "Yetersiz",
                basis, normal, standard, every, standard / every,
                settings.RepeatedLow ? repeated is null ? "Tekrar yok; ortalamayı seçebilirsiniz" : "Tekrarlanan düşük süre" : "Ortalama", groups, pace);
        }).ToList();
    }

    public static IReadOnlyList<CycleResult> Cycles(Study study) =>
        study.Observations.Where(x => !x.Excluded && x.EndMs > x.StartMs)
        .GroupBy(x => x.Cycle).OrderBy(g => g.Key).Select(group =>
        {
            long total = 0, end = 0;
            foreach (var row in group.OrderBy(x => x.StartMs))
            {
                total += Math.Max(0, row.EndMs - Math.Max(end, row.StartMs));
                end = Math.Max(end, row.EndMs);
            }
            return new CycleResult(group.Key, total / 1000d, group.Sum(x => x.Seconds));
        }).ToList();
}
