using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TraceViewer.Core;
using static System.Net.Mime.MediaTypeNames;

namespace TraceViewer
{
    public class HighlightingCollection
    {
        public static string[] jumps = new string[] { "jmp", "je", "jne", "jz", "jnz", "jg", "jge", "jl", "jle",
                                                      "ja", "jae", "jb", "jbe", "jo", "jno", "js", "jns", "jp",
                                                      "jnp", "jcxz", "jecxz", "loop", "loope", "loopne" };

        public static string[] moves = new string[] { "mov", "lea", "xchg", "xlat", "xlatb" };

        public static string[] compares = new string[] { "cmp", "test" };

        public static string[] arithmetic = new string[] { "add", "sub", "mul", "imul", "div", "idiv", "inc", "dec", "neg", "adc", "sbb",
                                                           "and", "or", "xor", "not", "shl", "shr", "sal", "sar", "rcl", "rcr", "rol", "ror" };

        public static string[] calls = new string[] { "call", "callf" };

        public static string[] returns = new string[] { "ret", "retn", "retf" };

        public static string[] stack = new string[] { "push", "pop", "pushfq", "popfq" };

        public static string[] registers = new string[] { "rax", "rbx", "rcx", "rdx", "rsi", "rdi", "rbp", "rsp", "rip", "r8", "r9", "r10", "r11", "r12", "r13", "r14", "r15",
                                                          "eax", "ebx", "ecx", "edx", "esi", "edi", "ax", "bx", "cx", "dx", "si", "di", "bp", "sp", "ip", "r8d", "r9d", "r10d", 
                                                          "r11d", "r12d", "r13d", "r14d", "r15d", "ah", "bh", "ch", "dh", "al", "bl", "cl", "dl", "sil", "dil", "bpl", "spl", 
                                                          "r8b", "r9b", "r10b", "r11b", "r12b", "r13b", "r14b", "r15b" };
        
        public static SolidColorBrush Check_Type(string instruction)
        {
            for(int i = 0; i < jumps.Length; i++)
            {
                if (jumps[i] == instruction)
                {
                    return Brushes.Red;
                }
            }
            for(int i = 0; i < moves.Length; i++)
            {
                if (moves[i] == instruction)
                {
                    return Brushes.LightGreen;
                }
            }
            for (int i = 0; i < compares.Length; i++)
            {
                if (compares[i] == instruction)
                {
                    return Brushes.Yellow;
                }
            }
            for (int i = 0; i < arithmetic.Length; i++)
            {
                if (arithmetic[i] == instruction)
                {
                    return Brushes.LightBlue;
                }
            }
            for (int i = 0; i < calls.Length; i++)
            {
                if (calls[i] == instruction)
                {
                    return Brushes.Red;
                }
            }
            for (int i = 0; i < returns.Length; i++)
            {
                if (returns[i] == instruction)
                {
                    return Brushes.Orange;
                }
            }
            for (int i = 0; i < stack.Length; i++)
            {
                if (stack[i] == instruction)
                {
                    return Brushes.Turquoise;
                }
            }
            for (int i = 0; i < registers.Length; i++)
            {
                if (registers[i] == instruction)
                {
                    return Brushes.Coral;
                }
            }
            if (instruction.StartsWith("0x"))
            {
                return Brushes.DarkGoldenrod;
            }
            return Brushes.White;
            

        }
    }
    public partial class WPF_TraceRow : UserControl
    {
        public WPF_TraceRow()
        {
            InitializeComponent();
        }

        public WPF_TraceRow(TraceRow traceRow)
        {
            InitializeComponent();
            Set(traceRow);
        }

        public void Set(TraceRow traceRow)
        {

            id.Text = traceRow.Id.ToString();
            id.Foreground = Brushes.White;

            address.Text = "0x" + traceRow.Ip.ToString("X");
            address.Foreground = Brushes.White;


            string[] single_instructions = Regex.Split(traceRow.Disasm, @"([ ,:\[\]*])");

            foreach (string single_instruction in single_instructions)
            {
                disasm.Inlines.Add(new Run(single_instruction)
                {
                    Foreground = HighlightingCollection.Check_Type(single_instruction)
                });
            }

            if (traceRow.Regchanges != null)
            {

                string[] single_changes = Regex.Split(traceRow.Regchanges, @"([ :])");

                for (int i = 0; i < single_changes.Length; i++)
                {

                    if (i == 4 || i == 8)
                    {
                        single_changes[i] = "0x" + single_changes[i];
                    }
                        
                    changes.Inlines.Add(new Run(single_changes[i])
                    {
                        Foreground = HighlightingCollection.Check_Type(single_changes[i])
                    });
                }
            }
        }
    }
}
