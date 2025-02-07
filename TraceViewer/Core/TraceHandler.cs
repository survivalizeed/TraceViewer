using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace TraceViewer.Core
{
    class TraceHandler
    {
        public static void Load(string path, ref ObservableCollection<WPF_TraceRow> instructions, ref ObservableCollection<WPF_RegisterRow> registers, 
            ItemsControl registers_view)
        {
            TraceLoader loader = new TraceLoader();

            try
            {
                TraceData traceData = loader.OpenX64dbgTrace(path);

                foreach (var row in traceData.Trace)
                {
                    var wpfRow = new WPF_TraceRow(row, registers_view);

                    instructions.Add(wpfRow);
                }
                if(traceData.Arch == "x64")
                {
                    for(int i = 0; i < prefs.X64_REGS.Length; i++)
                    {
                        var wpfRow = new WPF_RegisterRow(prefs.X64_REGS[i].ToUpper(), "0");
                        registers.Add(wpfRow);
                    }
                }

            }
            catch (System.Exception ex)
            {
                MessageBox.Show("Error loading the .trace64 file: " + ex.Message);
            }
        }
        


    }
}
