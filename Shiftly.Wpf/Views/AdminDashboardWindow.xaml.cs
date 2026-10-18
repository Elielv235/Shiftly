using System.Windows;
using Shiftly.Shared.DTOs;
using Shiftly.Wpf.ViewModels;

namespace Shiftly.Wpf.Views
{
    public partial class AdminDashboardWindow : Window
    {
        private readonly AdminDashboardViewModel _viewModel;

        public AdminDashboardWindow(LoginResponseDTO loggedInUser)
        {
            InitializeComponent();
            _viewModel = new AdminDashboardViewModel(loggedInUser);
            DataContext = _viewModel;

            _viewModel.RequestLogout += OnLogout;
        }

        private void OnLogout()
        {
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            Close();
        }
    }
}
