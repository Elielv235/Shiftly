using System.Windows;
using Shiftly.Shared.DTOs;
using Shiftly.Wpf.ViewModels;

namespace Shiftly.Wpf.Views
{
    public partial class LoginWindow : Window
    {
        private readonly LoginViewModel _viewModel;

        public LoginWindow()
        {
            InitializeComponent();
            _viewModel = (LoginViewModel)DataContext;

            _viewModel.RequestClose += () => Close();
            _viewModel.RequestOpenRegister += OpenRegisterWindow;
            _viewModel.RequestOpenDashboard += OpenDashboardWindow;
        }

        private void UserPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is LoginViewModel vm)
            {
                vm.Password = UserPasswordBox.Password;
            }
        }

        private void OpenRegisterWindow()
        {
            var registerWindow = new RegisterWindow
            {
                Owner = this
            };
            registerWindow.ShowDialog();
        }

        private void OpenDashboardWindow(LoginResponseDTO loggedInUser)
        {
            var dashboard = new AdminDashboardWindow(loggedInUser);
            dashboard.Show();
        }
    }
}
