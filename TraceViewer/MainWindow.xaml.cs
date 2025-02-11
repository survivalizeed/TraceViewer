using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TraceViewer.Core;

namespace TraceViewer
{
    public partial class MainWindow : Window
    {
        static int show_counter = 0;
        public ObservableCollection<WPF_TraceRow> instruction_view_items = new ObservableCollection<WPF_TraceRow>();
        public ObservableCollection<WPF_RegisterRow> register_view_items = new ObservableCollection<WPF_RegisterRow>();
        public TextBox current_comment_content_partner;

        public MainWindow()
        {
            InitializeComponent();
            instructions_view.ItemsSource = instruction_view_items;
            registers_view.ItemsSource = register_view_items;
            TraceHandler.Load("F:\\Tools\\Reversing\\x64dbg\\release\\x64\\db\\trace.trace64");
        }

        private void ID_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width != e.PreviousSize.Width)
            {
                instructions_view.BeginInit();
                try
                {
                    foreach (var item in instruction_view_items)
                    {
                        item.id.Width = e.NewSize.Width;
                        item.id_border.Width = e.NewSize.Width;
                    }
                }
                finally
                {
                    instructions_view.EndInit();
                    set_instructions_view_width();
                }
            }
        }

        private void ADDRESS_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width != e.PreviousSize.Width)
            {
                instructions_view.BeginInit();
                try
                {
                    foreach (var item in instruction_view_items)
                    {
                        item.address.Width = e.NewSize.Width;
                        item.address_border.Width = e.NewSize.Width;
                    }
                }
                finally
                {
                    instructions_view.EndInit();
                    set_instructions_view_width();
                }
            }
        }

        private void DISASM_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width != e.PreviousSize.Width)
            {
                instructions_view.BeginInit();
                try
                {
                    foreach (var item in instruction_view_items)
                    {
                        item.disasm.Width = e.NewSize.Width;
                        item.disasm_border.Width = e.NewSize.Width;
                    }
                }
                finally
                {
                    instructions_view.EndInit();
                    set_instructions_view_width();
                }
            }
        }

        private void CHANGES_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width != e.PreviousSize.Width)
            {
                instructions_view.BeginInit();
                try
                {
                    foreach (var item in instruction_view_items)
                    {
                        item.changes.Width = e.NewSize.Width;
                        item.changes_border.Width = e.NewSize.Width;
                    }
                }
                finally
                {
                    instructions_view.EndInit();
                    set_instructions_view_width();
                }
            }
        }

        private void COMMENTS_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width != e.PreviousSize.Width)
            {
                instructions_view.BeginInit();
                try
                {
                    foreach (var item in instruction_view_items)
                    {
                        item.comments.Width = e.NewSize.Width;
                    }
                }
                finally
                {
                    instructions_view.EndInit();
                    set_instructions_view_width();
                }
            }
        }

        private void set_instructions_view_width()
        {
            // +8 is for the scrollbar
            var total_width = cd0.Width.Value + cd1.Width.Value + cd2.Width.Value + cd3.Width.Value + cd4.Width.Value + cd5.Width.Value + 8;
            if (total_width > 0)
            {
                instructions_view.MinWidth = total_width;
                instructions_view.MaxWidth = total_width;
            }
        }

        private bool toggle = true;
        private void fpu_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            toggle = !toggle;
            Visibility fpu_visibility = Visibility.Visible;
            if (!toggle)
            {
                fpu.Foreground = Brushes.Gray;
                fpu_visibility = Visibility.Collapsed;
            }
            else
            {
                fpu.Foreground = Brushes.White;
                fpu_visibility = Visibility.Visible;
            }

            foreach (var item in register_view_items)
            {
                if (item.registerType == RegisterType.FPU)
                {
                    item.Visibility = fpu_visibility;
                }
            }
        }

        private void comment_content_grid_hitbox_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            big_comment_edit_inactive();
        }

        private void comment_content_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if(e.Key == System.Windows.Input.Key.Enter || e.Key == System.Windows.Input.Key.Escape)
            {
                big_comment_edit_inactive();
            }
        }

        private void big_comment_edit_inactive()
        {
            comment_content_grid_hitbox.Visibility = Visibility.Collapsed;
            disassembler_view.Visibility = Visibility.Visible;
            if (comment_content != null)
            {
                // Will set the content of the textbox which originally made the comment_content_grid_hitbox visible
                current_comment_content_partner.Text = comment_content.Text;
                current_comment_content_partner.Focus();
                current_comment_content_partner.SelectionStart = current_comment_content_partner.Text.Length;
            }
        }
    }
}