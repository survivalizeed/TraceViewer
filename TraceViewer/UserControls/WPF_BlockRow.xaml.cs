using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TraceViewer.Core;
using TraceViewer.UserControls;

namespace TraceViewer
{
    public partial class WPF_BlockRow : UserControl
    {
        private bool isInitializing = true;

        public WPF_BlockRow(string id, string address, string name)
        {
            InitializeComponent();
            this.id.Text = id;
            if (ulong.TryParse(address, out ulong addressValue))
            {
                this.address.Text = "0x" + addressValue.ToString("X");
            }
            this.block.Text = name;
            isInitializing = false;
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
                return;

            var window = Application.Current.MainWindow as MainWindow ?? throw new Exception("Main window not found");

            window.DisasmViewButton_MouseDown(null, null);
            window.ScrollTo(Convert.ToInt32(id.Text));
        }

        private void block_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isInitializing) return;
            if (int.TryParse(id.Text, out int rowId) && TraceHandler.Trace != null && rowId >= 0 && rowId < TraceHandler.Trace.Trace.Count)
            {
                TraceHandler.Trace.Trace[rowId].block = block.Text;

                var window = Application.Current.MainWindow as MainWindow;
                if (window != null)
                {
                    foreach (var item in window.InstructionViewItems)
                    {
                        if (item.id.Text == id.Text)
                        {
                            item.block.Text = block.Text;
                        }
                    }
                }
            }
        }

        private void block_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Keyboard.ClearFocus();
                e.Handled = true;
            }
        }

        private void block_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var input = new InputDialog("Enter block name:", block.Text);
            input.ShowDialog();
            if (input.IsSuccess)
            {
                block.Text = input.GetResult();
            }
            e.Handled = true;
        }
    }
}
