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
                    if (i == traceData.Trace.Count - 1)
                    {
                        wpfRow = new WPF_TraceRow(traceData.Trace[i], null, registers_view);
                    }
                    else
                    {
                        wpfRow = new WPF_TraceRow(traceData.Trace[i], traceData.Trace[i + 1], registers_view);
                    }
                    instructions.Add(wpfRow);
                }
                if(traceData.Arch == "x64")
                {
                    for(int i = 0; i < prefs.X64_REGS.Count; i++)
                    {
                        WPF_RegisterRow wpfRow;
                        if(prefs.X64_REGS[i].Item1 == "")
                        {
                        continue;
                        }
                        // All those if statements so the order is: rax, rbx, rcx, rdx
                        if (i == 1)
                        {
                            wpfRow = new WPF_RegisterRow(prefs.X64_REGS[3].Item1.ToUpper(), "0", RegisterType.GeneralPurpose);
                        }
                        else if (i == 2)
                        {
                            wpfRow = new WPF_RegisterRow(prefs.X64_REGS[1].Item1.ToUpper(), "0", RegisterType.GeneralPurpose);
                        }
                        else if (i == 3)
                        {
                            wpfRow = new WPF_RegisterRow(prefs.X64_REGS[2].Item1.ToUpper(), "0", RegisterType.GeneralPurpose);
                        }
                        else
                        {
                            RegisterType registerType = RegisterType.GeneralPurpose;
                            if (i == 17)
                            {
                                registerType = RegisterType.Flags;
                            }
                            else if (i >= 25 && i <= 30)
                            {
                                registerType = RegisterType.Debug;
                            }
                            else if(i >= 35)
                            {
                                registerType = RegisterType.FPU;
                            }
                            wpfRow = new WPF_RegisterRow(prefs.X64_REGS[i].Item1.ToUpper(), "0", registerType);
                        }
                        registers.Add(wpfRow);
                    }
                }
        }
        


    }
}
