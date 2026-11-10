using Shiftly.Shared.DTOs;
using Shiftly.Wpf.Common;
using Shiftly.Wpf.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Shiftly.Wpf.ViewModels
{
    public class AdminDashboardViewModel : ViewModelBase
    {
        private readonly ShiftlyApiClient _apiClient;
        private ObservableCollection<UserListDTO> _allUsers = new();
        private ObservableCollection<UserListDTO> _filteredUsers = new();
        private UserListDTO? _selectedUser;
        private string _selectedRole = "Worker";
        private string _searchText = string.Empty;
        private string _statusMessage = "מוכן";
        private DashboardStatsDTO _stats = new();

        public AdminDashboardViewModel()
        {
            _apiClient = ShiftlyApiClient.Instance;
            AvailableRoles = new ObservableCollection<string> { "Worker", "ShiftManager", "HRAdmin" };

            RefreshUsersCommand = new RelayCommand(async _ => await LoadDataAsync());
            SaveRoleCommand = new RelayCommand(async _ => await SaveRoleAsync(), _ => SelectedUser != null);
            ToggleStatusCommand = new RelayCommand(async _ => await ToggleStatusAsync(), _ => SelectedUser != null);
            Test403Command = new RelayCommand(async _ => await Test403Async());
            TriggerAiDialogCommand = new RelayCommand(_ => MessageBox.Show("מודול שיבוץ מבוסס AI מתוכנן לסבב 4 בהתאם להנחיות המנחה זכי חן.", "שיבוץ חכם", MessageBoxButton.OK, MessageBoxImage.Information));
            LogoutCommand = new RelayCommand(_ => Logout());

            _ = LoadDataAsync();
        }

        public AdminDashboardViewModel(LoginResponseDTO _) : this()
        {
        }

        public ObservableCollection<string> AvailableRoles { get; }
        public ObservableCollection<UserListDTO> FilteredUsers
        {
            get => _filteredUsers;
            set => SetProperty(ref _filteredUsers, value);
        }
        public UserListDTO? SelectedUser
        {
            get => _selectedUser;
            set
            {
                if (SetProperty(ref _selectedUser, value))
                {
                    OnPropertyChanged(nameof(SelectedUserDisplayName));
                    if (value != null && AvailableRoles.Contains(value.RoleName))
                        SelectedRole = value.RoleName;
                    ((RelayCommand)SaveRoleCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)ToggleStatusCommand).RaiseCanExecuteChanged();
                }
            }
        }
        public string SelectedRole
        {
            get => _selectedRole;
            set => SetProperty(ref _selectedRole, value);
        }
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                    FilterUsers();
            }
        }
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }
        public DashboardStatsDTO Stats
        {
            get => _stats;
            set => SetProperty(ref _stats, value);
        }
        public string SelectedUserDisplayName => SelectedUser != null ? $"{SelectedUser.FullName} ({SelectedUser.Username})" : "בחר עובד מהרשימה";

        public ICommand RefreshUsersCommand { get; }
        public ICommand SaveRoleCommand { get; }
        public ICommand ToggleStatusCommand { get; }
        public ICommand Test403Command { get; }
        public ICommand TriggerAiDialogCommand { get; }
        public ICommand LogoutCommand { get; }

        private async Task LoadDataAsync()
        {
            StatusMessage = "טוען נתונים מהשרת...";
            var users = await _apiClient.GetUsersAsync();
            _allUsers = new ObservableCollection<UserListDTO>(users);
            FilterUsers();

            var stats = await _apiClient.GetDashboardStatsAsync();
            if (stats != null) Stats = stats;

            StatusMessage = $"נטענו {_allUsers.Count} משתמשים ומדדי סניף עדכניים.";
        }

        private void FilterUsers()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
                FilteredUsers = new ObservableCollection<UserListDTO>(_allUsers);
            else
                FilteredUsers = new ObservableCollection<UserListDTO>(_allUsers.Where(u =>
                    u.FullName.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase) ||
                    u.Username.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase)));
        }

        private async Task SaveRoleAsync()
        {
            if (SelectedUser == null) return;
            StatusMessage = "מעדכן תפקיד...";
            bool success = await _apiClient.UpdateUserRoleAsync(SelectedUser.UserId, SelectedRole);
            if (success)
            {
                SelectedUser.RoleName = SelectedRole;
                FilterUsers();
                StatusMessage = "תפקיד המשתמש עודכן בהצלחה במסד.";
            }
            else StatusMessage = "שגיאה בעדכון התפקיד בשרת.";
        }

        private async Task ToggleStatusAsync()
        {
            if (SelectedUser == null) return;
            bool newStatus = !SelectedUser.IsActive;
            bool success = await _apiClient.UpdateUserStatusAsync(SelectedUser.UserId, newStatus);
            if (success)
            {
                SelectedUser.IsActive = newStatus;
                FilterUsers();
                StatusMessage = $"סטטוס המשתמש שונה ל-{(newStatus ? "פעיל" : "מושעה")}.";
            }
        }

        private async Task Test403Async()
        {
            string res = await _apiClient.Test403ForbiddenAsync();
            MessageBox.Show(res, "בדיקת הרשאות שרת", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void Logout()
        {
            _apiClient.Logout();
            var loginWin = new Views.LoginWindow();
            loginWin.Show();
            Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w is Views.AdminDashboardWindow)?.Close();
        }
    }
}
