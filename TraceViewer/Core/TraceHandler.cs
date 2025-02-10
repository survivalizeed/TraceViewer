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


                TraceData traceData = loader.OpenX64dbgTrace(path);

                for(int i = 0; i < traceData.Trace.Count; i++)
                {
                    WPF_TraceRow wpfRow;
                    if (i == 0)
                    {
                        wpfRow = new WPF_TraceRow(null, traceData.Trace[i], registers_view);
                    }
                    else
                    {
                        wpfRow = new WPF_TraceRow(traceData.Trace[i-1], traceData.Trace[i], registers_view);
                    }
                    instructions.Add(wpfRow);
                }
                if(traceData.Arch == "x64")
                {
                    for(int i = 0; i < prefs.X64_REGS.Count; i++)
                    {
                        WPF_RegisterRow wpfRow;
                        // For paddings
                        if (prefs.X64_REGS[i].Item1 == "")
                        {
                            continue;
                        }
                        // All those if statements so the order is: rax, rbx, rcx, rdx
                        if (i == 1)
                        {
                            wpfRow = new WPF_RegisterRow(prefs.X64_REGS[3].Item1.ToUpper(), "0");
                        }
                        else if (i == 2)
                        {
                            wpfRow = new WPF_RegisterRow(prefs.X64_REGS[1].Item1.ToUpper(), "0");
                        }
                        else if (i == 3)
                        {
                            wpfRow = new WPF_RegisterRow(prefs.X64_REGS[2].Item1.ToUpper(), "0");
                        }
                        else
                        {
                            wpfRow = new WPF_RegisterRow(prefs.X64_REGS[i].Item1.ToUpper(), "0");
                        }
                        registers.Add(wpfRow);
                    }
                }
        }
        


    }
}
