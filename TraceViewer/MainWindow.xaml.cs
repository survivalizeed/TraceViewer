using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TraceViewer.Core;

namespace TraceViewer
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<WPF_TraceRow> items = new ObservableCollection<WPF_TraceRow>();

        public MainWindow()
        {
            InitializeComponent();
            //instructions_view.BeginInit();
            //TraceLoader loader = new TraceLoader();
            //string traceFilePath = "F:\\Tools\\Reversing\\x64dbg\\release\\x64\\db\\trace.trace64";
            //
            //var traceData = loader.OpenX64dbgTrace(traceFilePath);
            //foreach (var row in traceData.Trace)
            //{
            //    var wpfRow = new WPF_TraceRow(row);
            //    items.Add(wpfRow);
            //   
            //}
            //instructions_view.ItemsSource = items;
            //instructions_view.EndInit();
            instructions_view.ItemsSource = items;
            LadeTraceAsynchron();
        }

        private async void LadeTraceAsynchron()
        {
            TraceLoader loader = new TraceLoader();
            string traceFilePath = "F:\\Tools\\Reversing\\x64dbg\\release\\x64\\db\\trace.trace64";

            try
            {
                TraceData traceData = await Task.Run(() => loader.OpenX64dbgTrace(traceFilePath));

                foreach (var row in traceData.Trace)
                {
                    var wpfRow = new WPF_TraceRow(row);

                    await Dispatcher.InvokeAsync(() =>
                    {
                        items.Add(wpfRow);
                    });

                    await Task.Delay(1);
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("Fehler beim Laden der Trace-Datei: " + ex.Message);
            }
        }

        private void ID_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width != e.PreviousSize.Width)
            {
                instructions_view.BeginInit();
                try
                {
                    foreach (var item in items)
                    {
                        item.id.Width = e.NewSize.Width;
                        item.id_border.Width = e.NewSize.Width;
                    }
                }
                finally
                {
                    instructions_view.EndInit();
                }
            }
        }

        private void ADDRESS_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width != e.PreviousSize.Width)
            {
                instructions_view.BeginInit();
                try
                {
                    foreach (var item in items)
                    {
                        item.address.Width = e.NewSize.Width;
                        item.address_border.Width = e.NewSize.Width;
                    }
                }
                finally
                {
                    instructions_view.EndInit();
                }
            }
        }

        private void DISASM_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width != e.PreviousSize.Width)
            {
                instructions_view.BeginInit();
                try
                {
                    foreach (var item in items)
                    {
                        item.disasm.Width = e.NewSize.Width;
                        item.disasm_border.Width = e.NewSize.Width;
                    }
                }
                finally
                {
                    instructions_view.EndInit();
                }
            }
        }

        private void CHANGES_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width != e.PreviousSize.Width)
            {
                instructions_view.BeginInit();
                try
                {
                    foreach (var item in items)
                    {
                        item.changes.Width = e.NewSize.Width;
                        item.changes_border.Width = e.NewSize.Width;
                    }
                }
                finally
                {
                    instructions_view.EndInit();
                }
            }
        }

        private void COMMENTS_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width != e.PreviousSize.Width)
            {
                instructions_view.BeginInit();
                try
                {
                    foreach (var item in items)
                    {
                        item.comments.Width = e.NewSize.Width;
                    }
                }
                finally
                {
                    instructions_view.EndInit();
                }
            }
        }
    }
}