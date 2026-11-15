using ClosedXML.Excel;

namespace VideoZamanEtudu;

internal static class StudyExport
{
    public static void Write(Study study, string path)
    {
        using var workbook = new XLWorkbook();
        var raw = Sheet(workbook, "Ham ölçümler", "Çevrim", "Adım", "Başlangıç (ms)", "Bitiş (ms)",
            "Başlangıç (sn)", "Bitiş (sn)", "Süre (sn)", "Sınıf", "Kapsam dışı");
        var steps = Sheet(workbook, "Adım analizi", "Adım", "Geçerli n", "Dışlanan n", "Ortalama (sn)", "Medyan (sn)",
            "Min (sn)", "Maks (sn)", "Örneklem std sapması", "Kritik t", "Bağıl hata (%)", "Yeterlilik",
            "Baz (sn)", "Normal (sn)", "Standart (sn)", "Her N çevrimde", "Standart katkı (sn/çevrim)", "Yöntem");
        var cycles = Sheet(workbook, "Çevrimler", "Çevrim", "Ölçülen geçen süre (sn)", "İş yükü (sn)");
        var groups = Sheet(workbook, "Süre grupları", "Adım", "Grup ortalaması (sn)", "Tekrar");
        var config = Sheet(workbook, "Ayarlar", "Parametre", "Değer");

        int index = 2;
        foreach (var row in study.Observations.OrderBy(x => x.StartMs).ThenBy(x => x.EndMs))
            Row(raw, index++, row.Cycle, row.Step, row.StartMs, row.EndMs, row.StartMs / 1000d, row.EndMs / 1000d,
                row.Seconds, row.Classification, row.Excluded ? "Evet" : "Hayır");

        index = 2;
        int groupIndex = 2;
        foreach (var row in Analysis.Steps(study))
        {
            Row(steps, index++, row.Step, row.Count, row.Excluded, row.Mean, row.Median, row.Min, row.Max,
                row.Deviation, row.CriticalT, row.ErrorPercent, row.Sufficiency, row.Base, row.Normal, row.Standard,
                row.Every, row.Contribution, row.Method);
            foreach (var group in row.Groups) Row(groups, groupIndex++, row.Step, group.Seconds, group.Count);
        }

        index = 2;
        foreach (var row in Analysis.Cycles(study))
            Row(cycles, index++, row.Cycle, row.Elapsed, row.Workload);

        var settings = study.Settings;
        string version = typeof(StudyExport).Assembly.GetName().Version?.ToString(3) ?? "";
        var info = new (string Name, object? Value)[]
        {
            ("Etüt adı", study.Name),
            ("Etüt kodu", study.Code),
            ("Video dosyası", study.VideoLabel),
            ("Ham kayıt sayısı", study.Observations.Count),
            ("Kapsam dışı kayıt sayısı", study.Observations.Count(x => x.Excluded)),
            ("Tempo (%)", settings.Pace),
            ("Tolerans (%)", settings.Allowance),
            ("Güven (%)", settings.Confidence),
            ("Hedef bağıl hata (%)", settings.TargetError),
            ("Baz süre yöntemi", settings.RepeatedLow ? "En çok tekrar eden en küçük süre" : "Ortalama"),
            ("Grup hassasiyeti (ms)", settings.GroupMs),
            ("Süreç kararlı", settings.Stable ? "Evet" : "Hayır"),
            ("Toyota esinli rehber (gözlem)", settings.Stable ? 5 : 10),
            ("Dışa aktarım zamanı", DateTime.Now.ToString("dd.MM.yyyy HH:mm")),
            ("Program sürümü", version)
        };
        for (int i = 0; i < info.Length; i++) Row(config, i + 2, info[i].Name, info[i].Value);
        // double parametreler: (int, int) aşırı yüklemesi satır aralığı olarak yorumlanırdı.
        foreach (var sheet in workbook.Worksheets) sheet.ColumnsUsed().AdjustToContents(10d, 48d);
        workbook.SaveAs(path);
    }

    private static IXLWorksheet Sheet(XLWorkbook workbook, string name, params string[] headers)
    {
        var sheet = workbook.AddWorksheet(name);
        Row(sheet, 1, headers);
        var header = sheet.Range(1, 1, 1, headers.Length);
        header.Style.Font.Bold = true;
        header.Style.Font.FontColor = XLColor.White;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#17364A");
        sheet.SheetView.FreezeRows(1);
        return sheet;
    }

    private static void Row(IXLWorksheet sheet, int row, params object?[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            var cell = sheet.Cell(row, i + 1);
            switch (values[i])
            {
                case string text: cell.Value = text; break;
                case int integer: cell.Value = integer; break;
                case long integer: cell.Value = (double)integer; break;
                case double number:
                    cell.Value = number;
                    cell.Style.NumberFormat.Format = "0.000";
                    break;
            }
        }
    }
}
