using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.CodeCompletion;
using Microsoft.Win32;
using TraceViewer.Core;
using TraceViewer.Core.Dumpulator;

namespace TraceViewer.UserControls
{
    public partial class DumpulatorControl : UserControl
    {
        private readonly DumpulatorRunner _runner = new();
        private const string DefaultPythonScript = @"from dumpulator import Dumpulator

dp = Dumpulator(DUMP_PATH)
";

        private CompletionWindow? _completionWindow;
        private MainWindow? _mainWindow;

        public DumpulatorControl()
        {
            InitializeComponent();
            Loaded += DumpulatorControl_Loaded;

            // Configure AvalonEdit
            PythonEditor.SyntaxHighlighting = PythonHighlighting.GetDefinition();
            PythonEditor.Text = DefaultPythonScript;

            PythonEditor.TextArea.TextEntering += TextArea_TextEntering;
            PythonEditor.TextArea.TextEntered += TextArea_TextEntered;
            PythonEditor.TextArea.Caret.PositionChanged += Caret_PositionChanged;

            // Configure Runner events
            _runner.OutputReceived += line => Dispatcher.Invoke(() => AppendLog(line, Brushes.Gainsboro));
            _runner.ErrorReceived += line => Dispatcher.Invoke(() => AppendLog(line, new SolidColorBrush(Color.FromRgb(240, 110, 110))));
            _runner.StatusChanged += status => Dispatcher.Invoke(() => UpdateProcessStatus(status));
            _runner.ProcessExited += code => Dispatcher.Invoke(() => OnProcessFinished(code));
        }

        public void SetMainWindow(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            UpdateTraceContextDisplay();
            TryAutoDetectDumpFile();
        }

        private void DumpulatorControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_mainWindow == null)
            {
                _mainWindow = Window.GetWindow(this) as MainWindow;
            }
            UpdateTraceContextDisplay();
            TryAutoDetectDumpFile();
        }

        public void UpdateTraceContextDisplay()
        {
            // Status bar removed as requested
        }

        public void TryAutoDetectDumpFile()
        {
            if (!string.IsNullOrWhiteSpace(DmpPathTextBox.Text) && File.Exists(DmpPathTextBox.Text))
            {
                return;
            }

            if (TraceHandler.Trace != null && !string.IsNullOrWhiteSpace(TraceHandler.Trace.Filename))
            {
                try
                {
                    string traceDir = Path.GetDirectoryName(TraceHandler.Trace.Filename) ?? "";
                    if (Directory.Exists(traceDir))
                    {
                        // 1. Look for same base name with .dmp
                        string sameBase = Path.ChangeExtension(TraceHandler.Trace.Filename, ".dmp");
                        if (File.Exists(sameBase))
                        {
                            DmpPathTextBox.Text = sameBase;
                            return;
                        }

                        // 2. Look for any .dmp in the same folder
                        var dmps = Directory.GetFiles(traceDir, "*.dmp");
                        if (dmps.Length > 0)
                        {
                            DmpPathTextBox.Text = dmps[0];
                            return;
                        }
                    }
                }
                catch
                {
                    // Ignore detection failures
                }
            }
        }

        private void Caret_PositionChanged(object? sender, EventArgs e)
        {
            CaretPositionText.Text = $"Ln {PythonEditor.TextArea.Caret.Line}, Col {PythonEditor.TextArea.Caret.Column}";
        }

        private void TextArea_TextEntering(object sender, TextCompositionEventArgs e)
        {
            if (e.Text.Length > 0 && _completionWindow != null)
            {
                // When typing space, punctuation, or non-identifier characters while completion is open,
                // do NOT force insertion! Simply dismiss the completion window so normal typing flows smoothly.
                if (!char.IsLetterOrDigit(e.Text[0]) && e.Text[0] != '_')
                {
                    _completionWindow.Close();
                    _completionWindow = null;
                }
            }
        }

        private void TextArea_TextEntered(object sender, TextCompositionEventArgs e)
        {
            // Auto-trigger completion ONLY when typing dot (for member access like dp. or dp.regs.)
            // Normal typing (e.g. declaring "res = ...") will never be interrupted.
            if (e.Text == ".")
            {
                ShowCompletionWindow();
            }
        }

        private void PythonEditor_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                RunScript();
                e.Handled = true;
            }
            else if (e.Key == Key.Space && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                // Explicit manual trigger on Ctrl+Space
                ShowCompletionWindow();
                e.Handled = true;
            }
        }

        private void ShowCompletionWindow()
        {
            int offset = PythonEditor.CaretOffset;
            var line = PythonEditor.Document.GetLineByOffset(offset);
            string lineText = PythonEditor.Document.GetText(line.Offset, offset - line.Offset);
            int col = offset - line.Offset;

            var suggestions = DumpulatorCompletionProvider.GetSuggestions(lineText, col).ToList();
            if (suggestions.Count == 0) return;

            if (_completionWindow != null)
            {
                _completionWindow.Close();
                _completionWindow = null;
            }

            _completionWindow = new CompletionWindow(PythonEditor.TextArea)
            {
                Background = new SolidColorBrush(Color.FromRgb(18, 18, 18)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(65, 65, 65)),
                BorderThickness = new Thickness(1),
                Width = 380,
                MaxHeight = 260
            };

            if (Application.Current.TryFindResource(typeof(ToolTip)) is Style toolTipStyle)
            {
                _completionWindow.Resources[typeof(ToolTip)] = toolTipStyle;
            }

            // Style ListBox to remove Windows default blue highlight and use Consolas dark glow
            var listBox = _completionWindow.CompletionList.ListBox;
            listBox.Background = new SolidColorBrush(Color.FromRgb(18, 18, 18));
            listBox.Foreground = Brushes.White;
            listBox.BorderThickness = new Thickness(0);
            listBox.FontFamily = new FontFamily("Consolas");
            listBox.FontSize = 12;
            listBox.Padding = new Thickness(2);

            var itemStyle = new Style(typeof(ListBoxItem));
            itemStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            itemStyle.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(Color.FromRgb(220, 220, 220))));
            itemStyle.Setters.Add(new Setter(Control.FontFamilyProperty, new FontFamily("Consolas")));
            itemStyle.Setters.Add(new Setter(Control.FontSizeProperty, 12.0));
            itemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(5, 3, 5, 3)));
            itemStyle.Setters.Add(new Setter(Control.MarginProperty, new Thickness(1, 0, 1, 1)));

            var template = new ControlTemplate(typeof(ListBoxItem));
            var borderFactory = new FrameworkElementFactory(typeof(Border), "ItemBd");
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(2));
            borderFactory.SetValue(Border.PaddingProperty, new Thickness(5, 3, 5, 3));
            borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            borderFactory.SetValue(Border.BorderBrushProperty, Brushes.Transparent);

            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(contentPresenter);
            template.VisualTree = borderFactory;

            var mouseOverTrigger = new Trigger { Property = ListBoxItem.IsMouseOverProperty, Value = true };
            mouseOverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(36, 36, 36)), "ItemBd"));
            mouseOverTrigger.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
            template.Triggers.Add(mouseOverTrigger);

            var isSelectedTrigger = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
            isSelectedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(38, 48, 44)), "ItemBd"));
            isSelectedTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(78, 201, 176)), "ItemBd"));
            isSelectedTrigger.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
            template.Triggers.Add(isSelectedTrigger);

            itemStyle.Setters.Add(new Setter(Control.TemplateProperty, template));
            listBox.ItemContainerStyle = itemStyle;

            // Set StartOffset so completion only replaces the member or identifier being typed
            int lastDot = lineText.LastIndexOf('.');
            if (lastDot >= 0 && col > lastDot)
            {
                _completionWindow.StartOffset = line.Offset + lastDot + 1;
            }
            else
            {
                int wordStart = col - 1;
                while (wordStart >= 0 && (char.IsLetterOrDigit(lineText[wordStart]) || lineText[wordStart] == '_'))
                {
                    wordStart--;
                }
                _completionWindow.StartOffset = line.Offset + wordStart + 1;
            }

            // Style completion items
            foreach (var item in suggestions)
            {
                _completionWindow.CompletionList.CompletionData.Add(item);
            }

            _completionWindow.Show();
            _completionWindow.Closed += delegate { _completionWindow = null; };
        }

        private async void RunScript()
        {
            if (_runner.IsRunning) return;

            string script = PythonEditor.Text;
            if (string.IsNullOrWhiteSpace(script))
            {
                AppendLog("[Error] Script is empty. Please enter Python code to execute.", Brushes.IndianRed);
                return;
            }

            string dumpPath = DmpPathTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(dumpPath) || !File.Exists(dumpPath))
            {
                AppendLog($"[Warning] Dump path '{dumpPath}' not found. Emulation relying on DUMP_PATH may fail.", Brushes.Goldenrod);
            }

            // Build execution context info
            var context = new ExecutionContextInfo
            {
                DumpPath = dumpPath
            };

            int rowId = _mainWindow?.CurrentHoveredTraceRowId ?? -1;
            if (TraceHandler.Trace != null && rowId >= 0 && rowId < TraceHandler.Trace.Trace.Count)
            {
                var row = TraceHandler.Trace.Trace[rowId];
                context.CurrentRow = row.Id;
                context.CurrentIp = row.Ip;

                var regDefs = REGDUMP.X64_REGS_PARSING;
                for (int i = 0; i < regDefs.Length && i < row.Regs.Count; i++)
                {
                    if (regDefs[i].IsNamed && row.Regs[i] != null && row.Regs[i].Length > 0)
                    {
                        ulong val = 0;
                        byte[] b = row.Regs[i];
                        for (int bi = 0; bi < b.Length && bi < 8; bi++)
                        {
                            val |= ((ulong)b[bi]) << (bi * 8);
                        }
                        context.Registers[regDefs[i].Name] = val;
                    }
                }
            }

            // Update UI
            RunButtonBorder.Opacity = 0.5;
            RunButtonBorder.IsEnabled = false;
            StopButtonBorder.Opacity = 1.0;
            StopButtonBorder.IsEnabled = true;
            ProcessStatusText.Text = "RUNNING";
            ProcessStatusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 200, 80));

            AppendLog($"\n>>> Execution Started at {DateTime.Now:HH:mm:ss} <<<", new SolidColorBrush(Color.FromRgb(78, 201, 176)));

            await _runner.RunScriptAsync(script, context);
        }

        private void OnProcessFinished(int exitCode)
        {
            RunButtonBorder.Opacity = 1.0;
            RunButtonBorder.IsEnabled = true;
            StopButtonBorder.Opacity = 0.5;
            StopButtonBorder.IsEnabled = false;

            ProcessStatusText.Text = exitCode == 0 ? "FINISHED" : $"FAILED ({exitCode})";
            ProcessStatusText.Foreground = exitCode == 0
                ? new SolidColorBrush(Color.FromRgb(78, 201, 176))
                : new SolidColorBrush(Color.FromRgb(240, 110, 110));

            AppendLog($">>> Process Finished with Exit Code {exitCode} <<<\n",
                exitCode == 0 ? new SolidColorBrush(Color.FromRgb(78, 201, 176)) : new SolidColorBrush(Color.FromRgb(240, 110, 110)));
        }

        private void UpdateProcessStatus(string status)
        {
            AppendLog($"[*] {status}", new SolidColorBrush(Color.FromRgb(150, 150, 150)));
        }

        private void AppendLog(string message, Brush brush)
        {
            var p = new Paragraph(new Run(message) { Foreground = brush })
            {
                Margin = new Thickness(0),
                LineHeight = 16
            };
            OutputTextBox.Document.Blocks.Add(p);
            OutputTextBox.ScrollToEnd();
        }

        private void RunButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                RunScript();
            }
        }

        private void StopButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _runner.Stop();
            }
        }

        private void ClearLogButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                OutputTextBox.Document.Blocks.Clear();
                AppendLog("[Log Cleared]", Brushes.Gray);
            }
        }

        private void BrowseDmp_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;

            var dlg = new OpenFileDialog
            {
                Title = "Select Target Minidump File (.dmp)",
                Filter = "Minidump (*.dmp)|*.dmp|All Files (*.*)|*.*",
                CheckFileExists = true
            };

            if (!string.IsNullOrWhiteSpace(DmpPathTextBox.Text))
            {
                try
                {
                    string dir = Path.GetDirectoryName(DmpPathTextBox.Text) ?? "";
                    if (Directory.Exists(dir)) dlg.InitialDirectory = dir;
                }
                catch { }
            }

            if (dlg.ShowDialog() == true)
            {
                DmpPathTextBox.Text = dlg.FileName;
            }
        }

        public string GetScriptCode() => PythonEditor.Text;
        public void SetScriptCode(string code) => PythonEditor.Text = code;
        public string GetDumpPath() => DmpPathTextBox.Text;
        public void SetDumpPath(string path) => DmpPathTextBox.Text = path;
    }
}
