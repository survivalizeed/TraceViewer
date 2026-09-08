using System.IO;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace TraceViewer.Core.Dumpulator
{
    public static class PythonHighlighting
    {
        private const string XshdDefinition = @"<?xml version=""1.0""?>
<SyntaxDefinition name=""PythonDumpulator"" extensions="".py"" xmlns=""http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008"">
    <Color name=""Comment"" foreground=""#6A9955"" exampleText=""# comment"" />
    <Color name=""String"" foreground=""#CE9178"" exampleText=""&quot;Hello, World!&quot;"" />
    <Color name=""Keywords"" foreground=""#C586C0"" fontWeight=""bold"" exampleText=""def"" />
    <Color name=""Builtins"" foreground=""#DCDCAA"" exampleText=""print"" />
    <Color name=""DumpulatorClass"" foreground=""#4EC9B0"" fontWeight=""bold"" exampleText=""Dumpulator"" />
    <Color name=""DumpulatorMethods"" foreground=""#569CD6"" fontWeight=""bold"" exampleText=""call"" />
    <Color name=""Registers"" foreground=""#9CDCFE"" exampleText=""rax"" />
    <Color name=""Numbers"" foreground=""#B5CEA8"" exampleText=""0x1234"" />
    <Color name=""TraceVariables"" foreground=""#FFD700"" fontWeight=""bold"" exampleText=""CURRENT_IP"" />

    <RuleSet>
        <!-- Single-line comments -->
        <Span color=""Comment"" begin=""#"" />

        <!-- Multiline docstrings / strings -->
        <Span color=""String"" multiline=""true"">
            <Begin>""""""</Begin>
            <End>""""""</End>
        </Span>
        <Span color=""String"" multiline=""true"">
            <Begin>''''''</Begin>
            <End>''''''</End>
        </Span>

        <!-- Regular strings -->
        <Span color=""String"">
            <Begin>""</Begin>
            <End>""</End>
            <RuleSet>
                <Span begin=""\\"" end=""."" />
            </RuleSet>
        </Span>
        <Span color=""String"">
            <Begin>'</Begin>
            <End>'</End>
            <RuleSet>
                <Span begin=""\\"" end=""."" />
            </RuleSet>
        </Span>

        <!-- TraceViewer Injected Variables -->
        <Keywords color=""TraceVariables"">
            <Word>DUMP_PATH</Word>
            <Word>CURRENT_IP</Word>
            <Word>CURRENT_ROW</Word>
            <Word>TRACE_REGS</Word>
        </Keywords>

        <!-- Dumpulator Core Classes & Objects -->
        <Keywords color=""DumpulatorClass"">
            <Word>Dumpulator</Word>
            <Word>ExceptionInfo</Word>
            <Word>ExceptionType</Word>
            <Word>MemoryViolation</Word>
            <Word>Registers</Word>
            <Word>MemoryManager</Word>
            <Word>ModuleManager</Word>
            <Word>HandleManager</Word>
            <Word>dp</Word>
        </Keywords>

        <!-- Dumpulator Methods & Properties -->
        <Keywords color=""DumpulatorMethods"">
            <Word>call</Word>
            <Word>read</Word>
            <Word>read_ptr</Word>
            <Word>read_byte</Word>
            <Word>read_char</Word>
            <Word>read_short</Word>
            <Word>read_ushort</Word>
            <Word>read_long</Word>
            <Word>read_ulong</Word>
            <Word>read_str</Word>
            <Word>write</Word>
            <Word>write_ptr</Word>
            <Word>write_byte</Word>
            <Word>write_char</Word>
            <Word>write_short</Word>
            <Word>write_ushort</Word>
            <Word>write_long</Word>
            <Word>write_ulong</Word>
            <Word>allocate</Word>
            <Word>load_dll</Word>
            <Word>map_module</Word>
            <Word>step</Word>
            <Word>start</Word>
            <Word>stop</Word>
            <Word>regs</Word>
            <Word>memory</Word>
            <Word>modules</Word>
            <Word>handles</Word>
            <Word>args</Word>
            <Word>syscalls</Word>
            <Word>print_memory</Word>
            <Word>set_exception_hook</Word>
            <Word>handle_exception</Word>
            <Word>NtCurrentProcess</Word>
            <Word>NtCurrentThread</Word>
        </Keywords>

        <!-- Python Keywords -->
        <Keywords color=""Keywords"">
            <Word>and</Word>
            <Word>as</Word>
            <Word>assert</Word>
            <Word>async</Word>
            <Word>await</Word>
            <Word>break</Word>
            <Word>class</Word>
            <Word>continue</Word>
            <Word>def</Word>
            <Word>del</Word>
            <Word>elif</Word>
            <Word>else</Word>
            <Word>except</Word>
            <Word>finally</Word>
            <Word>for</Word>
            <Word>from</Word>
            <Word>global</Word>
            <Word>if</Word>
            <Word>import</Word>
            <Word>in</Word>
            <Word>is</Word>
            <Word>lambda</Word>
            <Word>nonlocal</Word>
            <Word>not</Word>
            <Word>or</Word>
            <Word>pass</Word>
            <Word>raise</Word>
            <Word>return</Word>
            <Word>try</Word>
            <Word>while</Word>
            <Word>with</Word>
            <Word>yield</Word>
            <Word>None</Word>
            <Word>True</Word>
            <Word>False</Word>
        </Keywords>

        <!-- Python Builtins -->
        <Keywords color=""Builtins"">
            <Word>abs</Word>
            <Word>all</Word>
            <Word>any</Word>
            <Word>bin</Word>
            <Word>bool</Word>
            <Word>bytearray</Word>
            <Word>bytes</Word>
            <Word>chr</Word>
            <Word>dict</Word>
            <Word>dir</Word>
            <Word>divmod</Word>
            <Word>enumerate</Word>
            <Word>filter</Word>
            <Word>float</Word>
            <Word>format</Word>
            <Word>getattr</Word>
            <Word>hasattr</Word>
            <Word>hex</Word>
            <Word>id</Word>
            <Word>input</Word>
            <Word>int</Word>
            <Word>isinstance</Word>
            <Word>issubclass</Word>
            <Word>iter</Word>
            <Word>len</Word>
            <Word>list</Word>
            <Word>map</Word>
            <Word>max</Word>
            <Word>min</Word>
            <Word>next</Word>
            <Word>object</Word>
            <Word>oct</Word>
            <Word>open</Word>
            <Word>ord</Word>
            <Word>pow</Word>
            <Word>print</Word>
            <Word>range</Word>
            <Word>repr</Word>
            <Word>reversed</Word>
            <Word>round</Word>
            <Word>set</Word>
            <Word>setattr</Word>
            <Word>slice</Word>
            <Word>sorted</Word>
            <Word>str</Word>
            <Word>sum</Word>
            <Word>super</Word>
            <Word>tuple</Word>
            <Word>type</Word>
            <Word>vars</Word>
            <Word>zip</Word>
        </Keywords>

        <!-- Common x86/x64 Registers and Flags -->
        <Keywords color=""Registers"">
            <Word>rax</Word>
            <Word>rbx</Word>
            <Word>rcx</Word>
            <Word>rdx</Word>
            <Word>rsi</Word>
            <Word>rdi</Word>
            <Word>rbp</Word>
            <Word>rsp</Word>
            <Word>r8</Word>
            <Word>r9</Word>
            <Word>r10</Word>
            <Word>r11</Word>
            <Word>r12</Word>
            <Word>r13</Word>
            <Word>r14</Word>
            <Word>r15</Word>
            <Word>rip</Word>
            <Word>rflags</Word>
            <Word>eflags</Word>
            <Word>eax</Word>
            <Word>ebx</Word>
            <Word>ecx</Word>
            <Word>edx</Word>
            <Word>esi</Word>
            <Word>edi</Word>
            <Word>ebp</Word>
            <Word>esp</Word>
            <Word>zf</Word>
            <Word>cf</Word>
            <Word>sf</Word>
            <Word>of</Word>
            <Word>df</Word>
            <Word>af</Word>
            <Word>pf</Word>
            <Word>xmm0</Word>
            <Word>xmm1</Word>
            <Word>xmm2</Word>
            <Word>xmm3</Word>
            <Word>xmm4</Word>
            <Word>xmm5</Word>
            <Word>xmm6</Word>
            <Word>xmm7</Word>
            <Word>xmm8</Word>
            <Word>xmm9</Word>
            <Word>xmm10</Word>
            <Word>xmm11</Word>
            <Word>xmm12</Word>
            <Word>xmm13</Word>
            <Word>xmm14</Word>
            <Word>xmm15</Word>
            <Word>ymm0</Word>
            <Word>ymm1</Word>
            <Word>ymm2</Word>
            <Word>ymm3</Word>
            <Word>ymm4</Word>
            <Word>ymm5</Word>
            <Word>ymm6</Word>
            <Word>ymm7</Word>
            <Word>ymm8</Word>
            <Word>ymm9</Word>
            <Word>ymm10</Word>
            <Word>ymm11</Word>
            <Word>ymm12</Word>
            <Word>ymm13</Word>
            <Word>ymm14</Word>
            <Word>ymm15</Word>
        </Keywords>

        <!-- Numbers: Hex, binary, float, decimal -->
        <Rule color=""Numbers"">
            \b0[xX][0-9a-fA-F]+[lL]?\b|
            \b0[bB][01]+[lL]?\b|
            \b\d+(\.[0-9]+)?([eE][+-]?[0-9]+)?\b
        </Rule>
    </RuleSet>
</SyntaxDefinition>";

        private static IHighlightingDefinition? _cachedDefinition;

        public static IHighlightingDefinition GetDefinition()
        {
            if (_cachedDefinition != null) return _cachedDefinition;

            using var stringReader = new StringReader(XshdDefinition);
            using var xmlReader = XmlReader.Create(stringReader);
            _cachedDefinition = HighlightingLoader.Load(xmlReader, HighlightingManager.Instance);
            return _cachedDefinition;
        }
    }
}
