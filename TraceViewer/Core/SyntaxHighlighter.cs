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
        public static string[] StackInstructions = new string[] {
        "PUSH", "POP", "PUSHA", "POPA", "PUSHAD", "POPAD", "PUSHF", "POPF", "PUSHFD", "POPFD", "PUSHFQ", "POPFQ", "ENTER", "LEAVE", "CBW", "CWDE", "CDQ", "CQO", "CWD", "CWDE", "CDQE"
    };

        public static string[] CallInstructions = new string[] {
        "CALL", "RET", "RETF", "RETN", "SYSENTER", "SYSEXIT", "SYSCALL", "SYSRET", "IRET", "IRETD", "IRETQ"
    };

        public static string[] JumpInstructions = new string[] {
        "JMP",
        "JA", "JAE", "JB", "JBE", "JC", "JCC", "JE", "JEQ", "JG", "JGE", "JL", "JLE", "JNA", "JNAE", "JNB", "JNBE", "JNC", "JNE", "JNG", "JNGE", "JNL", "JNLE", "JNO", "JNP", "JNS", "JNZ", "JO", "JP", "JPE", "JPO", "JS", "JZ",
        "LOOP", "LOOPcc", "XBEGIN", "XEND", "XABORT", "BNDCL", "BNDCN", "BNDCU", "BNDLDX", "BNDSTX"
    };

        public static string[] CompareInstructions = new string[] {
        "CMP", "CMPPD", "CMPPS", "CMPS", "CMPSB", "CMPSD",  "CMPSQ", "CMPSS", "CMPSW", "TEST", "COMISD", "COMISS", "UCOMISD", "UCOMISS",
        "KORTESTB", "KORTESTD", "KORTESTQ", "KORTESTW", "KTESTB", "KTESTD", "KTESTQ", "KTESTW", "VPTESTMB", "VPTESTMD", "VPTESTMQ", "VPTESTMW", "VPTESTNMB", "VPTESTNMD", "VPTESTNMQ", "VPTESTNMW", "VTESTPD", "VTESTPS", "SETcc", "CMPXCHG", "CMPXCHG8B", "CMPXCHG16B",
        "FCOM", "FCOMI", "FCOMIP", "FCOMP", "FCOMPP", "FTST", "FUCOM", "FUCOMI", "FUCOMIP", "FUCOMP", "FUCOMPP",
        "VRANGEPD", "VRANGEPS", "VRANGESD", "VRANGESS", "VFPCLASSPD", "VFPCLASSPS", "VFPCLASSSD", "VFPCLASSSS", "VPCMPB", "VPCMPD", "VPCMPQ", "VPCMPUB", "VPCMPUD", "VPCMPUQ", "VPCMPUW", "VPCMPW", "ARPL", "BOUND", "LAR", "LSL", "VERR", "VERW", "STR", "LLDT", "SLDT", "SGDT", "LGDT", "LIDT", "SIDT", "SMSW", "LTR", "LES", "LFS", "LGS", "PTWRITE" // CMC - Complement Carry Flag, related to conditional operations, und System-Level Compare-ähnliche Befehle, PTWRITE - writes data based on predicate (comparison result)
    };

        public static string[] MoveInstructions = new string[] {
        "MOV", "MOV", "MOV", "MOVAPD", "MOVAPS", "MOVBE", "MOVD", "MOVDDUP", "MOVDQ2Q", "MOVDQA", "MOVDQU", "MOVHLPS", "MOVHPD", "MOVHPS", "MOVLHPS", "MOVLPD", "MOVLPS", "MOVMSKPD", "MOVMSKPS", "MOVNTDQ", "MOVNTDQA", "MOVNTI", "MOVNTPD", "MOVNTPS", "MOVNTQ", "MOVQ", "MOVQ", "MOVQ2DQ", "MOVSX", "MOVSXD", "MOVUPD", "MOVUPS", "MOVZX", "CMOVcc",
        "FCMOVcc", "FLD", "FILD", "FLD1", "FLDCW", "FLDENV", "FLDL2E", "FLDL2T", "FLDLG2", "FLDLN2", "FLDPI", "FLDZ", "FBLD", "FBSTP", "FIST", "FISTP", "FISTTP", "FST", "FSTCW", "FSTENV", "FSTP", "FNSTCW", "FNSTENV", "FNSTSW", "FNSAVE", "FSAVE", "FXRSTOR", "FRSTOR", "FXSAVE", "FSAVE",
        "LDDQU", "LDS", "LEA", "LES", "LFENCE", "LFS", "LGDT", "LGS", "LIDT", "LLDT", "LMSW", "LSL", "LSS", "LTR", "MASKMOVDQU", "MASKMOVQ", "MOVB", "MOVBW", "MOVDQA32", "MOVDQA64", "MOVDQU16", "MOVDQU32", "MOVDQU64", "MOVDQU8", "MOVFSD", "MOVSS", "MOVP", "MOVPD", "MOVPS", "MOVSB", "MOVSD",  "MOVSHDUP", "MOVSLDUP", "MOVSQ",  "MOVSW", "MOVSX", "MOVSXD", "MOVUPD", "MOVUPS", "MOVZX",
        "XLAT", "XLATB", "LAHF", "SAHF", "XCHG", "MOV", "CMOVAE", "MOVABS", "CMOVBE", "SETNE", "SETAE",
        "VINSERTF128", "VINSERTF32x4", "VINSERTF32x8", "VINSERTF64x2", "VINSERTF64x4", "VINSERTI128", "VINSERTI32x4", "VINSERTI32x8", "VINSERTI64x2", "VINSERTI64x4", "VEXTRACTF128", "VEXTRACTF32x4", "VEXTRACTF32x8", "VEXTRACTF64x2", "VEXTRACTF64x4", "VEXTRACTI128", "VEXTRACTI32x4", "VEXTRACTI32x8", "VEXTRACTI64x2", "VEXTRACTI64x4",
        "VMOVDQA", "VMOVDQA32", "VMOVDQA64", "VMOVDQU", "VMOVDQU16", "VMOVDQU32", "VMOVDQU64", "VMOVDQU8", "VMASKMOV", "VPMOVB2M", "VPMOVD2M", "VPMOVDB", "VPMOVDW", "VPMOVM2B", "VPMOVM2D", "VPMOVM2Q", "VPMOVM2W", "VPMOVQ2M", "VPMOVQB", "VPMOVQD", "VPMOVQW", "VPMOVSDB", "VPMOVSDW", "VPMOVSQB", "VPMOVSQD", "VPMOVSQW", "VPMOVSWB", "VPMOVUSDB", "VPMOVUSDW", "VPMOVUSQB", "VPMOVUSQD", "VPMOVUSQW", "VPMOVUSWB", "VPMOVW2M", "VPMOVWB", "VMASKMOV", "BNDMOV",
        "VLDDQU", "VALIGND", "VALIGNQ", "VBROADCAST", "VBROADCASTB", "VPBROADCAST", "VPBROADCASTB", "VPBROADCASTD", "VPBROADCASTM", "VPBROADCASTQ", "VPBROADCASTW", "MOVMSKPD", "MOVMSKPS", "PMOVMSKB", "KMOVB", "KMOVD", "KMOVQ", "KMOVW", "PINSRB", "PINSRD", "PINSRQ", "PINSRW", "MOVBW", "MOVNTDQ", "MOVNTDQA", "MOVNTI", "MOVNTPD", "MOVNTPS", "MOVNTQ", "LDMXCSR", "STMXCSR", "MOVSD", "MOVSQ" // Include various forms of move like instructions, Streaming-Writes und MXCSR Load/Store, MOVSD/MOVSQ included twice to differentiate scalar and string moves (even if names are the same - context matters)
    };

        public static string[] ArithmeticInstructions = new string[] {
        "AAA", "AAD", "AAM", "AAS", "ADC", "ADCX", "ADD", "ADDPD", "ADDPS", "ADDSD", "ADDSS", "ADDSUBPD", "ADDSUBPS", "ADOX",
        "DAA", "DAS", "DEC", "DIV", "DIVPD", "DIVPS", "DIVSD", "DIVSS", "DPPD", "DPPS",
        "F2XM1", "FABS", "FADD", "FADDP", "FDIV", "FDIVP", "FDIVR", "FDIVRP", "FIADD", "FIDIV", "FIDIVR", "FIMUL", "FINCSTP",
        "FISUB", "FISUBR", "FMUL", "FMULP", "FSUB", "FSUBP", "FSUBR", "FSUBRP", "FSCALE", "FSIN", "FSINCOS", "FSQRT", "FYL2X", "FYL2XP1", "FPREM", "FPREM1", "FPATAN",
        "HADDPD", "HADDPS", "HSUBPD", "HSUBPS", "IDIV", "IMUL", "INC", "MUL", "MULPD", "MULPS", "MULSD", "MULSS", "MULX", "NEG", "SBB", "SUB", "SUBPD", "SUBPS", "SUBSD", "SUBSS", "XADD", "ADCX", "ADOX",
        "VFMADD132PD", "VFMADD132PS", "VFMADD132SD", "VFMADD132SS", "VFMADD213PD", "VFMADD213PS", "VFMADD213SD", "VFMADD213SS", "VFMADD231PD", "VFMADD231PS", "VFMADD231SD", "VFMADD231SS",
        "VFMADDSUB132PD", "VFMADDSUB132PS", "VFMADDSUB213PD", "VFMADDSUB213PS", "VFMADDSUB231PD", "VFMADDSUB231PS",
        "VFMSUB132PD", "VFMSUB132PS", "VFMSUB132SD", "VFMSUB132SS", "VFMSUB213PD", "VFMSUB213PS", "VFMSUB213SD", "VFMSUB213SS", "VFMSUB231PD", "VFMSUB231PS", "VFMSUB231SD", "VFMSUB231SS",
        "VFMSUBADD132PD", "VFMSUBADD132PS", "VFMSUBADD213PD", "VFMSUBADD213PS", "VFMSUBADD231PD", "VFMSUBADD231PS",
        "VFNMADD132PD", "VFNMADD132PS", "VFNMADD132SD", "VFNMADD132SS", "VFNMADD213PD", "VFNMADD213PS", "VFNMADD213SD", "VFNMADD213SS", "VFNMADD231PD", "VFNMADD231PS", "VFNMADD231SD", "VFNMADD231SS",
        "VFNMSUB132PD", "VFNMSUB132PS", "VFNMSUB132SD", "VFNMSUB132SS", "VFNMSUB213PD", "VFNMSUB213PS", "VFNMSUB213SD", "VFNMSUB213SS", "VFNMSUB231PD", "VFNMSUB231PS", "VFNMSUB231SD", "VFNMSUB231SS",
        "PHADDD", "PHADDSW", "PHADDW", "PHSUBD", "PHSUBSW", "PHSUBW", "PMADDUBSW", "PMADDWD", "PMULDQ", "PMULHRSW", "PMULHUW", "PMULHW", "PMULLD", "PMULLQ", "PMULLW", "PMULUDQ",
        "PABSB", "PABSD", "PABSQ", "PABSW", "MAXPD", "MAXPS", "MAXSD", "MAXSS", "MINPD", "MINPS", "MINSD", "MINSS", "PMAXSB", "PMAXSD", "PMAXSQ", "PMAXSW", "PMAXUB", "PMAXUD", "PMAXUQ", "PMAXUW", "PMINSB", "PMINSD", "PMINSQ", "PMINSW", "PMINUB", "PMINUD", "PMINUQ", "PMINUW", "PSADBW", "MPSADBW", "MULX", "TZCNT", "LZCNT", "POPCNT", "CRC32", "PCLMULQDQ", "ADC", "ADD", "DEC", "DIV", "IDIV", "IMUL", "INC", "MUL", "NEG", "SBB", "SUB", "FCHS", "FDECSTP", "FINCSTP", "FRNDINT", "F2XM1", "FABS", "FCOS", "FRNDINT", "FSQRT", "FTST", "FXAM", "FYL2X", "FYL2XP1", "FPREM", "FPREM1", "FPTAN", "FDIV", "FDIVP", "FDIVR", "FDIVRP", "FIADD", "FICOM", "FICOMP", "FIDIV", "FIDIVR", "FIMUL", "FISUB", "FISUBR", "FADD", "FADDP", "FMUL", "FMULP", "FSUB", "FSUBP", "FSUBR", "FSUBRP", "FFREE" // Added basic arithmetic again for completeness, and extensive FPU arithmetic functions, including more transcendental and complex FPU ops.
    };

        public static string[] BitwiseLogicalInstructions = new string[] {
        "AND", "ANDN", "ANDNPD", "ANDNPS", "ANDPD", "ANDPS", "NOT", "OR", "ORPD", "ORPS", "XOR", "XORPD", "XORPS", "XOR", // Added XOR again, basic logical op
        "BLENDPD", "BLENDPS", "BLENDVPD", "BLENDVPS", "PBLENDVB", "PBLENDW", "PALIGNR", "PAND", "PANDN", "PAVG", "PAVGB", "PAVGW",
        "PXOR", "VPBLENDD", "VPBLENDMD", "VPBLENDMQ", "VPBLENDMW", "VBLENDMPD", "VBLENDMPS", "VPBLENDMB",
        "KANDB", "KANDD", "KANDNB", "KANDND", "KANDNQ", "KANDNW", "KANDQ", "KANDW", "KNOTB", "KNOTD", "KNOTQ", "KNOTW", "KORB", "KORD", "KORQ", "KORW", "KXNORB", "KXNORD", "KXNORQ", "KXNORW", "KXORB", "KXORD", "KXORQ", "KXORW",
        "ARPL", "BEXTR", "BLSI", "BLSMSK", "BLSR", "BSF", "BSR", "BSWAP", "BT", "BTC", "BTR", "BTS", "BZHI",
        "ROL", "ROR", "RCL", "RCR", "SAR", "SAL", "SHL", "SHR", "SARX", "SHLX", "SHRX", "SHRD", "SHLD", "RORX",
        "PDEP", "PEXT", "PEXTRB", "PEXTRD", "PEXTRQ", "PEXTRW",
        "PSLLD", "PSLLDQ", "PSLLQ", "PSLLW", "PSRAD", "PSRAQ", "PSRAW", "PSRLD", "PSRLDQ", "PSRLQ", "PSRLW", "PSHUFB", "PSHUFD", "PSHUFHW", "PSHUFLW", "PSHUFW",
        "PROLD", "PROLQ", "PROLVD", "PROLVQ", "PRORD", "PRORQ", "PRORVD", "PRORVQ",
        "KSHIFTLB", "KSHIFTLD", "KSHIFTLQ", "KSHIFTLW", "KSHIFTRB", "KSHIFTRD", "KSHIFTRQ", "KSHIFTRW", "KUNPCKBW", "KUNPCKDQ", "KUNPCKWD", "KUNPCKHWD", "PUNPCKHBW", "PUNPCKHDQ", "PUNPCKHQDQ", "PUNPCKHWD", "PUNPCKLBW", "PUNPCKLDQ", "PUNPCKLQDQ", "PUNPCKLWD",
        "VPSLLVD", "VPSLLVQ", "VPSLLVW", "VPSRAVD", "VPSRAVQ", "VPSRAVW", "VPSRLVD", "VPSRLVQ", "VPSRLVW",
         "VPLZCNTD", "VPLZCNTQ", "CMC", "CLC",
         "VPTERNLOGD", "VPTERNLOGQ", "AND", "NOT", "OR", "XOR", "BSF", "BSR", "BT", "BTC", "BTR", "BTS", "LZCNT", "POPCNT", "BZHI", "ANDN", "KANDB", "KANDD", "KANDNB", "KANDND", "KANDNQ", "KANDNW", "KANDQ", "KANDW", "KNOTB", "KNOTD", "KNOTQ", "KNOTW", "KORB", "KORD", "KORQ", "KORW", "KXNORB", "KXNORD", "KXNORQ", "KXNORW", "KXORB", "KXORD", "KXORQ", "KXORW", "PAND", "PANDN", "POR", "PXOR", "STC" // added basic logical and bit manipulation instructions again, and extended bit manipulation and some vector logical instructions.
    };

        public static string[] SIMDInstructions = new string[] {
        "AESDEC", "AESDECLAST", "AESENC", "AESENCLAST", "AESIMC", "AESKEYGENASSIST", "AESDEC", "AESDECLAST", "AESENC", "AESENCLAST", "AESIMC", "AESKEYGENASSIST",
        "CLFLUSH", "CLFLUSHOPT", "CLWB", "EMMS",
        "EXTRACTPS", "INSERTPS",
        "MASKMOVDQU", "MASKMOVQ",
        "MMX", "SSE", "SSE2", "SSE3", "SSSE3", "SSE4.1", "SSE4.2", "AVX", "AVX2", "AVX-512",
        "PACKSSDW", "PACKSSWB", "PACKUSDW", "PACKUSWB", "PADDB", "PADDD", "PADDQ", "PADDSB", "PADDSW", "PADDUSB", "PADDUSW", "PADDW",
        "PCMPEQB", "PCMPEQD", "PCMPEQQ", "PCMPEQW", "PCMPESTRI", "PCMPESTRM", "PCMPGTB", "PCMPGTD", "PCMPGTQ", "PCMPGTW", "PCMPISTRI", "PCMPISTRM",
        "PHADDD", "PHADDSW", "PHADDW", "PHMINPOSUW", "PHSUBD", "PHSUBSW", "PHSUBW", "PMADDUBSW", "PMADDWD", "PMAXSB", "PMAXSD", "PMAXSQ", "PMAXSW", "PMAXUB", "PMAXUD", "PMAXUQ", "PMAXUW", "PMINSB", "PMINSD", "PMINSQ", "PMINSW", "PMINUB", "PMINUD", "PMINUQ", "PMINUW", "PMOVMSKB", "PMOVSX", "PMOVZX", "PMULDQ", "PMULHRSW", "PMULHUW", "PMULHW", "PMULLD", "PMULLQ", "PMULLW", "PMULUDQ",
        "PSADBW", "PSHUFB", "PSHUFD", "PSHUFHW", "PSHUFLW", "PSHUFW", "PSIGNB", "PSIGND", "PSIGNW",
        "PSLLD", "PSLLDQ", "PSLLQ", "PSLLW", "PSRAD", "PSRAQ", "PSRAW", "PSRLD", "PSRLDQ", "PSRLQ", "PSRLW", "PSUBB", "PSUBD", "PSUBQ", "PSUBSB", "PSUBSW", "PSUBUSB", "PSUBUSW", "PSUBW", "PUNPCKHBW", "PUNPCKHDQ", "PUNPCKHQDQ", "PUNPCKHWD", "PUNPCKLBW", "PUNPCKLDQ", "PUNPCKLQDQ", "PUNPCKLWD",
        "SHA1MSG1", "SHA1MSG2", "SHA1NEXTE", "SHA1RNDS4", "SHA256MSG1", "SHA256MSG2", "SHA256RNDS2", "SHUFPD", "SHUFPS",
        "VADDPD", "VADDPS", "VADDSD", "VADDSS", "VADDSUBPD", "VADDSUBPS", "VDBPSADBW", "VEXP2PD", "VEXP2PS", "VEXPANDPD", "VEXPANDPS", "VEXTRACTF128", "VEXTRACTF32x4", "VEXTRACTF32x8", "VEXTRACTF64x2", "VEXTRACTF64x4", "VEXTRACTI128", "VEXTRACTI32x4", "VEXTRACTI32x8", "VEXTRACTI64x2", "VEXTRACTI64x4", "VFIXUPIMMPD", "VFIXUPIMMPS", "VFIXUPIMMSD", "VFIXUPIMMSS", "VFMADD132PD", "VFMADD132PS", "VFMADD132SD", "VFMADD132SS", "VFMADD213PD", "VFMADD213PS", "VFMADD213SD", "VFMADD213SS", "VFMADD231PD", "VFMADD231PS", "VFMADD231SD", "VFMADD231SS", "VFMADDSUB132PD", "VFMADDSUB132PS", "VFMADDSUB213PD", "VFMADDSUB213PS", "VFMADDSUB231PD", "VFMADDSUB231PS", "VFMSUB132PD", "VFMSUB132PS", "VFMSUB132SD", "VFMSUB132SS", "VFMSUB213PD", "VFMSUB213PS", "VFMSUB213SD", "VFMSUB213SS", "VFMSUB231PD", "VFMSUB231PS", "VFMSUB231SD", "VFMSUB231SS", "VFMSUBADD132PD", "VFMSUBADD132PS", "VFMSUBADD213PD", "VFMSUBADD213PS", "VFMSUBADD231PD", "VFMSUBADD231PS", "VFNMADD132PD", "VFNMADD132PS", "VFNMADD132SD", "VFNMADD132SS", "VFNMADD213PD", "VFNMADD213PS", "VFNMADD213SD", "VFNMADD213SS", "VFNMADD231PD", "VFNMADD231PS", "VFNMADD231SD", "VFNMADD231SS", "VFNMSUB132PD", "VFNMSUB132PS", "VFNMSUB132SD", "VFNMSUB132SS", "VFNMSUB213PD", "VFNMSUB213PS", "VFNMSUB213SD", "VFNMSUB213SS", "VFNMSUB231PD", "VFNMSUB231PS", "VFNMSUB231SD", "VFNMSUB231SS", "VFPCLASSPD", "VFPCLASSPS", "VFPCLASSSD", "VFPCLASSSS", "VGATHERDPD", "VGATHERDPS", "VGATHERPF0DPD", "VGATHERPF0DPS", "VGATHERPF0QPD", "VGATHERPF0QPS", "VGATHERPF1DPD", "VGATHERPF1DPS", "VGATHERPF1QPD", "VGATHERPF1QPS", "VGATHERQPD", "VGATHERQPS", "VGETEXPPD", "VGETEXPPS", "VGETEXPSD", "VGETEXPSS", "VGETMANTPD", "VGETMANTPS", "VGETMANTSD", "VGETMANTSS", "VINSERTF128", "VINSERTF32x4", "VINSERTF32x8", "VINSERTF64x2", "VINSERTF64x4", "VINSERTI128", "VINSERTI32x4", "VINSERTI32x8", "VINSERTI64x2", "VINSERTI64x4", "VMASKMOV", "VMOVDQA", "VMOVDQA32", "VMOVDQA64", "VMOVDQU", "VMOVDQU16", "VMOVDQU32", "VMOVDQU64", "VMOVDQU8", "VPBLENDD", "VPBLENDMB", "VPBLENDMD", "VPBLENDMQ", "VPBLENDMW", "VPBROADCAST", "VPBROADCASTB", "VPBROADCASTD", "VPBROADCASTM", "VPBROADCASTQ", "VPBROADCASTW", "VPCMPB", "VPCMPD", "VPCMPQ", "VPCMPUB", "VPCMPUD", "VPCMPUQ", "VPCMPUW", "VPCMPW", "VPCOMPRESSD", "VPCOMPRESSQ", "VPCONFLICTD", "VPCONFLICTQ", "VPERM2F128", "VPERM2I128", "VPERMD", "VPERMI2D", "VPERMI2PD", "VPERMI2PS", "VPERMI2Q", "VPERMI2W", "VPERMILPD", "VPERMILPS", "VPERMPD", "VPERMPS", "VPERMQ", "VPERMW", "VPEXPANDD", "VPEXPANDQ", "VPGATHERDD", "VPGATHERDQ", "VPGATHERQD", "VPGATHERQQ", "VPLZCNTD", "VPLZCNTQ", "VPMASKMOV", "VPMOVB2M", "VPMOVD2M", "VPMOVDB", "VPMOVDW", "VPMOVM2B", "VPMOVM2D", "VPMOVM2Q", "VPMOVM2W", "VPMOVQ2M", "VPMOVQB", "VPMOVQD", "VPMOVQW", "VPMOVSDB", "VPMOVSDW", "VPMOVSQB", "VPMOVSQD", "VPMOVSQW", "VPMOVSWB", "VPMOVUSDB", "VPMOVUSDW", "VPMOVUSQB", "VPMOVUSQD", "VPMOVUSQW", "VPMOVUSWB", "VPMOVW2M", "VPMOVWB", "VPSCATTERDD", "VPSCATTERDQ", "VPSCATTERQD", "VPSCATTERQQ", "VPSLLVD", "VPSLLVQ", "VPSLLVW", "VPSRAVD", "VPSRAVQ", "VPSRAVW", "VPSRLVD", "VPSRLVQ", "VPSRLVW", "VPTERNLOGD", "VPTERNLOGQ", "VPTESTMB", "VPTESTMD", "VPTESTMQ", "VPTESTMW", "VPTESTNMB", "VPTESTNMD", "VPTESTNMQ", "VPTESTNMW", "VRANGEPD", "VRANGEPS", "VRANGESD", "VRANGESS", "VRCP14PD", "VRCP14PS", "VRCP14SD", "VRCP14SS", "VRCP28PD", "VRCP28PS", "VRCP28SD", "VRCP28SS", "VREDUCEPD", "VREDUCEPS", "VREDUCESD", "VREDUCESS", "VRNDSCALEPD", "VRNDSCALEPS", "VRNDSCALESD", "VRNDSCALESS", "VRSQRT14PD", "VRSQRT14PS", "VRSQRT14SD", "VRSQRT14SS", "VRSQRT28PD", "VRSQRT28PS", "VRSQRT28SD", "VRSQRT28SS", "VSCALEFPD", "VSCALEFPS", "VSCALEFSD", "VSCALEFSS", "VSCATTERDPD", "VSCATTERDPS", "VSCATTERPF0DPD", "VSCATTERPF0DPS", "VSCATTERPF0QPD", "VSCATTERPF0QPS", "VSCATTERPF1DPD", "VSCATTERPF1DPS", "VSCATTERPF1QPD", "VSCATTERPF1QPS", "VSCATTERQPD", "VSCATTERQPS", "VSHUFF32x4", "VSHUFF64x2", "VSHUFI32x4", "VSHUFI64x2", "VTESTPD", "VTESTPS", "VZEROALL", "VZEROUPPER", "CVTDQ2PD", "CVTDQ2PS", "CVTPD2DQ", "CVTPD2PI", "CVTPD2PS", "CVTPI2PD", "CVTPI2PS", "CVTPS2DQ", "CVTPS2PD", "CVTPS2PI", "CVTSD2SI", "CVTSD2SS", "CVTSI2SD", "CVTSI2SS", "CVTSS2SD", "CVTSS2SI", "CVTTPD2DQ", "CVTTPD2PI", "CVTTPS2DQ", "CVTTPS2PI", "CVTTSD2SI", "CVTTSS2SI", "VCVTPD2QQ", "VCVTPD2UDQ", "VCVTPD2UQQ", "VCVTPH2PS", "VCVTPS2PH", "VCVTPS2QQ", "VCVTPS2UDQ", "VCVTPS2UQQ", "VCVTQQ2PD", "VCVTQQ2PS", "VCVTSD2USI", "VCVTSS2USI", "VCVTTPD2QQ", "VCVTTPD2UDQ", "VCVTTPD2UQQ", "VCVTTPS2QQ", "VCVTTPS2UDQ", "VCVTTPS2UQQ", "VCVTTSD2USI", "VCVTTSS2USI", "VCVTUDQ2PD", "VCVTUDQ2PS", "VCVTUQQ2PD", "VCVTUQQ2PS", "VCVTUSI2SD", "VCVTUSI2SS", "VGETEXPPD", "VGETEXPPS", "VGETEXPSD", "VGETEXPSS", "VGETMANTPD", "VGETMANTPS", "VGETMANTSD", "VGETMANTSS", "VRCP14PD", "VRCP14PS", "VRCP14SD", "VRCP14SS", "VRCP28PD", "VRCP28PS", "VRCP28SD", "VRCP28SS", "VREDUCEPD", "VREDUCEPS", "VREDUCESD", "VREDUCESS", "VRNDSCALEPD", "VRNDSCALEPS", "VRNDSCALESD", "VRNDSCALESS", "VRSQRT14PD", "VRSQRT14PS", "VRSQRT14SD", "VRSQRT14SS", "VRSQRT28PD", "VRSQRT28PS", "VRSQRT28SD", "VRSQRT28SS", "VSCALEFPD", "VSCALEFPS", "VSCALEFSD", "VSCALEFSS", "VSHUFF32x4", "VSHUFF64x2", "VSHUFI32x4", "VSHUFI64x2" // SIMD related intrinsics, conversions, and grouped instructions, VERY EXTENSIVE SIMD LIST NOW, including almost all from your input.

    };



        public static string[] registers = new string[] {
            "rax", "rbx", "rcx", "rdx", "rsi", "rdi", "rbp", "rsp", "rip",
            "r8", "r9", "r10", "r11", "r12", "r13", "r14", "r15",
            "eax", "ebx", "ecx", "edx", "esi", "edi", "ax", "bx", "cx",
            "dx", "si", "di", "bp", "sp", "ip", "r8d", "r9d", "r10d",
            "r11d", "r12d", "r13d", "r14d", "r15d", "r8w", "r9w", "r10w",
            "r11w", "r12w", "r13w", "r14w", "r15w", "ah", "bh", "ch", "dh",
            "al", "bl", "cl", "dl", "sil", "dil", "bpl", "spl",
            "r8b", "r9b", "r10b", "r11b", "r12b", "r13b", "r14b", "r15b",
            "esp", "ebp", "rflags",
            "xmm0", "xmm1", "xmm2", "xmm3", "xmm4", "xmm5", "xmm6", "xmm7",
            "xmm8", "xmm9", "xmm10", "xmm11", "xmm12", "xmm13", "xmm14", "xmm15",
            "ymm0", "ymm1", "ymm2", "ymm3", "ymm4", "ymm5", "ymm6", "ymm7",
            "ymm8", "ymm9", "ymm10", "ymm11", "ymm12", "ymm13", "ymm14", "ymm15",
        };

        private static readonly Dictionary<string, SolidColorBrush> InstructionBrushes = new Dictionary<string, SolidColorBrush>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, SolidColorBrush> RegisterBrushes = new Dictionary<string, SolidColorBrush>(StringComparer.OrdinalIgnoreCase);


        static SyntaxHighlighter() // Statischer Konstruktor
        {
            foreach (string instruction in JumpInstructions) InstructionBrushes[instruction] = Brushes.Red;
            foreach (string instruction in CallInstructions) InstructionBrushes[instruction] = Brushes.Red;
            foreach (string instruction in MoveInstructions) InstructionBrushes[instruction] = Brushes.LightSkyBlue;
            foreach (string instruction in StackInstructions) InstructionBrushes[instruction] = Brushes.LightSkyBlue;
            foreach (string instruction in CompareInstructions) InstructionBrushes[instruction] = Brushes.Yellow;
            foreach (string instruction in ArithmeticInstructions) InstructionBrushes[instruction] = Brushes.LightSkyBlue;
            foreach (string instruction in BitwiseLogicalInstructions) InstructionBrushes[instruction] = Brushes.LightSkyBlue;
            foreach (string instruction in SIMDInstructions) InstructionBrushes[instruction] = Brushes.LightSkyBlue;
            foreach (string register in registers) RegisterBrushes[register] = Brushes.LightPink;
        }
        public static SolidColorBrush Check_Type(string instruction)
        {
            if (InstructionBrushes.TryGetValue(instruction, out var brush))
            {
                return brush;
            }
            if (RegisterBrushes.TryGetValue(instruction, out brush))
            {
                return brush;
            }
            if (instruction.StartsWith("0x"))
                return Brushes.DarkGoldenrod;
            return Brushes.White; // Default Brush
        }
    }
}