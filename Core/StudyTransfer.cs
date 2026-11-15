using System.Text.Encodings.Web;
using System.Text.Json;
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

    public static void Export(Study study, string path)
    {
        var data = new Package
        {
            Kind = Format, Version = Version, Name = study.Name, Code = study.Code,
            VideoName = FileName(study.Video.Length > 0 ? study.Video : study.ExpectedVideoName),
            Settings = study.Settings, Observations = study.Observations, Frequency = study.Frequency
        };
        Validate(data);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(data, WriteOptions);
        CheckSize(bytes.Length);
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
        Package data;
        try
        {
            using var document = JsonDocument.Parse(file);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("Kind", out var kind) ||
                kind.ValueKind != JsonValueKind.String || kind.GetString() != Format ||
                !document.RootElement.TryGetProperty("Version", out var version) ||
                version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number))
                throw new InvalidDataException("Bu dosya bir Video Zaman Etüdü paketi değil.");
            if (number != Version)
                throw new InvalidDataException("Bu etüt paketi sürümü desteklenmiyor.");
            data = document.RootElement.Deserialize<Package>()
                ?? throw new InvalidDataException("Etüt paketi boş.");
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("Etüt paketi eksik veya bozuk.", error);
        }
        Validate(data);
        return new Study
        {
            Name = data.Name, Code = data.Code, ExpectedVideoName = FileName(data.VideoName),
            Settings = data.Settings, Observations = data.Observations, Frequency = data.Frequency
        };
    }

    private static string FileName(string path) => path.Replace('\\', '/').Split('/')[^1];

    private static void CheckSize(long length)
    {
        if (length <= 0 || length > MaxBytes)
            throw new InvalidDataException("Etüt paketi boş olamaz ve 32 MB’ı aşamaz.");
    }

    private static void Validate(Package data)
    {
        if (string.IsNullOrWhiteSpace(data.Name) || string.IsNullOrWhiteSpace(data.Code) ||
            data.VideoName is null || data.Settings is null || data.Observations is null || data.Frequency is null)
            throw new InvalidDataException("Etüt paketinde zorunlu alanlar eksik.");
        try
        {
            data.Settings.Validate();
        }
        catch (ArgumentException error)
        {
            throw new InvalidDataException("Etüt paketindeki analiz ayarları geçersiz. " + error.Message, error);
        }
        if (data.Observations.Any(row => row is null || row.StartMs < 0 || row.EndMs <= row.StartMs ||
            row.EndMs > (long)TimeSpan.MaxValue.TotalMilliseconds || row.Cycle < 1 ||
            string.IsNullOrWhiteSpace(row.Step) || row.Classification is not ("" or "VA" or "SVA" or "NVA")))
            throw new InvalidDataException("Etüt paketinde geçersiz ölçüm var.");
        if (data.Frequency.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Value < 1))
            throw new InvalidDataException("Etüt paketinde geçersiz iş sıklığı var.");
    }
}
