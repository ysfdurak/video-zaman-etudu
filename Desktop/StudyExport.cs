using ClosedXML.Excel;

namespace VideoZamanEtudu;

/// <summary>
/// Excel çıktısı. İlk sayfa "Özet" (v2, tarayıcı sürümüyle aynı satırlar); sonraki beş sayfa v1.2 ile aynı sütunlarda.
/// Türkçe arayüzde başlıklar Türkçe, İngilizcede İngilizcedir (metinler tarayıcı sözlüğünden).
/// </summary>
internal static class StudyExport
{
    public static void Write(Study study, string path, string lang)
    {
        string Tx(string key) => L.TIn(lang, key);
        string[] Head(string key) => Tx(key).Split('|');
        string YesNo(bool v) => Tx(v ? "yes" : "no");
        using var workbook = new XLWorkbook();

        // Özet
        var rows = StudyRules.InProcessOrder(study, Analysis.Steps(study));
        var (total, _) = StudyRules.TotalStandard(rows);
        var spread = StudyRules.CycleSpread(Analysis.Cycles(study));
        var defs = StudyRules.DeriveSteps(study).GroupBy(x => x.Name).ToDictionary(g => g.Key, g => g.First());
        var byClass = new Dictionary<string, long>();
        long all = 0;
        foreach (var o in study.Observations.Where(o => !o.Excluded && o.EndMs > o.StartMs))
        {
            string k = o.Classification.Length > 0 ? o.Classification : Tx("x.o.noClass");
            byClass[k] = byClass.GetValueOrDefault(k) + (o.EndMs - o.StartMs);
            all += o.EndMs - o.StartMs;
        }
        var s = study.Settings;
        var parts = StudyRules.MatchingParts(study.AllowanceParts, s.Allowance);
        var summary = new List<object?[]>
        {
            new object?[] { Tx("x.s.name"), study.Name }, new object?[] { Tx("x.s.code"), study.Code }
        };
        foreach (var k in StudyRules.InfoFields.Where(k => study.Info.ContainsKey(k)))
            summary.Add(new object?[] { Tx("info." + k), study.Info[k] });
        summary.Add(new object?[] { Tx("x.o.total"), total });
        summary.Add(new object?[] { Tx("x.o.rate"), total > 0 ? 3600 / total : null });
        if (study.Takt is double takt)
        {
            summary.Add(new object?[] { Tx("x.o.takt"), takt });
            summary.Add(new object?[] { Tx("x.o.load"), total > 0 ? total / takt * 100 : null });
            summary.Add(new object?[] { Tx("x.o.operators"), total > 0 ? total / takt : null });
        }
        summary.Add(new object?[] { Tx("x.o.cycles"), spread.Rows.Count });
        summary.Add(new object?[] { Tx("x.o.cv"), spread.Variation });
        summary.Add(new object?[] { Tx("x.o.insufficient"), rows.Count(r => r.Sufficiency == "Yetersiz") });
        summary.Add(new object?[] { Tx("x.o.notComputable"), rows.Count(r => r.Sufficiency == "Hesaplanamıyor" && r.Count > 0) });
        foreach (var (k, v) in byClass.OrderByDescending(x => x.Value))
            summary.Add(new object?[] { Tx("x.o.share") + " " + k, (double)v / all * 100 });
        summary.Add(new object?[] { Tx("x.s.pace"), s.Pace });
        summary.Add(new object?[] { Tx("x.s.allowance"), s.Allowance });
        foreach (var k in StudyRules.AllowancePartNames.Where(parts.ContainsKey))
            summary.Add(new object?[] { Tx("x.o.al") + " " + Tx("al." + k), parts[k] });
        var sumSheet = Sheet(workbook, Tx("x.sheet.summary"), Head("x.summary"));
        int index = 2;
        foreach (var r in summary) Row(sumSheet, index++, r);
        index++;
        var stepHead = Head("x.o.steps");
        Row(sumSheet, index, stepHead);
        Header(sumSheet.Range(index, 1, index, stepHead.Length));
        index++;
        foreach (var r in rows)
        {
            var def = defs.GetValueOrDefault(r.Step);
            Row(sumSheet, index++, r.Step, def?.Classification ?? "", r.Count, r.Pace, r.Standard, r.Every, r.Contribution,
                total > 0 && r.Contribution is double c ? c / total * 100 : null, L.Result(r.Sufficiency, lang),
                r.Sufficiency == "Yetersiz" ? StudyRules.RequiredObservations(r, s) : null, def?.Note ?? "");
        }

        // Ham ölçümler: not sütunu yalnız notlu kayıt varsa (yoksa v1.2 ile aynı).
        bool notes = study.Observations.Any(o => o.Note != null);
        var rawHead = Head("x.raw").ToList();
        if (notes) rawHead.Add(Tx("x.note"));
        var raw = Sheet(workbook, Tx("x.sheet.raw"), rawHead.ToArray());
        var steps = Sheet(workbook, Tx("x.sheet.steps"), Head("x.steps"));
        var cycles = Sheet(workbook, Tx("x.sheet.cycles"), Head("x.cycles"));
        var groups = Sheet(workbook, Tx("x.sheet.groups"), Head("x.groups"));
        var config = Sheet(workbook, Tx("x.sheet.settings"), Head("x.settings"));

        index = 2;
        foreach (var row in study.Observations.OrderBy(x => x.StartMs).ThenBy(x => x.EndMs))
        {
            var values = new List<object?> { row.Cycle, row.Step, row.StartMs, row.EndMs, row.StartMs / 1000d, row.EndMs / 1000d,
                row.Seconds, row.Classification, YesNo(row.Excluded) };
            if (notes) values.Add(row.Note ?? "");
            Row(raw, index++, values.ToArray());
        }

        index = 2;
        int groupIndex = 2;
        foreach (var row in Analysis.Steps(study))
        {
            Row(steps, index++, row.Step, row.Count, row.Excluded, row.Mean, row.Median, row.Min, row.Max,
                row.Deviation, row.CriticalT, row.ErrorPercent, L.Result(row.Sufficiency, lang), row.Base, row.Normal, row.Standard,
                row.Every, row.Contribution, L.Result(row.Method, lang));
            foreach (var group in row.Groups) Row(groups, groupIndex++, row.Step, group.Seconds, group.Count);
        }

        index = 2;
        foreach (var row in Analysis.Cycles(study))
            Row(cycles, index++, row.Cycle, row.Elapsed, row.Workload);

        string version = typeof(StudyExport).Assembly.GetName().Version?.ToString(3) ?? "";
        var info = new (string Name, object? Value)[]
        {
            (Tx("x.s.name"), study.Name),
            (Tx("x.s.code"), study.Code),
            (Tx("x.s.video"), study.VideoLabel),
            (Tx("x.s.raw"), study.Observations.Count),
            (Tx("x.s.excluded"), study.Observations.Count(x => x.Excluded)),
            (Tx("x.s.pace"), s.Pace),
            (Tx("x.s.allowance"), s.Allowance),
            (Tx("x.s.confidence"), s.Confidence),
            (Tx("x.s.target"), s.TargetError),
            (Tx("x.s.method"), Tx(s.RepeatedLow ? "x.s.methodRepeated" : "x.s.methodMean")),
            (Tx("x.s.group"), s.GroupMs),
            (Tx("x.s.stable"), YesNo(s.Stable)),
            (Tx("x.s.guide"), s.Stable ? 5 : 10),
            (Tx("x.s.exported"), lang == "tr" ? DateTime.Now.ToString("dd.MM.yyyy HH:mm") : DateTime.Now.ToString("yyyy-MM-dd HH:mm")),
            (Tx("x.s.version"), $"{version} ({Tx("x.s.edition")})")
        };
        for (int i = 0; i < info.Length; i++) Row(config, i + 2, info[i].Name, info[i].Value);
        // double parametreler: (int, int) aşırı yüklemesi satır aralığı olarak yorumlanırdı.
        foreach (var sheet in workbook.Worksheets) sheet.ColumnsUsed().AdjustToContents(10d, 48d);
        workbook.SaveAs(path);
    }

    private static void Header(IXLRange header)
    {
        header.Style.Font.Bold = true;
        header.Style.Font.FontColor = XLColor.White;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#17364A");
    }

    private static IXLWorksheet Sheet(XLWorkbook workbook, string name, params string[] headers)
    {
        var sheet = workbook.AddWorksheet(name);
        Row(sheet, 1, headers);
        Header(sheet.Range(1, 1, 1, headers.Length));
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
