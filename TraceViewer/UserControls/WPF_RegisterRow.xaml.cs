using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TraceViewer.UserWindows;

namespace TraceViewer
{
    public enum RegisterType
    {
        GeneralPurpose,
        Flags,
        Debug,
        FPU
    }
    public partial class WPF_RegisterRow : UserControl
    {
        public RegisterType registerType;
        public WPF_RegisterRow(string register, string value, RegisterType registerType)
        {
            InitializeComponent();
            Set(register, value, registerType);     
        }

        public void Set(string register, string value, RegisterType registerType)
        {
            this.register.Text = register;
            this.value.Text = value;
            this.registerType = registerType;
        }

        public void Set(string value)
        {
            this.value.Text = value;
        }

        private void value_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (register.Text.StartsWith("YMM"))
            {
                YMMDialog dialog = new YMMDialog(value.Text.Substring(2));
                dialog.ShowDialog();
            }
            else if(register.Text.StartsWith("XMM"))
            {
                XMMDialog dialog = new XMMDialog(value.Text.Substring(2));
                dialog.ShowDialog();
            }
            else
            {
                QWORDDialog dialog = new QWORDDialog(value.Text.Substring(2));
                dialog.ShowDialog();
            }
        }
    }
}
