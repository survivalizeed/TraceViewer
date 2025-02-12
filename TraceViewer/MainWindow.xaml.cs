using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TraceViewer.Core;

namespace TraceViewer
{
    public partial class MainWindow : Window
    {
        private bool _toggleFpu = true; // Use backing field for toggle state
        private bool _toggleMnemonic = true; // Use backing field for toggle state
        public double _disassemblerViewOffset = 0; // Use backing field for offset

        public ScrollViewer InstructionsScrollViewer { get; private set; } // Public property for ScrollViewer
        public ObservableCollection<WPF_TraceRow> InstructionViewItems { get; } = new(); // Use property initializer
        public ObservableCollection<WPF_RegisterRow> RegisterViewItems { get; } = new(); // Use property initializer
        public TextBox CurrentCommentContentPartner { get; set; } // Public property

        public MainWindow()
        {
            InitializeComponent();
            InstructionsView.ItemsSource = InstructionViewItems;
            RegistersView.ItemsSource = RegisterViewItems;
            InstructionsView.Loaded += InstructionsView_Loaded;
            TraceHandler.Load(@"F:\Tools\Reversing\x64dbg\release\x64\db\trace.trace64"); // Raw string literal
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
            if (sender is Label label)
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
            DisassemblerView.Visibility = Visibility.Visible;
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
            DisassemblerView.Visibility = Visibility.Visible;
            SetInstructionsViewScrollbar(_disassemblerViewOffset);
        }

        private void SetInstructionsViewScrollbar(double offset)
        {
            InstructionsScrollViewer?.ScrollToVerticalOffset(offset); // Null-conditional operator
        }
    }
}