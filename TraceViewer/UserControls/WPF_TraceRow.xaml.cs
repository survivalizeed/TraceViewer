using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
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

    public partial class WPF_TraceRow : UserControl
    {
        private List<byte[]> registers_x64;
        private ItemsControl registers_view;
        private List<Tuple<string, int>> regs; // here without the paddings

        public WPF_TraceRow(TraceRow current, TraceRow next, ItemsControl register_view)
        {
            InitializeComponent();
            regs = new List<Tuple<string, int>>(prefs.X64_REGS);
            for (int i = 0; i < regs.Count; i++)
            {
                if (regs[i].Item1 == "")
                {
                    regs.RemoveAt(i);
                    i--;
                }
            }
            Set(current, next);
            this.registers_view = register_view;
        }


        public void Set(TraceRow current, TraceRow next)
        {
            if (next != null)
            {
                for (int i = 0; i < regs.Count; ++i)
                {
                    if (!next.Regs[i].SequenceEqual(current.Regs[i]) && regs[i].Item1 != "rip")
                    {
                        string current_reg = string.Concat(current.Regs[i].Reverse().Where(b => b != 0x00).Select(b => b.ToString("X2")));
                        if(current_reg == "")
                        {
                            current_reg = "00";
                        }
                        string next_reg = string.Concat(next.Regs[i].Reverse().Where(b => b != 0x00).Select(b => b.ToString("X2")));
                        if(next_reg == "")
                        {
                            next_reg = "00";
                        }
                        changes.Text += regs[i].Item1 + ": " + "0x" + current_reg + " -> " + "0x" +
                            next_reg + "; ";
                    }
                }
            }

            byte[] rbx = new byte[8], rcx = new byte[8], rdx = new byte[8];
            // Make the order rax, rbx, rcx, rdx for the register
            registers_x64 = current.Regs;

            rbx = registers_x64[3];
            rcx = registers_x64[1];
            rdx = registers_x64[2];

            registers_x64[1] = rbx;
            registers_x64[2] = rcx;
            registers_x64[3] = rdx;


            id.Text = current.Id.ToString();
            id.Foreground = Brushes.White;

            address.Text = "0x" + current.Ip.ToString("X");
            address.Foreground = Brushes.White;


            string[] single_instructions = Regex.Split(current.Disasm, @"([ ,:\[\]*])");

            foreach (string single_instruction in single_instructions)
            {
                disasm.Inlines.Add(new Run(single_instruction)
                {
                    Foreground = SyntaxHighlighter.Check_Type(single_instruction)
                });
            }

        }

        private void OnHover(object sender, MouseEventArgs e)
        {
            int i = 0;
            foreach (var item in registers_view.Items)
            {
                var register = item as WPF_RegisterRow;
                if (register != null)
                {
                    register.value.Text = "0x" + string.Concat(registers_x64[i].Reverse().Select(b => b.ToString("X2"))); ;
                }
                i++;
            }
          
        }
    }
}
