using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using TraceViewer.Core.Analysis;

namespace TraceViewer.Core
{
    public class MnemObject
    {
        public string Mnem { get; set; } = "";
        public string Description { get; set; } = "";
    }

    class TraceHandler
    {
        public static TraceData? Trace { get; set; }
        public static MainWindow window { get; set; }

        // Mnemonic data — loaded once on first use (lazy), then cached via FrozenDictionary for O(1) lookup
        private static FrozenDictionary<string, string>? _briefLookup;
        private static FrozenDictionary<string, string>? _fullLookup;
        private static bool _mnemonicsLoaded;

        public static int load_count = 40;

        /// <summary>
        /// Loads the mnemonic database once from the embedded resource.
        /// Subsequent calls are no-ops. Uses System.Text.Json instead of Newtonsoft.
        /// </summary>
        private static void EnsureMnemonicsLoaded()
        {
            if (_mnemonicsLoaded) return;

            var uri = new Uri("pack://application:,,,/mnemdb.json");
            var stream = Application.GetResourceStream(uri)?.Stream
                ?? throw new InvalidOperationException("mnemdb.json resource stream not found");

            using var reader = new StreamReader(stream);
            string json = reader.ReadToEnd();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Build brief lookup
            var briefDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (root.TryGetProperty("x86-64-brief", out var briefArray))
            {
                foreach (var item in briefArray.EnumerateArray())
                {
                    string mnem = GetJsonString(item, "mnem", "Mnem");
                    string desc = GetJsonString(item, "description", "Description");
                    if (!string.IsNullOrEmpty(mnem))
                    {
                        briefDict.TryAdd(mnem, desc);
                    }
                }
            }
            _briefLookup = briefDict.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

            // Build full lookup with redirect resolution
            var rawDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (root.TryGetProperty("x86-64", out var fullArray))
            {
                foreach (var item in fullArray.EnumerateArray())
                {
                    string mnem = GetJsonString(item, "mnem", "Mnem");
                    string desc = GetJsonString(item, "description", "Description");
                    if (!string.IsNullOrEmpty(mnem))
                    {
                        rawDict.TryAdd(mnem, desc);
                    }
                }
            }

            // Resolve -R: redirects into a flat dictionary
            var resolvedDict = new Dictionary<string, string>(rawDict.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in rawDict)
            {
                resolvedDict[kvp.Key] = ResolveRedirect(rawDict, kvp.Key, kvp.Value, maxDepth: 10);
            }
            _fullLookup = resolvedDict.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

            _mnemonicsLoaded = true;
        }

        private static string GetJsonString(JsonElement elem, string propLower, string propUpper)
        {
            if (elem.TryGetProperty(propLower, out var p) || elem.TryGetProperty(propUpper, out p))
            {
                return p.GetString() ?? "";
            }
            return "";
        }

        private static string ResolveRedirect(Dictionary<string, string> raw, string key, string value, int maxDepth)
        {
            int depth = 0;
            string current = value;
            while (current.StartsWith("-R:") && depth < maxDepth)
            {
                string target = current[3..];
                if (!raw.TryGetValue(target, out string? resolved))
                    return "";
                current = resolved;
                depth++;
            }
            return current;
        }

        public static void OpenAndLoad(string path)
        {
            window = Application.Current.MainWindow as MainWindow
                ?? throw new InvalidOperationException("Main window not found");

            // Ensure mnemonics are loaded (one-time operation)
            EnsureMnemonicsLoaded();

            Trace = TraceLoader.OpenX64dbgTrace(path);

            if (Trace.Trace.Count == 0)
                throw new InvalidOperationException("Trace was empty");

            load_count = Trace.Trace.Count < 40 ? Trace.Trace.Count : 40;

            var x64Regs = REGDUMP.X64_REGS;

            for (int i = 0; i < x64Regs.Length; i++)
            {
                var regName = x64Regs[i].Name;
                if (string.IsNullOrEmpty(regName))
                    continue;

                // Handle register reordering (rbx↔rcx↔rdx display order)
                string displayName;
                int regTypeIndex = i;
                if (i == 1)
                {
                    displayName = x64Regs[3].Name.ToUpper();
                }
                else if (i == 2)
                {
                    displayName = x64Regs[1].Name.ToUpper();
                }
                else if (i == 3)
                {
                    displayName = x64Regs[2].Name.ToUpper();
                }
                else
                {
                    displayName = regName.ToUpper();
                }

                var wpfRow = new WPF_RegisterRow(displayName, "0", GetRegisterType(i));
                window.RegisterViewItems.Add(wpfRow);
            }

            MemoryHandler.ComposeMemory(Trace);
            GraphHandler.GenerateGraph();

            window.Stats.Content = $"IDs: {Trace.Trace.Count}  -  Unique Addresses: {GraphHandler.uniqueIPAccesses?.Count ?? 0}";
            window.index = load_count;
            LoadRange(0, load_count, false);
        }

        public static void LoadRange(int low, int high, bool prepend)
        {
            if (Trace is null)
                throw new InvalidOperationException("Trace was null");

            var traceData = Trace;
            int traceCount = traceData.Trace.Count;

            if (low < 0 || high > traceCount)
                throw new InvalidOperationException("low or high value out of bounds");

            for (int i = low; i < high; i++)
            {
                var row = traceData.Trace[i];
                string instructionMnemonic = row.Disasm.AsSpan().SliceToFirstSpace();

                // O(1) dictionary lookup instead of linear search
                string mnemonicBrief = _briefLookup?.GetValueOrDefault(instructionMnemonic) ?? "";
                string mnemonic = _fullLookup?.GetValueOrDefault(instructionMnemonic) ?? "";

                if (string.IsNullOrEmpty(mnemonic))
                    mnemonic = $"{mnemonicBrief}\nSadly thats it...\n\nMaybe this link can be helpful: https://faydoc.tripod.com/cpu/index.htm";

                var wpfRow = new WPF_TraceRow(row, mnemonicBrief, mnemonic);

                if (prepend)
                    window.InstructionViewItems.Insert(0, wpfRow);
                else
                    window.InstructionViewItems.Add(wpfRow);
            }
        }

        private static RegisterType GetRegisterType(int i) => i switch
        {
            1 or 2 or 3 => RegisterType.GeneralPurpose,
            17 => RegisterType.Flags,
            >= 18 and <= 23 => RegisterType.Debug,
            >= 24 => RegisterType.FPU,
            _ => RegisterType.GeneralPurpose,
        };

        public static void Clear()
        {
            if (Trace is null) return;
            Trace.Trace.Clear();
            Trace = null;
        }
    }

    /// <summary>Extension methods for span-based string operations.</summary>
    internal static class SpanStringExtensions
    {
        /// <summary>Extracts the substring before the first space. Returns the full string if no space found.</summary>
        public static string SliceToFirstSpace(this ReadOnlySpan<char> span)
        {
            int idx = span.IndexOf(' ');
            return idx < 0 ? span.ToString() : span[..idx].ToString();
        }
    }
}