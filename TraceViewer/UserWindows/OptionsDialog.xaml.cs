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
using System.Windows.Shapes;

namespace TraceViewer.UserWindows
{

    public partial class OptionsDialog : Window
    {

        private List<(string, Option)> options;
        List<CheckBox> checkBoxes = new List<CheckBox>();

        public OptionsDialog(string Title, List<(string, Option)> options, double? width = null, double? height = null)
        {
            InitializeComponent();
            this.OptionsText.Text = Title;
            this.Owner = Application.Current.MainWindow;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            this.options = options;

            if(width != null)
                this.Width = (double)width;
            if (height != null)
                this.Height = (double)height;

            foreach (var option_child in OptionsGrid.Children)
            {
                if (option_child is CheckBox checkBox)
                {
                    checkBox.Visibility = Visibility.Collapsed;
                    checkBoxes.Add(checkBox);
                }
            }
            if(options.Count > 8)
                throw new Exception("Too many options for OptionsDialog");
            for (int i = 0; i < options.Count; i++)
            {
                checkBoxes[i].Visibility = Visibility.Visible;
                checkBoxes[i].Content = options[i].Item1;
                checkBoxes[i].IsChecked = options[i].Item2;
            }
        }


        private void Ok_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                for (int i = 0; i < options.Count; i++)
                {
                    options[i].Item2.option = checkBoxes[i].IsChecked == true;
                }
                this.Close();
            }
        }

    }
}
