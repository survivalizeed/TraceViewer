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
using static System.Net.Mime.MediaTypeNames;

namespace TraceViewer
{

    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            InstructionsView.Loaded += InstructionsView_Loaded;
            InstructionsView.ItemsSource = InstructionViewItems;
            RegistersView.ItemsSource = RegisterViewItems;
            BookmarksViewItemControl.ItemsSource = BookmarkViewItems;
            PreviewKeyDown += MainWindow_PreviewKeyDown;
            SourceInitialized += MainWindow_SourceInitialized;

            InstructionsView.AllowDrop = true;
            InstructionsView.DragEnter += DragEnter;
            InstructionsView.DragLeave += DragLeave;
            InstructionsView.Drop += Drop;

            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;

            DisasmViewButton_MouseDown(null, null);

        }

        private void SetTitle(string text, bool append)
        {
            Dispatcher.Invoke(() => { this.Title = append ? Title + text : text; ; });
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