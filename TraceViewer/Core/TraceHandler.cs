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

        public static FrozenDictionary<string, string>? BriefLookup => _briefLookup;
        public static FrozenDictionary<string, string>? FullLookup => _fullLookup;

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

            EnsureMnemonicsLoaded();

            var loadedTrace = TraceLoader.OpenX64dbgTrace(path);

            if (loadedTrace.Trace.Count == 0)
                throw new InvalidOperationException("Trace was empty");

            InitializeLoadedTrace(loadedTrace, window);
        }

        public static void InitializeLoadedTrace(TraceData trace, MainWindow window)
        {
            EnsureMnemonicsLoaded();
            TraceHandler.window = window;
            Trace = trace;
            load_count = trace.Trace.Count < 40 ? trace.Trace.Count : 40;

            var x64Regs = REGDUMP.X64_REGS;
            window.RegisterViewItems.Clear();

            for (int i = 0; i < x64Regs.Length; i++)
            {
                var regName = x64Regs[i].Name;
                if (string.IsNullOrEmpty(regName))
                    continue;

                // Handle register reordering (rbx↔rcx↔rdx display order)
                string displayName = i switch
                {
                    1 => x64Regs[3].Name.ToUpper(),
                    2 => x64Regs[1].Name.ToUpper(),
                    3 => x64Regs[2].Name.ToUpper(),
                    _ => regName.ToUpper()
                };

                var wpfRow = new WPF_RegisterRow(displayName, "0", GetRegisterType(i));
                window.RegisterViewItems.Add(wpfRow);
            }

            MemoryHandler.ComposeMemory(trace);
            GraphHandler.GenerateGraph();

            window.Stats.Content = $"IDs: {trace.Trace.Count}  -  Unique Addresses: {GraphHandler.uniqueIPAccesses?.Count ?? 0}";
            window.InitTraceView();
        }

        public static void LoadRange(int low, int high, bool prepend)
        {
            if (Trace is null)
                throw new InvalidOperationException("Trace was null");

            window?.ScrollTo(low);
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