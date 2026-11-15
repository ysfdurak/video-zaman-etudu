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
}
