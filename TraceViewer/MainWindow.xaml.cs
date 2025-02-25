using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reflection.Emit;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using TraceViewer.Core;

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
        private bool _toggleFpu = true; // Use backing field for toggle state
        public bool _toggleMnemonic = true; // Use backing field for toggle state
        public double _disassemblerViewOffset = 0; // Use backing field for offset

        public ScrollViewer InstructionsScrollViewer { get; private set; } // Public property for ScrollViewer
        public ObservableCollection<WPF_TraceRow> InstructionViewItems { get; } = new(); // Use property initializer
        public ObservableCollection<WPF_RegisterRow> RegisterViewItems { get; } = new(); // Use property initializer
        public TextBox CurrentCommentContentPartner { get; set; } // Public property

        public MainWindow()
        {
            InitializeComponent();
            InstructionsView.Loaded += InstructionsView_Loaded;
            InstructionsView.ItemsSource = InstructionViewItems;
            RegistersView.ItemsSource = RegisterViewItems;

            DisasmViewButton_MouseDown(null, null); // Will be the default view when opening the application  
        }

        private void InstructionsView_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is ItemsControl itemsControl && // Using pattern matching
                itemsControl.Template.FindName("instructions_view_scrollviewer", itemsControl) is ScrollViewer scrollViewer)
            {
                InstructionsScrollViewer = scrollViewer;
            }
            else
            {
                throw new InvalidOperationException("ScrollViewer not found in template"); // More specific exception
            }
        }


        // Consolidated SizeChanged event handlers
        private void TitleLabel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.Label label)
            {
                InstructionsView.BeginInit();
                try
                {
                    foreach (var item in InstructionViewItems)
                    {
                        // Use a dictionary or a better way to map columns to properties
                        switch (label.Name)
                        {
                            case "id": // ID
                                item.id.Width = e.NewSize.Width;
                                item.id_border.Width = e.NewSize.Width;
                                break;
                            case "address": // Address
                                item.address.Width = e.NewSize.Width;
                                item.address_border.Width = e.NewSize.Width;
                                break;
                            case "disasm": // Disasm
                                item.disasm.Width = e.NewSize.Width;
                                item.disasm_border.Width = e.NewSize.Width;
                                break;
                            case "changes": // Changes
                                item.changes.Width = e.NewSize.Width;
                                item.changes_border.Width = e.NewSize.Width;
                                break;
                            case "comments": // Comments/MnemonicBrief
                                item.comments.Width = e.NewSize.Width;
                                item.mnemonicBrief.Width = e.NewSize.Width;
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
        }


        private void SetInstructionsViewWidth()
        {
            var totalWidth = cd0.Width.Value + cd1.Width.Value + cd2.Width.Value + cd3.Width.Value + cd4.Width.Value + cd5.Width.Value + 8;
            if (totalWidth > 0)
            {
                InstructionsView.MinWidth = totalWidth;
                InstructionsView.MaxWidth = totalWidth;
            }
        }

        private void Fpu_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _toggleFpu = !_toggleFpu;
            fpu.Foreground = _toggleFpu ? Brushes.White : Brushes.Gray;

            var fpuVisibility = _toggleFpu ? Visibility.Visible : Visibility.Collapsed;

            foreach (var item in RegisterViewItems)
            {
                if (item.registerType == RegisterType.FPU)
                {
                    item.Visibility = fpuVisibility;
                }
            }
        }

        private void CommentContentGridHitbox_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            BigCommentEditInactive();
        }

        private void CommentContent_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter || e.Key == System.Windows.Input.Key.Escape)
            {
                BigCommentEditInactive();
            }
        }

        private void BigCommentEditInactive()
        {
            CommentContentGridHitbox.Visibility = Visibility.Collapsed;
            MainView.Visibility = Visibility.Visible;
            CurrentCommentContentPartner?.Focus(); // Null-conditional operator and simplified focus setting
            CurrentCommentContentPartner.Text = CommentContent.Text; // Update text
        }

        private void Comments_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _toggleMnemonic = !_toggleMnemonic;
            InstructionsView.BeginInit();
            foreach (var item in InstructionViewItems)
            {
                item.display_mnemonic_brief(!_toggleMnemonic);
            }
            InstructionsView.EndInit();
            comments.Content = !_toggleMnemonic ? "MNEMONIC" : "COMMENTS";
        }

        private void MnemonicReader_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            BigMnemonicViewInactive();
        }

        private void BigMnemonicViewInactive()
        {
            MnemonicReaderScrollView.ScrollToVerticalOffset(0);
            MnemonicReaderScrollView.Visibility = Visibility.Collapsed;
            MainView.Visibility = Visibility.Visible;
            SetInstructionsViewScrollbar(_disassemblerViewOffset);
        }

        private void SetInstructionsViewScrollbar(double offset)
        {
            InstructionsScrollViewer?.ScrollToVerticalOffset(offset); // Null-conditional operator
        }

        private DropShadowEffect glowEffect = new DropShadowEffect
        {
            Color = Colors.White,
            BlurRadius = 10,
            ShadowDepth = 0,
            Opacity = 0.8
        };

        private void DisasmViewButton_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            DisasmViewButtonBorder.Background = new SolidColorBrush(Color.FromArgb(255, 40, 40, 40));
            DisasmViewButtonBorder.Effect = glowEffect;
            NotesViewButtonBorder.Background = Brushes.Transparent;
            NotesViewButtonBorder.Effect = null;
            BookmarksViewButtonBorder.Background = Brushes.Transparent;
            BookmarksViewButtonBorder.Effect = null;
            SetCurrentUIState(UIState.DisassemblerView);
        }

        private void NotesViewButton_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            DisasmViewButtonBorder.Background = Brushes.Transparent;
            DisasmViewButtonBorder.Effect = null;
            NotesViewButtonBorder.Background = new SolidColorBrush(Color.FromArgb(255, 40, 40, 40));
            NotesViewButtonBorder.Effect = glowEffect;
            BookmarksViewButtonBorder.Background = Brushes.Transparent;
            BookmarksViewButtonBorder.Effect = null;
            SetCurrentUIState(UIState.NotesView);
        }

        private void BookmarksViewButton_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            DisasmViewButtonBorder.Background = Brushes.Transparent;
            DisasmViewButtonBorder.Effect = null;
            NotesViewButtonBorder.Background = Brushes.Transparent;
            NotesViewButtonBorder.Effect = null;
            BookmarksViewButtonBorder.Background = new SolidColorBrush(Color.FromArgb(255, 40, 40, 40));
            BookmarksViewButtonBorder.Effect = glowEffect;
            SetCurrentUIState(UIState.BookmarksView);
        }

        private void SetCurrentUIState(UIState uiState)
        {
            switch (uiState)
            {
                case UIState.DisassemblerView:
                    DisassemblerView.Visibility = Visibility.Visible;
                    NotesView.Visibility = Visibility.Collapsed;
                    break;
                case UIState.NotesView:
                    DisassemblerView.Visibility = Visibility.Collapsed;
                    NotesView.Visibility = Visibility.Visible;
                    break;
                case UIState.BookmarksView:
                    DisassemblerView.Visibility = Visibility.Collapsed;
                    NotesView.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        private void OpenTrace_Click(object sender, RoutedEventArgs e)
        {
            InstructionViewItems.Clear();
            RegisterViewItems.Clear();
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Trace Files (*.trace64)|*.trace64|All Files (*.*)|*.*",
                FilterIndex = 1,
                Multiselect = false
            };
            if (openFileDialog.ShowDialog() == true)
            {
                TraceHandler.OpenAndLoad(openFileDialog.FileName);
            }
        }


        static int index = TraceHandler.load_count; // Default loaded size

        private void InstructionsScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if(Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                if (e.Delta > 0)
                    ScrollControl(15);
                else if (e.Delta < 0)
                    ScrollControl(-15);
            }
            if (e.Delta > 0)
                ScrollControl(3);
            else if(e.Delta < 0)
                ScrollControl(-3);
        }

        public bool ScrollControl(int steps)
        {
            if (TraceHandler.Trace == null)
                return false;

            // If the scroll jumps one or more entire pages, it will completely skip to the target control instead of loading/unloading them all
            int abs_steps = Math.Abs(steps);

            if (abs_steps > TraceHandler.load_count)
            {
                int full_cap = abs_steps / TraceHandler.load_count;
                full_cap--; // One page will later be loaded so take one away here
                int increment = 0;
                if (steps < 0)
                    increment = TraceHandler.load_count;
                else
                    increment = -TraceHandler.load_count;
                index += increment * full_cap;
                if (index < TraceHandler.load_count) index = TraceHandler.load_count * 2;
                if (index > TraceHandler.Trace.Trace.Count) 
                    index = TraceHandler.Trace.Trace.Count - TraceHandler.load_count;
                steps %= TraceHandler.load_count;
                steps += -increment; // One new page has to be loaded to fully refresh the view
                
            }

            bool return_value = false;
            if (steps > 0) // Up
            {
                for (int i = 0; i < steps; i++)
                {
                    if (InstructionViewItems.Count > 0 && index - TraceHandler.load_count - 1 >= 0)
                    {
                        InstructionViewItems.RemoveAt(InstructionViewItems.Count - 1);
                        TraceHandler.LoadRange(index - TraceHandler.load_count - 1, index - TraceHandler.load_count, true);
                        index--;
                        return_value = true;
                    }
                }
            }
            else if (steps < 0) // Down
            {
                for (int i = 0; i < Math.Abs(steps); i++)
                {
                    if (InstructionViewItems.Count > 0 && index < TraceHandler.Trace.Trace.Count)
                    {
                        InstructionViewItems.RemoveAt(0);
                        TraceHandler.LoadRange(index, index + 1, false);
                        index++;
                        return_value = true;
                    }
                }
            }
            return return_value; // To check if there was even a possible scroll
        }

        private void SaveProject_Click(object sender, RoutedEventArgs e)
        {
            if(TraceHandler.Trace == null)
                return;

            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.DefaultExt = ".tvproj";
            saveFileDialog.Filter = "Trace Viewer Project (.tvproj)|*.tvproj";
            saveFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            saveFileDialog.Title = "Save as";
            saveFileDialog.ShowDialog();

            Project project = new Project();
            project.TraceData = TraceHandler.Trace;


            //ProjectWriter.SaveProject(, saveFileDialog.FileName);
        }
    }
    
}