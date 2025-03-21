using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using TraceViewer.Core;
using TraceViewer.Core.Analysis;
using TraceViewer.UserControls;
using TraceViewer.UserWindows;

namespace TraceViewer
{
    public enum UIState
    {
        DisassemblerView,
        NotesView,
        BookmarksView
    }

    public partial class MainWindow : Window
    {
        private bool _toggleFpu = true;
        public bool _toggleMnemonic = true;
        private string _current_project_path = "";

        public ScrollViewer InstructionsScrollViewer { get; private set; }
        public ObservableCollection<WPF_TraceRow> InstructionViewItems = new();
        public ObservableCollection<WPF_RegisterRow> RegisterViewItems = new();


        public MainWindow()
        {
            InitializeComponent();
            InstructionsView.Loaded += InstructionsView_Loaded;
            InstructionsView.ItemsSource = InstructionViewItems;
            RegistersView.ItemsSource = RegisterViewItems;
            PreviewKeyDown += MainWindow_PreviewKeyDown;
            SourceInitialized += MainWindow_SourceInitialized;

            InstructionsView.AllowDrop = true;
            InstructionsView.DragEnter += DragEnter;
            InstructionsView.DragLeave += DragLeave;
            InstructionsView.Drop += Drop;

            DisasmViewButton_MouseDown(null, null); // Set Disassembler View as default
        }

        private void Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    string filePath = files[0];
                    string fileExtension = System.IO.Path.GetExtension(filePath);

                    
                    if (fileExtension == ".trace64")
                    {
                        Unload();
                        TraceHandler.OpenAndLoad(filePath);
                    }
                    else if (fileExtension == ".tvproj")
                    {
                        OpenProject(filePath);
                    }
                    else
                    {
                        MessageDialog messageDialog = new MessageDialog("Invalid file type. Use a .trace64 or .tvproj file!");
                        messageDialog.ShowDialog();
                    }
                }
            }
            DragLeave(null, null);
        }

        private void DragEnter(object sender, DragEventArgs e)
        {
            MainView.Opacity = 0; // Make MainView transparent during drag operation
            DROPZONE.Visibility = Visibility.Visible; // Show drop zone indicator
        }

        private void DragLeave(object sender, DragEventArgs e)
        {
            MainView.Opacity = 1; // Restore MainView opacity after drag leave
            DROPZONE.Visibility = Visibility.Hidden; // Hide drop zone indicator
        }


        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        private void MainWindow_SourceInitialized(object? sender, EventArgs e)
        {
            if (IsWindows10OrHigher())
            {
                // Enable immersive dark mode for Windows 10 and higher
                var hwnd = new WindowInteropHelper(this).Handle;
                int darkModeEnabled = 1;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkModeEnabled, sizeof(int));
            }
        }

        private bool IsWindows10OrHigher()
        {
            // Check if the OS version is Windows 10 or higher
            return Environment.OSVersion.Version.Major >= 10;
        }


        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Handle Ctrl+G shortcut for "Go To Row" functionality
            if ((Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) && e.Key == Key.G && TraceHandler.Trace != null)
            {
                InputDialog input = new InputDialog("Put in a row to go to:");
                input.ShowDialog();
                var res = input.GetResult();
                if (!string.IsNullOrEmpty(res))
                {
                    try
                    {
                        int goto_row = Convert.ToInt32(res);
                        ScrollControl(-goto_row, true);
                    }
                    catch (FormatException)
                    {
                        MessageDialog messageDialog = new MessageDialog("Invalid input. Use a numerical value!");
                        messageDialog.ShowDialog();
                    }
                }
            }

        }

        private void InstructionsView_Loaded(object sender, RoutedEventArgs e)
        {
            // Find the ScrollViewer within the InstructionsView template
            if (sender is ItemsControl itemsControl &&
        itemsControl.Template.FindName("instructions_view_scrollviewer", itemsControl) is ScrollViewer scrollViewer)
            {
                InstructionsScrollViewer = scrollViewer;
            }
            else
            {
                throw new InvalidOperationException("ScrollViewer not found in template");
            }
        }


        private void TitleLabel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.Label label)
            {
                UpdateInstructionViewColumnWidth(label.Name, e.NewSize.Width);
            }
        }

        private void UpdateInstructionViewColumnWidth(string columnName, double newWidth)
        {
            InstructionsView.BeginInit();
            try
            {
                foreach (var item in InstructionViewItems)
                {
                    // Update column widths based on label name
                    switch (columnName)
                    {
                        case "id":
                            item.id.Width = newWidth;
                            item.id_border.Width = newWidth;
                            break;
                        case "address":
                            item.address.Width = newWidth;
                            item.address_border.Width = newWidth;
                            break;
                        case "disasm":
                            item.disasm.Width = newWidth;
                            item.disasm_border.Width = newWidth;
                            break;
                        case "changes":
                            item.changes.Width = newWidth;
                            item.changes_border.Width = newWidth;
                            break;
                        case "comments":
                            item.comments.Width = newWidth;
                            item.mnemonicBrief.Width = newWidth; // Assuming comments and mnemonicBrief share column width
                            break;
                    }
                }
            }
            finally
            {
                InstructionsView.EndInit();
                SetInstructionsViewWidth();
            }
        }


        private void SetInstructionsViewWidth()
        {
            // Calculate and set the minimum and maximum width of InstructionsView based on column widths
            double totalWidth = cd0.Width.Value + cd1.Width.Value + cd2.Width.Value + cd3.Width.Value + cd4.Width.Value + cd5.Width.Value + 8; // Add a small buffer
            if (totalWidth > 0)
            {
                InstructionsView.MinWidth = totalWidth;
                InstructionsView.MaxWidth = totalWidth;
            }
        }

        private void Fpu_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            // Toggle FPU registers visibility
            _toggleFpu = !_toggleFpu;
            fpu.Foreground = _toggleFpu ? Brushes.White : Brushes.Gray;

            Visibility fpuVisibility = _toggleFpu ? Visibility.Visible : Visibility.Collapsed;

            foreach (var item in RegisterViewItems)
            {
                if (item.registerType == RegisterType.FPU)
                {
                    item.Visibility = fpuVisibility;
                }
            }
        }

        private void Comments_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            // Toggle between displaying comments and mnemonic brief in the comments column
            _toggleMnemonic = !_toggleMnemonic;
            InstructionsView.BeginInit();
            foreach (var item in InstructionViewItems)
            {
                item.display_mnemonic_brief(!_toggleMnemonic); // Call display toggle on each item
            }
            InstructionsView.EndInit();
            comments.Content = !_toggleMnemonic ? "MNEMONIC" : "COMMENTS"; // Update button content
        }

        private void MnemonicReader_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            BigMnemonicViewInactive(); // Deactivate big mnemonic view
        }

        private void BigMnemonicViewInactive()
        {
            // Hide big mnemonic view and restore focus to main view
            MnemonicReaderScrollView.ScrollToVerticalOffset(0); // Reset scroll position
            MnemonicReaderScrollView.Visibility = Visibility.Collapsed;
            MainView.Visibility = Visibility.Visible;
        }

        private readonly DropShadowEffect glowEffect = new DropShadowEffect // Make glow effect readonly
        {
            Color = Colors.White,
            BlurRadius = 10,
            ShadowDepth = 0,
            Opacity = 0.8
        };

        private void DisasmViewButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if(e != null)
                if (e.LeftButton != MouseButtonState.Pressed) return;
            SetViewButtonActive(DisasmViewButtonBorder); // Set Disassembler view button as active
            SetViewButtonInactive(NotesViewButtonBorder); // Deactivate Notes view button
            SetViewButtonInactive(BookmarksViewButtonBorder); // Deactivate Bookmarks view button
            SetCurrentUIState(UIState.DisassemblerView); // Set UI state to Disassembler view
        }

        private void NotesViewButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            SetViewButtonInactive(DisasmViewButtonBorder); // Deactivate Disassembler view button
            SetViewButtonActive(NotesViewButtonBorder); // Set Notes view button as active
            SetViewButtonInactive(BookmarksViewButtonBorder); // Deactivate Bookmarks view button
            SetCurrentUIState(UIState.NotesView); // Set UI state to Notes view
        }

        private void BookmarksViewButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            SetViewButtonInactive(DisasmViewButtonBorder); // Deactivate Disassembler view button
            SetViewButtonInactive(NotesViewButtonBorder); // Deactivate Notes view button
            SetViewButtonActive(BookmarksViewButtonBorder); // Set Bookmarks view button as active
            SetCurrentUIState(UIState.BookmarksView); // Set UI state to Bookmarks view
        }

        private void SetViewButtonActive(Border buttonBorder)
        {
            buttonBorder.Background = new SolidColorBrush(Color.FromArgb(255, 40, 40, 40));
            buttonBorder.Effect = glowEffect;
        }

        private void SetViewButtonInactive(Border buttonBorder)
        {
            buttonBorder.Background = Brushes.Transparent;
            buttonBorder.Effect = null;
        }


        private void SetCurrentUIState(UIState uiState)
        {
            // Set visibility of different UI views based on UIState enum
            DisassemblerView.Visibility = uiState == UIState.DisassemblerView ? Visibility.Visible : Visibility.Collapsed;
            NotesView.Visibility = uiState == UIState.NotesView ? Visibility.Visible : Visibility.Collapsed;
            BookmarksView.Visibility = uiState == UIState.BookmarksView ? Visibility.Visible : Visibility.Collapsed;
        }


        public void Unload()
        {
            // Clear all data and reset UI to initial state
            InstructionViewItems.Clear();
            RegisterViewItems.Clear();
            NotesContent.Text = "";
            _current_project_path = "";
            WPF_TraceRow.hiddenRows.Clear();
        }


        static int index = TraceHandler.load_count; // Initial index for trace loading

        private void InstructionsScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Handle mouse wheel scrolling, with Ctrl key for faster scrolling
            int scrollStep = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl) ? 15 : 3;
            int delta = e.Delta > 0 ? scrollStep : -scrollStep;
            ScrollControl(delta);
        }

        public bool ScrollControl(int steps, bool set = false)
        {
            if (TraceHandler.Trace == null)
                return false;

            if (set)
                index = TraceHandler.load_count;

            int absSteps = Math.Abs(steps);
            if (absSteps > TraceHandler.load_count)
            {
                int fullPageSteps = absSteps / TraceHandler.load_count - 1; // Calculate full page jumps
                int increment = (steps < 0) ? TraceHandler.load_count : -TraceHandler.load_count; // Determine increment direction
                index += increment * fullPageSteps; // Adjust index by full pages

                // Ensure index stays within bounds
                if (index < TraceHandler.load_count) index = TraceHandler.load_count * 2;
                if (index > TraceHandler.Trace.Trace.Count)
                    index = TraceHandler.Trace.Trace.Count - TraceHandler.load_count;

                steps %= TraceHandler.load_count; // Remaining steps after full page jumps
                steps -= increment; // Load one new page for refresh
            }

            bool returnValue = false;
            if (steps > 0) // Scroll Up
            {
                for (int i = 0; i < steps; i++)
                {
                    if (InstructionViewItems.Count > 0 && index - TraceHandler.load_count - 1 >= 0)
                    {
                        InstructionViewItems.RemoveAt(InstructionViewItems.Count - 1); // Remove last item
                        TraceHandler.LoadRange(index - TraceHandler.load_count - 1, index - TraceHandler.load_count, true); // Load new item at top
                        index--;
                        returnValue = true;
                    }
                }
            }
            else if (steps < 0) // Scroll Down
            {
                for (int i = 0; i < Math.Abs(steps); i++)
                {
                    if (InstructionViewItems.Count > 0 && index < TraceHandler.Trace.Trace.Count)
                    {
                        InstructionViewItems.RemoveAt(0); // Remove first item
                        TraceHandler.LoadRange(index, index + 1, false); // Load new item at bottom
                        index++;
                        returnValue = true;
                    }
                }
            }

            // Refresh view after setting index directly if no scroll happened within load_count range
            if (absSteps < TraceHandler.load_count && set)
            {
                RefreshView();
            }

            return returnValue; // Indicate if scroll action was possible
        }

        private void OpenTrace_Click(object sender, RoutedEventArgs e)
        {
            
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Trace Files (*.trace64)|*.trace64",
                FilterIndex = 1,
                Multiselect = false
            };
            if (openFileDialog.ShowDialog() == true)
            {
                Unload(); // Clear current project data
                TraceHandler.OpenAndLoad(openFileDialog.FileName); // Load selected trace file
            }
        }

        private void OpenProject_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Trace Viewer Project (.tvproj)|*.tvproj",
                FilterIndex = 1,
                Multiselect = false
            };
            if (openFileDialog.ShowDialog() == true)
            {
                OpenProject(openFileDialog.FileName); // Load selected project file
            }
        }

        public void OpenProject(string filename)
        {
            Unload(); // Clear current project data
            _current_project_path = filename; // Store current project path
            Project project = ProjectLoader.OpenProject(filename); // Load project from file
            TraceHandler.Trace = project.TraceData; // Set loaded trace data
            if (project.Comments != null) // Null check for comments
            {
                foreach (var item in project.Comments)
                {
                    TraceHandler.Trace.Trace[item.Item1].comments = item.Item2; // Apply loaded comments
                }
            }
            NotesContent.Text = project.Notes ?? ""; // Load notes, handle null 
            WPF_TraceRow.hiddenRows = project.HiddenRows; // Load hidden rows
            RefreshView(); // Refresh view after loading project
        }

        private void SaveProject_Click(object sender, RoutedEventArgs e)
        {
            if (TraceHandler.Trace == null)
                return;

            string filename = _current_project_path; // Default to current project path

            if (string.IsNullOrEmpty(_current_project_path)) // If no current path, prompt for save file
            {
                SaveFileDialog saveFileDialog = CreateSaveFileDialog();
                if (saveFileDialog.ShowDialog() == true)
                    filename = saveFileDialog.FileName;
                else
                    return; // Do not save if dialog is cancelled
            }

            SaveProjectToFile(filename); // Save project to file
        }

        private void SaveProjectAs_Click(object sender, RoutedEventArgs e)
        {
            if (TraceHandler.Trace == null)
                return;

            SaveFileDialog saveFileDialog = CreateSaveFileDialog(); // Create SaveFileDialog instance
            if (saveFileDialog.ShowDialog() == true)
            {
                _current_project_path = saveFileDialog.FileName; // Update current project path
                SaveProjectToFile(saveFileDialog.FileName); // Save project to the newly selected file
            }
        }

        private SaveFileDialog CreateSaveFileDialog()
        {
            // Create and configure SaveFileDialog
            return new SaveFileDialog
            {
                DefaultExt = ".tvproj",
                Filter = "Trace Viewer Project (.tvproj)|*.tvproj",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Title = "Save as"
            };
        }

        private void SaveProjectToFile(string filename)
        {
            // Save project data to the specified file
            Project project = new Project
            {
                TraceData = TraceHandler.Trace,
                HiddenRows = WPF_TraceRow.hiddenRows,
                Comments = new List<Tuple<int, string>>() // Initialize comments collection
            };
            foreach (var item in TraceHandler.Trace.Trace)
            {
                if (!string.IsNullOrEmpty(item.comments))
                    project.Comments.Add(new Tuple<int, string>(Convert.ToInt32(item.Id), item.comments)); // Add comments to project
            }
            project.Notes = NotesContent.Text; // Save notes content

            ProjectWriter.SaveProject(project, filename); // Write project to file
        }

        private void CloseProject_Click(object sender, RoutedEventArgs e)
        {
            Unload(); // Clear current project data
        }

        public void RefreshView()
        {
            ScrollControl(-TraceHandler.load_count);
            ScrollControl(TraceHandler.load_count);
        }

        private void HideUselessAssignments_Click(object sender, RoutedEventArgs e)
        {
            DeObfus.DeObfuscate();
        }

        private void ResetHidden_Click(object sender, RoutedEventArgs e)
        {
            WPF_TraceRow.hiddenRows.Clear();
            RefreshView();
        }
    }
}