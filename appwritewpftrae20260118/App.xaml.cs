using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace appwritewpftrae20260118
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            StartupHelper.EnsureRunOnStartup();

            var runInBackground = e.Args.Any(arg =>
                string.Equals(arg, "/background", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "-background", StringComparison.OrdinalIgnoreCase));

            var mainWindow = new MainWindow();
            Current.MainWindow = mainWindow;

            if (runInBackground)
            {
                mainWindow.ShowInTaskbar = false;
                mainWindow.WindowState = WindowState.Minimized;
                mainWindow.Visibility = Visibility.Hidden;

                _ = mainWindow.InitializeLogicAsync();
            }
            else
            {
                mainWindow.Show();
            }
        }
    }
}
