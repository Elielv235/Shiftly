using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Shiftly.Shared.DTOs;
using Shiftly.Shared.Enums;
using Shiftly.Wpf.Common;
using Shiftly.Wpf.Services;

namespace Shiftly.Wpf.ViewModels
{
    /// <summary>
    /// ViewModel for WorkerAvailabilityWindow (Cycle 2 Availabilities + Cycle 3 Worker Schedule & Request Swap).
    /// Enforces client-side real-time validation & role-aware UI visibility without replacing server-side 401/403 security.
    /// Zero direct database access.
    /// </summary>
    public class WorkerAvailabilityViewModel : ViewModelBase
    {
        private readonly ShiftlyApiClient _apiClient;
        private DateTime _targetDate = new DateTime(2026, 11, 15);
        private int _selectedDayIndex = 0;
        private bool _canWorkMorning;
        private bool _canWorkEvening;
        private string _notes = string.Empty;
        private string _statusMessage = "סמן משמרת מבוקשת ולחץ על שלח, או עבור לטאב המשמרות שלי לבקשת החלפה.";

        // Cycle 3: Worker Schedule & Request Swap state
        private ObservableCollection<WorkerShiftAssignmentDto> _myAssignments = new();
        private WorkerShiftAssignmentDto? _selectedAssignment;
        private ObservableCollection<CoworkerOptionDto> _coworkers = new();
        private CoworkerOptionDto? _selectedCoworker;
        private string _swapReason = string.Empty;
        private string? _swapValidationError = "בחר משמרת משובצת, עובד מחליף, והזן סיבת החלפה (לפחות 3 תווים).";
        private bool _isBusy;

        public WorkerAvailabilityViewModel()
        {
            _apiClient = ShiftlyApiClient.Instance;

            SubmitCommand = new RelayCommand(async _ => await SubmitAsync(), _ => CanSubmit());
            RefreshScheduleCommand = new RelayCommand(async _ => await LoadScheduleAsync(), _ => !IsBusy && !IsGuest);
            SubmitSwapCommand = new RelayCommand(async _ => await SubmitSwapAsync(), _ => CanSubmitSwap());
            LogoutCommand = new RelayCommand(_ => Logout());

            if (IsGuest)
            {
                StatusMessage = "מצב אורח (Guest Demo): צפייה בלבד — שליחת אילוצים ובקשות החלפה חסומות (SEC-M07/SEC-M10).";
                SwapValidationError = "אורח במצב הדגמה אינו מורשה להגיש בקשות החלפה.";
            }
            else
            {
                _ = LoadScheduleAsync();
            }
        }

        public bool IsGuest =>
            _apiClient.CurrentUser == null ||
            _apiClient.CurrentUser.UserId <= 0 ||
            _apiClient.CurrentUser.RoleName == nameof(UserRoleType.Guest);

        public Visibility GuestBannerVisibility => IsGuest ? Visibility.Visible : Visibility.Collapsed;

        public string CurrentUserHeader => _apiClient.CurrentUser != null
            ? $"מחובר: {_apiClient.CurrentUser.FullName} ({_apiClient.CurrentUser.RoleName})"
            : "אורח";

        // =========================================================================
        // CYCLE 2: AVAILABILITY FORM PROPERTIES
        // =========================================================================

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
                {
                    ((RelayCommand)SubmitCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public bool CanWorkEvening
        {
            get => _canWorkEvening;
            set
            {
                if (SetProperty(ref _canWorkEvening, value))
                {
                    ((RelayCommand)SubmitCommand).RaiseCanExecuteChanged();
                }
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

        // =========================================================================
        // CYCLE 3: WORKER SCHEDULE & REQUEST SWAP PROPERTIES
        // =========================================================================

        public ObservableCollection<WorkerShiftAssignmentDto> MyAssignments
        {
            get => _myAssignments;
            set => SetProperty(ref _myAssignments, value);
        }

        public WorkerShiftAssignmentDto? SelectedAssignment
        {
            get => _selectedAssignment;
            set
            {
                if (SetProperty(ref _selectedAssignment, value))
                {
                    OnPropertyChanged(nameof(SelectedAssignmentLabel));
                    ValidateSwapInput();
                }
            }
        }

        public string SelectedAssignmentLabel => SelectedAssignment != null
            ? $"משמרת נבחרת: {SelectedAssignment.ShiftDate:dd/MM/yyyy} ({SelectedAssignment.ShiftType})" +
              (SelectedAssignment.HasPendingSwap ? " — קיימת בקשת החלפה ממתינה" : string.Empty)
            : "בחר משמרת משובצת מהטבלה";

        public ObservableCollection<CoworkerOptionDto> Coworkers
        {
            get => _coworkers;
            set => SetProperty(ref _coworkers, value);
        }

        public CoworkerOptionDto? SelectedCoworker
        {
            get => _selectedCoworker;
            set
            {
                if (SetProperty(ref _selectedCoworker, value))
                {
                    ValidateSwapInput();
                }
            }
        }

        public string SwapReason
        {
            get => _swapReason;
            set
            {
                if (SetProperty(ref _swapReason, value))
                {
                    ValidateSwapInput();
                }
            }
        }

        public string? SwapValidationError
        {
            get => _swapValidationError;
            set => SetProperty(ref _swapValidationError, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    RaiseCommandStates();
                }
            }
        }

        public ICommand SubmitCommand { get; }
        public ICommand RefreshScheduleCommand { get; }
        public ICommand SubmitSwapCommand { get; }
        public ICommand LogoutCommand { get; }

        private bool CanSubmit() => !IsBusy && !IsGuest && (CanWorkMorning || CanWorkEvening);

        private bool CanSubmitSwap() =>
            !IsBusy &&
            !IsGuest &&
            SelectedAssignment != null &&
            !SelectedAssignment.HasPendingSwap &&
            SelectedCoworker != null &&
            string.IsNullOrEmpty(SwapValidationError);

        private void ValidateSwapInput()
        {
            if (IsGuest)
            {
                SwapValidationError = "אורח במצב הדגמה אינו מורשה להגיש בקשות החלפה.";
            }
            else if (SelectedAssignment == null)
            {
                SwapValidationError = "יש לבחור משמרת משובצת מהרשימה.";
            }
            else if (SelectedAssignment.HasPendingSwap)
            {
                SwapValidationError = "כבר קיימת בקשת החלפה ממתינה עבור משמרת זו.";
            }
            else if (SelectedCoworker == null)
            {
                SwapValidationError = "יש לבחור עובד מחליף מהרשימה.";
            }
            else if (string.IsNullOrWhiteSpace(SwapReason) || SwapReason.Trim().Length < 3)
            {
                SwapValidationError = "חובה להזין סיבת החלפה (לפחות 3 תווים).";
            }
            else if (SwapReason.Trim().Length > 255)
            {
                SwapValidationError = "סיבת ההחלפה ארוכה מדי (מקסימום 255 תווים).";
            }
            else
            {
                SwapValidationError = null;
            }

            RaiseCommandStates();
        }

        private void RaiseCommandStates()
        {
            ((RelayCommand)SubmitCommand).RaiseCanExecuteChanged();
            ((RelayCommand)RefreshScheduleCommand).RaiseCanExecuteChanged();
            ((RelayCommand)SubmitSwapCommand).RaiseCanExecuteChanged();
        }

        private async Task SubmitAsync()
        {
            if (!CanSubmit()) return;

            IsBusy = true;
            StatusMessage = "שולח אילוצים לשרת...";
            var dto = new WorkerAvailabilityDTO
            {
                UserId = _apiClient.CurrentUser?.UserId ?? 0,
                TargetDate = TargetDate,
                CanWorkMorning = CanWorkMorning,
                CanWorkEvening = CanWorkEvening,
                Notes = Notes
            };

            bool success = await _apiClient.SubmitAvailabilityAsync(dto);
            IsBusy = false;

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

        private async Task LoadScheduleAsync()
        {
            if (IsGuest) return;

            IsBusy = true;
            var assignmentsResult = await _apiClient.GetWorkerAssignmentsAsync();
            var coworkersResult = await _apiClient.GetSwapTargetWorkersAsync();
            IsBusy = false;

            if (!assignmentsResult.Success)
            {
                HandleAuthOrErrorStatus(assignmentsResult.StatusCode, assignmentsResult.Message);
                return;
            }

            if (!coworkersResult.Success)
            {
                HandleAuthOrErrorStatus(coworkersResult.StatusCode, coworkersResult.Message);
                return;
            }

            MyAssignments = new ObservableCollection<WorkerShiftAssignmentDto>(assignmentsResult.Data ?? new());
            Coworkers = new ObservableCollection<CoworkerOptionDto>(coworkersResult.Data ?? new());

            if (SelectedCoworker == null && Coworkers.Count > 0)
            {
                SelectedCoworker = Coworkers[0];
            }

            StatusMessage = $"נטענו {MyAssignments.Count} משמרות משובצות אישיות.";
            ValidateSwapInput();
        }

        private async Task SubmitSwapAsync()
        {
            ValidateSwapInput();
            if (!CanSubmitSwap() || SelectedAssignment == null || SelectedCoworker == null) return;

            IsBusy = true;
            StatusMessage = "שולח בקשת החלפת משמרת לשרת...";

            var dto = new ShiftSwapRequestCreateDto
            {
                AssignmentId = SelectedAssignment.AssignmentId,
                RequesterUserId = _apiClient.CurrentUser?.UserId ?? 0,
                TargetUserId = SelectedCoworker.UserId,
                Reason = SwapReason.Trim()
            };

            var result = await _apiClient.SubmitShiftSwapAsync(dto);
            IsBusy = false;

            if (result.Success)
            {
                SwapReason = string.Empty;
                SelectedAssignment = null;
                StatusMessage = result.Message;
                await LoadScheduleAsync();
            }
            else
            {
                HandleAuthOrErrorStatus(result.StatusCode, result.Message);
            }
        }

        private void HandleAuthOrErrorStatus(int statusCode, string message)
        {
            if (statusCode == 401 || statusCode == 403)
            {
                StatusMessage = $"חסימת אבטחה מהשרת ({statusCode}): {message}";
                MessageBox.Show(
                    message,
                    statusCode == 401 ? "נדרש אימות (401 Unauthorized)" : "אין הרשאה (403 Forbidden)",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            else
            {
                StatusMessage = $"שגיאה: {message}";
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
