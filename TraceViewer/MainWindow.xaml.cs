using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
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
        public bool _toggleStack = true;

        private string _current_project_path = "";
        private string original_title = "survivalizeed's Trace Viewer";
        public ScrollViewer InstructionsScrollViewer { get; private set; }
        public ObservableCollection<WPF_TraceRow> InstructionViewItems = new();
        public ObservableCollection<WPF_RegisterRow> RegisterViewItems = new();

        public MainWindow()
        {
            InitializeComponent();
            //InstructionsView.Loaded += InstructionsView_Loaded;
            InstructionsView.ItemsSource = InstructionViewItems;
            RegistersView.ItemsSource = RegisterViewItems;
            PreviewKeyDown += MainWindow_PreviewKeyDown;
            SourceInitialized += MainWindow_SourceInitialized;

            InstructionsView.AllowDrop = true;
            InstructionsView.DragEnter += DragEnter;
            InstructionsView.DragLeave += DragLeave;
            InstructionsView.Drop += Drop;

            DataContext = this;

            Gen();

            DisasmViewButton_MouseDown(null, null); // Set Disassembler View as default
        }

        private void SetTitle(string text, bool append)
        {
            Title = append ? Title + text : text;
        }

        public void RefreshView()
        {
            ScrollControl(-TraceHandler.load_count);
            ScrollControl(TraceHandler.load_count);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect(); // Here needed. Otherwise the GC will wait too long to collect the data leading to a strong memory consumption increase
        }
    }
}