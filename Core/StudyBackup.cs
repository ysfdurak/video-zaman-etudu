using System.IO.Compression;

namespace VideoZamanEtudu;

public record BackupResult(int Count, int Skipped);
public record RestorePlan(IReadOnlyList<Study> Add, int Same, IReadOnlyList<string> Failed);

/// <summary>
/// Toplu yedek (v2): bütün etütler tek .zip içinde, her biri ayrı bir .vzetud paketi.
/// Tarayıcı sürümünün yedeğiyle aynı biçim; iki sürüm birbirinin yedeğini açar.
/// Geri yüklemede aynı içerikteki etüt ikinci kez eklenmez; mevcut etütler değişmez.
/// </summary>
public static class StudyBackup
{
    private const int MaxEntries = 1000;

    public static BackupResult Write(IEnumerable<Study> studies, string path)
    {
        int count = 0, skipped = 0;
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string target = Path.GetFullPath(path);
        string temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = File.Create(temporary))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                foreach (var study in studies)
                {
                    byte[] bytes;
                    try { bytes = StudyTransfer.ExportBytes(study); }
                    catch (Exception e) when (e is InvalidDataException or ArgumentException) { skipped++; continue; }
                    string name = StudyTransfer.SuggestedFileName(study, StudyTransfer.Extension);
                    for (int n = 2; used.Contains(name); n++) name = StudyTransfer.SuggestedFileName(study, StudyTransfer.Extension, "-" + n);
                    used.Add(name);
                    var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
                    using var output = entry.Open();
                    output.Write(bytes);
                    count++;
                }
            }
            File.Move(temporary, target, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
        return new BackupResult(count, skipped);
    }

    /// <summary>Dosyanın ilk baytları ZIP imzası mı ("PK\x03\x04" ya da boş arşiv "PK\x05\x06").</summary>
    public static bool IsZip(string path)
    {
        using var file = File.OpenRead(path);
        Span<byte> head = stackalloc byte[4];
        return file.Read(head) == 4 && head[0] == 0x50 && head[1] == 0x4b && (head[2] == 3 || head[2] == 5);
    }

    /// <summary>Yedeği okur ve mevcut etütlerle karşılaştırır. Hiçbir şey kaydetmez.</summary>
    public static RestorePlan Read(string path, IEnumerable<Study> existing)
    {
        ZipArchive archive;
        try { archive = ZipFile.OpenRead(path); }
        catch (InvalidDataException e) { throw StudyRules.Keyed(new InvalidDataException("Bu dosya bir yedek (.zip) değil.", e), "backup.notZip"); }
        using (archive)
        {
            if (archive.Entries.Count > MaxEntries) throw StudyRules.Keyed(new InvalidDataException("Yedekte çok fazla dosya var."), "backup.tooMany");
            var entries = archive.Entries.Where(e => e.FullName.EndsWith(StudyTransfer.Extension, StringComparison.OrdinalIgnoreCase)).ToList();
            if (entries.Count == 0) throw StudyRules.Keyed(new InvalidDataException("Yedekte etüt paketi (.vzetud) yok."), "backup.empty");
            var known = new HashSet<string>();
            foreach (var study in existing)
            {
                try { known.Add(Text(StudyTransfer.ExportBytes(study))); }
                catch (Exception e) when (e is InvalidDataException or ArgumentException) { /* bozuk kayıt karşılaştırılmaz */ }
            }
            var add = new List<Study>();
            var failed = new List<string>();
            int same = 0;
            foreach (var entry in entries)
            {
                try
                {
                    if (entry.Length > 32 * 1024 * 1024) throw new InvalidDataException();
                    using var input = entry.Open();
                    using var memory = new MemoryStream();
                    input.CopyTo(memory);
                    var study = StudyTransfer.ImportBytes(memory.ToArray());
                    string text = Text(StudyTransfer.ExportBytes(study));
                    if (!known.Add(text)) { same++; continue; }
                    add.Add(study);
                }
                catch (Exception e) when (e is InvalidDataException or ArgumentException or IOException)
                {
                    failed.Add(entry.Name);
                }
            }
            return new RestorePlan(add, same, failed);
        }
    }

    private static string Text(byte[] bytes) => System.Text.Encoding.UTF8.GetString(bytes);
}
