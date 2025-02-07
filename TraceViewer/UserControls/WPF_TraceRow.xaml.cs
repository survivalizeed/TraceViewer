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
        private ulong[] registers_x64 = new ulong[17];
        private ItemsControl registers_view;

        public WPF_TraceRow()
        {
            InitializeComponent();
        }

        public WPF_TraceRow(TraceRow traceRow, ItemsControl register_view)
        {
            InitializeComponent();
            Set(traceRow);
            this.registers_view = register_view;
        }


        public void Set(TraceRow traceRow)
        {
            registers_x64 = traceRow.Regs;

            id.Text = traceRow.Id.ToString();
            id.Foreground = Brushes.White;

            address.Text = "0x" + traceRow.Ip.ToString("X");
            address.Foreground = Brushes.White;


            string[] single_instructions = Regex.Split(traceRow.Disasm, @"([ ,:\[\]*])");

            foreach (string single_instruction in single_instructions)
            {
                disasm.Inlines.Add(new Run(single_instruction)
                {
                    Foreground = SyntaxHighlighter.Check_Type(single_instruction)
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
                        Foreground = SyntaxHighlighter.Check_Type(single_changes[i])
                    });
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
                    register.value.Text = "0x" + registers_x64[i].ToString("X");
                }
                i++;
            }
          
        }
    }
}
