using System.Windows;
using NoteApp.Views;

namespace NoteApp
{
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            try
            {
                var mainWindow = new MainWindow();
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.ToString(), // ← ex.ToString() gibt ALLES aus inkl. Stack Trace
                    "Startfehler",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}