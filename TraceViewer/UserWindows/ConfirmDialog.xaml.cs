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

    public partial class ConfirmDialog : Window
    {
        private bool result = false;

        public ConfirmDialog(string Prompt, double? width = null, double? height = null)
        {
            InitializeComponent();
            this.PromptText.Text = Prompt;
            this.Owner = Application.Current.MainWindow;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            if(width != null)
                this.Width = (double)width;
            if (height != null)
                this.Height = (double)height;
        }

        private void Ok_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                result = true;
                this.Close();
            }
        }

        private void Cancel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                this.Close();
        }

        public bool GetResult()
        {
            return result;
        }
    }
}
