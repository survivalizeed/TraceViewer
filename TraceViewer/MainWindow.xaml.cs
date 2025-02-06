using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TraceViewer.Core;

namespace TraceViewer
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<WPF_TraceRow> items = new ObservableCollection<WPF_TraceRow>();

        public MainWindow()
        {
            InitializeComponent();
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
    }
}