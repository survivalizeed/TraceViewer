using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
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
        private Grid comment_content_grid_hitbox;
        private Grid disassembler_view;
        private List<Tuple<string, int>> regs; // here without the paddings
        private List<string> highlights = new List<string>();
        MainWindow window;

        public WPF_TraceRow(TraceRow current, TraceRow? next)
        {
            InitializeComponent();
            regs = prefs.X64_REGS.ToList();
            regs.RemoveAll(reg => string.IsNullOrEmpty(reg.Item1));
            Set(current, next);
            if (System.Windows.Application.Current.MainWindow is MainWindow window)
            {
                this.registers_view = window.registers_view;
                this.comment_content_grid_hitbox = window.comment_content_grid_hitbox;
                this.disassembler_view = window.disassembler_view;
                this.window = window;
            }
            else
            {
                throw new Exception("Main window not found");
            }
            
        }


        public void Set(TraceRow current, TraceRow? next)
        {
            if (next != null)
            {
                for (int i = 0; i < regs.Count; ++i)
                {
                    if (!next.Regs[i].SequenceEqual(current.Regs[i]) && regs[i].Item1 != "rip")
                    {
                        string current_reg = ByteArrayToHexString(current.Regs[i], true);
                        string next_reg = ByteArrayToHexString(next.Regs[i], true);         
                        changes.Inlines.Add(new Run(regs[i].Item1) { Foreground = SyntaxHighlighter.Check_Type(regs[i].Item1) });
                        changes.Inlines.Add(new Run(": ") { Foreground = Brushes.White });
                        changes.Inlines.Add(new Run("0x" + current_reg) { Foreground = SyntaxHighlighter.Check_Type("0x" + current_reg) });
                        changes.Inlines.Add(new Run(" -> ") { Foreground = Brushes.White });
                        changes.Inlines.Add(new Run("0x" + next_reg) { Foreground = SyntaxHighlighter.Check_Type("0x" + next_reg) });
                        changes.Inlines.Add(new Run("; ") { Foreground = Brushes.White });
                        highlights.Add(regs[i].Item1);
                    }
                }
            }

            // Make the order rax, rbx, rcx, rdx for the register (Optimized swapping)
            registers_x64 = current.Regs;
            SwapRegisters(registers_x64, 1, 3);
            SwapRegisters(registers_x64, 2, 3); 


            id.Text = current.Id.ToString();
            id.Foreground = Brushes.White;

            address.Text = "0x" + current.Ip.ToString("X");
            address.Foreground = Brushes.White;


            string[] single_instructions = Regex.Split(current.Disasm, @"([ ,:\[\]*])");

            foreach (string single_instruction in single_instructions)
            {
                disasm.Inlines.Add(new Run(single_instruction) { Foreground = SyntaxHighlighter.Check_Type(single_instruction) });
            }
        }

        private void OnHover(object sender, MouseEventArgs e)
        {
            int i = 0;
            
            foreach (var item in registers_view.Items)
            {
                bool found_something = false;
                var register = item as WPF_RegisterRow;
                if (register != null)
                {
                    foreach(string highlight in highlights)
                    {
                        if (register.register.Text.ToLower() == highlight.ToLower())
                        {
                            register.register.Foreground = Brushes.Red;
                            register.value.Foreground = Brushes.Red;
                            found_something = true;
                        }
                    }
                    register.value.Text = "0x" + ByteArrayToHexString(registers_x64[i], false);
                    if (found_something)
                    {
                        i++;
                        continue;
                    }
                    register.register.Foreground = Brushes.Coral;
                    register.value.Foreground = Brushes.DarkGoldenrod;
               
                }
                i++;
            }
        }

        private string ByteArrayToHexString(byte[] bytes, bool zero_removal)
        {
            string hex = "";
            if(zero_removal) hex = string.Concat(bytes.Reverse().Where(b => b != 0x00).Select(b => b.ToString("X2")));
            else hex = string.Concat(bytes.Reverse().Select(b => b.ToString("X2")));       
            return string.IsNullOrEmpty(hex) ? "00" : hex;
        }

        private void SwapRegisters<T>(List<T> list, int index1, int index2)
        {
            (list[index1], list[index2]) = (list[index2], list[index1]);
        }

        private void OnDoubleClick(object sender, MouseButtonEventArgs e)
        {
            big_comment_edit_active();
        }

        private void OnKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                big_comment_edit_active();
            }
        }

        private void big_comment_edit_active()
        {
            disassembler_view.Visibility = Visibility.Collapsed;
            comment_content_grid_hitbox.Visibility = Visibility.Visible;
            if (comment_content_grid_hitbox.Children[0] is DockPanel dockPanel && dockPanel.Children[0] is TextBox comment_content)
            {
                comment_content.Text = this.comments.Text;
                comment_content.Focus();
                comment_content.SelectionStart = comment_content.Text.Length;
                // Allows for text update in the comment box
                window.current_comment_content_partner = comments;
            }
        }
    }
}