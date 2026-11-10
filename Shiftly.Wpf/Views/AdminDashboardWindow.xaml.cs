using System.Windows;
using Shiftly.Shared.DTOs;
using Shiftly.Wpf.ViewModels;

namespace Shiftly.Wpf.Views
{
    public partial class AdminDashboardWindow : Window
    {
        private readonly AdminDashboardViewModel _viewModel;

        public AdminDashboardWindow()
        {
            InitializeComponent();
            _viewModel = new AdminDashboardViewModel();
            DataContext = _viewModel;
        }

        public AdminDashboardWindow(LoginResponseDTO loggedInUser) : this()
        {
        }
    }
}
