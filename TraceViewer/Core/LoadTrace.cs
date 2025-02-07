using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows;
using System.Collections.ObjectModel;

namespace TraceViewer.Core
{
    class LoadTrace
    {
        public static void Load(string path, ref ObservableCollection<WPF_TraceRow> items)
        {
            TraceLoader loader = new TraceLoader();

            try
            {
                TraceData traceData = loader.OpenX64dbgTrace(path);

                foreach (var row in traceData.Trace)
                {
                    var wpfRow = new WPF_TraceRow(row);

                    items.Add(wpfRow);
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("Error loading the .trace64 file: " + ex.Message);
            }
        }

        //public static async ObservableCollection<WPF_TraceRow> LoadAsync(string path)
        //{
        //
        //    ObservableCollection<WPF_TraceRow> items = new ObservableCollection<WPF_TraceRow>();
        //    TraceLoader loader = new TraceLoader();
        //
        //    try
        //    {
        //        TraceData traceData = await Task.Run(() => loader.OpenX64dbgTrace(path));
        //
        //        foreach (var row in traceData.Trace)
        //        {
        //            var wpfRow = new WPF_TraceRow(row);
        //
        //            await Dispatcher.InvokeAsync(() =>
        //            {
        //                items.Add(wpfRow);
        //            });
        //
        //            await Task.Delay(1);
        //        }
        //    }
        //    catch (System.Exception ex)
        //    {
        //        MessageBox.Show("Fehler beim Laden der Trace-Datei: " + ex.Message);
        //    }
        //}
    }
}
