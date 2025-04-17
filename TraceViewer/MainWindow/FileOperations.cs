using Microsoft.Win32;
using System.IO;
using System.Windows;
using static System.Net.WebRequestMethods;
using TraceViewer.Core.Analysis;
using TraceViewer.Core;
using TraceViewer.UserWindows;
using TraceViewer.UserControls;

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
            TraceHandler.Clear(); // Clear trace data
            BlocksViewItemControl.Items.Clear(); // Clear blocks view items
            BlocksHandler.BlocksItems.Clear(); // Clear blocks items

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect(); // Here needed. Otherwise the GC will wait too long to collect big unloaded traces

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

            foreach(var block in project.Blocks)
            {
                var row = TraceHandler.Trace.Trace[block.Item1];
                if (row != null)
                {
                    row.block = block.Item2; // Apply loaded block information
                    row.isBlockStart = true;
                }
            }

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
            _current_project_path = filename;
            SetTitle(original_title + "  -   " + filename, false);
            SaveProjectToFile(filename); // Save project to file
        }

        private void SaveProjectAs_Click(object sender, RoutedEventArgs e)
        {
            if (TraceHandler.Trace == null)
                return;

            SaveFileDialog saveFileDialog = CreateSaveFileDialog(); // Create SaveFileDialog instance
            if (saveFileDialog.ShowDialog() == true)
            {
                _current_project_path = saveFileDialog.FileName;
                SetTitle(original_title + "  -   " + saveFileDialog.FileName, false);
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



        private void RenewTrace_Click(object sender, RoutedEventArgs e)
        {
            if (TraceHandler.Trace == null)
                return;

            if(_current_project_path == "")
            {
                ConfirmDialog con = new ConfirmDialog("You need to save your project first.\r\nWould you like to do this now?");
                con.ShowDialog();
                if (con.GetResult())
                {
                    SaveProject_Click(sender, e);
                }
                else
                    return;
            }
            else
            {
                SaveProject_Click(sender, e);
            }

            MessageDialog messageDialog = new MessageDialog("This option will load a new trace while trying to keep as much progress you have as possible!\r\n" +
                "Shifts in IDs and additional control flows will very likely end up in a lot of lost progress.\r\n" +
                "For your own safety, a backup of your project will be created!\r\n" +
                "Make sure to use the same base instruction in both traces!", 900, 210);


            messageDialog.ShowDialog();

            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Trace Files (*.trace64)|*.trace64",
                FilterIndex = 1,
                Multiselect = false
            };
            if (openFileDialog.ShowDialog() == true)
            {
                CreateBackUp("before_renew", true);

                Dictionary<int, string> disasm = new Dictionary<int, string>();
                Dictionary<int, string> comments = new Dictionary<int, string>();
                Dictionary<int, string> blocks = new Dictionary<int, string>();
                List<int> blockStarts = new List<int>();
                foreach (var item in TraceHandler.Trace.Trace)
                {
                    bool any = false;
                    if(item.comments != "")
                    {
                        comments[item.Id] = item.comments;
                        any = true;
                    }
                    if (item.isBlockStart)
                    {
                        blocks[item.Id] = item.block;
                        blockStarts.Add(item.Id);
                        any = true;
                    }
                    if (any)
                        disasm[item.Id] = item.Disasm;
                }

                // Partially delete the data
                TraceHandler.Clear(); // Clear trace data
                MemoryHandler.Clear();
                GraphHandler.Clear();
                GraphViewClear();
                BlocksHandler.BlocksItems.Clear(); // Clear blocks items
                StackView.Document.Blocks.Clear();
                HeapView.Document.Blocks.Clear();
                Stats.Content = "";
                InstructionViewItems.Clear();
                RegisterViewItems.Clear();
                TraceHandler.OpenAndLoad(openFileDialog.FileName);
                if (TraceHandler.Trace != null) {
                    // Restore the data. Double check if the ID is still valid by comparing the disasm
                    foreach (var item in TraceHandler.Trace.Trace)
                    {
                        if (comments.ContainsKey(item.Id) && item.Disasm == disasm[item.Id])
                        {
                            item.comments = comments[item.Id];
                        }
                        if (blocks.ContainsKey(item.Id) && item.Disasm == disasm[item.Id])
                        {
                            item.block = blocks[item.Id];
                            item.isBlockStart = true;
                        }
                    }
                }
            }
        }


        public void CreateBackUp(string message = "", bool timestamp = true)
        {
            if (_current_project_path == "")
                return;
            string projectName = Path.GetFileNameWithoutExtension(_current_project_path);
            string backupPath = Path.Combine(Path.GetDirectoryName(_current_project_path), projectName + "_backup");
            Directory.CreateDirectory(backupPath);
            string backupFilename = timestamp ? 
                $"{Path.GetFileNameWithoutExtension(_current_project_path)}_{DateTime.Now:dd.MM.yy_HH.mm.ss}{message}.tvproj" : 
                $"{Path.GetFileNameWithoutExtension(_current_project_path)}{message}.tvproj";
            string backupFullPath = Path.Combine(backupPath, backupFilename);
            SaveProjectToFile(backupFullPath); // Save project to file
        }


        private void CloseProject_Click(object sender, RoutedEventArgs e)
        {
            Unload(); // Clear current project data
        }
    }
}