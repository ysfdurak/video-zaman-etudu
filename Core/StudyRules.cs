using System.Globalization;
using MathNet.Numerics.Distributions;

namespace VideoZamanEtudu;

public record CycleSpreadRow(int Cycle, double Elapsed, double Workload, double? DiffPercent, bool Outlier);
public record CycleSpreadResult(double? Mean, double? Deviation, double? Variation, IReadOnlyList<CycleSpreadRow> Rows);
public record CompareRow(string Step, double? Before, double? After, double? Diff, double? DiffPercent);
public record Comparison(IReadOnlyList<CompareRow> Rows, double? Before, double? After, double? Diff, double? DiffPercent);
public record ResumePoint(int Step, int Cycle, long? At);

/// <summary>
/// v2 kuralları: tarayıcı sürümündeki Web/js/core.js (v1.5–v1.7) işlevlerinin C# karşılığı.
/// Hesap çekirdeği (Analysis) değişmez; bunlar adım listesi, etüt bilgileri, özet ve karşılaştırma içindir.
/// </summary>
public static class StudyRules
{
    public static readonly string[] InfoFields = { "Operator", "Station", "Shift", "Date", "Observer", "Note" };
    public static readonly string[] AllowancePartNames = { "Personal", "Fatigue", "Delay", "Other" };
    public static readonly string[] Classes = { "", "VA", "SVA", "NVA" };
    public static readonly string[] WorkTypes = { "Manual", "Machine", "Walk", "Wait" };
    public static readonly string[] Wastes = { "Transport", "Inventory", "Motion", "Waiting", "Overproduction", "Overprocessing", "Defects" };
    private const long MaxMs = 922337203685477;
    public const int MaxText = 200;
    public const int MaxSteps = 50;
    public const int MaxStepName = 80;

    /// <summary>Hata iletisine çeviri anahtarı ekler (arayüz dili İngilizceyse ileti çevrilir).</summary>
    public static T Keyed<T>(T error, string key, params (string Name, object Value)[] values) where T : Exception
    {
        error.Data["key"] = key;
        foreach (var (name, value) in values) error.Data["p:" + name] = value;
        return error;
    }

    public static string? CleanNote(string? value)
    {
        if (value is null) return null;
        var text = value.Trim();
        if (text.Length > MaxText) text = text[..MaxText];
        return text.Length == 0 ? null : text;
    }

    private static string? Clip(string? value, int max)
    {
        var text = (value ?? "").Trim();
        if (text.Length > max) text = text[..max];
        return text.Length == 0 ? null : text;
    }

    /// <summary>Kayıt temposu (%): 0'dan büyük, en fazla 300; değilse null.</summary>
    public static double? CleanRating(double? value) => value is double v && double.IsFinite(v) && v > 0 && v <= 300 ? v : null;

    public static string CleanStudyType(string? value) => value == "sampling" ? value : "";

    public static List<SampleCategory> CleanCategories(IEnumerable<SampleCategory?>? list) =>
        CleanSteps(list?.Select(c => c is null ? null : new StepDef { Name = c.Name ?? "", Classification = c.Classification ?? "" }))
            .Select(s => new SampleCategory { Name = s.Name, Classification = s.Classification }).ToList();

    public static List<Sample> CleanSamples(IEnumerable<Sample?>? list)
    {
        var result = new List<Sample>();
        if (list is null) return result;
        foreach (var x in list)
        {
            if (x?.AtMs is not long at || at < 0 || at > MaxMs || Clip(x.Category, 80) is not string category) continue;
            result.Add(new Sample { AtMs = at, Category = category, Note = CleanNote(x.Note) });
            if (result.Count >= 10000) break;
        }
        return result;
    }

    public static double? CleanSampleError(double? value) => value is double v && double.IsFinite(v) && v > 0 && v < 50 ? v : null;

    public static Dictionary<string, string> CleanInfo(IDictionary<string, string>? info)
    {
        var result = new Dictionary<string, string>();
        if (info is null) return result;
        foreach (var field in InfoFields)
            if (info.TryGetValue(field, out var value) && CleanNote(value) is string text) result[field] = text;
        return result;
    }

    public static List<StepDef> CleanSteps(IEnumerable<StepDef?>? steps)
    {
        var result = new List<StepDef>();
        if (steps is null) return result;
        foreach (var step in steps)
        {
            var name = (step?.Name ?? "").Trim();
            if (name.Length > MaxStepName) name = name[..MaxStepName];
            if (name.Length == 0 || result.Any(x => x.Name == name)) continue;
            var station = Clip(step!.Station, 40);
            result.Add(new StepDef
            {
                Name = name,
                Classification = Classes.Contains(step.Classification ?? "") ? step.Classification ?? "" : "",
                Note = CleanNote(step.Note),
                Type = WorkTypes.Contains(step.Type) ? step.Type : null,
                Station = station,
                Waste = Wastes.Contains(step.Waste) ? step.Waste : null,
                Idea = CleanNote(step.Idea),
                FrameMs = step.FrameMs is long f && f >= 0 && f <= MaxMs ? f : null
            });
            if (result.Count >= MaxSteps) break;
        }
        return result;
    }

    public static Dictionary<string, double> CleanStepPace(IDictionary<string, double>? value)
    {
        var result = new Dictionary<string, double>();
        if (value is null) return result;
        foreach (var (key, pace) in value)
        {
            var name = (key ?? "").Trim();
            if (name.Length > 0 && double.IsFinite(pace) && pace > 0 && pace <= 300) result[name] = pace;
        }
        return result;
    }

    public static double? CleanTakt(double? value) =>
        value is double v && double.IsFinite(v) && v > 0 && v < 1e7 ? v : null;

    public static Dictionary<string, double> CleanAllowanceParts(IDictionary<string, double>? value)
    {
        var result = new Dictionary<string, double>();
        if (value is null) return result;
        foreach (var name in AllowancePartNames)
            if (value.TryGetValue(name, out var part) && double.IsFinite(part) && part > 0 && part < 100) result[name] = part;
        return AllowanceSum(result) < 100 ? result : new();
    }

    /// <summary>Kalemlerin toplamı; ondalık kaymasını önlemek için 1e-9'a yuvarlanır.</summary>
    public static double AllowanceSum(IDictionary<string, double>? parts) =>
        Math.Round((parts?.Values.Sum() ?? 0) * 1e9, MidpointRounding.AwayFromZero) / 1e9;

    /// <summary>Kalemler ancak toplamları ayardaki toleransa eşitse geçerlidir.</summary>
    public static Dictionary<string, double> MatchingParts(IDictionary<string, double>? parts, double allowance)
    {
        var clean = CleanAllowanceParts(parts);
        return clean.Count > 0 && Math.Abs(AllowanceSum(clean) - allowance) < 1e-6 ? clean : new();
    }

    private static IEnumerable<Observation> TimeOrder(Study study) =>
        study.Observations.OrderBy(o => o.StartMs).ThenBy(o => o.EndMs);

    /// <summary>Adım listesi: tanımlıysa o; değilse ölçümlerden ilk görülme sırasıyla, sınıfı en sık kullanılan sınıf.</summary>
    public static IReadOnlyList<StepDef> DeriveSteps(Study study)
    {
        if (study.Steps is { Count: > 0 }) return study.Steps;
        var order = new List<string>();
        var classes = new Dictionary<string, List<(string Class, int Count)>>();
        foreach (var o in TimeOrder(study))
        {
            if (!classes.TryGetValue(o.Step, out var list)) { order.Add(o.Step); classes[o.Step] = list = new(); }
            int i = list.FindIndex(x => x.Class == o.Classification);
            if (i < 0) list.Add((o.Classification, 1)); else list[i] = (list[i].Class, list[i].Count + 1);
        }
        return order.Select(name => new StepDef
        {
            Name = name,
            Classification = classes[name].Where(x => x.Class != "").OrderByDescending(x => x.Count).Select(x => x.Class).FirstOrDefault() ?? ""
        }).ToList();
    }

    /// <summary>Süreç sırası: adım listesindeki sıra, listede olmayanlar ölçümdeki ilk görülme sırasıyla sonda.</summary>
    public static List<string> StepOrder(Study study)
    {
        var names = DeriveSteps(study).Select(x => x.Name).ToList();
        foreach (var o in TimeOrder(study))
            if (!names.Contains(o.Step)) names.Add(o.Step);
        return names;
    }

    public static List<StepResult> InProcessOrder(Study study, IEnumerable<StepResult> rows)
    {
        var order = StepOrder(study);
        int Rank(string step) { int i = order.IndexOf(step); return i < 0 ? order.Count : i; }
        return rows.OrderBy(r => Rank(r.Step)).ToList();
    }

    /// <summary>Çevrim değişkenliği: ortalama, standart sapma ve ortalamadan %20'den fazla sapan çevrimler (en az 3 çevrimde).</summary>
    public static CycleSpreadResult CycleSpread(IReadOnlyList<CycleResult> cycles, double limitPercent = 20)
    {
        int n = cycles.Count;
        double? mean = n > 0 ? cycles.Sum(c => c.Elapsed) / n : null;
        double? sd = n > 1 ? Math.Sqrt(cycles.Sum(c => Math.Pow(c.Elapsed - mean!.Value, 2)) / (n - 1)) : null;
        var rows = cycles.Select(c =>
        {
            double? diff = mean is double m && m != 0 ? (c.Elapsed - m) / m * 100 : null;
            return new CycleSpreadRow(c.Cycle, c.Elapsed, c.Workload, diff, diff is double d && n > 2 && Math.Abs(d) > limitPercent);
        }).ToList();
        return new CycleSpreadResult(mean, sd, sd is null || mean is null or 0d ? null : sd / mean * 100, rows);
    }

    /// <summary>Çevrim başına standart süre (katkıların toplamı) ve standardı hesaplanamayan ölçülmüş adım var mı.</summary>
    public static (double? Total, bool Incomplete) TotalStandard(IEnumerable<StepResult> rows)
    {
        var list = rows.ToList();
        var with = list.Where(r => r.Contribution is not null).ToList();
        return (with.Count > 0 ? with.Sum(r => r.Contribution!.Value) : null, list.Any(r => r.Contribution is null && r.Count > 0));
    }

    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    /// <summary>Önce / sonra: adımlar ada göre eşleşir (büyük/küçük harf ve baştaki/sondaki boşluk önemsiz). Fark = sonra − önce.</summary>
    public static Comparison Compare(Study before, Study after)
    {
        static string Key(string s) => s.Trim().ToLower(Turkish);
        var a = Analysis.Steps(before);
        var b = Analysis.Steps(after);
        var rows = new List<CompareRow>();
        foreach (var name in StepOrder(before).Concat(StepOrder(after)))
        {
            if (rows.Any(r => Key(r.Step) == Key(name))) continue;
            var x = a.FirstOrDefault(r => Key(r.Step) == Key(name));
            var y = b.FirstOrDefault(r => Key(r.Step) == Key(name));
            if (x is null && y is null) continue;
            double? p = x?.Contribution, q = y?.Contribution;
            rows.Add(new CompareRow(name, p, q, p is not null && q is not null ? q - p : null,
                p is double pv && pv != 0 && q is not null ? (q - pv) / pv * 100 : null));
        }
        var tb = TotalStandard(a).Total;
        var ta = TotalStandard(b).Total;
        return new Comparison(rows, tb, ta, tb is not null && ta is not null ? ta - tb : null,
            tb is double tv && tv != 0 && ta is not null ? (ta - tv) / tv * 100 : null);
    }

    /// <summary>Hedef hataya ulaşmak için gereken yaklaşık gözlem sayısı (t dağılımıyla yinelemeli). Yetersiz satırlar için.</summary>
    public static int RequiredObservations(StepResult r, Settings settings)
    {
        double p = (1 + settings.Confidence / 100) / 2;
        double k = (r.Deviation ?? 0) * 100 / (settings.TargetError * (r.Mean ?? 1));
        int n = r.Count;
        for (int i = 0; i < 50; i++)
        {
            int next = Math.Max(r.Count + 1, (int)Math.Ceiling(Math.Pow(StudentT.InvCDF(0, 1, n - 1, p) * k, 2)));
            if (next == n) break;
            n = next;
        }
        return n;
    }

    /// <summary>Kaldığı yerden devam: son geçerli kaydın ardındaki adım ve çevrim; video son kaydın bitişine gider.</summary>
    public static ResumePoint Resume(Study study)
    {
        var list = study.Steps.Select(x => x.Name).ToList();
        var rows = study.Observations.OrderBy(o => o.EndMs).ThenBy(o => o.StartMs).ToList();
        long? at = rows.Count > 0 ? rows[^1].EndMs : null;
        var last = rows.AsEnumerable().Reverse().FirstOrDefault(o => !o.Excluded && list.Contains(o.Step));
        if (last is null) return new ResumePoint(0, Math.Max(1, study.Observations.Select(o => o.Cycle).DefaultIfEmpty(1).Max()), at);
        int i = list.IndexOf(last.Step);
        return i + 1 < list.Count ? new ResumePoint(i + 1, last.Cycle, at) : new ResumePoint(0, last.Cycle + 1, at);
    }
}

/// <summary>
/// Ölçüm sırası (tarayıcıdaki selectStep / afterRecord / advance): sıradaki adım ve çevrim.
/// Kapsam dışı kayıt sırayı ilerletmez; elle başka adıma geçilmişse önceki adıma döner.
/// </summary>
public sealed class MeasureFlow
{
    public int Index { get; set; }
    public int? Resume { get; set; }
    public int Cycle { get; set; } = 1;

    /// <summary>Elle adım seçimi; önceki adım hatırlanır.</summary>
    public void Select(int index, int count)
    {
        if (index < 0 || index >= count) return;
        if (index != Index) Resume = Index;
        Index = index;
    }

    /// <summary>Sıradaki adıma geçer; son adımdan sonra ilk adıma döner ve çevrimi artırır.</summary>
    public void Advance(int count)
    {
        if (Index + 1 < count) Index++;
        else { Index = 0; Cycle++; }
    }

    public void AfterRecord(bool excluded, bool autoAdvance, int count)
    {
        if (excluded)
        {
            if (Resume is int r && r < count) Index = r;
            Resume = null;
            return;
        }
        Resume = null;
        if (autoAdvance) Advance(count);
    }

    /// <summary>Yeni adım eklendi ve seçildi (araya giren bir kayıtsa sonra önceki adıma dönülür).</summary>
    public void Added(int count)
    {
        if (count > 1) Resume = Index;
        Index = count - 1;
    }

    public void Clamp(int count)
    {
        if (Index >= count) Index = Math.Max(0, count - 1);
    }
}
