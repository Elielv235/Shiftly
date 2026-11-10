using Shiftly.Shared.DTOs;
using Shiftly.Wpf.Common;
using Shiftly.Wpf.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Shiftly.Wpf.ViewModels
{
    public class WorkerAvailabilityViewModel : ViewModelBase
    {
        private readonly ShiftlyApiClient _apiClient;
        private DateTime _targetDate = new DateTime(2026, 11, 15);
        private int _selectedDayIndex = 0;
        private bool _canWorkMorning;
        private bool _canWorkEvening;
        private string _notes = string.Empty;
        private string _statusMessage = "סמן משמרת מבוקשת ולחץ על שלח.";

        public WorkerAvailabilityViewModel()
        {
            _apiClient = ShiftlyApiClient.Instance;
            SubmitCommand = new RelayCommand(async _ => await SubmitAsync(), _ => CanSubmit());
            LogoutCommand = new RelayCommand(_ => Logout());
        }

        public int SelectedDayIndex
        {
            get => _selectedDayIndex;
            set
            {
                if (SetProperty(ref _selectedDayIndex, value))
                {
                    TargetDate = new DateTime(2026, 11, 15).AddDays(value >= 0 ? value : 0);
                }
            }
        }

        public DateTime TargetDate
        {
            get => _targetDate;
            set => SetProperty(ref _targetDate, value);
        }
        public bool CanWorkMorning
        {
            get => _canWorkMorning;
            set
            {
                if (SetProperty(ref _canWorkMorning, value))
                    CommandManager.InvalidateRequerySuggested();
            }
        }
        public bool CanWorkEvening
        {
            get => _canWorkEvening;
            set
            {
                if (SetProperty(ref _canWorkEvening, value))
                    CommandManager.InvalidateRequerySuggested();
            }
        }
        public string Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public ICommand SubmitCommand { get; }
        public ICommand LogoutCommand { get; }

        private bool CanSubmit() => CanWorkMorning || CanWorkEvening;

        private async Task SubmitAsync()
        {
            StatusMessage = "שולח אילוצים לשרת...";
            var dto = new WorkerAvailabilityDTO
            {
                UserId = _apiClient.CurrentUser?.UserId ?? 2,
                TargetDate = TargetDate,
                CanWorkMorning = CanWorkMorning,
                CanWorkEvening = CanWorkEvening,
                Notes = Notes
            };

            bool success = await _apiClient.SubmitAvailabilityAsync(dto);
            if (success)
            {
                StatusMessage = "האילוצים נקלטו בהצלחה בטבלת Availabilities בסטטוס Pending!";
                CanWorkMorning = false;
                CanWorkEvening = false;
                Notes = string.Empty;
            }
            else
            {
                StatusMessage = "שגיאה בשליחת האילוצים לשרת.";
            }
        }

        private void Logout()
        {
            _apiClient.Logout();
            var login = new Views.LoginWindow();
            login.Show();
            Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w is Views.WorkerAvailabilityWindow)?.Close();
        }
    }
}

