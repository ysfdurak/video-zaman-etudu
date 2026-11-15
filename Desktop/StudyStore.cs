using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace VideoZamanEtudu;

internal sealed class StudyStore
{
    private readonly string connectionString;

    public StudyStore()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VideoZamanEtudu");
        Directory.CreateDirectory(directory);
        connectionString = new SqliteConnectionStringBuilder { DataSource = Path.Combine(directory, "studies.sqlite") }.ToString();
        using var db = Open();
        using var command = db.CreateCommand();
        command.CommandText = "CREATE TABLE IF NOT EXISTS studies (id TEXT PRIMARY KEY, updated TEXT NOT NULL, data TEXT NOT NULL)";
        command.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var db = new SqliteConnection(connectionString);
        db.Open();
        return db;
    }

    /// <summary>Son ReadAll çağrısında okunamayıp atlanan kayıt sayısı.</summary>
    public int LastSkipped { get; private set; }

    public List<Study> ReadAll()
    {
        using var db = Open();
        using var command = db.CreateCommand();
        command.CommandText = "SELECT data FROM studies ORDER BY updated DESC";
        using var reader = command.ExecuteReader();
        var studies = new List<Study>();
        int skipped = 0;
        while (reader.Read())
        {
            // Tek bir bozuk satır tüm listeyi ve uygulamanın açılışını engellemesin.
            try
            {
                var study = JsonSerializer.Deserialize<Study>(reader.GetString(0));
                if (study?.Observations is null || study.Settings is null || study.Frequency is null) skipped++;
                else studies.Add(study);
            }
            catch (Exception error) when (error is JsonException or InvalidCastException or NotSupportedException)
            {
                skipped++;
            }
        }
        LastSkipped = skipped;
        return studies;
    }

    public void Delete(Guid id)
    {
        using var db = Open();
        using var command = db.CreateCommand();
        command.CommandText = "DELETE FROM studies WHERE id = $id";
        command.Parameters.AddWithValue("$id", id.ToString());
        command.ExecuteNonQuery();
    }

    public void Save(Study study)
    {
        using var db = Open();
        using var command = db.CreateCommand();
        command.CommandText = """
            INSERT INTO studies (id, updated, data) VALUES ($id, $updated, $data)
            ON CONFLICT (id) DO UPDATE SET updated = excluded.updated, data = excluded.data
            """;
        command.Parameters.AddWithValue("$id", study.Id.ToString());
        command.Parameters.AddWithValue("$updated", study.Updated.ToString("O"));
        command.Parameters.AddWithValue("$data", JsonSerializer.Serialize(study));
        command.ExecuteNonQuery();
    }
}
