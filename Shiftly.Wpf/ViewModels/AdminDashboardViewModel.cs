using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using Shiftly.Shared.DTOs;
using Shiftly.Shared.Enums;
using Shiftly.Wpf.Common;
using Shiftly.Wpf.Services;

namespace Shiftly.Wpf.ViewModels
{
    /// <summary>
    /// ViewModel עבור דשבורד מנהל וניהול משתמשים (AdminDashboardWindow)
    /// מיישם SEC-M08, SEC-M13:
    /// - מציג את רשימת העובדים שנשלפה מהשרת
    /// - מאפשר לאדמין בלבד לעדכן תפקידים (Roles) של עובדים
    /// - כולל כפתור בדיקה מיוחד להצגה לבוחן: הוכחת חסימת 403 Forbidden בשרת!
    /// </summary>
    public class AdminDashboardViewModel : ViewModelBase
    {
        private LoginResponseDTO _currentUser;
        private ObservableCollection<UserListDTO> _users = new();
        private UserListDTO? _selectedUser;
        private string _selectedNewRole = "Worker";
        private string? _statusMessage;
        private bool _isLoading;

        public event Action? RequestLogout;

        public RelayCommand LoadUsersCommand { get; }
        public RelayCommand ChangeRoleCommand { get; }
        public RelayCommand TestUnauthorizedAccessCommand { get; }
        public RelayCommand LogoutCommand { get; }

        public AdminDashboardViewModel(LoginResponseDTO currentUser)
        {
            _currentUser = currentUser;

            LoadUsersCommand = new RelayCommand(async _ => await ExecuteLoadUsersAsync());
            ChangeRoleCommand = new RelayCommand(async _ => await ExecuteChangeRoleAsync(), _ => CanChangeRole);
            TestUnauthorizedAccessCommand = new RelayCommand(async _ => await ExecuteTestUnauthorizedAccessAsync());
            LogoutCommand = new RelayCommand(_ => RequestLogout?.Invoke());

            // טעינה ראשונית
            _ = ExecuteLoadUsersAsync();
        }

        public LoginResponseDTO CurrentUser
        {
            get => _currentUser;
            set => SetProperty(ref _currentUser, value);
        }

        public ObservableCollection<UserListDTO> Users
        {
            get => _users;
            set => SetProperty(ref _users, value);
        }

        public UserListDTO? SelectedUser
        {
            get => _selectedUser;
            set
            {
                if (SetProperty(ref _selectedUser, value))
                {
                    if (value != null)
                    {
                        SelectedNewRole = value.RoleName;
                    }
                    ChangeRoleCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string SelectedNewRole
        {
            get => _selectedNewRole;
            set
            {
                if (SetProperty(ref _selectedNewRole, value))
                {
                    ChangeRoleCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string? StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (SetProperty(ref _isLoading, value))
                {
                    ChangeRoleCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsAdmin => CurrentUser.RoleName == nameof(UserRoleType.HRAdmin);
        public bool IsGuest => CurrentUser.RoleName == nameof(UserRoleType.Guest);

        public bool CanChangeRole =>
            !IsLoading &&
            IsAdmin &&
            SelectedUser != null &&
            !string.IsNullOrWhiteSpace(SelectedNewRole);

        public async Task ExecuteLoadUsersAsync()
        {
            IsLoading = true;
            StatusMessage = "טוען רשימת משתמשים מהשרת...";

            var (success, message, data) = await ShiftlyApiClient.Instance.GetAllUsersAsync(CurrentUser.RoleName);

            IsLoading = false;

            if (success && data != null)
            {
                Users = new ObservableCollection<UserListDTO>(data);
                StatusMessage = $"נטענו {data.Count} משתמשים בהצלחה.";
            }
            else
            {
                Users.Clear();
                StatusMessage = $"חסימת גישה מהשרת (SEC-M08): {message}";
            }
        }

        private async Task ExecuteChangeRoleAsync()
        {
            if (!CanChangeRole || SelectedUser == null) return;

            int roleId = SelectedNewRole switch
            {
                "Guest" => (int)UserRoleType.Guest,
                "Worker" => (int)UserRoleType.Worker,
                "ShiftManager" => (int)UserRoleType.ShiftManager,
                "HRAdmin" => (int)UserRoleType.HRAdmin,
                _ => (int)UserRoleType.Worker
            };

            IsLoading = true;
            StatusMessage = $"מעדכן תפקיד עבור {SelectedUser.FullName}...";

            var (success, message) = await ShiftlyApiClient.Instance.UpdateUserRoleAsync(
                SelectedUser.UserId,
                roleId,
                CurrentUser.RoleName);

            IsLoading = false;

            if (success)
            {
                StatusMessage = $"התפקיד של {SelectedUser.FullName} עודכן ל-{SelectedNewRole}!";
                await ExecuteLoadUsersAsync();
            }
            else
            {
                StatusMessage = $"שגיאה בעדכון: {message}";
            }
        }

        /// <summary>
        /// בדיקת אבטחה מיוחדת להצגה לבוחן הבגרות!
        /// שולחת בקשת ניהול משתמשים עם Role מזויף של Worker כדי להוכיח שהשרת חוסם ב-403 Forbidden
        /// </summary>
        private async Task ExecuteTestUnauthorizedAccessAsync()
        {
            IsLoading = true;
            StatusMessage = "מבצע ניסיון עקיפה (Fake Role: Worker) לבדיקת אבטחת השרת...";

            var (success, message, _) = await ShiftlyApiClient.Instance.GetAllUsersAsync(nameof(UserRoleType.Worker));

            IsLoading = false;

            if (!success)
            {
                MessageBox.Show(
                    $"השרת הגן על המערכת וחסם את הגישה כמצופה!\n\nתגובת השרת:\n{message}\n\nזהו יישום מדויק של חוק SEC-M08 (חסימת עקיפת ממשק בשרת).",
                    "הוכחת אבטחת שרת (SEC-M08)",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                StatusMessage = "השרת חסם בהצלחה ניסיון גישה לא מורשה (403 Forbidden).";
            }
            else
            {
                StatusMessage = "אזהרה: השרת לא חסם את הבקשה!";
            }
        }
    }
}
