using Microsoft.Win32;
using System.IO;
using System.Windows;
using static System.Net.WebRequestMethods;
using TraceViewer.Core.Analysis;
using TraceViewer.Core;
using TraceViewer.UserWindows;

namespace TraceViewer
{
    public partial class MainWindow : Window
    {
        private void Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    string filePath = files[0];
                    string fileExtension = Path.GetExtension(filePath);

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
            DropZone.Visibility = Visibility.Visible; // Show drop zone indicator
        }

        private void DragLeave(object sender, DragEventArgs e)
        {
            MainView.Opacity = 1; // Restore MainView opacity after drag leave
            DropZone.Visibility = Visibility.Hidden; // Hide drop zone indicator
        }

        public void Unload()
        {
            // Clear all data and reset UI to initial state
            InstructionViewItems.Clear();
            RegisterViewItems.Clear();
            NotesContent.Text = "";
            StackView.Document.Blocks.Clear();
            HeapView.Document.Blocks.Clear();
            Stats.Content = "";
            _current_project_path = "";
            SetTitle("survivalizeed's Trace Viewer", false);
            WPF_TraceRow.hiddenRows.Clear();
            WPF_TraceRow.stack_alignment = 8;
            WPF_TraceRow.stack_alignment_base = 0;
            WPF_TraceRow.heap_alignment = 8;
            WPF_TraceRow.heap_alignment_base = 0;
            DeObfus.deObHiddenRows.Clear();
            MemoryHandler.Clear();
            GraphHandler.Clear();
            GraphViewClear();
            
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
                SetTitle("  -  UNSAVED WORK", true); // Set title to indicate unsaved work
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
            SetTitle(original_title + "  -  " + _current_project_path, false);
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
            DeObfus.deObHiddenRows = project.DeObHiddenRows; // Load deobfuscated hidden rows
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
            Dispatcher.Invoke(() =>
            {
                _current_project_path = filename;
                SetTitle(original_title + "  -   " + filename, false);
            });

            Project project = new Project
            {
                TraceData = TraceHandler.Trace,
                HiddenRows = WPF_TraceRow.hiddenRows,
                DeObHiddenRows = DeObfus.deObHiddenRows,
                Comments = new List<Tuple<int, string>>(), 
                Notes = Dispatcher.Invoke(() => NotesContent.Text) 
            };

            foreach (var item in TraceHandler.Trace.Trace)
            {
                if (!string.IsNullOrEmpty(item.comments))
                    project.Comments.Add(new Tuple<int, string>(Convert.ToInt32(item.Id), item.comments)); 
            }

            ProjectWriter.SaveProject(project, filename);
        }


        private void CloseProject_Click(object sender, RoutedEventArgs e)
        {
            Unload(); // Clear current project data
        }
    }
}