using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace TraceViewer.UserControls
{

    public partial class InputDialog : Window
    {
        private string result = "";

        public InputDialog(string Prompt)
        {
            InitializeComponent();
            this.Prompt.Content = Prompt;
            this.Owner = Application.Current.MainWindow;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            this.Input.Focus();
        }

        private void Ok_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                result = Input.Text;
                this.Close();
            }
        }

        private void Cancel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if(e.LeftButton == MouseButtonState.Pressed)
                this.Close();
        }

        public string GetResult()
        {
            string tmp = result;
            result = "";
            return tmp;
        }

        private void Input_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if(e.Key == Key.Enter)
            {
                Ok_MouseDown(null, null);
            }
            if (e.Key == Key.Escape)
            {
                Cancel_MouseDown(null, null);
            }
        }
    }
}
