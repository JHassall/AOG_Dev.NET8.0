using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AOG_WPF.ViewModels
{
    /// <summary>
    /// Base ViewModel for MVVM Pattern Implementation
    /// =============================================
    /// 
    /// This base class provides the foundation for all ViewModels in the WPF 3D migration.
    /// It implements INotifyPropertyChanged for data binding and provides common functionality
    /// needed across all agricultural data ViewModels.
    /// 
    /// Migration Notes:
    /// - Replaces direct property access pattern from Windows Forms version
    /// - Enables real-time UI updates through data binding
    /// - Provides consistent property change notification across all ViewModels
    /// 
    /// ABLS Integration:
    /// - Supports real-time updates for GPS position, boom angles, and sensor data
    /// - Enables smooth UI transitions when switching between camera views
    /// - Provides foundation for terrain and DEM data binding
    /// </summary>
    public abstract class BaseViewModel : INotifyPropertyChanged
    {
        #region INotifyPropertyChanged Implementation

        /// <summary>
        /// Property Changed Event
        /// =====================
        /// 
        /// Raised when a property value changes, enabling WPF data binding to update the UI.
        /// Critical for real-time agricultural data visualization including:
        /// - GPS position updates
        /// - Boom position changes
        /// - Sensor readings
        /// - Field coverage updates
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Raise Property Changed Event
        /// ===========================
        /// 
        /// Protected method to notify the UI that a property has changed.
        /// Uses CallerMemberName attribute to automatically get the property name.
        /// 
        /// Usage Example:
        /// set { _latitude = value; OnPropertyChanged(); }
        /// 
        /// Parameters:
        /// - propertyName: Name of the property that changed (automatically provided)
        /// </summary>
        /// <param name="propertyName">Name of the property that changed</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Set Property with Change Notification
        /// ====================================
        /// 
        /// Helper method to set a property value and raise PropertyChanged if the value actually changes.
        /// This prevents unnecessary UI updates and provides consistent behavior across all ViewModels.
        /// 
        /// Migration Benefits:
        /// - Reduces UI update overhead for high-frequency GPS/sensor data
        /// - Provides consistent property setting pattern across all ViewModels
        /// - Automatically handles change detection and notification
        /// 
        /// Usage Example:
        /// set { SetProperty(ref _latitude, value); }
        /// 
        /// Parameters:
        /// - field: Reference to the backing field
        /// - value: New value to set
        /// - propertyName: Name of the property (automatically provided)
        /// 
        /// Returns:
        /// - true if the value was changed and PropertyChanged was raised
        /// - false if the value was the same and no change occurred
        /// </summary>
        /// <typeparam name="T">Type of the property</typeparam>
        /// <param name="field">Reference to the backing field</param>
        /// <param name="value">New value to set</param>
        /// <param name="propertyName">Name of the property that changed</param>
        /// <returns>True if the property was changed</returns>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            // Check if the value is actually different
            if (Equals(field, value))
            {
                return false;
            }

            // Set the new value and notify of the change
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion

        #region Common Properties

        /// <summary>
        /// Backing field for IsLoading property
        /// </summary>
        private bool _isLoading;

        /// <summary>
        /// Is Loading Property
        /// ==================
        /// 
        /// Indicates whether this ViewModel is currently loading data.
        /// Useful for showing loading indicators during:
        /// - GPS connection establishment
        /// - DEM data loading
        /// - Field boundary calculation
        /// - ABLS sensor calibration
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        /// <summary>
        /// Backing field for ErrorMessage property
        /// </summary>
        private string? _errorMessage;

        /// <summary>
        /// Error Message Property
        /// =====================
        /// 
        /// Contains any error message that should be displayed to the user.
        /// Used for communicating issues with:
        /// - GPS connection failures
        /// - Sensor communication errors
        /// - DEM loading problems
        /// - ABLS system faults
        /// </summary>
        public string? ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        /// <summary>
        /// Backing field for IsEnabled property
        /// </summary>
        private bool _isEnabled = true;

        /// <summary>
        /// Is Enabled Property
        /// ==================
        /// 
        /// Indicates whether this ViewModel and its associated UI elements should be enabled.
        /// Used to disable controls during:
        /// - System initialization
        /// - Critical operations
        /// - Error conditions
        /// - ABLS safety interlocks
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Clear Error Message
        /// ==================
        /// 
        /// Helper method to clear any existing error message.
        /// Typically called when starting a new operation or when an error condition is resolved.
        /// </summary>
        protected void ClearError()
        {
            ErrorMessage = null;
        }

        /// <summary>
        /// Set Error Message
        /// ================
        /// 
        /// Helper method to set an error message and optionally disable the ViewModel.
        /// 
        /// Parameters:
        /// - message: Error message to display
        /// - disableViewModel: Whether to disable the ViewModel (default: false)
        /// </summary>
        /// <param name="message">Error message to display</param>
        /// <param name="disableViewModel">Whether to disable the ViewModel</param>
        protected void SetError(string message, bool disableViewModel = false)
        {
            ErrorMessage = message;
            if (disableViewModel)
            {
                IsEnabled = false;
            }
        }

        #endregion
    }
}
