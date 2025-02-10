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

    public partial class WPF_TraceRow : UserControl
    {
        private List<byte[]> registers_x64;
        private ItemsControl registers_view;
        private List<Tuple<string, int>> regs; // here without the paddings

        public WPF_TraceRow(TraceRow previous, TraceRow current, ItemsControl register_view)
        {
            InitializeComponent();
            regs = prefs.X64_REGS;
            for (int i = 0; i < regs.Count; i++)
            {
                if (regs[i].Item1 == "")
                {
                    regs.RemoveAt(i);
                }
            }
            Set(previous, current);
            this.registers_view = register_view;
        }


        public void Set(TraceRow previous, TraceRow current)
        {
            byte[] rbx = new byte[8], rcx = new byte[8], rdx = new byte[8];
            // Make the order rax, rbx, rcx, rdx for the register
            rbx = current.Regs[3];
            rcx = current.Regs[1];
            rdx = current.Regs[2];

            current.Regs[1] = rbx;
            current.Regs[2] = rcx;
            current.Regs[3] = rdx;

            registers_x64 = current.Regs;
            


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



            if (previous != null) {
                for (int i = 0; i < regs.Count; ++i)
                {
                    if (!previous.Regs[i].SequenceEqual(current.Regs[i]) && regs[i].Item1 != "rip")
                    {
                        changes.Text += regs[i].Item1 + ": " + "0x" + string.Concat(previous.Regs[i].Reverse().Where(b => b != 0x00).Select(b => b.ToString("X2"))) + " -> " + "0x" +
                            string.Concat(current.Regs[i].Reverse().Where(b => b != 0x00).Select(b => b.ToString("X2"))) + "; ";
                    }
                }
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
