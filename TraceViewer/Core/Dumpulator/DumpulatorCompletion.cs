using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;

namespace TraceViewer.Core.Dumpulator
{
    public class DumpulatorCompletionItem : ICompletionData
    {
        public string Text { get; }
        public object Content { get; }
        public object Description { get; }
        public double Priority { get; }
        public ImageSource? Image => null;
        public string Category { get; }

        public DumpulatorCompletionItem(string text, string displayText, string description, string category, double priority = 0)
        {
            Text = text;
            Category = category;
            Priority = priority;
            Content = CreateVisualContent(displayText, category);
            Description = CreateVisualDescription(displayText, description, category);
        }

        private static object CreateVisualContent(string displayText, string category)
        {
            var panel = new DockPanel { LastChildFill = true };

            string badgeText = category switch
            {
                "Dumpulator" => "dp",
                "Register" => "reg",
                "Flag" => "flg",
                "Vector" => "vec",
                "Trace Context" => "var",
                "Builtin" => "py",
                "Keyword" => "kw",
                "Import" => "mod",
                _ => "id"
            };

            Color badgeColor = category switch
            {
                "Dumpulator" => Color.FromRgb(78, 201, 176),   // Cyan
                "Register" => Color.FromRgb(156, 220, 254),    // Light Blue
                "Flag" => Color.FromRgb(197, 134, 192),        // Purple
                "Vector" => Color.FromRgb(224, 108, 117),      // Coral
                "Trace Context" => Color.FromRgb(255, 215, 0), // Gold
                "Builtin" => Color.FromRgb(220, 220, 170),     // Light Yellow
                "Keyword" => Color.FromRgb(197, 134, 192),     // Purple
                _ => Color.FromRgb(180, 180, 180)
            };

            var badgeBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, badgeColor.R, badgeColor.G, badgeColor.B)),
                BorderBrush = new SolidColorBrush(badgeColor),
                BorderThickness = new Thickness(0.5),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(3, 0, 3, 1),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(badgeBorder, Dock.Left);

            var badgeBlock = new TextBlock
            {
                Text = badgeText,
                Foreground = new SolidColorBrush(badgeColor),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                FontWeight = FontWeights.Bold
            };
            badgeBorder.Child = badgeBlock;
            panel.Children.Add(badgeBorder);

            var textBlock = new TextBlock
            {
                Text = displayText,
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            panel.Children.Add(textBlock);

            return panel;
        }

        private static object CreateVisualDescription(string displayText, string description, string category)
        {
            var container = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(18, 18, 18)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(65, 65, 65)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(10, 8, 10, 8),
                MaxWidth = 420
            };

            var stack = new StackPanel();

            var headerDock = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 6) };
            var catTag = new TextBlock
            {
                Text = category.ToUpper(),
                Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120)),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(catTag, Dock.Right);
            headerDock.Children.Add(catTag);

            var titleBlock = new TextBlock
            {
                Text = displayText,
                Foreground = new SolidColorBrush(Color.FromRgb(78, 201, 176)),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerDock.Children.Add(titleBlock);
            stack.Children.Add(headerDock);

            stack.Children.Add(new Rectangle
            {
                Height = 1,
                Fill = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
                Margin = new Thickness(0, 0, 0, 6)
            });

            var descBlock = new TextBlock
            {
                Text = description,
                Foreground = new SolidColorBrush(Color.FromRgb(215, 215, 215)),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 17
            };
            stack.Children.Add(descBlock);

            container.Child = stack;
            return container;
        }

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            textArea.Document.Replace(completionSegment, Text);
            if (Text.EndsWith("()"))
            {
                textArea.Caret.Offset = completionSegment.Offset + Text.Length - 1;
            }
        }
    }

    public static class DumpulatorCompletionProvider
    {
        private static readonly List<DumpulatorCompletionItem> DumpulatorMethods =
        [
            new("call()", "call(addr, args=[])", "Emulates function execution starting at addr until it returns.\nargs: list of integer or pointer arguments.\nReturns: Return value (RAX)", "Dumpulator", 10),
            new("read()", "read(addr, size)", "Read 'size' bytes from memory at 'addr' as bytes.", "Dumpulator", 9),
            new("read_ptr()", "read_ptr(addr)", "Read a pointer-sized integer (4 or 8 bytes depending on architecture) from memory.", "Dumpulator", 9),
            new("read_byte()", "read_byte(addr)", "Read an 8-bit unsigned byte from memory.", "Dumpulator", 8),
            new("read_short()", "read_short(addr)", "Read a 16-bit signed integer from memory.", "Dumpulator", 8),
            new("read_ushort()", "read_ushort(addr)", "Read a 16-bit unsigned integer from memory.", "Dumpulator", 8),
            new("read_long()", "read_long(addr)", "Read a 32-bit signed integer from memory.", "Dumpulator", 8),
            new("read_ulong()", "read_ulong(addr)", "Read a 32-bit unsigned integer from memory.", "Dumpulator", 8),
            new("read_str()", "read_str(addr, max_chars=256)", "Read a null-terminated ASCII string from memory.", "Dumpulator", 8),
            new("write()", "write(addr, data)", "Write bytes or bytearray to memory at 'addr'.", "Dumpulator", 9),
            new("write_ptr()", "write_ptr(addr, val)", "Write a pointer-sized integer to memory.", "Dumpulator", 9),
            new("write_byte()", "write_byte(addr, val)", "Write an 8-bit byte to memory.", "Dumpulator", 8),
            new("write_short()", "write_short(addr, val)", "Write a 16-bit signed integer to memory.", "Dumpulator", 8),
            new("write_ushort()", "write_ushort(addr, val)", "Write a 16-bit unsigned integer to memory.", "Dumpulator", 8),
            new("write_long()", "write_long(addr, val)", "Write a 32-bit signed integer to memory.", "Dumpulator", 8),
            new("write_ulong()", "write_ulong(addr, val)", "Write a 32-bit unsigned integer to memory.", "Dumpulator", 8),
            new("allocate()", "allocate(size)", "Allocate executable/read-write memory pages in the emulator space.\nReturns: Allocated memory address", "Dumpulator", 9),
            new("load_dll()", "load_dll(path)", "Map and load a DLL module into the emulator process space.", "Dumpulator", 8),
            new("map_module()", "map_module(path)", "Map a PE binary module into memory.", "Dumpulator", 8),
            new("step()", "step()", "Single-step one machine instruction in the emulator.", "Dumpulator", 8),
            new("start()", "start(entry_point, end_addr=0)", "Start emulation from entry_point until end_addr is reached.", "Dumpulator", 8),
            new("stop()", "stop()", "Stop the current emulation loop.", "Dumpulator", 8),
            new("regs", "regs", "Access CPU registers object (e.g. dp.regs.rax = 0x123).", "Dumpulator", 10),
            new("memory", "memory", "MemoryManager instance controlling page allocations and mappings.", "Dumpulator", 7),
            new("modules", "modules", "ModuleManager instance containing loaded module headers and exports.", "Dumpulator", 7),
            new("handles", "handles", "HandleManager instance simulating Windows kernel handles.", "Dumpulator", 6),
            new("args", "args", "Function calling convention arguments helper.", "Dumpulator", 6),
            new("syscalls", "syscalls", "List of registered syscall hooks.", "Dumpulator", 6),
            new("print_memory()", "print_memory()", "Print the emulator's memory mapping layout to standard output.", "Dumpulator", 6),
            new("set_exception_hook()", "set_exception_hook(hook)", "Register custom callback function for unhandled exceptions.", "Dumpulator", 6),
            new("NtCurrentProcess", "NtCurrentProcess", "Pseudo handle for current process (-1).", "Dumpulator", 5),
            new("NtCurrentThread", "NtCurrentThread", "Pseudo handle for current thread (-2).", "Dumpulator", 5)
        ];

        private static readonly List<DumpulatorCompletionItem> Registers =
        [
            new("rax", "rax", "64-bit Accumulator Register", "Register", 8),
            new("rbx", "rbx", "64-bit Base Register", "Register", 8),
            new("rcx", "rcx", "64-bit Counter Register / 1st function arg", "Register", 8),
            new("rdx", "rdx", "64-bit Data Register / 2nd function arg", "Register", 8),
            new("rsi", "rsi", "64-bit Source Index Register", "Register", 8),
            new("rdi", "rdi", "64-bit Destination Index Register", "Register", 8),
            new("rsp", "rsp", "64-bit Stack Pointer Register", "Register", 8),
            new("rbp", "rbp", "64-bit Base/Frame Pointer Register", "Register", 8),
            new("r8", "r8", "64-bit General Purpose Register 8 / 3rd arg", "Register", 8),
            new("r9", "r9", "64-bit General Purpose Register 9 / 4th arg", "Register", 8),
            new("r10", "r10", "64-bit General Purpose Register 10", "Register", 8),
            new("r11", "r11", "64-bit General Purpose Register 11", "Register", 8),
            new("r12", "r12", "64-bit General Purpose Register 12", "Register", 8),
            new("r13", "r13", "64-bit General Purpose Register 13", "Register", 8),
            new("r14", "r14", "64-bit General Purpose Register 14", "Register", 8),
            new("r15", "r15", "64-bit General Purpose Register 15", "Register", 8),
            new("rip", "rip", "64-bit Instruction Pointer (EIP/RIP)", "Register", 9),
            new("rflags", "rflags", "64-bit Flags Register", "Register", 7),
            new("eflags", "eflags", "32-bit Flags Register", "Register", 7),
            new("zf", "zf", "Zero Flag (0 or 1)", "Flag", 6),
            new("cf", "cf", "Carry Flag (0 or 1)", "Flag", 6),
            new("sf", "sf", "Sign Flag (0 or 1)", "Flag", 6),
            new("of", "of", "Overflow Flag (0 or 1)", "Flag", 6),
            new("df", "df", "Direction Flag (0 or 1)", "Flag", 6),
            new("eax", "eax", "32-bit Accumulator Register", "Register", 6),
            new("ebx", "ebx", "32-bit Base Register", "Register", 6),
            new("ecx", "ecx", "32-bit Counter Register", "Register", 6),
            new("edx", "edx", "32-bit Data Register", "Register", 6),
            new("esi", "esi", "32-bit Source Index Register", "Register", 6),
            new("edi", "edi", "32-bit Destination Index Register", "Register", 6),
            new("esp", "esp", "32-bit Stack Pointer Register", "Register", 6),
            new("ebp", "ebp", "32-bit Base Pointer Register", "Register", 6),
            new("xmm0", "xmm0", "128-bit Vector/Float Register 0", "Vector", 5),
            new("xmm1", "xmm1", "128-bit Vector/Float Register 1", "Vector", 5),
            new("xmm2", "xmm2", "128-bit Vector/Float Register 2", "Vector", 5),
            new("xmm3", "xmm3", "128-bit Vector/Float Register 3", "Vector", 5),
            new("ymm0", "ymm0", "256-bit Vector Register 0", "Vector", 5),
            new("ymm1", "ymm1", "256-bit Vector Register 1", "Vector", 5)
        ];

        private static readonly List<DumpulatorCompletionItem> TraceVariables =
        [
            new("DUMP_PATH", "DUMP_PATH", "Injected path to the active minidump file (.dmp)", "Trace Context", 10),
            new("CURRENT_IP", "CURRENT_IP", "Injected current instruction pointer address (e.g. 0x140001000)", "Trace Context", 10),
            new("CURRENT_ROW", "CURRENT_ROW", "Injected current trace row index in TraceViewer", "Trace Context", 9),
            new("TRACE_REGS", "TRACE_REGS", "Injected dictionary of register values at current trace row:\n{'rax': 0x..., 'rcx': 0x...}", "Trace Context", 9)
        ];

        private static readonly List<DumpulatorCompletionItem> PythonKeywordsAndBuiltins =
        [
            new("from dumpulator import Dumpulator", "from dumpulator import Dumpulator", "Import Dumpulator class", "Import", 10),
            new("Dumpulator()", "Dumpulator(DUMP_PATH)", "Create Dumpulator emulator instance", "Dumpulator", 10),
            new("def ", "def", "Define function", "Keyword", 5),
            new("class ", "class", "Define class", "Keyword", 5),
            new("return ", "return", "Return statement", "Keyword", 5),
            new("import ", "import", "Import module", "Keyword", 5),
            new("from ", "from", "From import statement", "Keyword", 5),
            new("if ", "if", "If condition", "Keyword", 5),
            new("elif ", "elif", "Else if condition", "Keyword", 5),
            new("else:", "else:", "Else clause", "Keyword", 5),
            new("for ", "for", "For loop", "Keyword", 5),
            new("in ", "in", "In membership operator", "Keyword", 5),
            new("while ", "while", "While loop", "Keyword", 5),
            new("try:", "try:", "Try block", "Keyword", 5),
            new("except ", "except", "Except clause", "Keyword", 5),
            new("finally:", "finally:", "Finally block", "Keyword", 5),
            new("with ", "with", "Context manager statement", "Keyword", 5),
            new("as ", "as", "As alias operator", "Keyword", 5),
            new("print()", "print()", "Print output to console", "Builtin", 6),
            new("hex()", "hex()", "Convert integer to hexadecimal string", "Builtin", 6),
            new("len()", "len()", "Return length of sequence", "Builtin", 5),
            new("range()", "range()", "Generate range of integers", "Builtin", 5),
            new("isinstance()", "isinstance()", "Check instance type", "Builtin", 5),
            new("None", "None", "Python None object", "Keyword", 5),
            new("True", "True", "Boolean True", "Keyword", 5),
            new("False", "False", "Boolean False", "Keyword", 5)
        ];

        public static IEnumerable<ICompletionData> GetSuggestions(string lineText, int caretColumn)
        {
            string prefix = lineText.Substring(0, Math.Min(caretColumn, lineText.Length));

            // Case 1: Typing after "dp.regs." or "regs."
            if (prefix.EndsWith("dp.regs.", StringComparison.OrdinalIgnoreCase) ||
                prefix.EndsWith(".regs.", StringComparison.OrdinalIgnoreCase))
            {
                return Registers;
            }

            // Case 2: Typing after "dp."
            if (prefix.EndsWith("dp.", StringComparison.OrdinalIgnoreCase))
            {
                return DumpulatorMethods;
            }

            // Case 3: Partial identifier after "dp." (e.g. "dp.ca")
            int lastDot = prefix.LastIndexOf('.');
            if (lastDot >= 0)
            {
                string beforeDot = prefix.Substring(0, lastDot);
                string afterDot = prefix.Substring(lastDot + 1);

                if (beforeDot.EndsWith("dp.regs", StringComparison.OrdinalIgnoreCase) ||
                    beforeDot.EndsWith("regs", StringComparison.OrdinalIgnoreCase))
                {
                    return Registers.Where(r => r.Text.StartsWith(afterDot, StringComparison.OrdinalIgnoreCase));
                }

                if (beforeDot.EndsWith("dp", StringComparison.OrdinalIgnoreCase))
                {
                    return DumpulatorMethods.Where(m => m.Text.StartsWith(afterDot, StringComparison.OrdinalIgnoreCase));
                }
            }

            // Case 4: General word typing -> Extract last word token
            int wordStart = prefix.Length - 1;
            while (wordStart >= 0 && (char.IsLetterOrDigit(prefix[wordStart]) || prefix[wordStart] == '_'))
            {
                wordStart--;
            }
            string currentWord = prefix.Substring(wordStart + 1);

            var all = TraceVariables
                .Concat(DumpulatorMethods)
                .Concat(Registers)
                .Concat(PythonKeywordsAndBuiltins);

            if (string.IsNullOrEmpty(currentWord))
            {
                return all;
            }

            return all.Where(item => item.Text.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase) ||
                                     item.Text.Contains(currentWord, StringComparison.OrdinalIgnoreCase));
        }
    }
}
