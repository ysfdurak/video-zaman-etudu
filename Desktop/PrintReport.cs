using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace VideoZamanEtudu;

/// <summary>
/// Yazdır / PDF (v2): analiz sayfasının kâğıt hâli. Başlıkta etüt bilgileri ve analizin dayandığı ayarlar,
/// ardından özet, grafikler, adım ve çevrim tabloları. PDF için Windows'un "Microsoft Print to PDF" yazıcısı seçilir.
/// </summary>
internal static class PrintReport
{
    /// <summary>v2.2: "Etüt durumu: Onaylandı · onaylayan · tarih" (taslakta yok).</summary>
    public static string? StatusLine(Study study)
    {
        if (study.Status.Length == 0) return null;
        var detail = study.Status == "approved"
            ? new[] { study.ApprovedBy, study.ApprovedAt.Length >= 10 ? (L.Lang == "tr" ? $"{study.ApprovedAt[8..10]}.{study.ApprovedAt[5..7]}.{study.ApprovedAt[..4]}" : study.ApprovedAt[..10]) : "" }.Where(x => x.Length > 0).ToList()
            : new List<string>();
        return L.T("print.status", ("status", L.T("status." + study.Status)), ("detail", detail.Count > 0 ? " · " + string.Join(" · ", detail) : ""));
    }

    public static void Print(Window owner, Study study, IReadOnlyList<(string Label, string Value, IReadOnlyList<string> Details)> cards,
        Func<double, IReadOnlyList<UIElement>> charts)
    {
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true) return;
        double width = dialog.PrintableAreaWidth, height = dialog.PrintableAreaHeight;
        var doc = new FlowDocument
        {
            PageWidth = width, PageHeight = height, ColumnWidth = width, PagePadding = new Thickness(40),
            FontFamily = new FontFamily("Segoe UI"), FontSize = 10.5, Foreground = Ui.Ink
        };
        double content = width - 80;
        doc.Blocks.Add(new Paragraph(new Bold(new Run($"{study.Code} · {study.Name}"))) { FontSize = 16, Margin = new Thickness(0, 0, 0, 2) });
        var info = StudyRules.InfoFields.Where(study.Info.ContainsKey).Select(k => $"{L.T("info." + k)}: {study.Info[k]}").ToList();
        if (info.Count > 0) doc.Blocks.Add(new Paragraph(new Run(string.Join(" · ", info))) { Margin = new Thickness(0, 0, 0, 2) });
        if (StatusLine(study) is string status) doc.Blocks.Add(new Paragraph(new Run(status)) { Margin = new Thickness(0, 0, 0, 2) });
        doc.Blocks.Add(new Paragraph(new Run(SettingsLine(study))) { Margin = new Thickness(0, 0, 0, 10), Foreground = Ui.Soft });

        doc.Blocks.Add(Heading(L.T("h.results")));
        var cardTable = new Table { CellSpacing = 4 };
        int per = 3;
        for (int i = 0; i < per; i++) cardTable.Columns.Add(new TableColumn { Width = new GridLength(content / per) });
        var group = new TableRowGroup();
        cardTable.RowGroups.Add(group);
        for (int i = 0; i < cards.Count; i += per)
        {
            var row = new TableRow();
            foreach (var card in cards.Skip(i).Take(per))
            {
                var p = new Paragraph { Margin = new Thickness(0) };
                p.Inlines.Add(new Run(card.Label) { Foreground = Ui.Soft, FontSize = 9.5 });
                p.Inlines.Add(new LineBreak());
                p.Inlines.Add(new Bold(new Run(card.Value)) { FontSize = 13 });
                foreach (var d in card.Details) { p.Inlines.Add(new LineBreak()); p.Inlines.Add(new Run(d) { FontSize = 9.5 }); }
                row.Cells.Add(new TableCell(p) { BorderBrush = Ui.Line, BorderThickness = new Thickness(1), Padding = new Thickness(6) });
            }
            group.Rows.Add(row);
        }
        doc.Blocks.Add(cardTable);
        foreach (var chart in charts(content)) doc.Blocks.Add(new BlockUIContainer(chart) { Margin = new Thickness(0, 6, 0, 6) });

        if (study.IsSampling)
        {
            var r = Advanced.AnalyzeSampling(study);
            doc.Blocks.Add(Heading(L.T("s.results")));
            doc.Blocks.Add(TableOf(content, new[] { L.T("s.thCategory"), L.T("th.class"), L.T("s.thCount"), L.T("th.share"), L.T("s.thRange"), L.T("s.thRequired") },
                r.Rows.Select(x => new[] { x.Category, x.Classification, x.Count.ToString(), L.Pct(x.Share, 1), x.Low is null ? "" : $"{L.Pct(x.Low, 1)} – {L.Pct(x.High, 1)}", x.Required?.ToString() ?? "" }).ToList(), firstWide: true));
            Foot(doc, study);
            dialog.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, $"{study.Code} · {L.T("app.title")}");
            return;
        }
        var rows = StudyRules.InProcessOrder(study, Analysis.Steps(study));
        doc.Blocks.Add(Heading(L.T("h.stepTable")));
        doc.Blocks.Add(TableOf(content,
            new[] { "th.r.step", "th.r.n", "th.r.excluded", "th.r.mean", "th.r.sd", "th.r.error", "th.r.sufficiency", "th.r.base", "th.r.pace", "th.r.normal", "th.r.standard", "th.r.every", "th.r.contribution" }
                .Select(k => L.T(k)).ToArray(),
            rows.Select(r => new[] { r.Step, r.Count.ToString(), r.Excluded.ToString(), L.Num3(r.Mean), L.Num3(r.Deviation), L.Num3(r.ErrorPercent),
                L.Result(r.Sufficiency), L.Num3(r.Base), L.Num(r.Pace) + (study.StepPace.ContainsKey(r.Step) ? " *" : ""), L.Num3(r.Normal), L.Num3(r.Standard),
                r.Every.ToString(), L.Num3(r.Contribution) }).ToList(), firstWide: true));

        var spread = StudyRules.CycleSpread(Analysis.Cycles(study));
        doc.Blocks.Add(Heading(L.T("h.cycles")));
        doc.Blocks.Add(TableOf(content * 0.7, new[] { L.T("th.c.cycle"), L.T("th.c.elapsed"), L.T("th.c.workload"), L.T("th.c.diff") },
            spread.Rows.Select(c => new[] { c.Cycle.ToString(), L.Num3(c.Elapsed), L.Num3(c.Workload), (c.Outlier ? "⚠ " : "") + L.Pct(c.DiffPercent, 0, true) }).ToList()));

        Foot(doc, study);
        dialog.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, $"{study.Code} · {L.T("app.title")}");
    }

    private static void Foot(FlowDocument doc, Study study)
    {
        string version = typeof(PrintReport).Assembly.GetName().Version?.ToString(3) ?? "";
        doc.Blocks.Add(new Paragraph(new Run(L.T("print.foot", ("code", study.Code), ("name", study.Name), ("date", L.Date(DateTime.Now))) + $" · {L.T("app.title")} {version}"))
        { Foreground = Ui.Soft, Margin = new Thickness(0, 12, 0, 0) });
    }

    /// <summary>Adım kartları (görsel iş talimatı): sayfa başına iki sütun.</summary>
    public static void PrintStory(Window owner, Study study, Func<double, IReadOnlyList<Border>> cards)
    {
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true) return;
        double width = dialog.PrintableAreaWidth, height = dialog.PrintableAreaHeight;
        var doc = new FlowDocument
        {
            PageWidth = width, PageHeight = height, ColumnWidth = width, PagePadding = new Thickness(40),
            FontFamily = new FontFamily("Segoe UI"), FontSize = 10.5, Foreground = Ui.Ink
        };
        doc.Blocks.Add(new Paragraph(new Bold(new Run(L.T("story.title", ("code", study.Code), ("name", study.Name))))) { FontSize = 16 });
        double content = width - 80, cardWidth = content / 2 - 12;
        var table = new Table { CellSpacing = 8 };
        table.Columns.Add(new TableColumn { Width = new GridLength(content / 2) });
        table.Columns.Add(new TableColumn { Width = new GridLength(content / 2) });
        var group = new TableRowGroup();
        table.RowGroups.Add(group);
        var list = cards(cardWidth);
        for (int i = 0; i < list.Count; i += 2)
        {
            var row = new TableRow();
            foreach (var card in list.Skip(i).Take(2))
            {
                card.Margin = new Thickness(0);
                row.Cells.Add(new TableCell(new BlockUIContainer(card)));
            }
            group.Rows.Add(row);
        }
        doc.Blocks.Add(table);
        Foot(doc, study);
        dialog.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, $"{study.Code} · {L.T("h.story")}");
    }

    public static string SettingsLine(Study study)
    {
        if (study.IsSampling)
            return $"{L.T("field.confidence")} {L.Num(study.Settings.Confidence)} · {L.T("field.sampleError")} {L.Num(study.SampleError ?? 5)}";
        var s = study.Settings;
        var parts = StudyRules.MatchingParts(study.AllowanceParts, s.Allowance);
        string line = L.T("print.settings", ("conf", L.Num(s.Confidence)), ("target", L.Num(s.TargetError)), ("pace", L.Num(s.Pace)),
            ("al", L.Num(s.Allowance)), ("method", L.T(s.RepeatedLow ? "method.repeated" : "method.mean")));
        if (parts.Count > 0)
            line += " (" + string.Join(" + ", StudyRules.AllowancePartNames.Where(parts.ContainsKey).Select(k => PartName(k) + " " + L.Num(parts[k]))) + ")";
        if (study.Takt is double takt) line += " · " + L.T("print.takt", ("takt", L.Num(takt)));
        if (study.StepPace.Count > 0) line += " · " + L.T("print.stepPace");
        return line;
    }

    /// <summary>"Kişisel ihtiyaç %" → "Kişisel ihtiyaç".</summary>
    public static string PartName(string key) => L.T("al." + key).Replace("%", "").Trim();

    private static Paragraph Heading(string text) => new(new Bold(new Run(text))) { FontSize = 13, Margin = new Thickness(0, 10, 0, 4) };

    private static Table TableOf(double width, string[] headers, IReadOnlyList<string[]> rows, bool firstWide = false)
    {
        var table = new Table { CellSpacing = 0, BorderBrush = Ui.Line, BorderThickness = new Thickness(0, 1, 0, 0) };
        double first = firstWide ? width * 0.16 : width / headers.Length;
        double other = firstWide ? (width - first) / (headers.Length - 1) : first;
        for (int i = 0; i < headers.Length; i++) table.Columns.Add(new TableColumn { Width = new GridLength(i == 0 ? first : other) });
        var group = new TableRowGroup();
        table.RowGroups.Add(group);
        group.Rows.Add(RowOf(headers, true));
        foreach (var r in rows) group.Rows.Add(RowOf(r, false));
        return table;
    }

    private static TableRow RowOf(string[] cells, bool head)
    {
        var row = new TableRow();
        for (int i = 0; i < cells.Length; i++)
        {
            var run = new Run(cells[i]);
            var p = new Paragraph(head ? new Bold(run) : (Inline)run) { Margin = new Thickness(0), TextAlignment = i == 0 || head ? TextAlignment.Left : TextAlignment.Right, FontSize = 9 };
            row.Cells.Add(new TableCell(p) { Padding = new Thickness(3, 2, 3, 2), BorderBrush = Ui.Line, BorderThickness = new Thickness(0, 0, 0, 1) });
        }
        return row;
    }
}
