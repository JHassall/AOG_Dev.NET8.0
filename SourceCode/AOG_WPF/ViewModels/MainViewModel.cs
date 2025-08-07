using System;
using System.Windows.Input;
using Microsoft.Extensions.Logging;

namespace AOG_WPF.ViewModels
{
    /// <summary>
    /// Main ViewModel for WPF 3D Agricultural Application
    /// =================================================
    /// 
    /// This is the primary ViewModel that orchestrates all other ViewModels and manages
    /// the overall application state for the WPF 3D migration of AgOpenGPS.
    /// 
    /// Migration Architecture:
    /// - Coordinates GPS, ABLS, and Terrain ViewModels
    /// - Manages application-wide state and commands
    /// - Provides data binding for the main window
    /// - Handles inter-ViewModel communication and updates
    /// 
    /// ABLS Integration:
    /// - Coordinates GPS position updates with terrain elevation queries
    /// - Manages ABLS system activation based on GPS quality and terrain data
    /// - Provides unified interface for boom control and monitoring
    /// - Supports multiple camera views and real-time data updates
    /// </summary>
    public class MainViewModel : BaseViewModel
    {
        #region Private Fields

        /// <summary>
        /// Logger for debugging and monitoring main ViewModel operations
        /// </summary>
        private readonly ILogger<MainViewModel> _logger;

        /// <summary>
        /// Child ViewModels for different system components
        /// </summary>
        private readonly GPSViewModel _gpsViewModel;
        private readonly ABLSViewModel _ablsViewModel;
        private readonly TerrainViewModel _terrainViewModel;

        /// <summary>
        /// Application state fields
        /// </summary>
        private bool _isSystemReady;
        private string _systemStatusMessage = "Initializing...";
        private double _frameRate;
        private double _dataUpdateRate;

        #endregion

        #region Child ViewModels

        /// <summary>
        /// GPS ViewModel Property
        /// =====================
        /// 
        /// Provides access to GPS position, quality, and connection status.
        /// </summary>
        public GPSViewModel GPS => _gpsViewModel;

        /// <summary>
        /// ABLS ViewModel Property
        /// ======================
        /// 
        /// Provides access to ABLS boom control, sensor data, and system status.
        /// </summary>
        public ABLSViewModel ABLS => _ablsViewModel;

        /// <summary>
        /// Terrain ViewModel Property
        /// =========================
        /// 
        /// Provides access to DEM data, terrain elevation, and 3D mesh generation.
        /// </summary>
        public TerrainViewModel Terrain => _terrainViewModel;

        #endregion

        #region Application State Properties

        /// <summary>
        /// Is System Ready Property
        /// ========================
        /// 
        /// Indicates whether the entire system is ready for operation.
        /// Requires GPS connection, valid sensor data, and optionally DEM data for ABLS.
        /// </summary>
        public bool IsSystemReady
        {
            get => _isSystemReady;
            private set => SetProperty(ref _isSystemReady, value);
        }

        /// <summary>
        /// System Status Message Property
        /// =============================
        /// 
        /// Overall system status message for display in the status bar.
        /// </summary>
        public string SystemStatusMessage
        {
            get => _systemStatusMessage;
            private set => SetProperty(ref _systemStatusMessage, value);
        }

        /// <summary>
        /// Frame Rate Property
        /// ===================
        /// 
        /// Current 3D rendering frame rate for performance monitoring.
        /// </summary>
        public double FrameRate
        {
            get => _frameRate;
            set
            {
                if (SetProperty(ref _frameRate, value))
                {
                    OnPropertyChanged(nameof(FrameRateText));
                }
            }
        }

        /// <summary>
        /// Data Update Rate Property
        /// ========================
        /// 
        /// Current GPS/sensor data update rate in Hz.
        /// </summary>
        public double DataUpdateRate
        {
            get => _dataUpdateRate;
            set
            {
                if (SetProperty(ref _dataUpdateRate, value))
                {
                    OnPropertyChanged(nameof(DataUpdateRateText));
                }
            }
        }

        #endregion

        #region UI Display Properties

        /// <summary>
        /// Frame Rate Text for Status Bar
        /// ==============================
        /// </summary>
        public string FrameRateText => $"FPS: {FrameRate:F1}";

        /// <summary>
        /// Data Update Rate Text for Status Bar
        /// ====================================
        /// </summary>
        public string DataUpdateRateText => $"GPS: {DataUpdateRate:F1} Hz";

        /// <summary>
        /// Connection Status Text for Status Bar
        /// =====================================
        /// </summary>
        public string ConnectionStatusText
        {
            get
            {
                if (!GPS.IsConnected)
                    return "Connections: GPS Disconnected";
                
                if (!ABLS.IsSystemActive)
                    return "Connections: GPS Connected, ABLS Inactive";
                
                return "Connections: GPS Connected, ABLS Active";
            }
        }

        #endregion

        #region Commands

        /// <summary>
        /// Command properties for UI binding
        /// These will be implemented as RelayCommand or similar ICommand implementations
        /// </summary>
        public ICommand? ActivateABLSCommand { get; private set; }
        public ICommand? DeactivateABLSCommand { get; private set; }
        public ICommand? LoadDEMCommand { get; private set; }
        public ICommand? ResetSystemCommand { get; private set; }

        #endregion

        #region Constructor

        /// <summary>
        /// Main ViewModel Constructor
        /// =========================
        /// 
        /// Initializes the main ViewModel and all child ViewModels.
        /// Sets up event handlers for inter-ViewModel communication.
        /// 
        /// Parameters:
        /// - logger: Logger instance for debugging and monitoring
        /// </summary>
        public MainViewModel(ILogger<MainViewModel> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _logger.LogInformation("Initializing MainViewModel");

            try
            {
                // Initialize child ViewModels
                _gpsViewModel = new GPSViewModel();
                _ablsViewModel = new ABLSViewModel();
                _terrainViewModel = new TerrainViewModel();

                // Set up event handlers for inter-ViewModel communication
                SetupEventHandlers();

                // Initialize commands
                InitializeCommands();

                // Set initial system state
                UpdateSystemStatus();

                _logger.LogInformation("MainViewModel initialization completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during MainViewModel initialization");
                SetError($"Failed to initialize application: {ex.Message}");
            }
        }

        /// <summary>
        /// Parameterless constructor for design-time support
        /// ================================================
        /// 
        /// Used by WPF designer and XAML preview.
        /// Creates a minimal ViewModel with mock data.
        /// </summary>
        public MainViewModel() : this(CreateDesignTimeLogger())
        {
            // Set up design-time data for XAML preview
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(new System.Windows.DependencyObject()))
            {
                SetupDesignTimeData();
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Update GPS Data
        /// ==============
        /// 
        /// Updates GPS position and quality data from external GPS receiver.
        /// This method should be called when new GPS data is received.
        /// 
        /// Parameters:
        /// - latitude: GPS latitude in decimal degrees
        /// - longitude: GPS longitude in decimal degrees
        /// - altitude: GPS altitude in meters
        /// - speed: Ground speed in km/h
        /// - heading: Heading in degrees (0-360)
        /// - satelliteCount: Number of satellites in use
        /// - horizontalAccuracy: Horizontal accuracy in meters
        /// - isRTKFixed: Whether RTK fixed mode is active
        /// </summary>
        public void UpdateGPSData(double latitude, double longitude, double altitude, double speed, double heading,
                                 int satelliteCount, double horizontalAccuracy, bool isRTKFixed)
        {
            try
            {
                // Update GPS ViewModel
                GPS.UpdatePosition(latitude, longitude, altitude, speed, heading);
                GPS.UpdateQuality(satelliteCount, horizontalAccuracy, isRTKFixed);
                GPS.IsConnected = true;

                // Convert GPS coordinates to local coordinates for terrain lookup
                // TODO: Implement proper coordinate transformation
                double easting = longitude * 111320; // Rough conversion for demo
                double northing = latitude * 111320;

                // Update terrain elevation at current position
                if (Terrain.IsDEMLoaded)
                {
                    Terrain.UpdateCurrentPositionElevation(easting, northing);
                    
                    // Update ABLS boom elevations (assuming boom positions relative to GPS)
                    // TODO: Calculate actual boom positions based on vehicle geometry
                    Terrain.UpdateABLSBoomElevations(
                        easting, northing,           // Center boom
                        easting - 16.5, northing,   // Left wing (16.5m offset)
                        easting + 16.5, northing    // Right wing (16.5m offset)
                    );
                }

                // Update system status
                UpdateSystemStatus();

                _logger.LogDebug("GPS data updated: Lat={Lat:F6}, Lon={Lon:F6}, Quality={Quality}", 
                                latitude, longitude, GPS.QualityDescription);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating GPS data");
                SetError($"GPS update error: {ex.Message}");
            }
        }

        /// <summary>
        /// Update ABLS Sensor Data
        /// ======================
        /// 
        /// Updates ABLS boom position and sensor data from the ABLS system.
        /// 
        /// Parameters:
        /// - centerHeight: Center boom height in cm
        /// - leftHeight: Left wing height in cm
        /// - rightHeight: Right wing height in cm
        /// - leftAngle: Left wing angle in degrees
        /// - rightAngle: Right wing angle in degrees
        /// - centerHydraulic: Center hydraulic position (0-100%)
        /// - leftHydraulic: Left hydraulic position (0-100%)
        /// - rightHydraulic: Right hydraulic position (0-100%)
        /// </summary>
        public void UpdateABLSData(double centerHeight, double leftHeight, double rightHeight,
                                  double leftAngle, double rightAngle,
                                  double centerHydraulic, double leftHydraulic, double rightHydraulic)
        {
            try
            {
                // Update ABLS ViewModel
                ABLS.UpdateBoomPositions(centerHeight, leftHeight, rightHeight, leftAngle, rightAngle);
                ABLS.UpdateHydraulicPositions(centerHydraulic, leftHydraulic, rightHydraulic);
                ABLS.IsSystemActive = true;

                // Update system status
                UpdateSystemStatus();

                _logger.LogDebug("ABLS data updated: Center={Center:F1}cm, Left={Left:F1}cm, Right={Right:F1}cm", 
                                centerHeight, leftHeight, rightHeight);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating ABLS data");
                SetError($"ABLS update error: {ex.Message}");
            }
        }

        /// <summary>
        /// Load DEM File
        /// =============
        /// 
        /// Loads a Digital Elevation Model file for terrain visualization and ABLS calculations.
        /// 
        /// Parameters:
        /// - filePath: Path to the DEM file
        /// 
        /// Returns:
        /// - true if DEM was loaded successfully
        /// - false if loading failed
        /// </summary>
        public bool LoadDEMFile(string filePath)
        {
            try
            {
                _logger.LogInformation("Loading DEM file: {FilePath}", filePath);

                // TODO: Implement actual DEM file loading
                // For now, create mock elevation data for demonstration
                var mockElevationData = CreateMockElevationData();
                
                bool success = Terrain.LoadDEMData(
                    filePath,
                    0.0, 0.0,           // Origin coordinates
                    1.0, 1.0,           // Grid spacing
                    mockElevationData   // Elevation data
                );

                if (success)
                {
                    _logger.LogInformation("DEM file loaded successfully");
                    UpdateSystemStatus();
                }
                else
                {
                    _logger.LogWarning("Failed to load DEM file");
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading DEM file: {FilePath}", filePath);
                SetError($"DEM loading error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Activate ABLS Auto Mode
        /// =======================
        /// 
        /// Attempts to activate ABLS automatic boom control mode.
        /// Performs safety checks before activation.
        /// 
        /// Returns:
        /// - true if auto mode was activated successfully
        /// - false if safety checks failed
        /// </summary>
        public bool ActivateABLSAutoMode()
        {
            try
            {
                _logger.LogInformation("Attempting to activate ABLS auto mode");

                // Check GPS quality requirements
                if (!GPS.IsConnected || !GPS.IsRTKFixed || GPS.HorizontalAccuracy > 0.1)
                {
                    SetError("RTK GPS fix required for ABLS auto mode");
                    return false;
                }

                // Check terrain data requirements
                if (!Terrain.IsDEMLoaded)
                {
                    SetError("DEM terrain data required for ABLS auto mode");
                    return false;
                }

                // Attempt to activate ABLS auto mode
                bool success = ABLS.ActivateAutoMode();
                
                if (success)
                {
                    _logger.LogInformation("ABLS auto mode activated successfully");
                    ClearError();
                    UpdateSystemStatus();
                }
                else
                {
                    _logger.LogWarning("Failed to activate ABLS auto mode");
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error activating ABLS auto mode");
                SetError($"ABLS activation error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Deactivate ABLS Auto Mode
        /// =========================
        /// 
        /// Immediately deactivates ABLS automatic boom control mode.
        /// This is a safety function that can always be called.
        /// </summary>
        public void DeactivateABLSAutoMode()
        {
            try
            {
                _logger.LogInformation("Deactivating ABLS auto mode");
                
                ABLS.DeactivateAutoMode();
                ClearError();
                UpdateSystemStatus();
                
                _logger.LogInformation("ABLS auto mode deactivated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating ABLS auto mode");
            }
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Setup Event Handlers
        /// ====================
        /// 
        /// Sets up event handlers for inter-ViewModel communication.
        /// Enables automatic updates when child ViewModels change.
        /// </summary>
        private void SetupEventHandlers()
        {
            // GPS ViewModel events
            GPS.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(GPS.IsConnected) || 
                    e.PropertyName == nameof(GPS.IsRTKFixed))
                {
                    UpdateSystemStatus();
                    OnPropertyChanged(nameof(ConnectionStatusText));
                }
            };

            // ABLS ViewModel events
            ABLS.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(ABLS.IsSystemActive) || 
                    e.PropertyName == nameof(ABLS.IsAutoModeEnabled))
                {
                    UpdateSystemStatus();
                    OnPropertyChanged(nameof(ConnectionStatusText));
                }
            };

            // Terrain ViewModel events
            Terrain.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(Terrain.IsDEMLoaded))
                {
                    UpdateSystemStatus();
                }
            };
        }

        /// <summary>
        /// Initialize Commands
        /// ==================
        /// 
        /// Initializes ICommand implementations for UI binding.
        /// TODO: Implement actual RelayCommand or similar ICommand implementations.
        /// </summary>
        private void InitializeCommands()
        {
            // TODO: Implement commands using RelayCommand or similar
            // ActivateABLSCommand = new RelayCommand(() => ActivateABLSAutoMode(), () => CanActivateABLS());
            // DeactivateABLSCommand = new RelayCommand(() => DeactivateABLSAutoMode(), () => ABLS.IsAutoModeEnabled);
            // LoadDEMCommand = new RelayCommand(() => ShowLoadDEMDialog());
            // ResetSystemCommand = new RelayCommand(() => ResetSystem());
        }

        /// <summary>
        /// Update System Status
        /// ===================
        /// 
        /// Updates the overall system status based on child ViewModel states.
        /// </summary>
        private void UpdateSystemStatus()
        {
            try
            {
                // Determine overall system readiness
                bool gpsReady = GPS.IsConnected && GPS.IsRTKFixed;
                bool ablsReady = ABLS.IsSystemActive && ABLS.IsSensorDataValid;
                bool terrainReady = Terrain.IsDEMLoaded;

                IsSystemReady = gpsReady && ablsReady && terrainReady;

                // Update status message
                if (!GPS.IsConnected)
                {
                    SystemStatusMessage = "System: GPS Disconnected";
                }
                else if (!GPS.IsRTKFixed)
                {
                    SystemStatusMessage = "System: Waiting for RTK Fix";
                }
                else if (!ABLS.IsSystemActive)
                {
                    SystemStatusMessage = "System: ABLS Inactive";
                }
                else if (!Terrain.IsDEMLoaded)
                {
                    SystemStatusMessage = "System: DEM Not Loaded";
                }
                else if (ABLS.IsAutoModeEnabled)
                {
                    SystemStatusMessage = $"System: ABLS Auto Mode (±{ABLS.SystemAccuracy:F1}cm)";
                }
                else
                {
                    SystemStatusMessage = "System: Ready for Auto Mode";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating system status");
                SystemStatusMessage = "System: Status Update Error";
            }
        }

        /// <summary>
        /// Create Design Time Logger
        /// =========================
        /// 
        /// Creates a logger for design-time use when dependency injection is not available.
        /// </summary>
        private static ILogger<MainViewModel> CreateDesignTimeLogger()
        {
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            return loggerFactory.CreateLogger<MainViewModel>();
        }

        /// <summary>
        /// Setup Design Time Data
        /// =====================
        /// 
        /// Sets up mock data for design-time XAML preview.
        /// </summary>
        private void SetupDesignTimeData()
        {
            // Mock GPS data
            GPS.UpdatePosition(-37.7749, 144.9651, 100.0, 15.5, 45.0);
            GPS.UpdateQuality(12, 0.05, true);
            GPS.IsConnected = true;

            // Mock ABLS data
            ABLS.UpdateBoomPositions(50.0, 48.5, 51.2, -2.1, 1.8);
            ABLS.IsSystemActive = true;
            ABLS.IsAutoModeEnabled = true;

            // Mock terrain data
            var mockData = CreateMockElevationData();
            Terrain.LoadDEMData("MockTerrain.dem", 0, 0, 1, 1, mockData);

            // Update system status
            UpdateSystemStatus();
        }

        /// <summary>
        /// Create Mock Elevation Data
        /// =========================
        /// 
        /// Creates mock elevation data for demonstration purposes.
        /// </summary>
        private double[,] CreateMockElevationData()
        {
            const int size = 100;
            var data = new double[size, size];

            // Create a simple undulating terrain
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    // Create gentle hills using sine waves
                    double elevation = 100.0 + 
                                     10.0 * Math.Sin(x * 0.1) * Math.Cos(y * 0.1) +
                                     5.0 * Math.Sin(x * 0.2) * Math.Sin(y * 0.15);
                    data[x, y] = elevation;
                }
            }

            return data;
        }

        #endregion
    }
}
