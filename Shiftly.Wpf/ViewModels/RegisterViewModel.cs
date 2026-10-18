using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Shiftly.Shared.DTOs;
using Shiftly.Wpf.Common;
using Shiftly.Wpf.Services;

namespace Shiftly.Wpf.ViewModels
{
    /// <summary>
    /// ViewModel עבור מסך הרשמת עובד חדש (RegisterWindow)
    /// מיישם SEC-M01, SEC-M02, SEC-M11:
    /// 1. ולידציה מיידית בזמן אמת לכל שדה בנפרד (Real-Time Validation)
    /// 2. חסימת כפתור הרשמה עד שכל השדות תקינים ב-100% (Pre-Submit Block)
    /// 3. אין בחירת Role בטופס! השרת קובע תמיד עובד רגיל (Worker) כברירת מחדל
    /// </summary>
    public class RegisterViewModel : ViewModelBase
    {
        private string _username = string.Empty;
        private string _password = string.Empty;
        private string _confirmPassword = string.Empty;
        private string _fullName = string.Empty;
        private string _email = string.Empty;
        private string _phoneNumber = string.Empty;

        private string? _usernameError;
        private string? _passwordError;
        private string? _confirmPasswordError;
        private string? _fullNameError;
        private string? _emailError;
        private string? _phoneNumberError;

        private string? _statusMessage;
        private bool _isLoading;

        public event Action? RequestClose;
        public RelayCommand RegisterCommand { get; }
        public RelayCommand CancelCommand { get; }

        public RegisterViewModel()
        {
            RegisterCommand = new RelayCommand(async _ => await ExecuteRegisterAsync(), _ => CanRegister);
            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke());
        }

        public string Username
        {
            get => _username;
            set
            {
                if (SetProperty(ref _username, value))
                {
                    ValidateUsername();
                    RegisterCommand.RaiseCanExecuteChanged();
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
                    ValidateConfirmPassword();
                    RegisterCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string ConfirmPassword
        {
            get => _confirmPassword;
            set
            {
                if (SetProperty(ref _confirmPassword, value))
                {
                    ValidateConfirmPassword();
                    RegisterCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string FullName
        {
            get => _fullName;
            set
            {
                if (SetProperty(ref _fullName, value))
                {
                    ValidateFullName();
                    RegisterCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string Email
        {
            get => _email;
            set
            {
                if (SetProperty(ref _email, value))
                {
                    ValidateEmail();
                    RegisterCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string PhoneNumber
        {
            get => _phoneNumber;
            set
            {
                if (SetProperty(ref _phoneNumber, value))
                {
                    ValidatePhoneNumber();
                    RegisterCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string? UsernameError { get => _usernameError; set => SetProperty(ref _usernameError, value); }
        public string? PasswordError { get => _passwordError; set => SetProperty(ref _passwordError, value); }
        public string? ConfirmPasswordError { get => _confirmPasswordError; set => SetProperty(ref _confirmPasswordError, value); }
        public string? FullNameError { get => _fullNameError; set => SetProperty(ref _fullNameError, value); }
        public string? EmailError { get => _emailError; set => SetProperty(ref _emailError, value); }
        public string? PhoneNumberError { get => _phoneNumberError; set => SetProperty(ref _phoneNumberError, value); }

        public string? StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (SetProperty(ref _isLoading, value))
                {
                    RegisterCommand.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// שכבה 2: חסימת כפתור הרשמה עד שכל השדות מאומתים
        /// </summary>
        public bool CanRegister =>
            !IsLoading &&
            !string.IsNullOrWhiteSpace(Username) &&
            !string.IsNullOrWhiteSpace(Password) &&
            !string.IsNullOrWhiteSpace(ConfirmPassword) &&
            !string.IsNullOrWhiteSpace(FullName) &&
            !string.IsNullOrWhiteSpace(Email) &&
            !string.IsNullOrWhiteSpace(PhoneNumber) &&
            UsernameError == null &&
            PasswordError == null &&
            ConfirmPasswordError == null &&
            FullNameError == null &&
            EmailError == null &&
            PhoneNumberError == null;

        private void ValidateUsername()
        {
            if (string.IsNullOrWhiteSpace(Username))
                UsernameError = "שם משתמש הוא שדה חובה.";
            else if (Username.Trim().Length < 3)
                UsernameError = "שם משתמש חייב להכיל לפחות 3 תווים.";
            else
                UsernameError = null;
        }

        private void ValidatePassword()
        {
            if (string.IsNullOrWhiteSpace(Password))
                PasswordError = "סיסמה היא שדה חובה.";
            else if (Password.Length < 6)
                PasswordError = "סיסמה חייבת להכיל לפחות 6 תווים.";
            else
                PasswordError = null;
        }

        private void ValidateConfirmPassword()
        {
            if (ConfirmPassword != Password)
                ConfirmPasswordError = "הסיסמאות אינן תואמות.";
            else
                ConfirmPasswordError = null;
        }

        private void ValidateFullName()
        {
            if (string.IsNullOrWhiteSpace(FullName))
                FullNameError = "שם מלא הוא שדה חובה.";
            else if (FullName.Trim().Length < 2)
                FullNameError = "נא להזין שם מלא תקין.";
            else
                FullNameError = null;
        }

        private void ValidateEmail()
        {
            if (string.IsNullOrWhiteSpace(Email))
            {
                EmailError = "כתובת אימייל היא שדה חובה.";
            }
            else
            {
                var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
                if (!emailRegex.IsMatch(Email.Trim()))
                    EmailError = "כתובת אימייל אינה בפורמט תקין.";
                else
                    EmailError = null;
            }
        }

        private void ValidatePhoneNumber()
        {
            if (string.IsNullOrWhiteSpace(PhoneNumber))
            {
                PhoneNumberError = "מספר טלפון הוא שדה חובה.";
            }
            else
            {
                var phoneClean = Regex.Replace(PhoneNumber, @"[^\d]", "");
                if (phoneClean.Length < 9 || phoneClean.Length > 11)
                    PhoneNumberError = "מספר טלפון חייב להכיל 9 עד 11 ספרות.";
                else
                    PhoneNumberError = null;
            }
        }

        private async Task ExecuteRegisterAsync()
        {
            if (!CanRegister) return;

            IsLoading = true;
            StatusMessage = "שולח נתונים לשרת ה-API...";

            var dto = new UserRegisterDTO
            {
                Username = Username.Trim(),
                Password = Password,
                FullName = FullName.Trim(),
                Email = Email.Trim(),
                PhoneNumber = PhoneNumber.Trim()
            };

            var (success, message) = await ShiftlyApiClient.Instance.RegisterAsync(dto);

            IsLoading = false;

            if (success)
            {
                StatusMessage = "ההרשמה הסתיימה בהצלחה! הינך רשום כעובד (Worker).";
                await Task.Delay(1500);
                RequestClose?.Invoke();
            }
            else
            {
                StatusMessage = $"שגיאת הרשמה: {message}";
            }
        }
    }
}
