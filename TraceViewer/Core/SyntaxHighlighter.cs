using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace TraceViewer.Core
{
    internal class SyntaxHighlighter
    {
        public static string[] jumps = { 
            "jmp", "je", "jne", "jz", "jnz", "jg", "jge", "jl", "jle",
            "ja", "jae", "jb", "jbe", "jo", "jno", "js", "jns", "jp",
            "jnp", "jcxz", "jecxz", "loop", "loope", "loopne" 
        };

        public static string[] moves = {
            "mov", "movabs", "movsx", "movsxd", "movzx",
            "lea", "xchg", "bswap",
            "cmova",  "cmovae", "cmovb",  "cmovbe",
            "cmovc",  "cmove",  "cmovg",  "cmovge",
            "cmovl",  "cmovle", "cmovna", "cmovnae",
            "cmovnb", "cmovnbe", "cmovnc", "cmovne",
            "cmovng", "cmovnge", "cmovnl", "cmovnle",
            "cmovno", "cmovnp", "cmovns", "cmovnz",
            "cmovo",  "cmovp",  "cmovpe", "cmovpo",
            "cmovs",  "cmovz", "setae", "setne"
        };


        public static string[] compares = {
            "cmp", "test", "cmps", "cmpsb", "cmpsw", "cmpsd", "cmpsq",
            "scas", "scasb", "scasw", "scasd", "scasq",
            "bt", "btc", "btr", "bts",
            "cmpxchg", "cmpxchg8b", "cmpxchg16b"

        };

        public static string[] arithmetic = {
            "add", "adc", "sub", "sbb", "mul", "imul", "div", "idiv",
            "inc", "dec", "neg", "cmp", "test",
            "and", "or", "xor", "not",
            "shl", "sal", "shr", "sar", "rol", "ror", "rcl", "rcr",
            "bsf", "bsr", "bt", "btc", "btr", "bts",
            "popcnt", "tzcnt", "lzcnt", "cmc", "stc", "cqo", "cwd"

        };


        public static string[] calls = {
            "call", "callf", "ret", "retf",
            "int", "into", "iret", "iretd", "iretq"
        };


        public static string[] stack = {
            "push", "pop", "pusha", "popa", "pushad", "popad",
            "pushf", "popf", "pushfq", "popfq",
            "enter", "leave"
        };

        public static string[] registers = { 
            "rax", "rbx", "rcx", "rdx", "rsi", "rdi", "rbp", "rsp", "rip", 
            "r8", "r9", "r10", "r11", "r12", "r13", "r14", "r15",
            "eax", "ebx", "ecx", "edx", "esi", "edi", "ax", "bx", "cx", 
            "dx", "si", "di", "bp", "sp", "ip", "r8d", "r9d", "r10d",
            "r11d", "r12d", "r13d", "r14d", "r15d", "ah", "bh", "ch", "dh",
            "al", "bl", "cl", "dl", "sil", "dil", "bpl", "spl",
            "r8b", "r9b", "r10b", "r11b", "r12b", "r13b", "r14b", "r15b",
            "esp", "ebp",
            "xmm0", "xmm1", "xmm2", "xmm3", "xmm4", "xmm5", "xmm6", "xmm7",
            "xmm8", "xmm9", "xmm10", "xmm11", "xmm12", "xmm13", "xmm14", "xmm15",
            "ymm0", "ymm1", "ymm2", "ymm3", "ymm4", "ymm5", "ymm6", "ymm7",
            "ymm8", "ymm9", "ymm10", "ymm11", "ymm12", "ymm13", "ymm14", "ymm15",
        };

        public static SolidColorBrush Check_Type(string instruction)
        {
            for (int i = 0; i < jumps.Length; i++)
            {
                if (jumps[i] == instruction)
                {
                    return Brushes.Red;
                }
            }
            for (int i = 0; i < moves.Length; i++)
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
}