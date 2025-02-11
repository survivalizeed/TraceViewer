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
        public static void Load(string path)
        {
            var window = System.Windows.Application.Current.MainWindow as MainWindow;
            if (window == null)
                return;

            TraceLoader loader = new TraceLoader();
            TraceData traceData = loader.OpenX64dbgTrace(path);

            int traceCount = traceData.Trace.Count; 

            for (int i = 0; i < traceCount; i++)
            {
                WPF_TraceRow wpfRow = new WPF_TraceRow(traceData.Trace[i], (i < traceCount - 1) ? traceData.Trace[i + 1] : null);
                window.instruction_view_items.Add(wpfRow);
            }

            if (traceData.Arch == "x64")
            {
                var x64Regs = prefs.X64_REGS;

                for (int i = 0; i < x64Regs.Count; i++)
                {
                    if (string.IsNullOrEmpty(x64Regs[i].Item1)) 
                    {
                        continue;
                    }

                    WPF_RegisterRow wpfRow;
                    RegisterType registerType = RegisterType.GeneralPurpose; 

                    // case 1,2,3 are for the correct order in the register view
                    switch (i)
                    {
                        case 1: // rbx
                            wpfRow = new WPF_RegisterRow(x64Regs[3].Item1.ToUpper(), "0", registerType);
                            break;
                        case 2: // rcx
                            wpfRow = new WPF_RegisterRow(x64Regs[1].Item1.ToUpper(), "0", registerType);
                            break;
                        case 3: // rdx
                            wpfRow = new WPF_RegisterRow(x64Regs[2].Item1.ToUpper(), "0", registerType);
                            break;
                        case 17: //
                            registerType = RegisterType.Flags;
                            wpfRow = new WPF_RegisterRow(x64Regs[i].Item1.ToUpper(), "0", registerType);
                            break;
                        case 25: // Fallthrough
                        case 26:
                        case 27:
                        case 28:
                        case 29:
                        case 30:
                            registerType = RegisterType.Debug;
                            wpfRow = new WPF_RegisterRow(x64Regs[i].Item1.ToUpper(), "0", registerType);
                            break;
                        default: // FPU (last entries)
                            if (i >= 35)
                            {
                                registerType = RegisterType.FPU;
                            }
                            wpfRow = new WPF_RegisterRow(x64Regs[i].Item1.ToUpper(), "0", registerType);
                            break;
                    }
                    window.register_view_items.Add(wpfRow);
                }
            }
        }
    }
}