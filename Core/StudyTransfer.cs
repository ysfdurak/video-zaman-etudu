using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace VideoZamanEtudu;

public static class StudyTransfer
{
    public const string Extension = ".vzetud";
    private const int MaxBytes = 32 * 1024 * 1024;
    private const string Format = "video-zaman-etudu";
    private const int Version = 1;

    // Türkçe harfler paket dosyasında kaçış dizisi (\u00D6) yerine doğrudan (Ö) yazılır.
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    private static readonly string[] ReservedNames =
    {
        "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    /// <summary>Etüt kodundan Windows'ta geçerli bir dosya adı önerir (ör. "DEMO-01.vzetud").</summary>
    public static string SuggestedFileName(Study study, string extension, string suffix = "")
    {
        var chars = (study.Code ?? "").Trim()
            .Select(c => c < 32 || "<>:\"/\\|?*".Contains(c) ? '_' : c).ToArray();
        string name = new string(chars).Trim().TrimEnd('.', ' ');
        if (name.Length == 0) name = "VideoZamanEtudu";
        if (ReservedNames.Contains(name.ToUpperInvariant())) name += "_";
        if (name.Length > 80) name = name[..80];
        return name + suffix + extension;
    }

    private sealed class Package
    {
        public Package() { }

        public required string Kind { get; init; }
        public required int Version { get; init; }
        public required string Name { get; init; }
        public required string Code { get; init; }
        public required string VideoName { get; init; }
        public required Settings Settings { get; init; }
        public required List<Observation> Observations { get; init; }
        public required Dictionary<string, int> Frequency { get; init; }
    }

    /// <summary>Yazılan paket: zorunlu alanlar + v2 isteğe bağlı alanlar (boşsa yazılmaz; sürüm 1 olarak kalır).</summary>
    private sealed class PackageOut
    {
        public required string Kind { get; init; }
        public required int Version { get; init; }
        public required string Name { get; init; }
        public required string Code { get; init; }
        public required string VideoName { get; init; }
        public required Settings Settings { get; init; }
        public required List<Observation> Observations { get; init; }
        public required Dictionary<string, int> Frequency { get; init; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<StepDef>? Steps { get; init; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Dictionary<string, string>? Info { get; init; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Dictionary<string, double>? StepPace { get; init; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double? Takt { get; init; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Dictionary<string, double>? AllowanceParts { get; init; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? StudyType { get; init; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<SampleCategory>? Categories { get; init; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<Sample>? Samples { get; init; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double? SampleError { get; init; }
    }

    /// <summary>Etüt → paket baytları (UTF-8 JSON). Tarayıcı sürümüyle aynı alan adları ve sıra.</summary>
    public static byte[] ExportBytes(Study study)
    {
        var data = new Package
        {
            Kind = Format, Version = Version, Name = study.Name, Code = study.Code,
            VideoName = FileName(study.Video.Length > 0 ? study.Video : study.ExpectedVideoName),
            Settings = study.Settings, Observations = study.Observations, Frequency = study.Frequency
        };
        Validate(data);
        var steps = StudyRules.CleanSteps(study.Steps);
        var info = StudyRules.CleanInfo(study.Info);
        var pace = StudyRules.CleanStepPace(study.StepPace);
        var parts = StudyRules.MatchingParts(study.AllowanceParts, study.Settings.Allowance);
        var output = new PackageOut
        {
            Kind = data.Kind, Version = data.Version, Name = data.Name, Code = data.Code, VideoName = data.VideoName,
            Settings = data.Settings, Observations = data.Observations, Frequency = data.Frequency,
            Steps = steps.Count > 0 ? steps : null, Info = info.Count > 0 ? info : null,
            StepPace = pace.Count > 0 ? pace : null, Takt = StudyRules.CleanTakt(study.Takt),
            AllowanceParts = parts.Count > 0 ? parts : null,
            StudyType = StudyRules.CleanStudyType(study.StudyType) is { Length: > 0 } type ? type : null,
            Categories = StudyRules.CleanCategories(study.Categories) is { Count: > 0 } cats ? cats : null,
            Samples = StudyRules.CleanSamples(study.Samples) is { Count: > 0 } samples ? samples : null,
            SampleError = StudyRules.CleanSampleError(study.SampleError)
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(output, WriteOptions);
        CheckSize(bytes.Length);
        return bytes;
    }

    public static void Export(Study study, string path)
    {
        var bytes = ExportBytes(study);
        string target = Path.GetFullPath(path);
        string temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temporary, bytes);
            File.Move(temporary, target, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public static Study Import(string path)
    {
        using var file = File.OpenRead(path);
        CheckSize(file.Length);
        using var memory = new MemoryStream();
        file.CopyTo(memory);
        return ImportBytes(memory.ToArray());
    }

    /// <summary>Paket baytları → yeni etüt. Geçersiz paket InvalidDataException fırlatır.</summary>
    public static Study ImportBytes(byte[] bytes)
    {
        CheckSize(bytes.Length);
        Package data;
        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(new MemoryStream(bytes, writable: false));
            root = document.RootElement.Clone();
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("Kind", out var kind) ||
                kind.ValueKind != JsonValueKind.String || kind.GetString() != Format ||
                !root.TryGetProperty("Version", out var version) ||
                version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number))
                throw Keyed(new InvalidDataException("Bu dosya bir Video Zaman Etüdü paketi değil."), "pkg.notPackage");
            if (number != Version)
                throw Keyed(new InvalidDataException("Bu etüt paketi sürümü desteklenmiyor."), "pkg.version");
            data = root.Deserialize<Package>()
                ?? throw Keyed(new InvalidDataException("Etüt paketi boş."), "pkg.corrupt");
        }
        catch (JsonException error)
        {
            throw Keyed(new InvalidDataException("Etüt paketi eksik veya bozuk.", error), "pkg.corrupt");
        }
        Validate(data);
        var settings = data.Settings;
        return new Study
        {
            Name = data.Name, Code = data.Code, ExpectedVideoName = FileName(data.VideoName),
            Settings = settings, Observations = data.Observations, Frequency = data.Frequency,
            // İsteğe bağlı alanlar hoşgörüyle okunur: bozuksa yok sayılır.
            Steps = StudyRules.CleanSteps(Optional<List<StepDef?>>(root, "Steps")),
            Info = StudyRules.CleanInfo(OptionalStrings(root, "Info")),
            StepPace = StudyRules.CleanStepPace(OptionalNumbers(root, "StepPace")),
            Takt = StudyRules.CleanTakt(root.TryGetProperty("Takt", out var takt) && takt.ValueKind == JsonValueKind.Number && takt.TryGetDouble(out var t) ? t : null),
            AllowanceParts = StudyRules.MatchingParts(OptionalNumbers(root, "AllowanceParts"), settings.Allowance),
            StudyType = StudyRules.CleanStudyType(root.TryGetProperty("StudyType", out var st) && st.ValueKind == JsonValueKind.String ? st.GetString() : null),
            Categories = StudyRules.CleanCategories(Optional<List<SampleCategory?>>(root, "Categories")),
            Samples = StudyRules.CleanSamples(Optional<List<Sample?>>(root, "Samples")),
            SampleError = StudyRules.CleanSampleError(root.TryGetProperty("SampleError", out var se) && se.ValueKind == JsonValueKind.Number && se.TryGetDouble(out var sev) ? sev : null)
        };
    }

    private static T? Optional<T>(JsonElement root, string name) where T : class
    {
        if (!root.TryGetProperty(name, out var node)) return null;
        try { return node.Deserialize<T>(); } catch (Exception e) when (e is JsonException or InvalidOperationException or NotSupportedException) { return null; }
    }

    /// <summary>Nesnedeki metin değerleri; metin olmayanlar atlanır.</summary>
    private static Dictionary<string, string>? OptionalStrings(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var node) || node.ValueKind != JsonValueKind.Object) return null;
        return node.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.String)
            .GroupBy(p => p.Name).ToDictionary(g => g.Key, g => g.Last().Value.GetString()!);
    }

    /// <summary>Nesnedeki sayı değerleri; sayı olmayanlar atlanır.</summary>
    private static Dictionary<string, double>? OptionalNumbers(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var node) || node.ValueKind != JsonValueKind.Object) return null;
        var result = new Dictionary<string, double>();
        foreach (var p in node.EnumerateObject())
            if (p.Value.ValueKind == JsonValueKind.Number && p.Value.TryGetDouble(out var v)) result[p.Name] = v;
        return result;
    }

    private static T Keyed<T>(T error, string key) where T : Exception => StudyRules.Keyed(error, key);

    private static string FileName(string path) => path.Replace('\\', '/').Split('/')[^1];

    private static void CheckSize(long length)
    {
        if (length <= 0 || length > MaxBytes)
            throw Keyed(new InvalidDataException("Etüt paketi boş olamaz ve 32 MB’ı aşamaz."), "pkg.size");
    }

    private static void Validate(Package data)
    {
        if (string.IsNullOrWhiteSpace(data.Name) || string.IsNullOrWhiteSpace(data.Code) ||
            data.VideoName is null || data.Settings is null || data.Observations is null || data.Frequency is null)
            throw Keyed(new InvalidDataException("Etüt paketinde zorunlu alanlar eksik."), "pkg.required");
        try
        {
            data.Settings.Validate();
        }
        catch (ArgumentException error)
        {
            throw StudyRules.Keyed(new InvalidDataException("Etüt paketindeki analiz ayarları geçersiz. " + error.Message, error), "pkg.settings", ("detail", "settings.invalid"));
        }
        if (data.Observations.Any(row => row is null || row.StartMs < 0 || row.EndMs <= row.StartMs ||
            row.EndMs > (long)TimeSpan.MaxValue.TotalMilliseconds || row.Cycle < 1 ||
            string.IsNullOrWhiteSpace(row.Step) || row.Classification is not ("" or "VA" or "SVA" or "NVA")))
            throw Keyed(new InvalidDataException("Etüt paketinde geçersiz ölçüm var."), "pkg.observation");
        if (data.Frequency.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Value < 1))
            throw Keyed(new InvalidDataException("Etüt paketinde geçersiz iş sıklığı var."), "pkg.frequency");
    }
}
