using System.Windows;
using Shiftly.Wpf.ViewModels;

namespace Shiftly.Wpf.Views
{
    public partial class WorkerAvailabilityWindow : Window
    {
        private readonly WorkerAvailabilityViewModel _viewModel;

        public WorkerAvailabilityWindow()
        {
            InitializeComponent();
            _viewModel = new WorkerAvailabilityViewModel();
            DataContext = _viewModel;
        }
    }
}

