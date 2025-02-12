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
        public static void Load(string path)
        {
            var window = Application.Current.MainWindow as MainWindow;
            if (window == null)
                throw new InvalidOperationException("Main window not found");

            var loader = new TraceLoader();
            var traceData = loader.OpenX64dbgTrace(path);

            var traceCount = traceData.Trace.Count;

            var uri = new Uri("pack://application:,,,/mnemdb.json");
            using (var stream = Application.GetResourceStream(uri)?.Stream) // Null check
            using (var reader = stream != null ? new StreamReader(stream) : null) // Conditional stream reader creation
            {
                if (reader != null) // Check if reader is valid before proceeding
                {
                    var root = JsonConvert.DeserializeObject<Dictionary<string, object>>(reader.ReadToEnd());

                    if (root != null && // Null check for root
                        root.TryGetValue("x86-64-brief", out var x86_64DataBrief) && x86_64DataBrief is JArray jsonArrayBrief &&
                        root.TryGetValue("x86-64", out var x86_64Data) && x86_64Data is JArray jsonArray)
                    {
                        var dataBrief = jsonArrayBrief.ToObject<List<MnemObject>>();
                        var data = jsonArray.ToObject<List<MnemObject>>();

                        for (int i = 0; i < traceCount; i++)
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
                                (i < traceCount - 1) ? traceData.Trace[i + 1] : null,
                                mnemonicBrief,
                                mnemonic
                            );

                            window.InstructionViewItems.Add(wpfRow);
                        }
                    }
                }
            }

            if (traceData.Arch == "x64")
            {
                var x64Regs = prefs.X64_REGS;

                for (int i = 0; i < x64Regs.Count; i++)
                {
                    var regName = x64Regs[i].Item1;
                    if (string.IsNullOrEmpty(regName))
                    {
                        continue;
                    }

                    var registerType = GetRegisterType(i); // Extracting register type determination

                    var wpfRow = new WPF_RegisterRow(regName.ToUpper(), "0", registerType);
                    window.RegisterViewItems.Add(wpfRow);
                }
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