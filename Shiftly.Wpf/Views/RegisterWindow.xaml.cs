using System.Windows;
using Shiftly.Wpf.ViewModels;

namespace Shiftly.Wpf.Views
{
    public partial class RegisterWindow : Window
    {
        private readonly RegisterViewModel _viewModel;

        public RegisterWindow()
        {
            InitializeComponent();
            _viewModel = (RegisterViewModel)DataContext;
            _viewModel.RequestClose += () => Close();
        }

        private void RegPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is RegisterViewModel vm)
            {
                vm.Password = RegPasswordBox.Password;
            }
        }

        private void RegConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is RegisterViewModel vm)
            {
                vm.ConfirmPassword = RegConfirmPasswordBox.Password;
            }
        }
    }
}
