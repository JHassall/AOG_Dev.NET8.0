using System;
using System.Windows.Media;

namespace AOG_WPF.ViewModels
{
    /// <summary>
    /// GPS Data ViewModel
    /// ==================
    /// 
    /// Manages GPS position, quality, and connection status for the WPF 3D migration.
    /// This ViewModel replaces the direct GPS property access from the original FormGPS
    /// and provides data binding for real-time GPS information display.
    /// 
    /// Migration Notes:
    /// - Consolidates GPS data from multiple sources in the original AOG codebase
    /// - Provides consistent GPS status indicators and quality metrics
    /// - Enables real-time UI updates through data binding
    /// - Supports both RTK and standard GPS positioning modes
    /// 
    /// ABLS Integration:
    /// - Provides high-precision GPS data for boom positioning calculations
    /// - Supports RTK quality monitoring for centimeter-level accuracy
    /// - Enables GPS-based terrain following and guidance line projection
    /// - Integrates with DEM coordinate system for accurate positioning
    /// </summary>
    public class GPSViewModel : BaseViewModel
    {
        #region Private Fields

        /// <summary>
        /// Backing fields for GPS position properties
        /// </summary>
        private double _latitude;
        private double _longitude;
        private double _altitude;
        private double _speed;
        private double _heading;

        /// <summary>
        /// Backing fields for GPS quality and status properties
        /// </summary>
        private string _qualityDescription = "No Fix";
        private int _satelliteCount;
        private double _horizontalAccuracy;
        private bool _isRTKFixed;
        private bool _isConnected;
        private DateTime _lastUpdateTime;

        /// <summary>
        /// Backing fields for UI display properties
        /// </summary>
        private Brush _statusIndicatorBrush = Brushes.Red;
        private string _connectionStatusText = "Disconnected";

        #endregion

        #region GPS Position Properties

        /// <summary>
        /// Latitude Property (Decimal Degrees)
        /// ===================================
        /// 
        /// Current GPS latitude position in decimal degrees.
        /// Positive values indicate North, negative values indicate South.
        /// 
        /// ABLS Usage:
        /// - Used for DEM coordinate transformation
        /// - Enables precise boom positioning relative to terrain
        /// - Supports guidance line projection onto 3D surface
        /// </summary>
        public double Latitude
        {
            get => _latitude;
            set
            {
                if (SetProperty(ref _latitude, value))
                {
                    // Update formatted display text when latitude changes
                    OnPropertyChanged(nameof(LatitudeText));
                    UpdateLastUpdateTime();
                }
            }
        }

        /// <summary>
        /// Longitude Property (Decimal Degrees)
        /// ====================================
        /// 
        /// Current GPS longitude position in decimal degrees.
        /// Positive values indicate East, negative values indicate West.
        /// 
        /// ABLS Usage:
        /// - Used for DEM coordinate transformation
        /// - Enables precise boom positioning relative to terrain
        /// - Supports guidance line projection onto 3D surface
        /// </summary>
        public double Longitude
        {
            get => _longitude;
            set
            {
                if (SetProperty(ref _longitude, value))
                {
                    // Update formatted display text when longitude changes
                    OnPropertyChanged(nameof(LongitudeText));
                    UpdateLastUpdateTime();
                }
            }
        }

        /// <summary>
        /// Altitude Property (Meters above sea level)
        /// ==========================================
        /// 
        /// Current GPS altitude in meters above mean sea level.
        /// 
        /// ABLS Usage:
        /// - Reference altitude for DEM elevation calculations
        /// - Used in boom height calculations relative to ground
        /// - Supports terrain-following algorithms
        /// </summary>
        public double Altitude
        {
            get => _altitude;
            set
            {
                if (SetProperty(ref _altitude, value))
                {
                    // Update formatted display text when altitude changes
                    OnPropertyChanged(nameof(AltitudeText));
                    UpdateLastUpdateTime();
                }
            }
        }

        /// <summary>
        /// Speed Property (km/h)
        /// =====================
        /// 
        /// Current ground speed in kilometers per hour.
        /// Used for display and coverage rate calculations.
        /// </summary>
        public double Speed
        {
            get => _speed;
            set
            {
                if (SetProperty(ref _speed, value))
                {
                    OnPropertyChanged(nameof(SpeedText));
                    UpdateLastUpdateTime();
                }
            }
        }

        /// <summary>
        /// Heading Property (Degrees)
        /// ==========================
        /// 
        /// Current heading/course in degrees (0-360).
        /// 0 = North, 90 = East, 180 = South, 270 = West.
        /// 
        /// ABLS Usage:
        /// - Used for vehicle orientation in 3D scene
        /// - Supports boom alignment calculations
        /// - Enables proper camera following behavior
        /// </summary>
        public double Heading
        {
            get => _heading;
            set => SetProperty(ref _heading, value);
        }

        #endregion

        #region GPS Quality Properties

        /// <summary>
        /// Quality Description Property
        /// ===========================
        /// 
        /// Human-readable description of current GPS fix quality.
        /// Examples: "No Fix", "2D Fix", "3D Fix", "RTK Float", "RTK Fixed"
        /// </summary>
        public string QualityDescription
        {
            get => _qualityDescription;
            set => SetProperty(ref _qualityDescription, value);
        }

        /// <summary>
        /// Satellite Count Property
        /// ========================
        /// 
        /// Number of satellites currently being used for position calculation.
        /// Higher numbers generally indicate better position accuracy.
        /// </summary>
        public int SatelliteCount
        {
            get => _satelliteCount;
            set
            {
                if (SetProperty(ref _satelliteCount, value))
                {
                    OnPropertyChanged(nameof(SatelliteCountText));
                }
            }
        }

        /// <summary>
        /// Horizontal Accuracy Property (meters)
        /// =====================================
        /// 
        /// Estimated horizontal position accuracy in meters.
        /// Lower values indicate better accuracy.
        /// 
        /// ABLS Critical:
        /// - Must be < 0.1m for RTK fixed mode
        /// - Used to validate boom positioning accuracy
        /// - Determines when ABLS auto mode can be safely engaged
        /// </summary>
        public double HorizontalAccuracy
        {
            get => _horizontalAccuracy;
            set => SetProperty(ref _horizontalAccuracy, value);
        }

        /// <summary>
        /// Is RTK Fixed Property
        /// =====================
        /// 
        /// Indicates whether GPS is in RTK Fixed mode (centimeter-level accuracy).
        /// Critical for ABLS operations requiring high precision.
        /// </summary>
        public bool IsRTKFixed
        {
            get => _isRTKFixed;
            set
            {
                if (SetProperty(ref _isRTKFixed, value))
                {
                    UpdateQualityDescription();
                    UpdateStatusIndicator();
                }
            }
        }

        /// <summary>
        /// Is Connected Property
        /// ====================
        /// 
        /// Indicates whether GPS receiver is connected and communicating.
        /// </summary>
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if (SetProperty(ref _isConnected, value))
                {
                    UpdateConnectionStatus();
                    UpdateStatusIndicator();
                }
            }
        }

        /// <summary>
        /// Last Update Time Property
        /// ========================
        /// 
        /// Timestamp of the last GPS data update.
        /// Used to detect communication timeouts and stale data.
        /// </summary>
        public DateTime LastUpdateTime
        {
            get => _lastUpdateTime;
            private set => SetProperty(ref _lastUpdateTime, value);
        }

        #endregion

        #region UI Display Properties

        /// <summary>
        /// Formatted Latitude Text for Display
        /// ===================================
        /// 
        /// Returns latitude formatted for UI display with appropriate precision.
        /// </summary>
        public string LatitudeText => $"Lat: {_latitude:F6}°";

        /// <summary>
        /// Formatted Longitude Text for Display
        /// ====================================
        /// 
        /// Returns longitude formatted for UI display with appropriate precision.
        /// </summary>
        public string LongitudeText => $"Lon: {_longitude:F6}°";

        /// <summary>
        /// Formatted Altitude Text for Display
        /// ===================================
        /// 
        /// Returns altitude formatted for UI display in meters.
        /// </summary>
        public string AltitudeText => $"Alt: {_altitude:F1} m";

        /// <summary>
        /// Formatted Speed Text for Display
        /// ================================
        /// 
        /// Returns speed formatted for UI display in km/h.
        /// </summary>
        public string SpeedText => $"Speed: {_speed:F1} km/h";

        /// <summary>
        /// Formatted Satellite Count Text for Display
        /// ==========================================
        /// 
        /// Returns satellite count formatted for UI display.
        /// </summary>
        public string SatelliteCountText => $"Satellites: {_satelliteCount}";

        /// <summary>
        /// Status Indicator Brush Property
        /// ===============================
        /// 
        /// Brush color for GPS status indicator:
        /// - Red: Disconnected or no fix
        /// - Yellow: Connected but poor accuracy
        /// - Green: Good fix with acceptable accuracy
        /// - Lime: RTK fixed mode (highest accuracy)
        /// </summary>
        public Brush StatusIndicatorBrush
        {
            get => _statusIndicatorBrush;
            private set => SetProperty(ref _statusIndicatorBrush, value);
        }

        /// <summary>
        /// Connection Status Text Property
        /// ==============================
        /// 
        /// Text description of current connection and fix status.
        /// </summary>
        public string ConnectionStatusText
        {
            get => _connectionStatusText;
            private set => SetProperty(ref _connectionStatusText, value);
        }

        #endregion

        #region Constructor

        /// <summary>
        /// GPS ViewModel Constructor
        /// ========================
        /// 
        /// Initializes the GPS ViewModel with default values.
        /// Sets up initial state for disconnected GPS receiver.
        /// </summary>
        public GPSViewModel()
        {
            // Initialize with default "no connection" state
            _latitude = 0.0;
            _longitude = 0.0;
            _altitude = 0.0;
            _speed = 0.0;
            _heading = 0.0;
            _satelliteCount = 0;
            _horizontalAccuracy = 999.0;
            _isRTKFixed = false;
            _isConnected = false;
            _lastUpdateTime = DateTime.MinValue;

            UpdateConnectionStatus();
            UpdateQualityDescription();
            UpdateStatusIndicator();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Update GPS Position
        /// ==================
        /// 
        /// Updates GPS position data from external source (GPS receiver).
        /// This method should be called when new GPS data is received.
        /// 
        /// Parameters:
        /// - lat: Latitude in decimal degrees
        /// - lon: Longitude in decimal degrees
        /// - alt: Altitude in meters above sea level
        /// - speed: Ground speed in km/h
        /// - heading: Heading in degrees (0-360)
        /// </summary>
        /// <param name="lat">Latitude in decimal degrees</param>
        /// <param name="lon">Longitude in decimal degrees</param>
        /// <param name="alt">Altitude in meters</param>
        /// <param name="speed">Speed in km/h</param>
        /// <param name="heading">Heading in degrees</param>
        public void UpdatePosition(double lat, double lon, double alt, double speed, double heading)
        {
            Latitude = lat;
            Longitude = lon;
            Altitude = alt;
            Speed = speed;
            Heading = heading;
        }

        /// <summary>
        /// Update GPS Quality
        /// =================
        /// 
        /// Updates GPS quality and accuracy information.
        /// 
        /// Parameters:
        /// - satelliteCount: Number of satellites in use
        /// - horizontalAccuracy: Horizontal accuracy in meters
        /// - isRTKFixed: Whether RTK fixed mode is active
        /// </summary>
        /// <param name="satelliteCount">Number of satellites</param>
        /// <param name="horizontalAccuracy">Horizontal accuracy in meters</param>
        /// <param name="isRTKFixed">RTK fixed mode status</param>
        public void UpdateQuality(int satelliteCount, double horizontalAccuracy, bool isRTKFixed)
        {
            SatelliteCount = satelliteCount;
            HorizontalAccuracy = horizontalAccuracy;
            IsRTKFixed = isRTKFixed;
        }

        /// <summary>
        /// Check for Data Timeout
        /// ======================
        /// 
        /// Checks if GPS data is stale based on last update time.
        /// Returns true if data is older than the specified timeout.
        /// 
        /// Parameters:
        /// - timeoutSeconds: Timeout threshold in seconds
        /// 
        /// Returns:
        /// - true if data is stale, false if data is current
        /// </summary>
        /// <param name="timeoutSeconds">Timeout threshold in seconds</param>
        /// <returns>True if data is stale</returns>
        public bool IsDataStale(int timeoutSeconds = 5)
        {
            if (!IsConnected || LastUpdateTime == DateTime.MinValue)
            {
                return true;
            }

            return (DateTime.Now - LastUpdateTime).TotalSeconds > timeoutSeconds;
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Update Last Update Time
        /// ======================
        /// 
        /// Updates the timestamp when GPS data was last received.
        /// Called automatically when position properties change.
        /// </summary>
        private void UpdateLastUpdateTime()
        {
            LastUpdateTime = DateTime.Now;
        }

        /// <summary>
        /// Update Quality Description
        /// =========================
        /// 
        /// Updates the human-readable quality description based on current GPS status.
        /// </summary>
        private void UpdateQualityDescription()
        {
            if (!IsConnected)
            {
                QualityDescription = "No GPS connection";
            }
            else if (IsRTKFixed)
            {
                QualityDescription = "RTK Fixed";
            }
            else if (SatelliteCount >= 4 && HorizontalAccuracy < 5.0)
            {
                QualityDescription = "3D Fix";
            }
            else if (SatelliteCount >= 3)
            {
                QualityDescription = "2D Fix";
            }
            else
            {
                QualityDescription = "No Fix";
            }
        }

        /// <summary>
        /// Update Connection Status
        /// =======================
        /// 
        /// Updates the connection status text based on current GPS state.
        /// </summary>
        private void UpdateConnectionStatus()
        {
            if (!IsConnected)
            {
                ConnectionStatusText = "GPS: Disconnected";
            }
            else if (IsRTKFixed)
            {
                ConnectionStatusText = $"GPS: RTK Fixed ({HorizontalAccuracy:F2}m)";
            }
            else
            {
                ConnectionStatusText = $"GPS: Connected ({QualityDescription})";
            }
        }

        /// <summary>
        /// Update Status Indicator
        /// ======================
        /// 
        /// Updates the status indicator brush color based on GPS quality.
        /// </summary>
        private void UpdateStatusIndicator()
        {
            if (!IsConnected)
            {
                StatusIndicatorBrush = Brushes.Red;
            }
            else if (IsRTKFixed && HorizontalAccuracy < 0.1)
            {
                StatusIndicatorBrush = Brushes.Lime; // Best quality - RTK fixed
            }
            else if (SatelliteCount >= 4 && HorizontalAccuracy < 2.0)
            {
                StatusIndicatorBrush = Brushes.Green; // Good quality
            }
            else if (SatelliteCount >= 3)
            {
                StatusIndicatorBrush = Brushes.Yellow; // Marginal quality
            }
            else
            {
                StatusIndicatorBrush = Brushes.Red; // Poor or no fix
            }
        }

        #endregion
    }
}
