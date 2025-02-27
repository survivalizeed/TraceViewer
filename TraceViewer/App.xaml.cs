using System.Configuration;
using System.Data;
using System.Windows;
using TraceViewer.Core;
using TraceViewer.UserWindows;

namespace TraceViewer
{

    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            //base.OnStartup(e);
            MainWindow mainWindow = new MainWindow();
            if (e.Args.Length > 0) 
            {
                string filePath = e.Args[1];
                string fileExtension = System.IO.Path.GetExtension(filePath);


                if (fileExtension == ".trace64")
                {
                    mainWindow.Unload();
                    TraceHandler.OpenAndLoad(filePath);
                }
                else if (fileExtension == ".tvproj")
                {
                    mainWindow.OpenProject(filePath);
                }
                else
                {
                    MessageDialog messageDialog = new MessageDialog("INVALID FILE", "Invalid file type. Use a .trace64 or .tvproj file!");
                    messageDialog.ShowDialog();
                }
            }

            
        }
    }

}
