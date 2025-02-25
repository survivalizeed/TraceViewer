using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TraceViewer.Core
{
    class TraceHandler
    {
        public static TraceData? Trace { get; set; }

        public static MainWindow window { get; set; }

        private static Dictionary<string, object> root;

        private static List<MnemObject> dataBrief;

        private static List<MnemObject> data;

        public static int load_count = 30;

        public static void OpenAndLoad(string path)
        {
            window = Application.Current.MainWindow as MainWindow;
            if (window == null)
                throw new InvalidOperationException("Main window not found");

            if (Trace != null)
            {
                Trace.Trace.Clear();
                Trace.Regs.Clear();
                Trace = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect(); // Here needed. Otherwise the GC will wait too long to collect big unloaded traces
            }

            Trace = TraceLoader.OpenX64dbgTrace(path);

            if (Trace.Trace.Count < load_count)
                load_count = Trace.Trace.Count;
            else
                load_count = 30;

            var uri = new Uri("pack://application:,,,/mnemdb.json");
            var stream = Application.GetResourceStream(uri)?.Stream;
            var reader = stream != null ? new StreamReader(stream) : null;
            if(reader == null)
                throw new InvalidOperationException("Stream reader was null");

            root = JsonConvert.DeserializeObject<Dictionary<string, object>>(reader.ReadToEnd());

            if (root != null && // Null check for root
                    root.TryGetValue("x86-64-brief", out var x86_64DataBrief) && x86_64DataBrief is JArray jsonArrayBrief &&
                    root.TryGetValue("x86-64", out var x86_64Data) && x86_64Data is JArray jsonArray)
            {
                dataBrief = jsonArrayBrief.ToObject<List<MnemObject>>();
                data = jsonArray.ToObject<List<MnemObject>>();
            }
            else
                throw new InvalidOperationException("JSON root was null or the target dicts weren't found");

            var x64Regs = prefs.X64_REGS;

            for (int i = 0; i < x64Regs.Count; i++)
            {
                var regName = x64Regs[i].Item1;
                if (string.IsNullOrEmpty(regName))
                {
                    continue;
                }
                if(i == 1)
                {
                    var rbx_wpfRow = new WPF_RegisterRow(x64Regs[3].Item1.ToUpper(), "0", GetRegisterType(i));
                    window.RegisterViewItems.Add(rbx_wpfRow);
                    continue;
                }
                if (i == 2)
                {
                    var rcx_wpfRow = new WPF_RegisterRow(x64Regs[1].Item1.ToUpper(), "0", GetRegisterType(i));
                    window.RegisterViewItems.Add(rcx_wpfRow);
                    continue;
                }
                if (i == 3)
                {
                    var rdx_wpfRow = new WPF_RegisterRow(x64Regs[2].Item1.ToUpper(), "0", GetRegisterType(i));
                    window.RegisterViewItems.Add(rdx_wpfRow);
                    continue;
                }
                var wpfRow = new WPF_RegisterRow(regName.ToUpper(), "0", GetRegisterType(i));
                window.RegisterViewItems.Add(wpfRow);
            }

            LoadRange(0, load_count, false);
        }

        public static void LoadRange(int low, int high, bool prepend)
        {
            if(Trace == null)
                throw new InvalidOperationException("Trace was null");

            var traceData = Trace;

            var traceCount = traceData.Trace.Count;

            if(low < 0 || high > traceCount)
                throw new InvalidOperationException("low or high value out of bounds");


            for (int i = low; i < high; i++)
            {
                var instructionMnemonic = traceData.Trace[i].Disasm.Split(' ').First();
            
                var mnemonicBrief = dataBrief.FirstOrDefault(m => m.Mnem == instructionMnemonic)?.Description ?? ""; // Using LINQ for brief mnemonic lookup
            
                var mnemonic = FindMnemonic(data, instructionMnemonic.ToUpper()); // Extracting mnemonic finding logic
            
                if (string.IsNullOrEmpty(mnemonic))
                {
                    mnemonic = $"{mnemonicBrief}\nSadly thats it...\n\nMaybe this link can be helpful: https://faydoc.tripod.com/cpu/index.htm"; // String interpolation
                }
            
                var wpfRow = new WPF_TraceRow(
                    traceData.Trace[i],
                    mnemonicBrief,
                    mnemonic
                );
                
                if (prepend)
                    window.InstructionViewItems.Insert(0, wpfRow);
                else
                    window.InstructionViewItems.Add(wpfRow);
            }
            
        }


        private static string FindMnemonic(List<MnemObject> data, string instructionMnemonic)
        {
            foreach (var mnemObj in data)
            {
                if (mnemObj.Mnem == instructionMnemonic)
                {
                    if (mnemObj.Description.StartsWith("-R:"))
                    {
                        return FindMnemonic(data, mnemObj.Description.Substring(3)); // Recursive call for -R: prefixed mnemonics
                    }
                    return mnemObj.Description;
                }
            }
            return "";
        }

        private static RegisterType GetRegisterType(int i)
        {
            switch (i)
            {
                case 1:
                case 2:
                case 3:
                    return RegisterType.GeneralPurpose;
                case 17:
                    return RegisterType.Flags;
                case 25:
                case 26:
                case 27:
                case 28:
                case 29:
                case 30:
                    return RegisterType.Debug;
                default:
                    return i >= 35 ? RegisterType.FPU : RegisterType.GeneralPurpose; // Simplified FPU check
            }
        }
    }
}