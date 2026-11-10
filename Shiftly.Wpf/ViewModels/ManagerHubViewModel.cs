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
    /// ViewModel for ManagerHubWindow (Cycle 3: ShiftSwapRequests & SupplyOrders).
    /// Enforces real-time (RT) client validation while relying on server-side 401/403 enforcement.
    /// Zero direct database access.
    /// </summary>
    public class ManagerHubViewModel : ViewModelBase
    {
        private readonly ShiftlyApiClient _apiClient;
        private ObservableCollection<ShiftSwapItemDto> _pendingSwaps = new();
        private ShiftSwapItemDto? _selectedSwap;
        private ObservableCollection<SupplyOrderItemDto> _supplyOrders = new();
        private string _itemsSummary = string.Empty;
        private string _orderNotes = string.Empty;
        private string? _itemsSummaryValidationError = "חובה להזין פירוט פריטים להזמנה (לפחות 3 תווים).";
        private string _statusMessage = "טוען נתוני מרכז ניהול סבב 3...";
        private bool _isBusy;

        public ManagerHubViewModel()
        {
            _apiClient = ShiftlyApiClient.Instance;

            RefreshAllCommand = new RelayCommand(async _ => await LoadAllAsync(), _ => !IsBusy);
            ApproveSwapCommand = new RelayCommand(async _ => await ReviewSwapAsync(true), _ => CanReviewSwap);
            RejectSwapCommand = new RelayCommand(async _ => await ReviewSwapAsync(false), _ => CanReviewSwap);
            CreateSupplyOrderCommand = new RelayCommand(async _ => await CreateSupplyOrderAsync(), _ => CanCreateSupplyOrder);
            OpenAdminDashboardCommand = new RelayCommand(_ => OpenAdminDashboard(), _ => IsHRAdmin);
            LogoutCommand = new RelayCommand(_ => Logout());

            _ = LoadAllAsync();
        }

        public string CurrentUserHeader => _apiClient.CurrentUser != null
            ? $"מחובר: {_apiClient.CurrentUser.FullName} ({_apiClient.CurrentUser.RoleName})"
            : "לא מחובר";

        public bool IsManagerRole =>
            _apiClient.CurrentUser?.RoleName == nameof(UserRoleType.ShiftManager) ||
            _apiClient.CurrentUser?.RoleName == nameof(UserRoleType.HRAdmin);

        public bool IsHRAdmin => _apiClient.CurrentUser?.RoleName == nameof(UserRoleType.HRAdmin);

        public Visibility AdminBackButtonVisibility => IsHRAdmin ? Visibility.Visible : Visibility.Collapsed;

        public ObservableCollection<ShiftSwapItemDto> PendingSwaps
        {
            get => _pendingSwaps;
            set => SetProperty(ref _pendingSwaps, value);
        }

        public ShiftSwapItemDto? SelectedSwap
        {
            get => _selectedSwap;
            set
            {
                if (SetProperty(ref _selectedSwap, value))
                {
                    OnPropertyChanged(nameof(SelectedSwapSummary));
                    RaiseCommandStates();
                }
            }
        }

        public string SelectedSwapSummary => SelectedSwap != null
            ? $"בקשה #{SelectedSwap.SwapId}: {SelectedSwap.RequesterName} ⇄ {SelectedSwap.TargetWorkerName} ({SelectedSwap.ShiftDate:dd/MM/yyyy} - {SelectedSwap.ShiftType})"
            : "בחר בקשת החלפה ממתינה מהרשימה כדי לאשר או לדחות.";

        public ObservableCollection<SupplyOrderItemDto> SupplyOrders
        {
            get => _supplyOrders;
            set => SetProperty(ref _supplyOrders, value);
        }

        public string ItemsSummary
        {
            get => _itemsSummary;
            set
            {
                if (SetProperty(ref _itemsSummary, value))
                {
                    ValidateItemsSummary();
                    RaiseCommandStates();
                }
            }
        }

        public string OrderNotes
        {
            get => _orderNotes;
            set
            {
                if (SetProperty(ref _orderNotes, value))
                {
                    ValidateItemsSummary();
                    RaiseCommandStates();
                }
            }
        }

        public string? ItemsSummaryValidationError
        {
            get => _itemsSummaryValidationError;
            set => SetProperty(ref _itemsSummaryValidationError, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
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

        public bool CanReviewSwap => !IsBusy && IsManagerRole && SelectedSwap != null;

        public bool CanCreateSupplyOrder =>
            !IsBusy &&
            IsManagerRole &&
            string.IsNullOrEmpty(ItemsSummaryValidationError) &&
            !string.IsNullOrWhiteSpace(ItemsSummary);

        public ICommand RefreshAllCommand { get; }
        public ICommand ApproveSwapCommand { get; }
        public ICommand RejectSwapCommand { get; }
        public ICommand CreateSupplyOrderCommand { get; }
        public ICommand OpenAdminDashboardCommand { get; }
        public ICommand LogoutCommand { get; }

        private void ValidateItemsSummary()
        {
            string trimmed = ItemsSummary?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                ItemsSummaryValidationError = "חובה להזין פירוט פריטים להזמנה (לפחות 3 תווים).";
            }
            else if (trimmed.Length < 3)
            {
                ItemsSummaryValidationError = "פירוט הפריטים קצר מדי — נדרשים לפחות 3 תווים.";
            }
            else if (trimmed.Length > 500)
            {
                ItemsSummaryValidationError = "פירוט הפריטים חורג מהמגבלה (מקסימום 500 תווים).";
            }
            else if (!string.IsNullOrEmpty(OrderNotes) && OrderNotes.Trim().Length > 255)
            {
                ItemsSummaryValidationError = "ההערות חורגות מהמגבלה (מקסימום 255 תווים).";
            }
            else
            {
                ItemsSummaryValidationError = null;
            }
        }

        private void RaiseCommandStates()
        {
            ((RelayCommand)RefreshAllCommand).RaiseCanExecuteChanged();
            ((RelayCommand)ApproveSwapCommand).RaiseCanExecuteChanged();
            ((RelayCommand)RejectSwapCommand).RaiseCanExecuteChanged();
            ((RelayCommand)CreateSupplyOrderCommand).RaiseCanExecuteChanged();
            ((RelayCommand)OpenAdminDashboardCommand).RaiseCanExecuteChanged();
        }

        private async Task LoadAllAsync()
        {
            IsBusy = true;
            StatusMessage = "טוען בקשות החלפה והזמנות מלאי מהשרת...";

            var swapsResult = await _apiClient.GetPendingShiftSwapsAsync();
            var ordersResult = await _apiClient.GetSupplyOrdersAsync();

            IsBusy = false;

            if (!swapsResult.Success)
            {
                PendingSwaps.Clear();
                HandleAuthOrErrorStatus(swapsResult.StatusCode, swapsResult.Message);
                return;
            }

            if (!ordersResult.Success)
            {
                SupplyOrders.Clear();
                HandleAuthOrErrorStatus(ordersResult.StatusCode, ordersResult.Message);
                return;
            }

            PendingSwaps = new ObservableCollection<ShiftSwapItemDto>(swapsResult.Data ?? new());
            SupplyOrders = new ObservableCollection<SupplyOrderItemDto>(ordersResult.Data ?? new());
            StatusMessage = $"נטענו {PendingSwaps.Count} בקשות החלפה ממתינות ו-{SupplyOrders.Count} הזמנות מלאי.";
        }

        private async Task ReviewSwapAsync(bool isApproved)
        {
            if (SelectedSwap == null) return;

            IsBusy = true;
            StatusMessage = isApproved ? "מאשר בקשת החלפה ומעדכן שיבוץ בשרת..." : "דוחה בקשת החלפה בשרת...";

            var dto = new ShiftSwapReviewDto
            {
                SwapId = SelectedSwap.SwapId,
                IsApproved = isApproved,
                ReviewerUserId = _apiClient.CurrentUser?.UserId ?? 0
            };

            var result = await _apiClient.ReviewShiftSwapAsync(dto);
            IsBusy = false;

            if (result.Success)
            {
                StatusMessage = result.Message;
                SelectedSwap = null;
                await LoadAllAsync();
            }
            else
            {
                HandleAuthOrErrorStatus(result.StatusCode, result.Message);
            }
        }

        private async Task CreateSupplyOrderAsync()
        {
            ValidateItemsSummary();
            if (!CanCreateSupplyOrder) return;

            IsBusy = true;
            StatusMessage = "שולח הזמנת מלאי חדשה לשרת...";

            var dto = new SupplyOrderCreateDto
            {
                CreatedByUserId = _apiClient.CurrentUser?.UserId ?? 0,
                ItemsSummary = ItemsSummary.Trim(),
                Notes = string.IsNullOrWhiteSpace(OrderNotes) ? null : OrderNotes.Trim()
            };

            var result = await _apiClient.CreateSupplyOrderAsync(dto);
            IsBusy = false;

            if (result.Success)
            {
                ItemsSummary = string.Empty;
                OrderNotes = string.Empty;
                StatusMessage = result.Message;
                await LoadAllAsync();
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

        private void OpenAdminDashboard()
        {
            var adminWin = new Views.AdminDashboardWindow();
            adminWin.Show();
            Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w is Views.ManagerHubWindow)?.Close();
        }

        private void Logout()
        {
            _apiClient.Logout();
            var loginWin = new Views.LoginWindow();
            loginWin.Show();
            Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w is Views.ManagerHubWindow)?.Close();
        }
    }
}
