using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using TraceViewer.Core;
using TraceViewer.Core.Diff;

namespace TraceViewer.UserControls
{
    public partial class DiffControl : UserControl
    {
        private static readonly SolidColorBrush BrushBranchA = new(Color.FromArgb(0x45, 0x61, 0xAF, 0xEF));
        private static readonly SolidColorBrush BrushBranchB = new(Color.FromArgb(0x45, 0xE5, 0xC0, 0x7B));
        private static readonly SolidColorBrush BrushDimText = new(Color.FromRgb(0x88, 0x88, 0x88));
        private static readonly SolidColorBrush BrushScoreHigh = new(Color.FromRgb(0x98, 0xC3, 0x79));
        private static readonly SolidColorBrush BrushScoreMid = new(Color.FromRgb(0xE5, 0xC0, 0x7B));
        private static readonly SolidColorBrush BrushScoreLow = new(Color.FromRgb(0xE0, 0x6C, 0x75));

        private TraceData? _traceA;
        private TraceData? _traceB;
        private BranchDiffReport? _report;
        private BranchDivergence? _selectedBranch;

        static DiffControl()
        {
            BrushBranchA.Freeze();
            BrushBranchB.Freeze();
            BrushDimText.Freeze();
            BrushScoreHigh.Freeze();
            BrushScoreMid.Freeze();
            BrushScoreLow.Freeze();
        }

        public DiffControl()
        {
            InitializeComponent();
        }

        public void EnsurePrimaryTrace(TraceData trace)
        {
            if (_traceA != trace)
            {
                _traceA = trace;
                string filename = string.IsNullOrEmpty(trace.Filename)
                    ? "Current Trace"
                    : System.IO.Path.GetFileName(trace.Filename);
                Txt_TraceA_Name.Text = filename;
                Txt_TraceA_Stats.Text = $"({trace.Trace.Count:N0} rows)";
            }
        }

        public void LoadAndDiff(TraceData traceA, TraceData traceB)
        {
            _traceA = traceA;
            _traceB = traceB;

            Txt_TraceA_Name.Text = string.IsNullOrEmpty(traceA.Filename)
                ? "Current Trace"
                : System.IO.Path.GetFileName(traceA.Filename);
            Txt_TraceA_Stats.Text = $"({traceA.Trace.Count:N0} rows)";

            Txt_TraceB_Name.Text = System.IO.Path.GetFileName(traceB.Filename);
            Txt_TraceB_Stats.Text = $"({traceB.Trace.Count:N0} rows)";

            Placeholder_Empty.Visibility = Visibility.Collapsed;
            Placeholder_Identical.Visibility = Visibility.Collapsed;

            _report = BranchDivergenceAnalyzer.Analyze(traceA, traceB);

            Txt_ScorePercent.Text = $"{_report.PathSimilarityScore:0.#}%";
            Txt_DivergentBranchCount.Text = _report.DivergentBranches.Count.ToString();

            Txt_ScorePercent.Foreground = _report.PathSimilarityScore >= 80.0
                ? BrushScoreHigh
                : _report.PathSimilarityScore >= 50.0
                    ? BrushScoreMid
                    : BrushScoreLow;

            List_Branches.ItemsSource = _report.DivergentBranches;

            if (_report.DivergentBranches.Count == 0)
            {
                Placeholder_Identical.Visibility = Visibility.Visible;
                ClearContextView();
            }
            else
            {
                List_Branches.SelectedIndex = 0;
            }
        }

        private void List_Branches_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (List_Branches.SelectedItem is BranchDivergence branch)
            {
                _selectedBranch = branch;
                UpdateContextView(branch);
            }
        }

        private void UpdateContextView(BranchDivergence branch)
        {
            Txt_DetailBranchTitle.Text = $"#{branch.Index}  {branch.Disasm}  @ 0x{branch.AddressA:X}  (A #{branch.RowIdA} | B #{branch.RowIdB})";

            Items_ContextA.ItemsSource = BuildContextItems(branch.ContextA, BrushBranchA);
            Items_ContextB.ItemsSource = BuildContextItems(branch.ContextB, BrushBranchB);

            var sb = new StringBuilder();

            if (!string.IsNullOrEmpty(branch.ComparisonDisasm))
                sb.AppendLine($"Compare: {branch.ComparisonDisasm}");

            if (!string.IsNullOrEmpty(branch.FlagCause))
                sb.AppendLine($"Flags: {branch.FlagCause}");

            if (!string.IsNullOrEmpty(branch.ReconvergenceSummary))
                sb.AppendLine($"Reconvergence: {branch.ReconvergenceSummary}");

            sb.AppendLine();
            sb.AppendLine("REGISTERS:");
            if (branch.DifferingRegsList.Count > 0)
            {
                foreach (var reg in branch.DifferingRegsList)
                    sb.AppendLine($"  {reg}");
            }
            else
            {
                sb.AppendLine("  No GPR differences (flags-only divergence)");
            }

            Txt_DifferingRegsDetail.Text = sb.ToString().TrimEnd();
        }

        private static List<ContextItemView> BuildContextItems(List<(int RowId, ulong Ip, string Disasm, bool IsBranch)> items, Brush highlightBrush)
        {
            var result = new List<ContextItemView>(items.Count);
            foreach (var item in items)
            {
                result.Add(new ContextItemView
                {
                    RowId = item.RowId,
                    Ip = item.Ip,
                    Disasm = item.Disasm,
                    BackgroundBrush = item.IsBranch ? highlightBrush : Brushes.Transparent,
                    ForegroundBrush = item.IsBranch ? Brushes.White : BrushDimText,
                    FontWeight = item.IsBranch ? FontWeights.Bold : FontWeights.Normal
                });
            }
            return result;
        }

        private void ClearContextView()
        {
            Txt_DetailBranchTitle.Text = "No divergent branches found.";
            Items_ContextA.ItemsSource = null;
            Items_ContextB.ItemsSource = null;
            Txt_DifferingRegsDetail.Text = "All conditional branches took the same path.";
        }

        private void List_Branches_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            JumpToSelectedInDisasm();
        }

        private void Btn_JumpDisasm_MouseDown(object sender, MouseButtonEventArgs e)
        {
            JumpToSelectedInDisasm();
        }

        public void JumpToSelectedInDisasm()
        {
            if (_selectedBranch == null) return;

            if (Application.Current.MainWindow is MainWindow window)
            {
                window.DisasmViewButton_MouseDown(null, null);
                window.ScrollTo(_selectedBranch.RowIdA);
            }
        }

        public void JumpToFirstDivergence()
        {
            if (_report != null && _report.DivergentBranches.Count > 0)
            {
                List_Branches.SelectedIndex = 0;
                List_Branches.ScrollIntoView(List_Branches.SelectedItem);
            }
        }

        public void JumpToNextDifference()
        {
            if (_report != null && _report.DivergentBranches.Count > 0)
            {
                int nextIndex = List_Branches.SelectedIndex + 1;
                if (nextIndex < _report.DivergentBranches.Count)
                {
                    List_Branches.SelectedIndex = nextIndex;
                    List_Branches.ScrollIntoView(List_Branches.SelectedItem);
                }
            }
        }

        public void JumpToPrevDifference()
        {
            if (_report != null && _report.DivergentBranches.Count > 0)
            {
                int prevIndex = List_Branches.SelectedIndex - 1;
                if (prevIndex >= 0)
                {
                    List_Branches.SelectedIndex = prevIndex;
                    List_Branches.ScrollIntoView(List_Branches.SelectedItem);
                }
            }
        }

        public void RunDifferentialSliceOnSelected()
        {
            ExecuteDifferentialSlice();
        }

        private void Btn_DiffSlice_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ExecuteDifferentialSlice();
        }

        public void ExecuteDifferentialSlice()
        {
            if (_traceA == null || _traceB == null || _selectedBranch == null)
            {
                MessageBox.Show("Select a divergent branch first.", "Differential Slice", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Txt_DifferingRegsDetail.Text = "Computing differential backward slice...";

            var sliceRes = DifferentialSlicer.Run(_traceA, _selectedBranch.RowIdA, _traceB, _selectedBranch.RowIdB);

            if (sliceRes.Success)
            {
                var sb = new StringBuilder();
                sb.AppendLine("DIFFERENTIAL SLICE:");
                sb.AppendLine(sliceRes.RootCauseDescription);
                sb.AppendLine();

                if (sliceRes.UniqueSliceRowsA.Count > 0)
                {
                    sb.AppendLine("Trace A causal chain:");
                    foreach (var r in sliceRes.UniqueSliceRowsA.Take(8))
                        sb.AppendLine($"  #{r.Id} 0x{r.Ip:X}: {r.Disasm}");
                }

                if (sliceRes.UniqueSliceRowsB.Count > 0)
                {
                    sb.AppendLine("Trace B causal chain:");
                    foreach (var r in sliceRes.UniqueSliceRowsB.Take(8))
                        sb.AppendLine($"  #{r.Id} 0x{r.Ip:X}: {r.Disasm}");
                }

                Txt_DifferingRegsDetail.Text = sb.ToString().TrimEnd();
            }
            else
            {
                Txt_DifferingRegsDetail.Text = "No direct dependencies could be isolated.";
            }
        }

        private void Btn_LoadTraceB_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Trace Files (*.trace64)|*.trace64",
                Title = "Select Secondary Trace (.trace64) to Compare"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    var loadedB = TraceLoader.OpenX64dbgTrace(openFileDialog.FileName);
                    if (_traceA == null)
                    {
                        if (TraceHandler.Trace != null)
                        {
                            _traceA = TraceHandler.Trace;
                        }
                        else
                        {
                            MessageBox.Show("Load a primary trace in the main view first.", "Trace Diff", MessageBoxButton.OK, MessageBoxImage.Information);
                            return;
                        }
                    }

                    LoadAndDiff(_traceA, loadedB);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to load Trace B: {ex.Message}", "Trace Diff Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public class ContextItemView
        {
            public int RowId { get; set; }
            public ulong Ip { get; set; }
            public string Disasm { get; set; } = "";
            public Brush BackgroundBrush { get; set; } = Brushes.Transparent;
            public Brush ForegroundBrush { get; set; } = Brushes.White;
            public FontWeight FontWeight { get; set; } = FontWeights.Normal;
        }
    }
}
