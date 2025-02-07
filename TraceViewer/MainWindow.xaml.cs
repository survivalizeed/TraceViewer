using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TraceViewer.Core;

namespace TraceViewer
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<WPF_TraceRow> instruction_view_items = new ObservableCollection<WPF_TraceRow>();
        private ObservableCollection<WPF_RegisterRow> register_view_items = new ObservableCollection<WPF_RegisterRow>();
        public MainWindow()
        {
            InitializeComponent();
            instructions_view.ItemsSource = instruction_view_items;
            registers_view.ItemsSource = register_view_items;
            TraceHandler.Load("F:\\Tools\\Reversing\\x64dbg\\release\\x64\\db\\trace.trace64", ref instruction_view_items, ref register_view_items, registers_view);
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
                }
            }
        }
    }
}