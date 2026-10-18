using System;
using System.Windows;
using Shiftly.Wpf.Views;

namespace Shiftly.Wpf
{
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // תפיסת שגיאות גלובלית למניעת קריסות פתאומיות
            DispatcherUnhandledException += (s, args) =>
            {
                MessageBox.Show(
                    $"התרחשה שגיאה בלתי צפויה:\n\n{args.Exception.Message}\n\nפרטים:\n{args.Exception.InnerException?.Message}",
                    "שגיאת מערכת Shiftly",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                args.Handled = true;
            };

            var loginWindow = new LoginWindow();
            loginWindow.Show();
        }
    }
}
