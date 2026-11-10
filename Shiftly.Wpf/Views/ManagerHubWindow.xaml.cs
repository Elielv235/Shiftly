using System.Windows;
using Shiftly.Wpf.ViewModels;

namespace Shiftly.Wpf.Views
{
    public partial class ManagerHubWindow : Window
    {
        public ManagerHubWindow()
        {
            InitializeComponent();
            DataContext = new ManagerHubViewModel();
        }
    }
}
