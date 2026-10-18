using System;
using System.Threading.Tasks;
using System.Windows;
using Shiftly.Shared.DTOs;
using Shiftly.Shared.Enums;
using Shiftly.Wpf.Common;
using Shiftly.Wpf.Services;

namespace Shiftly.Wpf.ViewModels
{
    /// <summary>
    /// ViewModel עבור מסך ההתחברות (LoginWindow)
    /// מיישם שלוש שכבות אבטחה: אימות בזמן אמת, חסימת כפתור ב-CanExecute, ובדיקה בשרת
    /// </summary>
    public class LoginViewModel : ViewModelBase
    {
        private string _username = string.Empty;
        private string _password = string.Empty;
        private string? _usernameError;
        private string? _passwordError;
        private string? _statusMessage;
        private bool _isLoading;

        public event Action? RequestClose;
        public event Action? RequestOpenRegister;
        public event Action<LoginResponseDTO>? RequestOpenDashboard;

        public RelayCommand LoginCommand { get; }
        public RelayCommand OpenRegisterCommand { get; }
        public RelayCommand GuestDemoCommand { get; }

        public LoginViewModel()
        {
            LoginCommand = new RelayCommand(async _ => await ExecuteLoginAsync(), _ => CanLogin);
            OpenRegisterCommand = new RelayCommand(_ => RequestOpenRegister?.Invoke());
            GuestDemoCommand = new RelayCommand(ExecuteGuestDemo);
        }

        public string Username
        {
            get => _username;
            set
            {
                if (SetProperty(ref _username, value))
                {
                    ValidateUsername();
                    LoginCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string Password
        {
            get => _password;
            set
            {
                if (SetProperty(ref _password, value))
                {
                    ValidatePassword();
                    LoginCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string? UsernameError
        {
            get => _usernameError;
            set => SetProperty(ref _usernameError, value);
        }

        public string? PasswordError
        {
            get => _passwordError;
            set => SetProperty(ref _passwordError, value);
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
                    LoginCommand.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// שכבה 2: חסימת שליחה בלקוח (Pre-Submit Block)
        /// כפתור ההתחברות יהיה מושבת לחלוטין כל עוד הקלט אינו תקין
        /// </summary>
        public bool CanLogin =>
            !IsLoading &&
            !string.IsNullOrWhiteSpace(Username) &&
            !string.IsNullOrWhiteSpace(Password) &&
            string.IsNullOrEmpty(UsernameError) &&
            string.IsNullOrEmpty(PasswordError);

        /// <summary>
        /// שכבה 1: אימות קלט בזמן אמת (Real-Time Validation) בעת ההקלדה
        /// </summary>
        private void ValidateUsername()
        {
            if (string.IsNullOrWhiteSpace(Username))
            {
                UsernameError = "שם משתמש הוא שדה חובה.";
            }
            else if (Username.Trim().Length < 3)
            {
                UsernameError = "שם משתמש חייב להכיל לפחות 3 תווים.";
            }
            else
            {
                UsernameError = null;
            }
        }

        private void ValidatePassword()
        {
            if (string.IsNullOrWhiteSpace(Password))
            {
                PasswordError = "סיסמה היא שדה חובה.";
            }
            else
            {
                PasswordError = null;
            }
        }

        private async Task ExecuteLoginAsync()
        {
            if (!CanLogin) return;

            IsLoading = true;
            StatusMessage = "מאמת נתונים מול שרת ה-API...";

            var request = new LoginRequestDTO
            {
                Username = Username.Trim(),
                Password = Password
            };

            var (success, message, data) = await ShiftlyApiClient.Instance.LoginAsync(request);

            IsLoading = false;

            if (success && data != null)
            {
                StatusMessage = $"שלום {data.FullName}! מחובר כ-{data.RoleName}.";
                RequestOpenDashboard?.Invoke(data);
                RequestClose?.Invoke();
            }
            else
            {
                StatusMessage = $"שגיאת כניסה: {message}";
            }
        }

        /// <summary>
        /// כניסה אינטראקטיבית במצב אורח (Guest Mode) לבדיקת הבוחן
        /// </summary>
        private void ExecuteGuestDemo()
        {
            var guestUser = new LoginResponseDTO
            {
                UserId = 0,
                Username = "guest",
                FullName = "אורח / מצב הדגמה (Demo)",
                RoleName = nameof(UserRoleType.Guest)
            };

            ShiftlyApiClient.Instance.SetCurrentUser(guestUser);
            StatusMessage = "מחובר במצב אורח (דמו בלבד).";
            RequestOpenDashboard?.Invoke(guestUser);
            RequestClose?.Invoke();
        }
    }
}
