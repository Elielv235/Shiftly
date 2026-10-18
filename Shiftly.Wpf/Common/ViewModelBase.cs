using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Shiftly.Wpf.Common
{
    /// <summary>
    /// מחלקת בסיס לכל ה-ViewModels במערכת (מימוש תבנית MVVM)
    /// מטפלת בעדכון ממשק המשתמש (Data Binding) באמצעות ממשק INotifyPropertyChanged
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(storage, value))
                return false;

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
