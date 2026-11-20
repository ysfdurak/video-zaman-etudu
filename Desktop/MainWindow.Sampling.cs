using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace VideoZamanEtudu;

/// <summary>
/// v2.1 ölçüm ekleri: kayıt temposu, adımın türü/israfı/fikri/karesi ve iş örneklemesi paneli
/// (tarayıcıdaki Web/js/app.js v1.8 ile aynı akış).
/// </summary>
public partial class MainWindow
{
    private UIElement timeSide = null!;
    private UIElement samplingSide = null!;
    private readonly WrapPanel categoriesPanel = new() { Margin = new Thickness(0, 2, 0, 4) };
    private readonly TextBox newCategory = Ui.Input(220);
    private readonly ComboBox newCategoryClass = Ui.ClassCombo();
    private readonly TextBox sampleStep = Ui.Input(50, "30");
    private readonly TextBlock sampleCount = Ui.Text("", 13.5, true, Ui.Accent);
    private readonly DataGrid samples = Ui.Table();
    private readonly Border sampleEditor = new();
    private readonly TextBox sampleNote = Ui.Input(300);
    private readonly List<Sample> sampleUndo = new();
    private readonly Random random = new();
    private Sample? selectedSample;

    /// <summary>Tempo puanı alanı: boş → null; geçersiz → hata.</summary>
    private static double? ReadRating(string text)
    {
        if (text.Trim().Length == 0) return null;
        double v = Number(text, "name.rating");
        return StudyRules.CleanRating(v) ?? throw Error("msg.ratingInvalid");
    }

    /// <summary>Seçili adımın türü, israf türü ya da fikri (boş değer alanı siler).</summary>
    private void SetStepFrame()
    {
        RequireVideo();
        if (CurrentStep is not StepDef st) throw Error("msg.noSteps");
        st.FrameMs = PositionMs;
        SaveSteps();
        SetStatus(L.T("status.frameSet", ("step", st.Name), ("at", L.Clock(st.FrameMs.Value))));
    }

    // ------------------------------------------------------------ iş örneklemesi paneli

    private UIElement BuildSamplingSide()
    {
        newCategory.PreviewKeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; Guard(AddCategory); } };
        Ui.Columns(samples, (L.T("s.thAt"), "At", true), (L.T("s.thCategory"), "Category", false), (L.T("th.class"), "Class", false), (L.T("th.note"), "Note", false));
        samples.MinHeight = 160;
        samples.SelectionChanged += (_, _) =>
        {
            if (suppressSelection || samples.SelectedItem is not SampleRow row) return;
            selectedSample = row.Source;
            sampleNote.Text = row.Source.Note ?? "";
            sampleEditor.Visibility = Visibility.Visible;
            if (ready) Seek(row.Source.AtMs ?? 0);
        };
        sampleEditor.Child = Ui.Row(Ui.Field(L.T("field.rowNote"), sampleNote), Btn("btn.editRow", SaveSampleNote, primary: true), Btn("btn.deleteRow", DeleteSample, danger: true));
        sampleEditor.Visibility = Visibility.Collapsed;
        var head = new DockPanel();
        var keys = Ui.Hint(L.T("s.keys"));
        DockPanel.SetDock(keys, Dock.Right);
        head.Children.Add(keys);
        head.Children.Add(Ui.Text(L.T("s.title"), 14, true));
        var box = Ui.Box(Ui.Stack(head, Ui.Hint(L.T("s.how")), categoriesPanel,
            Ui.Row(Ui.Field(L.T("s.newCategory"), Ui.WithPlaceholder(newCategory, L.T("s.newCategoryPh"))), Ui.Field(L.T("field.class"), newCategoryClass), Btn("s.addCategory", AddCategory))));
        var random_ = Btn("s.random", GoRandomMoment, primary: true);
        var top = Ui.Stack(box,
            Ui.Row(random_, Ui.Field(L.T("s.every") + " (" + L.T("s.seconds") + ")", sampleStep), Btn("s.next", NextMoment), Btn("s.undo", UndoSample)),
            sampleCount);
        var panel = new DockPanel();
        DockPanel.SetDock(top, Dock.Top);
        DockPanel.SetDock(sampleEditor, Dock.Bottom);
        panel.Children.Add(top);
        panel.Children.Add(sampleEditor);
        panel.Children.Add(samples);
        return new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    private string CategoryClass(string name) => current?.Categories.FirstOrDefault(c => c.Name == name)?.Classification ?? "";

    private void RenderSampling()
    {
        if (current is not { IsSampling: true } study) return;
        categoriesPanel.Children.Clear();
        if (study.Categories.Count == 0) categoriesPanel.Children.Add(Ui.Hint(L.T("s.empty")));
        var counts = study.Samples.GroupBy(x => x.Category).ToDictionary(g => g.Key, g => g.Count());
        for (int i = 0; i < study.Categories.Count; i++)
        {
            var c = study.Categories[i];
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            content.Children.Add(new Border
            {
                Background = Ui.Brush("#E3EAEE"), CornerRadius = new CornerRadius(3), Padding = new Thickness(5, 0, 5, 0), Margin = new Thickness(0, 0, 6, 0),
                Child = new TextBlock { Text = i < 9 ? (i + 1).ToString() : "·", FontSize = 11.5, Foreground = Ui.Soft }
            });
            content.Children.Add(new TextBlock { Text = $"{c.Name} · {counts.GetValueOrDefault(c.Name)}", Foreground = Ui.Ink });
            if (c.Classification.Length > 0)
                content.Children.Add(new Border
                {
                    BorderBrush = Ui.ClassBrush(c.Classification), BorderThickness = new Thickness(3, 0, 0, 0), Margin = new Thickness(6, 0, 0, 0), Padding = new Thickness(4, 0, 0, 0),
                    Child = new TextBlock { Text = c.Classification, FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = Ui.Ink }
                });
            var chip = new Button { Content = content, Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 0, 6, 6), Background = Ui.Paper, BorderBrush = Ui.Line };
            int index = i;
            chip.Click += (_, _) => Guard(() => RecordSample(index));
            categoriesPanel.Children.Add(chip);
        }
        var view = study.Samples.OrderBy(x => x.AtMs).Select(x => new SampleRow(x, L.Clock(x.AtMs ?? 0), x.Category, CategoryClass(x.Category), x.Note ?? "")).ToList();
        suppressSelection = true;
        samples.ItemsSource = view;
        samples.SelectedItem = view.FirstOrDefault(v => v.Source == selectedSample);
        suppressSelection = false;
        if (selectedSample is null || !study.Samples.Contains(selectedSample)) sampleEditor.Visibility = Visibility.Collapsed;
        var r = Advanced.AnalyzeSampling(study);
        sampleCount.Text = r.N > 0 ? L.T("s.count", ("n", r.N), ("e", L.Num(r.Error)), ("req", r.Required)) : L.T("s.countNone");
        RenderSegments();
    }

    private void RecordSample(int index)
    {
        RequireVideo();
        var study = current!;
        if (index < 0 || index >= study.Categories.Count) throw Error("s.noCategory");
        var cat = study.Categories[index];
        long at = PositionMs;
        if (study.Samples.Any(x => x.AtMs == at)) throw Error("s.duplicate");
        var sample = new Sample { AtMs = at, Category = cat.Name };
        study.Samples.Add(sample);
        sampleUndo.Add(sample);
        SetStatus(L.T("s.saved", ("cat", cat.Name), ("at", L.Clock(at))));
        Save();
        RenderSampling();
    }

    private void UndoSample()
    {
        if (current == null) return;
        Sample? last = null;
        while (sampleUndo.Count > 0)
        {
            var item = sampleUndo[^1];
            sampleUndo.RemoveAt(sampleUndo.Count - 1);
            if (current.Samples.Contains(item)) { last = item; break; }
        }
        if (last is null) { SetStatus(L.T("s.nothing")); return; }
        current.Samples.Remove(last);
        SetStatus(L.T("s.undone", ("cat", last.Category), ("at", L.Clock(last.AtMs ?? 0))));
        Save();
        RenderSampling();
    }

    private void GoRandomMoment()
    {
        RequireVideo();
        Pause();
        if (Advanced.RandomMoment(current!, (long)timeline.Maximum, random) is long at)
        {
            video.Position = TimeSpan.FromMilliseconds(at);
            playFrom = at;
            UpdatePosition();
        }
    }

    private void NextMoment()
    {
        RequireVideo();
        Pause();
        int step = PositiveInt(sampleStep.Text, "s.every");
        long at = Math.Min((long)timeline.Maximum, PositionMs + step * 1000L);
        video.Position = TimeSpan.FromMilliseconds(at);
        playFrom = at;
        UpdatePosition();
    }

    private void AddCategory()
    {
        if (current == null) return;
        string name = newCategory.Text.Trim();
        if (name.Length == 0) throw Error("s.emptyName");
        if (current.Categories.Any(c => c.Name == name)) throw Error("s.exists", ("cat", name));
        current.Categories = StudyRules.CleanCategories(current.Categories.Append(new SampleCategory { Name = name, Classification = Ui.ClassOf(newCategoryClass) }));
        newCategory.Text = "";
        Save();
        RenderSampling();
    }

    private void SaveSampleNote()
    {
        if (selectedSample is null) return;
        selectedSample.Note = StudyRules.CleanNote(sampleNote.Text);
        Save();
        RenderSampling();
    }

    private void DeleteSample()
    {
        if (selectedSample is null || current == null) return;
        current.Samples.Remove(selectedSample);
        selectedSample = null;
        Save();
        RenderSampling();
    }

    /// <summary>Etüt türüne göre ölçüm ve analiz panelleri.</summary>
    private void ApplyStudyType()
    {
        bool sampling = current?.IsSampling == true;
        timeSide.Visibility = sampling ? Visibility.Collapsed : Visibility.Visible;
        samplingSide.Visibility = sampling ? Visibility.Visible : Visibility.Collapsed;
        foreach (var e in timeOnly) e.Visibility = sampling ? Visibility.Collapsed : Visibility.Visible;
        foreach (var e in samplingOnly) e.Visibility = sampling ? Visibility.Visible : Visibility.Collapsed;
        sampleUndo.Clear();
        selectedSample = null;
    }
}
