using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace VideoZamanEtudu;

/// <summary>Öz-deneme (SelfTest.cs) için iç erişim. Normal kullanımda TestLog boştur ve hiçbir şey değişmez.</summary>
public partial class MainWindow
{
    internal static string? TestStoreDirectory;
    internal static string? TestDemoDirectory;
    /// <summary>Doluysa iletiler kutu açmadan buraya yazılır ve sorular "evet" sayılır.</summary>
    internal static Action<string>? TestLog;

    internal void TestGuard(Action action) => Guard(action);
    internal void TestOpenDemo() => Guard(OpenDemo);
    internal void TestSelectTab(int index) => tabs.SelectedIndex = index;
    internal FrameworkElement TestTab(int index) => (FrameworkElement)((TabItem)tabs.Items[index]).Content;
    internal Study? TestCurrent => current;
    internal IReadOnlyList<Study> TestStudies => studyList;
    internal int TestStepChips => stepsPanel.Children.OfType<Button>().Count();
    internal string TestNow => nowStep.Text;
    internal string TestStatus => status.Text;
    internal bool TestReady => ready;
    internal int TestObservationRows => (observations.ItemsSource as ICollection)?.Count ?? 0;
    internal int TestResultRows => (results.ItemsSource as ICollection)?.Count ?? 0;
    internal int TestSummaryCards => summary.Children.Count;
    internal int TestCharts => charts.Children.Count;
    internal string TestSettingsError => settingsError.Visibility == Visibility.Visible ? settingsError.Text : "";
    internal string TestAllowance => allowance.Text;
    internal void TestSelectStep(int index) => Guard(() => SelectStep(index));
    internal void TestAddStep(string name) { newStep.Text = name; Guard(AddStep); }
    internal void TestFilterStep(string step) => filterStep.SelectedItem = step;
    internal void TestSelectFirstRow() { if (observations.Items.Count > 0) observations.SelectedIndex = 0; }
    internal bool TestRowEditorVisible => rowEditor.Visibility == Visibility.Visible;

    internal void TestSettings(double personal, double fatigue, double takt_)
    {
        allowanceParts["Personal"].Text = L.Num(personal, 6);
        allowanceParts["Fatigue"].Text = L.Num(fatigue, 6);
        takt.Text = L.Num(takt_, 6);
        ApplySettingsInline();
    }

    internal void TestStepPace(string step, double value)
    {
        selectedResult = step;
        stepPace.Text = L.Num(value, 6);
        Guard(ApplyStep);
    }

    internal List<(string Label, string Value, IReadOnlyList<string> Details)> TestCards() =>
        current == null ? new() : SummaryCards(current, StudyRules.InProcessOrder(current, Analysis.Steps(current)));

    // v2.1
    internal void TestStepField(int index, string field, string value) => Guard(() => SetStepFieldAt(index, field, value));

    // v2.2
    internal void TestCopySteps(Study source, bool replace) => Guard(() => CopyStepsFrom(source, replace));
    internal void TestModules(params string[] modules) => Guard(() =>
    {
        foreach (var (m, box) in moduleBoxes) box.IsChecked = modules.Contains(m);
    });
    internal bool TestImproveVisible => ((TabItem)tabs.Items[TabImprove]).Visibility == Visibility.Visible;
    internal int TestStepTableRows => stepTable.RowDefinitions.Count - 1;
    internal int TestSelectedTab => tabs.SelectedIndex;
    internal void TestStatus_(int index, string by)
    {
        approvedBy.Text = by;
        studyStatus.SelectedIndex = index;
    }
    internal void TestBulk(int count, string cls, string cycle)
    {
        observations.SelectedItems.Clear();
        foreach (var item in observations.Items.Cast<object>().Take(count)) observations.SelectedItems.Add(item);
        bulkClass.SelectedItem = bulkClass.Items.Cast<ComboBoxItem>().First(i => i.Tag as string == cls);
        bulkCycle.Text = cycle;
        Guard(BulkApply);
    }
    internal bool TestBulkVisible => bulkEditor.Visibility == Visibility.Visible;
    internal string TestStudyStatusLabel(Study s) => StatusLabel(s);
    internal void TestAutoBalance() => Guard(AutoBalanceStations);
    internal int TestYamazumi => yamazumiBox.Children.Count;
    internal int TestWork => workBox.Children.OfType<Border>().Count();
    internal int TestIdeas => (ideasGrid.ItemsSource as ICollection)?.Count ?? 0;
    internal int TestOutliers => (outlierGrid.ItemsSource as ICollection)?.Count ?? 0;
    internal string TestBalanceSummary => balanceSummary.Text;
    internal void TestLoadStory() => Guard(LoadStory);
    internal void TestRateRow(int index, double rating)
    {
        if (current == null || index >= current.Observations.Count) return;
        current.Observations.OrderBy(o => o.StartMs).ElementAt(index).Rating = rating;
        Save();
        RefreshObservations();
    }
    internal void TestOpenStudy(Study study) { store.Save(study); ReloadStudies(); OpenStudy(studyList.First(s => s.Id == study.Id)); }
    internal void TestRecordSample(int index) => Guard(() => RecordSample(index));
    internal void TestRandomMoment() => Guard(GoRandomMoment);
    internal void TestNextMoment(int seconds) { sampleStep.Text = seconds.ToString(); Guard(NextMoment); }
    internal void TestUndoSample() => Guard(UndoSample);
    internal int TestSamplingRows => (samplingGrid.ItemsSource as ICollection)?.Count ?? 0;
    internal int TestSamplingCards => samplingSummary.Children.Count;
    internal bool TestSamplingVisible => samplingSide.Visibility == Visibility.Visible && timeSide.Visibility != Visibility.Visible;
}
