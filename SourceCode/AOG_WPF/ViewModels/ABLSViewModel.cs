using System;
using System.Windows.Media;

namespace AOG_WPF.ViewModels
{
    /// <summary>
    /// ABLS (Automatic Boom Level System) ViewModel
    /// ============================================
    /// 
    /// Manages ABLS boom control data, sensor readings, and system status for the WPF 3D migration.
    /// This ViewModel provides data binding for real-time boom positioning, height control, and
    /// terrain following functionality specific to the ABLS system.
    /// 
    /// Migration Notes:
    /// - Consolidates ABLS data from multiple sensor sources
    /// - Provides real-time boom position and angle updates for 3D visualization
    /// - Enables terrain-following mode with DEM integration
    /// - Supports multiple camera views for boom operation monitoring
    /// 
    /// ABLS System Architecture:
    /// - Center boom: Main hydraulic control with GPS, IMU, radar sensors
    /// - Left wing: Articulated boom section with independent sensors
    /// - Right wing: Articulated boom section with independent sensors
    /// - All sections work together to maintain target height above terrain
    /// </summary>
    public class ABLSViewModel : BaseViewModel
    {
        #region Private Fields

        /// <summary>
        /// System status and mode fields
        /// </summary>
        private bool _isSystemActive;
        private bool _isAutoModeEnabled;
        private string _systemStatusText = "System Inactive";
        private double _targetHeight = 50.0; // Default 50cm target height

        /// <summary>
        /// Center boom position and sensor data
        /// </summary>
        private double _centerHeight;
        private double _centerTargetHeight;
        private double _centerHydraulicPosition;
        private double _centerGroundDistance;

        /// <summary>
        /// Left wing boom position and sensor data
        /// </summary>
        private double _leftWingHeight;
        private double _leftWingAngle;
        private double _leftWingTargetHeight;
        private double _leftWingHydraulicPosition;
        private double _leftWingGroundDistance;

        /// <summary>
        /// Right wing boom position and sensor data
        /// </summary>
        private double _rightWingHeight;
        private double _rightWingAngle;
        private double _rightWingTargetHeight;
        private double _rightWingHydraulicPosition;
        private double _rightWingGroundDistance;

        /// <summary>
        /// System performance and status indicators
        /// </summary>
        private Brush _statusIndicatorBrush = Brushes.Red;
        private DateTime _lastSensorUpdate;
        private double _systemAccuracy;
        private bool _isSensorDataValid;

        #endregion

        #region System Status Properties

        /// <summary>
        /// Is System Active Property
        /// ========================
        /// 
        /// Indicates whether the ABLS system is powered on and operational.
        /// When false, all boom controls are in manual mode.
        /// </summary>
        public bool IsSystemActive
        {
            get => _isSystemActive;
            set
            {
                if (SetProperty(ref _isSystemActive, value))
                {
                    UpdateSystemStatus();
                    UpdateStatusIndicator();
                    
                    // If system is deactivated, disable auto mode
                    if (!value)
                    {
                        IsAutoModeEnabled = false;
                    }
                }
            }
        }

        /// <summary>
        /// Is Auto Mode Enabled Property
        /// ============================
        /// 
        /// Indicates whether ABLS automatic boom control is active.
        /// When true, booms automatically adjust to maintain target height.
        /// When false, boom control is manual only.
        /// 
        /// Safety Requirements:
        /// - Requires RTK GPS fix for activation
        /// - Requires valid sensor data from all boom sections
        /// - Requires DEM data for terrain following
        /// </summary>
        public bool IsAutoModeEnabled
        {
            get => _isAutoModeEnabled;
            set
            {
                if (SetProperty(ref _isAutoModeEnabled, value))
                {
                    UpdateSystemStatus();
                    UpdateStatusIndicator();
                    OnPropertyChanged(nameof(ModeText));
                }
            }
        }

        /// <summary>
        /// Target Height Property (centimeters)
        /// ===================================
        /// 
        /// Desired boom height above ground in centimeters.
        /// This is the target height that all boom sections will try to maintain
        /// when auto mode is enabled.
        /// 
        /// Range: 20-100 cm (typical agricultural spray boom heights)
        /// </summary>
        public double TargetHeight
        {
            get => _targetHeight;
            set
            {
                // Clamp target height to safe operating range
                var clampedValue = Math.Max(20.0, Math.Min(100.0, value));
                
                if (SetProperty(ref _targetHeight, clampedValue))
                {
                    OnPropertyChanged(nameof(TargetHeightText));
                    
                    // Update individual boom target heights
                    CenterTargetHeight = clampedValue;
                    LeftWingTargetHeight = clampedValue;
                    RightWingTargetHeight = clampedValue;
                }
            }
        }

        /// <summary>
        /// System Status Text Property
        /// ==========================
        /// 
        /// Human-readable description of current ABLS system status.
        /// </summary>
        public string SystemStatusText
        {
            get => _systemStatusText;
            private set => SetProperty(ref _systemStatusText, value);
        }

        /// <summary>
        /// Mode Text Property
        /// =================
        /// 
        /// Returns current operating mode as text for UI display.
        /// </summary>
        public string ModeText => IsAutoModeEnabled ? "Mode: Auto" : "Mode: Manual";

        /// <summary>
        /// Target Height Text Property
        /// ==========================
        /// 
        /// Returns target height formatted for UI display.
        /// </summary>
        public string TargetHeightText => $"Target Height: {TargetHeight:F0} cm";

        #endregion

        #region Center Boom Properties

        /// <summary>
        /// Center Height Property (centimeters)
        /// ===================================
        /// 
        /// Current measured height of center boom above ground in centimeters.
        /// This is the primary reference height for the ABLS system.
        /// </summary>
        public double CenterHeight
        {
            get => _centerHeight;
            set
            {
                if (SetProperty(ref _centerHeight, value))
                {
                    OnPropertyChanged(nameof(CenterHeightText));
                    UpdateLastSensorUpdate();
                }
            }
        }

        /// <summary>
        /// Center Target Height Property (centimeters)
        /// ==========================================
        /// 
        /// Target height for center boom section.
        /// Usually matches the global target height.
        /// </summary>
        public double CenterTargetHeight
        {
            get => _centerTargetHeight;
            set => SetProperty(ref _centerTargetHeight, value);
        }

        /// <summary>
        /// Center Hydraulic Position Property (percentage)
        /// ==============================================
        /// 
        /// Current hydraulic ram position for center boom as percentage.
        /// 0% = fully retracted, 100% = fully extended.
        /// </summary>
        public double CenterHydraulicPosition
        {
            get => _centerHydraulicPosition;
            set => SetProperty(ref _centerHydraulicPosition, value);
        }

        /// <summary>
        /// Center Ground Distance Property (centimeters)
        /// ============================================
        /// 
        /// Distance from center boom to ground measured by radar sensor.
        /// Used for real-time height feedback and control.
        /// </summary>
        public double CenterGroundDistance
        {
            get => _centerGroundDistance;
            set
            {
                if (SetProperty(ref _centerGroundDistance, value))
                {
                    UpdateLastSensorUpdate();
                }
            }
        }

        /// <summary>
        /// Center Height Text for UI Display
        /// ================================
        /// </summary>
        public string CenterHeightText => $"Height: {CenterHeight:F1} cm";

        #endregion

        #region Left Wing Properties

        /// <summary>
        /// Left Wing Height Property (centimeters)
        /// ======================================
        /// 
        /// Current measured height of left wing boom above ground.
        /// </summary>
        public double LeftWingHeight
        {
            get => _leftWingHeight;
            set
            {
                if (SetProperty(ref _leftWingHeight, value))
                {
                    OnPropertyChanged(nameof(LeftWingHeightText));
                    UpdateLastSensorUpdate();
                }
            }
        }

        /// <summary>
        /// Left Wing Angle Property (degrees)
        /// =================================
        /// 
        /// Current angle of left wing relative to center boom.
        /// Positive angles indicate wing is angled upward.
        /// Range: typically -15° to +15°
        /// </summary>
        public double LeftWingAngle
        {
            get => _leftWingAngle;
            set
            {
                if (SetProperty(ref _leftWingAngle, value))
                {
                    OnPropertyChanged(nameof(LeftWingAngleText));
                    UpdateLastSensorUpdate();
                }
            }
        }

        /// <summary>
        /// Left Wing Target Height Property (centimeters)
        /// =============================================
        /// 
        /// Target height for left wing boom section.
        /// May differ from global target due to terrain variations.
        /// </summary>
        public double LeftWingTargetHeight
        {
            get => _leftWingTargetHeight;
            set => SetProperty(ref _leftWingTargetHeight, value);
        }

        /// <summary>
        /// Left Wing Hydraulic Position Property (percentage)
        /// =================================================
        /// 
        /// Current hydraulic ram position for left wing boom.
        /// </summary>
        public double LeftWingHydraulicPosition
        {
            get => _leftWingHydraulicPosition;
            set => SetProperty(ref _leftWingHydraulicPosition, value);
        }

        /// <summary>
        /// Left Wing Ground Distance Property (centimeters)
        /// ===============================================
        /// 
        /// Distance from left wing to ground measured by radar sensor.
        /// </summary>
        public double LeftWingGroundDistance
        {
            get => _leftWingGroundDistance;
            set
            {
                if (SetProperty(ref _leftWingGroundDistance, value))
                {
                    UpdateLastSensorUpdate();
                }
            }
        }

        /// <summary>
        /// Left Wing Display Properties
        /// ===========================
        /// </summary>
        public string LeftWingHeightText => $"Height: {LeftWingHeight:F1} cm";
        public string LeftWingAngleText => $"Angle: {LeftWingAngle:F1}°";

        #endregion

        #region Right Wing Properties

        /// <summary>
        /// Right Wing Height Property (centimeters)
        /// =======================================
        /// 
        /// Current measured height of right wing boom above ground.
        /// </summary>
        public double RightWingHeight
        {
            get => _rightWingHeight;
            set
            {
                if (SetProperty(ref _rightWingHeight, value))
                {
                    OnPropertyChanged(nameof(RightWingHeightText));
                    UpdateLastSensorUpdate();
                }
            }
        }

        /// <summary>
        /// Right Wing Angle Property (degrees)
        /// ==================================
        /// 
        /// Current angle of right wing relative to center boom.
        /// Positive angles indicate wing is angled upward.
        /// Range: typically -15° to +15°
        /// </summary>
        public double RightWingAngle
        {
            get => _rightWingAngle;
            set
            {
                if (SetProperty(ref _rightWingAngle, value))
                {
                    OnPropertyChanged(nameof(RightWingAngleText));
                    UpdateLastSensorUpdate();
                }
            }
        }

        /// <summary>
        /// Right Wing Target Height Property (centimeters)
        /// ==============================================
        /// 
        /// Target height for right wing boom section.
        /// May differ from global target due to terrain variations.
        /// </summary>
        public double RightWingTargetHeight
        {
            get => _rightWingTargetHeight;
            set => SetProperty(ref _rightWingTargetHeight, value);
        }

        /// <summary>
        /// Right Wing Hydraulic Position Property (percentage)
        /// ==================================================
        /// 
        /// Current hydraulic ram position for right wing boom.
        /// </summary>
        public double RightWingHydraulicPosition
        {
            get => _rightWingHydraulicPosition;
            set => SetProperty(ref _rightWingHydraulicPosition, value);
        }

        /// <summary>
        /// Right Wing Ground Distance Property (centimeters)
        /// ================================================
        /// 
        /// Distance from right wing to ground measured by radar sensor.
        /// </summary>
        public double RightWingGroundDistance
        {
            get => _rightWingGroundDistance;
            set
            {
                if (SetProperty(ref _rightWingGroundDistance, value))
                {
                    UpdateLastSensorUpdate();
                }
            }
        }

        /// <summary>
        /// Right Wing Display Properties
        /// ============================
        /// </summary>
        public string RightWingHeightText => $"Height: {RightWingHeight:F1} cm";
        public string RightWingAngleText => $"Angle: {RightWingAngle:F1}°";

        #endregion

        #region Status and Performance Properties

        /// <summary>
        /// Status Indicator Brush Property
        /// ===============================
        /// 
        /// Brush color for ABLS status indicator:
        /// - Red: System inactive or error condition
        /// - Yellow: System active but manual mode
        /// - Green: System active in auto mode
        /// - Lime: System active in auto mode with high accuracy
        /// </summary>
        public Brush StatusIndicatorBrush
        {
            get => _statusIndicatorBrush;
            private set => SetProperty(ref _statusIndicatorBrush, value);
        }

        /// <summary>
        /// Last Sensor Update Property
        /// ===========================
        /// 
        /// Timestamp of the last sensor data update from any boom section.
        /// Used to detect sensor communication timeouts.
        /// </summary>
        public DateTime LastSensorUpdate
        {
            get => _lastSensorUpdate;
            private set => SetProperty(ref _lastSensorUpdate, value);
        }

        /// <summary>
        /// System Accuracy Property (centimeters)
        /// =====================================
        /// 
        /// Current system accuracy in maintaining target height.
        /// Lower values indicate better performance.
        /// </summary>
        public double SystemAccuracy
        {
            get => _systemAccuracy;
            set => SetProperty(ref _systemAccuracy, value);
        }

        /// <summary>
        /// Is Sensor Data Valid Property
        /// ============================
        /// 
        /// Indicates whether sensor data from all boom sections is valid and current.
        /// </summary>
        public bool IsSensorDataValid
        {
            get => _isSensorDataValid;
            set
            {
                if (SetProperty(ref _isSensorDataValid, value))
                {
                    UpdateSystemStatus();
                    UpdateStatusIndicator();
                }
            }
        }

        #endregion

        #region Constructor

        /// <summary>
        /// ABLS ViewModel Constructor
        /// =========================
        /// 
        /// Initializes the ABLS ViewModel with default values.
        /// Sets up initial state for inactive ABLS system.
        /// </summary>
        public ABLSViewModel()
        {
            // Initialize system status
            _isSystemActive = false;
            _isAutoModeEnabled = false;
            _targetHeight = 50.0;

            // Initialize boom positions to safe defaults
            _centerHeight = 50.0;
            _centerTargetHeight = 50.0;
            _leftWingHeight = 50.0;
            _leftWingTargetHeight = 50.0;
            _rightWingHeight = 50.0;
            _rightWingTargetHeight = 50.0;

            // Initialize angles to level (0 degrees)
            _leftWingAngle = 0.0;
            _rightWingAngle = 0.0;

            // Initialize hydraulic positions to mid-range
            _centerHydraulicPosition = 50.0;
            _leftWingHydraulicPosition = 50.0;
            _rightWingHydraulicPosition = 50.0;

            // Initialize sensor data
            _isSensorDataValid = false;
            _lastSensorUpdate = DateTime.MinValue;
            _systemAccuracy = 999.0;

            UpdateSystemStatus();
            UpdateStatusIndicator();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Update Boom Positions
        /// ====================
        /// 
        /// Updates all boom position data from ABLS sensor readings.
        /// This method should be called when new sensor data is received.
        /// 
        /// Parameters:
        /// - centerHeight: Center boom height in cm
        /// - leftHeight: Left wing height in cm
        /// - rightHeight: Right wing height in cm
        /// - leftAngle: Left wing angle in degrees
        /// - rightAngle: Right wing angle in degrees
        /// </summary>
        public void UpdateBoomPositions(double centerHeight, double leftHeight, double rightHeight, 
                                       double leftAngle, double rightAngle)
        {
            CenterHeight = centerHeight;
            LeftWingHeight = leftHeight;
            RightWingHeight = rightHeight;
            LeftWingAngle = leftAngle;
            RightWingAngle = rightAngle;

            // Calculate system accuracy based on target vs actual heights
            CalculateSystemAccuracy();
        }

        /// <summary>
        /// Update Hydraulic Positions
        /// =========================
        /// 
        /// Updates hydraulic ram positions for all boom sections.
        /// 
        /// Parameters:
        /// - centerPosition: Center hydraulic position (0-100%)
        /// - leftPosition: Left wing hydraulic position (0-100%)
        /// - rightPosition: Right wing hydraulic position (0-100%)
        /// </summary>
        public void UpdateHydraulicPositions(double centerPosition, double leftPosition, double rightPosition)
        {
            CenterHydraulicPosition = centerPosition;
            LeftWingHydraulicPosition = leftPosition;
            RightWingHydraulicPosition = rightPosition;
        }

        /// <summary>
        /// Update Ground Distances
        /// ======================
        /// 
        /// Updates radar sensor ground distance readings for all boom sections.
        /// 
        /// Parameters:
        /// - centerDistance: Center boom ground distance in cm
        /// - leftDistance: Left wing ground distance in cm
        /// - rightDistance: Right wing ground distance in cm
        /// </summary>
        public void UpdateGroundDistances(double centerDistance, double leftDistance, double rightDistance)
        {
            CenterGroundDistance = centerDistance;
            LeftWingGroundDistance = leftDistance;
            RightWingGroundDistance = rightDistance;

            // Validate sensor data based on reasonable distance ranges
            IsSensorDataValid = ValidateSensorData();
        }

        /// <summary>
        /// Activate Auto Mode
        /// =================
        /// 
        /// Attempts to activate ABLS automatic boom control mode.
        /// Performs safety checks before activation.
        /// 
        /// Returns:
        /// - true if auto mode was successfully activated
        /// - false if safety checks failed
        /// </summary>
        public bool ActivateAutoMode()
        {
            // Safety checks before enabling auto mode
            if (!IsSystemActive)
            {
                SetError("ABLS system must be active before enabling auto mode");
                return false;
            }

            if (!IsSensorDataValid)
            {
                SetError("Invalid sensor data - cannot enable auto mode");
                return false;
            }

            if (IsDataStale())
            {
                SetError("Sensor data is stale - cannot enable auto mode");
                return false;
            }

            // All safety checks passed - enable auto mode
            ClearError();
            IsAutoModeEnabled = true;
            return true;
        }

        /// <summary>
        /// Deactivate Auto Mode
        /// ===================
        /// 
        /// Immediately deactivates ABLS automatic boom control mode.
        /// This is a safety function that can always be called.
        /// </summary>
        public void DeactivateAutoMode()
        {
            IsAutoModeEnabled = false;
            ClearError();
        }

        /// <summary>
        /// Check for Sensor Data Timeout
        /// =============================
        /// 
        /// Checks if sensor data is stale based on last update time.
        /// 
        /// Parameters:
        /// - timeoutSeconds: Timeout threshold in seconds (default: 2 seconds)
        /// 
        /// Returns:
        /// - true if data is stale, false if data is current
        /// </summary>
        public bool IsDataStale(int timeoutSeconds = 2)
        {
            if (LastSensorUpdate == DateTime.MinValue)
            {
                return true;
            }

            return (DateTime.Now - LastSensorUpdate).TotalSeconds > timeoutSeconds;
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Update System Status
        /// ===================
        /// 
        /// Updates the system status text based on current ABLS state.
        /// </summary>
        private void UpdateSystemStatus()
        {
            if (!IsSystemActive)
            {
                SystemStatusText = "ABLS System: Inactive";
            }
            else if (!IsSensorDataValid)
            {
                SystemStatusText = "ABLS System: Sensor Error";
            }
            else if (IsAutoModeEnabled)
            {
                SystemStatusText = $"ABLS System: Auto Mode (±{SystemAccuracy:F1}cm)";
            }
            else
            {
                SystemStatusText = "ABLS System: Manual Mode";
            }
        }

        /// <summary>
        /// Update Status Indicator
        /// ======================
        /// 
        /// Updates the status indicator brush color based on ABLS system state.
        /// </summary>
        private void UpdateStatusIndicator()
        {
            if (!IsSystemActive || !IsSensorDataValid)
            {
                StatusIndicatorBrush = Brushes.Red;
            }
            else if (IsAutoModeEnabled && SystemAccuracy < 2.0)
            {
                StatusIndicatorBrush = Brushes.Lime; // High accuracy auto mode
            }
            else if (IsAutoModeEnabled)
            {
                StatusIndicatorBrush = Brushes.Green; // Auto mode active
            }
            else
            {
                StatusIndicatorBrush = Brushes.Yellow; // Manual mode
            }
        }

        /// <summary>
        /// Update Last Sensor Update Time
        /// ==============================
        /// 
        /// Updates the timestamp when sensor data was last received.
        /// </summary>
        private void UpdateLastSensorUpdate()
        {
            LastSensorUpdate = DateTime.Now;
        }

        /// <summary>
        /// Calculate System Accuracy
        /// ========================
        /// 
        /// Calculates the current system accuracy based on how well
        /// actual boom heights match target heights.
        /// </summary>
        private void CalculateSystemAccuracy()
        {
            var centerError = Math.Abs(CenterHeight - CenterTargetHeight);
            var leftError = Math.Abs(LeftWingHeight - LeftWingTargetHeight);
            var rightError = Math.Abs(RightWingHeight - RightWingTargetHeight);

            // Use RMS error as overall system accuracy metric
            SystemAccuracy = Math.Sqrt((centerError * centerError + leftError * leftError + rightError * rightError) / 3.0);
        }

        /// <summary>
        /// Validate Sensor Data
        /// ====================
        /// 
        /// Validates that sensor readings are within reasonable ranges.
        /// 
        /// Returns:
        /// - true if all sensor data is valid
        /// - false if any sensor data is out of range
        /// </summary>
        private bool ValidateSensorData()
        {
            // Check if ground distances are within reasonable range (10-200 cm)
            if (CenterGroundDistance < 10 || CenterGroundDistance > 200 ||
                LeftWingGroundDistance < 10 || LeftWingGroundDistance > 200 ||
                RightWingGroundDistance < 10 || RightWingGroundDistance > 200)
            {
                return false;
            }

            // Check if boom angles are within safe range (-20° to +20°)
            if (Math.Abs(LeftWingAngle) > 20 || Math.Abs(RightWingAngle) > 20)
            {
                return false;
            }

            // All validations passed
            return true;
        }

        #endregion
    }
}
