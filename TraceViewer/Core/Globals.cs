using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TraceViewer.Core
{
    public class Globals
    {
        public static readonly Dictionary<string, string[]> registerFamilies = new Dictionary<string, string[]>
        {
            { "raxx", new[] { "rax", "eax", "ax", "ah", "al" } },
            { "rbxx", new[] { "rbx", "ebx", "bx", "bh", "bl" } },
            { "rcxx", new[] { "rcx", "ecx", "cx", "ch", "cl" } },
            { "rdxx", new[] { "rdx", "edx", "dx", "dh", "dl" } },
            { "rspx", new[] { "rsp", "esp", "sp", "spl" } },
            { "rbpx", new[] { "rbp", "ebp", "bp", "bpl" } },
            { "rsix", new[] { "rsi", "esi", "si", "sil" } },
            { "rdix", new[] { "rdi", "edi", "di", "dil" } },
            { "r8x",  new[] { "r8", "r8d", "r8w", "r8b" } },
            { "r9x",  new[] { "r9", "r9d", "r9w", "r9b" } },
            { "r10x", new[] { "r10", "r10d", "r10w", "r10b" } },
            { "r11x", new[] { "r11", "r11d", "r11w", "r11b" } },
            { "r12x", new[] { "r12", "r12d", "r12w", "r12b" } },
            { "r13x", new[] { "r13", "r13d", "r13w", "r13b" } },
            { "r14x", new[] { "r14", "r14d", "r14w", "r14b" } },
            { "r15x", new[] { "r15", "r15d", "r15w", "r15b" } },
            { "ripx", new[] { "rip", "eip" } }
        };
    }
}
