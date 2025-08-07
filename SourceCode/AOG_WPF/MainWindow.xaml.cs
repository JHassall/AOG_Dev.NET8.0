using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using Microsoft.Extensions.Logging;
using AOG_WPF.ViewModels;

namespace AOG_WPF
{
    /// <summary>
    /// Main Window - WPF 3D Agricultural Visualization Code Behind
    /// ===========================================================
    /// 
    /// This is the main window code-behind for the WPF 3D migration of AgOpenGPS.
    /// Replaces the Windows Forms FormGPS with modern WPF 3D visualization capabilities.
    /// 
    /// Migration Architecture:
    /// - Manages HelixToolkit 3D viewport and scene graph
    /// - Implements MVVM pattern with ViewModels for agricultural data
    /// - Handles multiple camera views (field view, top/rear view for ABLS)
    /// - Integrates real-time GPS, sensor, and boom positioning data
    /// 
    /// Key Responsibilities:
    /// 1. 3D Scene Management - Terrain, vehicle, boom, and overlay rendering
    /// 2. Camera Control - Multiple view modes for different operational needs
    /// 3. Real-time Data Updates - GPS positions, boom angles, coverage areas
    /// 4. User Interface Events - Menu actions, control interactions
    /// 5. ABLS Integration - Boom visualization and terrain following
    /// 
    /// ABLS Features:
    /// - DEM-based terrain surface rendering
    /// - 3D boom sections with real-time positioning
    /// - Top/rear camera view for boom operation monitoring
    /// - Terrain-following guidance line projection
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Private Fields

        /// <summary>
        /// Logger for debugging and monitoring main window operations
        /// Critical for troubleshooting 3D rendering and data integration issues
        /// </summary>
        private readonly ILogger<MainWindow> _logger;

        /// <summary>
        /// Current camera view mode for switching between field and ABLS views
        /// </summary>
        private CameraViewMode _currentCameraMode = CameraViewMode.FieldView;

        /// <summary>
        /// Flag to track if DEM terrain data is currently loaded
        /// </summary>
        private bool _isDEMLoaded = false;

        /// <summary>
        /// Flag to track ABLS system status
        /// </summary>
        private bool _isABLSActive = false;

        /// <summary>
        /// Main ViewModel coordinating all agricultural data and operations
        /// Implements MVVM pattern for clean separation of UI and business logic
        /// </summary>
        public MainViewModel MainViewModel { get; private set; }

        /// <summary>
        /// Direct access to GPS ViewModel for quick status updates
        /// </summary>
        public GPSViewModel GPSViewModel => MainViewModel.GPS;

        /// <summary>
        /// Direct access to ABLS ViewModel for boom control operations
        /// </summary>
        public ABLSViewModel ABLSViewModel => MainViewModel.ABLS;

        /// <summary>
        /// Direct access to Terrain ViewModel for DEM and elevation data
        /// </summary>
        public TerrainViewModel TerrainViewModel => MainViewModel.Terrain;

        /// <summary>
        /// Current target boom height in centimeters
        /// </summary>
        private double _targetBoomHeight = 50.0;

        // TODO: Add ViewModels for MVVM pattern
        // private FieldViewModel _fieldViewModel;
        // private VehicleViewModel _vehicleViewModel;
        // private ABLSViewModel _ablsViewModel;

        #endregion

        #region Enumerations

        /// <summary>
        /// Camera View Modes
        /// =================
        /// Defines the different camera perspectives available for agricultural operations
        /// </summary>
        public enum CameraViewMode
        {
            /// <summary>
            /// Standard field view - similar to original OpenGL implementation
            /// Camera follows vehicle with adjustable distance and angle
            /// </summary>
            FieldView,

            /// <summary>
            /// Top/rear view for ABLS boom monitoring
            /// Camera positioned above and behind vehicle, looking down and forward
            /// Shows tracks over front of machine and booms following terrain contours
            /// </summary>
            TopRearView
        }

        #endregion

        #region Constructor

        /// <summary>
        /// Main Window Constructor
        /// ======================
        /// 
        /// Initializes the WPF 3D main window and sets up the migration infrastructure.
        /// 
        /// Migration Tasks:
        /// 1. Initialize WPF components and HelixToolkit 3D viewport
        /// 2. Set up logging for debugging migration issues
        /// 3. Configure initial camera settings and 3D scene
        /// 4. Initialize data binding for real-time updates
        /// 5. Set up event handlers for user interface interactions
        /// </summary>
        public MainWindow()
        {
            // Initialize WPF components first
            InitializeComponent();

            // Set up logging for this window
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            _logger = loggerFactory.CreateLogger<MainWindow>();

            _logger.LogInformation("=== MainWindow Initialization Starting ===");

            try
            {
                // Initialize ViewModels first - they provide the data for the 3D scene
                InitializeViewModels();

                // Initialize the 3D scene and camera settings
                Initialize3DScene();

                // Set up data binding and ViewModels
                InitializeDataBinding();

                // Configure initial UI state
                InitializeUIState();

                // Set up event handlers for real-time updates
                InitializeEventHandlers();

                _logger.LogInformation("MainWindow initialization completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during MainWindow initialization");
                MessageBox.Show($"Error initializing main window: {ex.Message}", "Initialization Error", 
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Initialization Methods

        /// <summary>
        /// Initialize ViewModels
        /// ====================
        /// 
        /// Creates and configures all MVVM ViewModels for agricultural data management.
        /// This establishes the data layer that feeds the 3D visualization.
        /// 
        /// ViewModels Created:
        /// - MainViewModel: Coordinates all child ViewModels and system state
        /// - GPSViewModel: GPS position, quality, connection status
        /// - ABLSViewModel: Boom control, sensor data, hydraulic states
        /// - TerrainViewModel: DEM data, elevation queries, terrain mesh
        /// </summary>
        private void InitializeViewModels()
        {
            _logger.LogInformation("Initializing MVVM ViewModels");

            try
            {
                // Create the main ViewModel which coordinates all others
                MainViewModel = new MainViewModel();

                // Initialize with mock data for development and testing
                // MainViewModel.InitializeMockData(); // TODO: Implement this method

                _logger.LogInformation("ViewModels initialized successfully");
                _logger.LogInformation($"GPS Status: {MainViewModel.GPS.ConnectionStatusText}");
                _logger.LogInformation($"ABLS Status: {MainViewModel.ABLS.SystemStatusText}");
                _logger.LogInformation($"Terrain Status: {MainViewModel.Terrain.IsDEMLoaded}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing ViewModels");
                throw;
            }
        }

        /// <summary>
        /// Initialize 3D Scene
        /// ===================
        /// 
        /// Sets up the HelixToolkit 3D scene with initial camera position and lighting.
        /// This replaces the OpenGL context initialization from the original FormGPS.
        /// 
        /// Migration Notes:
        /// - HelixToolkit uses retained mode (scene graph) vs OpenGL immediate mode
        /// - Camera and lighting are configured declaratively vs programmatically
        /// - 3D models are added to the scene graph vs drawn each frame
        /// </summary>
        private void Initialize3DScene()
        {
            _logger.LogInformation("Initializing 3D scene with HelixToolkit");

            try
            {
                // Set initial camera position for field view
                // Equivalent to the camera setup in the original OpenGL implementation
                SetFieldViewCamera();

                // Initialize 3D models and scene objects
                InitializeTerrainMesh();
                InitializeVehicleModel();
                InitializeBoomModels();
                InitializeGuidanceLines();
                InitializeLighting();

                _logger.LogInformation("3D scene initialization completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing 3D scene");
                throw;
            }
        }

        /// <summary>
        /// Initialize Data Binding
        /// ======================
        /// 
        /// Sets up MVVM data binding for real-time agricultural data updates.
        /// This replaces the direct property access pattern from the Windows Forms version.
        /// 
        /// Data Binding Setup:
        /// - Set MainViewModel as DataContext for the entire window
        /// - Bind UI controls to ViewModel properties
        /// - Set up property change notifications for real-time updates
        /// - Configure data templates for complex objects
        /// </summary>
        private void InitializeDataBinding()
        {
            _logger.LogInformation("Initializing MVVM data binding");

            try
            {
                // Set the MainViewModel as the DataContext for the entire window
                // This enables data binding in XAML using {Binding PropertyName} syntax
                this.DataContext = MainViewModel;

                // Subscribe to property change events for real-time UI updates
                MainViewModel.PropertyChanged += MainViewModel_PropertyChanged;
                MainViewModel.GPS.PropertyChanged += GPSViewModel_PropertyChanged;
                MainViewModel.ABLS.PropertyChanged += ABLSViewModel_PropertyChanged;
                MainViewModel.Terrain.PropertyChanged += TerrainViewModel_PropertyChanged;

                _logger.LogInformation("Data binding initialization completed");
                _logger.LogInformation("DataContext set to MainViewModel with property change subscriptions");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing data binding");
                throw;
            }
        }

        /// <summary>
        /// Initialize UI State
        /// ==================
        /// 
        /// Sets up the initial state of UI controls and status indicators.
        /// Configures default values and visual states for the agricultural interface.
        /// </summary>
        private void InitializeUIState()
        {
            _logger.LogInformation("Initializing UI state");

            try
            {
                // Set initial GPS status (disconnected)
                UpdateGPSStatus(false, "No GPS connection");

                // Set initial ABLS status (inactive)
                UpdateABLSStatus(false, "ABLS system inactive");

                // Configure target height slider
                sliderTargetHeight.Value = _targetBoomHeight;
                txtTargetHeightValue.Text = $"{_targetBoomHeight:F0} cm";

                // Set initial field information
                txtFieldName.Text = "Field: (No field loaded)";
                txtFieldArea.Text = "Area: 0.0 ha";
                txtCoveredArea.Text = "Covered: 0.0 ha (0%)";

                // Set initial system status
                txtSystemStatus.Text = "System: Ready";
                txtFrameRate.Text = "FPS: --";
                txtDataRate.Text = "GPS: -- Hz";
                txtConnectionStatus.Text = "Connections: Disconnected";

                _logger.LogInformation("UI state initialization completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing UI state");
                throw;
            }
        }

        /// <summary>
        /// Initialize Event Handlers
        /// =========================
        /// 
        /// Sets up event handlers for real-time data updates and user interactions.
        /// This includes GPS data updates, sensor readings, and UI control events.
        /// </summary>
        private void InitializeEventHandlers()
        {
            _logger.LogInformation("Initializing event handlers");

            try
            {
                // TODO: Set up GPS data update timer
                // TODO: Set up ABLS sensor data update timer
                // TODO: Set up 3D rendering update timer
                // TODO: Configure data binding event handlers

                _logger.LogInformation("Event handlers initialization completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing event handlers");
                throw;
            }
        }

        #endregion

        #region Camera Control Methods

        /// <summary>
        /// Set Field View Camera
        /// ====================
        /// 
        /// Configures the camera for standard field operations view.
        /// Similar to the original OpenGL camera positioning in FormGPS.
        /// 
        /// Camera Configuration:
        /// - Positioned behind and above the vehicle
        /// - Follows vehicle movement and rotation
        /// - Adjustable distance and pitch angle
        /// </summary>
        private void SetFieldViewCamera()
        {
            _logger.LogInformation("Setting field view camera");

            try
            {
                // Set camera position for field view
                // This replaces the OpenGL camera matrix calculations
                camera.Position = new System.Windows.Media.Media3D.Point3D(0, -50, 30);
                camera.LookDirection = new System.Windows.Media.Media3D.Vector3D(0, 1, -0.3);
                camera.UpDirection = new System.Windows.Media.Media3D.Vector3D(0, 0, 1);
                camera.FieldOfView = 45;

                _currentCameraMode = CameraViewMode.FieldView;
                _logger.LogInformation("Field view camera configured");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting field view camera");
                throw;
            }
        }

        /// <summary>
        /// Set Top/Rear View Camera
        /// =======================
        /// 
        /// Configures the camera for ABLS boom monitoring view.
        /// Positioned above and behind the vehicle, looking down and forward.
        /// 
        /// ABLS Camera Requirements:
        /// - Shows tracks visible over front of machine
        /// - Displays booms following terrain contours
        /// - Provides clear view of boom positioning relative to ground
        /// </summary>
        private void SetTopRearViewCamera()
        {
            _logger.LogInformation("Setting top/rear view camera for ABLS monitoring");

            try
            {
                // Set camera position for top/rear view
                // Higher altitude, steeper downward angle for boom visibility
                camera.Position = new System.Windows.Media.Media3D.Point3D(0, -80, 60);
                camera.LookDirection = new System.Windows.Media.Media3D.Vector3D(0, 1, -0.8);
                camera.UpDirection = new System.Windows.Media.Media3D.Vector3D(0, 0, 1);
                camera.FieldOfView = 50; // Slightly wider field of view

                _currentCameraMode = CameraViewMode.TopRearView;
                _logger.LogInformation("Top/rear view camera configured for ABLS monitoring");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting top/rear view camera");
                throw;
            }
        }

        #endregion

        #region Status Update Methods

        /// <summary>
        /// Update GPS Status
        /// ================
        /// 
        /// Updates the GPS status indicator and information display.
        /// 
        /// Parameters:
        /// - isConnected: True if GPS is connected and receiving data
        /// - statusMessage: Descriptive status message for display
        /// </summary>
        private void UpdateGPSStatus(bool isConnected, string statusMessage)
        {
            try
            {
                // Update status indicator color
                gpsStatusIndicator.Fill = isConnected ? 
                    new SolidColorBrush(Colors.Green) : 
                    new SolidColorBrush(Colors.Red);

                // Update tooltip with status message
                gpsStatusIndicator.ToolTip = statusMessage;

                _logger.LogDebug("GPS status updated: {Status}", statusMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating GPS status");
            }
        }

        /// <summary>
        /// Update ABLS Status
        /// ==================
        /// 
        /// Updates the ABLS system status indicator and information display.
        /// 
        /// Parameters:
        /// - isActive: True if ABLS system is active and controlling booms
        /// - statusMessage: Descriptive status message for display
        /// </summary>
        private void UpdateABLSStatus(bool isActive, string statusMessage)
        {
            try
            {
                // Update status indicator color
                ablsStatusIndicator.Fill = isActive ? 
                    new SolidColorBrush(Colors.Green) : 
                    new SolidColorBrush(Colors.Red);

                // Update tooltip with status message
                ablsStatusIndicator.ToolTip = statusMessage;

                _isABLSActive = isActive;

                _logger.LogDebug("ABLS status updated: {Status}", statusMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating ABLS status");
            }
        }

        #endregion

        #region Event Handlers - Menu Actions

        /// <summary>
        /// Menu Event Handlers
        /// ===================
        /// 
        /// Event handlers for main menu actions.
        /// These will be implemented to provide the same functionality as the original FormGPS menus.
        /// </summary>

        private void NewField_Click(object sender, RoutedEventArgs e)
        {
            _logger.LogInformation("New field menu item clicked");
            // TODO: Implement new field creation dialog
            MessageBox.Show("New field functionality will be implemented in the next phase.", 
                           "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OpenField_Click(object sender, RoutedEventArgs e)
        {
            _logger.LogInformation("Open field menu item clicked");
            // TODO: Implement field loading dialog
            MessageBox.Show("Open field functionality will be implemented in the next phase.", 
                           "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SaveField_Click(object sender, RoutedEventArgs e)
        {
            _logger.LogInformation("Save field menu item clicked");
            // TODO: Implement field saving functionality
            MessageBox.Show("Save field functionality will be implemented in the next phase.", 
                           "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ImportDEM_Click(object sender, RoutedEventArgs e)
        {
            _logger.LogInformation("Import DEM menu item clicked");
            // TODO: Implement DEM import dialog and terrain generation
            MessageBox.Show("DEM import functionality will be implemented in the next phase.", 
                           "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void EditBoundaries_Click(object sender, RoutedEventArgs e)
        {
            _logger.LogInformation("Edit boundaries menu item clicked");
            // TODO: Implement boundary editing dialog
            MessageBox.Show("Boundary editing functionality will be implemented in the next phase.", 
                           "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void GPSSettings_Click(object sender, RoutedEventArgs e)
        {
            _logger.LogInformation("GPS settings menu item clicked");
            // TODO: Implement GPS settings dialog
            MessageBox.Show("GPS settings functionality will be implemented in the next phase.", 
                           "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void GPSCalibration_Click(object sender, RoutedEventArgs e)
        {
            _logger.LogInformation("GPS calibration menu item clicked");
            // TODO: Implement GPS calibration dialog
            MessageBox.Show("GPS calibration functionality will be implemented in the next phase.", 
                           "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void GPSStatus_Click(object sender, RoutedEventArgs e)
        {
            _logger.LogInformation("GPS status menu item clicked");
            // TODO: Implement GPS status dialog
            MessageBox.Show("GPS status functionality will be implemented in the next phase.", 
                           "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BoomConfig_Click(object sender, RoutedEventArgs e)
        {
            _logger.LogInformation("Boom configuration menu item clicked");
            // TODO: Implement ABLS boom configuration dialog
            MessageBox.Show("Boom configuration functionality will be implemented in the next phase.", 
                           "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SensorCalibration_Click(object sender, RoutedEventArgs e)
        {
            _logger.LogInformation("Sensor calibration menu item clicked");
            // TODO: Implement ABLS sensor calibration dialog
            MessageBox.Show("Sensor calibration functionality will be implemented in the next phase.", 
                           "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void HeightSettings_Click(object sender, RoutedEventArgs e)
        {
            _logger.LogInformation("Height settings menu item clicked");
            // TODO: Implement ABLS height settings dialog
            MessageBox.Show("Height settings functionality will be implemented in the next phase.", 
                           "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ABLSStatus_Click(object sender, RoutedEventArgs e)
        {
            _logger.LogInformation("ABLS status menu item clicked");
            // TODO: Implement ABLS status dialog
            MessageBox.Show("ABLS status functionality will be implemented in the next phase.", 
                           "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region Event Handlers - View Controls

        /// <summary>
        /// Field View Button Click Handler
        /// ==============================
        /// 
        /// Switches to field view camera mode for standard agricultural operations.
        /// </summary>
        private void FieldView_Click(object sender, RoutedEventArgs e)
        {
            _logger.LogInformation("Switching to field view");
            SetFieldViewCamera();
        }

        /// <summary>
        /// Top/Rear View Button Click Handler
        /// ==================================
        /// 
        /// Switches to top/rear view camera mode for ABLS boom monitoring.
        /// </summary>
        private void TopRearView_Click(object sender, RoutedEventArgs e)
        {
            _logger.LogInformation("Switching to top/rear view for ABLS monitoring");
            SetTopRearViewCamera();
        }

        /// <summary>
        /// Reset Camera Button Click Handler
        /// =================================
        /// 
        /// Resets the camera to default position for the current view mode.
        /// </summary>
        private void ResetCamera_Click(object sender, RoutedEventArgs e)
        {
            _logger.LogInformation("Resetting camera to default position");
            
            switch (_currentCameraMode)
            {
                case CameraViewMode.FieldView:
                    SetFieldViewCamera();
                    break;
                case CameraViewMode.TopRearView:
                    SetTopRearViewCamera();
                    break;
            }
        }

        private void FullScreen_Click(object sender, RoutedEventArgs e)
        {
            _logger.LogInformation("Full screen menu item clicked");
            // TODO: Implement full screen mode
            MessageBox.Show("Full screen functionality will be implemented in the next phase.", 
                           "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region Event Handlers - ABLS Controls

        /// <summary>
        /// Target Height Slider Change Handler
        /// ===================================
        /// 
        /// Updates the target boom height when the slider value changes.
        /// </summary>
        private void TargetHeight_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (txtTargetHeightValue != null)
            {
                _targetBoomHeight = e.NewValue;
                txtTargetHeightValue.Text = $"{_targetBoomHeight:F0} cm";
                
                _logger.LogDebug("Target boom height changed to {Height} cm", _targetBoomHeight);
                
                // TODO: Send new target height to ABLS system
            }
        }

        /// <summary>
        /// Auto Mode Button Click Handler
        /// ==============================
        /// 
        /// Activates ABLS automatic boom control mode.
        /// </summary>
        private void AutoMode_Click(object sender, RoutedEventArgs e)
        {
            _logger.LogInformation("ABLS auto mode activated");
            
            // TODO: Activate ABLS automatic control
            txtABLSMode.Text = "Mode: Auto";
            UpdateABLSStatus(true, "ABLS auto mode active");
        }

        /// <summary>
        /// Manual Mode Button Click Handler
        /// ================================
        /// 
        /// Deactivates ABLS automatic boom control mode.
        /// </summary>
        private void ManualMode_Click(object sender, RoutedEventArgs e)
        {
            _logger.LogInformation("ABLS manual mode activated");
            
            // TODO: Deactivate ABLS automatic control
            txtABLSMode.Text = "Mode: Manual";
            UpdateABLSStatus(false, "ABLS manual mode active");
        }

        /// <summary>
        /// Load DEM Button Click Handler
        /// =============================
        /// 
        /// Opens file dialog to load Digital Elevation Model data for terrain visualization.
        /// </summary>
        private void LoadDEM_Click(object sender, RoutedEventArgs e)
        {
            _logger.LogInformation("Load DEM button clicked");
            
            // TODO: Implement DEM file loading dialog
            // TODO: Generate 3D terrain mesh from DEM data
            // TODO: Update terrain visualization in 3D viewport
            
            MessageBox.Show("DEM loading functionality will be implemented in the next phase.", 
                           "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region Event Handlers - Help Menu

        private void About_Click(object sender, RoutedEventArgs e)
        {
            _logger.LogInformation("About menu item clicked");
            
            MessageBox.Show(
                "AgOpenGPS - WPF 3D Agricultural Guidance System\n\n" +
                "This is the modern WPF 3D migration of AgOpenGPS with ABLS support.\n\n" +
                "Features:\n" +
                "• High-performance 3D visualization using HelixToolkit\n" +
                "• DEM-based terrain rendering\n" +
                "• ABLS boom control and monitoring\n" +
                "• Multiple camera views for different operations\n" +
                "• Real-time GPS and sensor integration\n\n" +
                "Migration from OpenGL/Windows Forms to WPF 3D for improved\n" +
                "performance, maintainability, and modern UI capabilities.",
                "About AgOpenGPS WPF 3D",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void MigrationGuide_Click(object sender, RoutedEventArgs e)
        {
            _logger.LogInformation("Migration guide menu item clicked");
            
            MessageBox.Show(
                "WPF 3D Migration Guide\n\n" +
                "This application represents a complete migration from the original\n" +
                "Windows Forms + OpenGL implementation to modern WPF 3D.\n\n" +
                "Key Changes:\n" +
                "• OpenGL immediate mode → HelixToolkit retained mode\n" +
                "• Windows Forms → WPF with MVVM pattern\n" +
                "• Direct property access → Data binding\n" +
                "• Manual rendering → Declarative 3D scene graph\n\n" +
                "Benefits:\n" +
                "• Better performance with DirectX 11 backend\n" +
                "• Modern UI with responsive design\n" +
                "• Improved maintainability and extensibility\n" +
                "• Enhanced ABLS integration capabilities",
                "Migration Guide",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        #endregion

        #region 3D Scene Construction Methods

        /// <summary>
        /// Initialize Terrain Mesh
        /// =======================
        /// 
        /// Creates the initial terrain mesh for field visualization.
        /// Starts with a flat grid and will be replaced with DEM-based terrain when loaded.
        /// 
        /// Migration Notes:
        /// - Replaces OpenGL CWorldGrid immediate mode rendering
        /// - Uses HelixToolkit MeshGeometry3D for retained mode rendering
        /// - Supports texture mapping for field surface visualization
        /// </summary>
        private void InitializeTerrainMesh()
        {
            _logger.LogInformation("Initializing terrain mesh");

            try
            {
                // Create a flat terrain grid as default (100m x 100m)
                var terrainMesh = new MeshGeometry3D();
                var positions = new Point3DCollection();
                var triangleIndices = new Int32Collection();
                var textureCoordinates = new PointCollection();

                // Generate grid vertices (20x20 grid for 100m x 100m field)
                int gridSize = 20;
                double fieldSize = 100.0; // meters
                double stepSize = fieldSize / gridSize;

                for (int i = 0; i <= gridSize; i++)
                {
                    for (int j = 0; j <= gridSize; j++)
                    {
                        double x = (i - gridSize / 2.0) * stepSize;
                        double y = (j - gridSize / 2.0) * stepSize;
                        double z = 0.0; // Flat terrain initially

                        positions.Add(new Point3D(x, y, z));
                        textureCoordinates.Add(new Point((double)i / gridSize, (double)j / gridSize));
                    }
                }

                // Generate triangle indices for the grid
                for (int i = 0; i < gridSize; i++)
                {
                    for (int j = 0; j < gridSize; j++)
                    {
                        int topLeft = i * (gridSize + 1) + j;
                        int topRight = topLeft + 1;
                        int bottomLeft = (i + 1) * (gridSize + 1) + j;
                        int bottomRight = bottomLeft + 1;

                        // First triangle
                        triangleIndices.Add(topLeft);
                        triangleIndices.Add(bottomLeft);
                        triangleIndices.Add(topRight);

                        // Second triangle
                        triangleIndices.Add(topRight);
                        triangleIndices.Add(bottomLeft);
                        triangleIndices.Add(bottomRight);
                    }
                }

                terrainMesh.Positions = positions;
                terrainMesh.TriangleIndices = triangleIndices;
                terrainMesh.TextureCoordinates = textureCoordinates;

                // Create terrain material (green field color)
                var terrainMaterial = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(34, 139, 34)));

                // Create terrain model and add to viewport
                var terrainModel = new GeometryModel3D(terrainMesh, terrainMaterial);
                var terrainVisual = new ModelVisual3D { Content = terrainModel };
                
                // Add to the 3D viewport
                viewport3D.Children.Add(terrainVisual);

                _logger.LogInformation("Terrain mesh initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing terrain mesh");
                throw;
            }
        }

        /// <summary>
        /// Initialize Vehicle Model
        /// ========================
        /// 
        /// Creates the 3D vehicle model for tractor visualization.
        /// Replaces the OpenGL CVehicle immediate mode rendering.
        /// 
        /// Migration Notes:
        /// - Converts from GL.Begin/End primitives to 3D mesh geometry
        /// - Maintains vehicle dimensions and visual appearance
        /// - Supports real-time position and rotation updates
        /// </summary>
        private void InitializeVehicleModel()
        {
            _logger.LogInformation("Initializing vehicle 3D model");

            try
            {
                // Create a simple box geometry for the tractor body
                var vehicleMesh = new MeshGeometry3D();
                
                // Define tractor dimensions (in meters)
                double length = 6.0;  // 6 meter tractor
                double width = 2.5;   // 2.5 meter width
                double height = 1.5;  // 1.5 meter height

                // Create box vertices
                var positions = new Point3DCollection
                {
                    // Bottom face
                    new Point3D(-length/2, -width/2, 0),
                    new Point3D(length/2, -width/2, 0),
                    new Point3D(length/2, width/2, 0),
                    new Point3D(-length/2, width/2, 0),
                    // Top face
                    new Point3D(-length/2, -width/2, height),
                    new Point3D(length/2, -width/2, height),
                    new Point3D(length/2, width/2, height),
                    new Point3D(-length/2, width/2, height)
                };

                // Define triangle indices for box faces
                var triangleIndices = new Int32Collection
                {
                    // Bottom face
                    0, 2, 1, 0, 3, 2,
                    // Top face
                    4, 5, 6, 4, 6, 7,
                    // Front face
                    1, 2, 6, 1, 6, 5,
                    // Back face
                    0, 4, 7, 0, 7, 3,
                    // Left face
                    0, 1, 5, 0, 5, 4,
                    // Right face
                    2, 3, 7, 2, 7, 6
                };

                vehicleMesh.Positions = positions;
                vehicleMesh.TriangleIndices = triangleIndices;

                // Create vehicle material (blue tractor color)
                var vehicleMaterial = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(0, 100, 200)));

                // Create vehicle model
                var vehicleModel = new GeometryModel3D(vehicleMesh, vehicleMaterial);
                var vehicleVisual = new ModelVisual3D { Content = vehicleModel };
                
                // Position vehicle at origin initially
                var vehicleTransform = new Transform3DGroup();
                vehicleTransform.Children.Add(new TranslateTransform3D(0, 0, 0));
                vehicleVisual.Transform = vehicleTransform;
                
                // Add to the 3D viewport
                viewport3D.Children.Add(vehicleVisual);

                _logger.LogInformation("Vehicle 3D model initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing vehicle model");
                throw;
            }
        }

        /// <summary>
        /// Initialize Boom Models
        /// ======================
        /// 
        /// Creates 3D models for ABLS boom sections (center, left wing, right wing).
        /// Enables real-time boom positioning and terrain following visualization.
        /// 
        /// ABLS Integration:
        /// - Center boom section with hydraulic ram
        /// - Left and right wing sections with independent height control
        /// - Visual indicators for boom height and angle
        /// </summary>
        private void InitializeBoomModels()
        {
            _logger.LogInformation("Initializing ABLS boom 3D models");

            try
            {
                // Create center boom section
                CreateBoomSection("Center", 0, 0, Colors.Orange);
                
                // Create left wing boom section
                CreateBoomSection("LeftWing", -8, 0, Colors.Yellow);
                
                // Create right wing boom section
                CreateBoomSection("RightWing", 8, 0, Colors.Yellow);

                _logger.LogInformation("ABLS boom models initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing boom models");
                throw;
            }
        }

        /// <summary>
        /// Create Boom Section
        /// ===================
        /// 
        /// Helper method to create individual boom section 3D models.
        /// </summary>
        private void CreateBoomSection(string name, double xOffset, double yOffset, Color color)
        {
            // Create boom geometry (rectangular beam)
            var boomMesh = new MeshGeometry3D();
            
            double length = 6.0;  // 6 meter boom section
            double width = 0.3;   // 30cm boom width
            double height = 0.2;  // 20cm boom height
            double boomHeight = 0.5; // Initial height above ground

            // Create boom vertices
            var positions = new Point3DCollection
            {
                // Bottom face
                new Point3D(-length/2, -width/2, boomHeight),
                new Point3D(length/2, -width/2, boomHeight),
                new Point3D(length/2, width/2, boomHeight),
                new Point3D(-length/2, width/2, boomHeight),
                // Top face
                new Point3D(-length/2, -width/2, boomHeight + height),
                new Point3D(length/2, -width/2, boomHeight + height),
                new Point3D(length/2, width/2, boomHeight + height),
                new Point3D(-length/2, width/2, boomHeight + height)
            };

            var triangleIndices = new Int32Collection
            {
                // Bottom face
                0, 2, 1, 0, 3, 2,
                // Top face
                4, 5, 6, 4, 6, 7,
                // Front face
                1, 2, 6, 1, 6, 5,
                // Back face
                0, 4, 7, 0, 7, 3,
                // Left face
                0, 1, 5, 0, 5, 4,
                // Right face
                2, 3, 7, 2, 7, 6
            };

            boomMesh.Positions = positions;
            boomMesh.TriangleIndices = triangleIndices;

            // Create boom material
            var boomMaterial = new DiffuseMaterial(new SolidColorBrush(color));

            // Create boom model
            var boomModel = new GeometryModel3D(boomMesh, boomMaterial);
            var boomVisual = new ModelVisual3D { Content = boomModel };
            
            // Position boom section
            var boomTransform = new Transform3DGroup();
            boomTransform.Children.Add(new TranslateTransform3D(xOffset, yOffset, 0));
            boomVisual.Transform = boomTransform;
            
            // Add to the 3D viewport
            viewport3D.Children.Add(boomVisual);
        }

        /// <summary>
        /// Initialize Guidance Lines
        /// =========================
        /// 
        /// Sets up 3D visualization for guidance lines, AB lines, and field boundaries.
        /// Replaces OpenGL line rendering with HelixToolkit 3D line visuals.
        /// </summary>
        private void InitializeGuidanceLines()
        {
            _logger.LogInformation("Initializing guidance lines");

            try
            {
                // TODO: Create AB line visualization
                // TODO: Create curve guidance line visualization
                // TODO: Create field boundary visualization
                // TODO: Create coverage area visualization

                _logger.LogInformation("Guidance lines initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing guidance lines");
                throw;
            }
        }

        /// <summary>
        /// Initialize Lighting
        /// ===================
        /// 
        /// Sets up 3D scene lighting for optimal agricultural visualization.
        /// Replaces OpenGL lighting setup with HelixToolkit lighting models.
        /// </summary>
        private void InitializeLighting()
        {
            _logger.LogInformation("Initializing 3D scene lighting");

            try
            {
                // Add ambient light for overall scene illumination
                var ambientLight = new AmbientLight(Color.FromRgb(64, 64, 64));
                var ambientLightVisual = new ModelVisual3D { Content = ambientLight };
                viewport3D.Children.Add(ambientLightVisual);

                // Add directional light to simulate sunlight
                var directionalLight = new DirectionalLight(Color.FromRgb(255, 255, 255), new Vector3D(-1, -1, -1));
                var directionalLightVisual = new ModelVisual3D { Content = directionalLight };
                viewport3D.Children.Add(directionalLightVisual);

                _logger.LogInformation("3D scene lighting initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing lighting");
                throw;
            }
        }

        #endregion

        #region ViewModel Property Change Handlers

        /// <summary>
        /// Main ViewModel Property Changed Handler
        /// ======================================
        /// 
        /// Handles property changes from the MainViewModel for system-wide updates.
        /// </summary>
        private void MainViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == null) return;

            _logger.LogDebug($"MainViewModel property changed: {e.PropertyName}");

            switch (e.PropertyName)
            {
                case nameof(MainViewModel.SystemStatusMessage):
                    Dispatcher.Invoke(() => txtSystemStatus.Text = $"System: {MainViewModel.SystemStatusMessage}");
                    break;
                case nameof(MainViewModel.IsSystemReady):
                    Dispatcher.Invoke(() => UpdateSystemConnectionStatus());
                    break;
            }
        }

        /// <summary>
        /// GPS ViewModel Property Changed Handler
        /// =====================================
        /// 
        /// Handles GPS data updates for real-time position and status display.
        /// </summary>
        private void GPSViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == null) return;

            var gps = MainViewModel.GPS;
            _logger.LogDebug($"GPS property changed: {e.PropertyName}");

            switch (e.PropertyName)
            {
                case nameof(GPSViewModel.IsConnected):
                case nameof(GPSViewModel.ConnectionStatusText):
                    Dispatcher.Invoke(() => UpdateGPSStatus(gps.IsConnected, gps.ConnectionStatusText));
                    break;
                case nameof(GPSViewModel.Latitude):
                case nameof(GPSViewModel.Longitude):
                    Dispatcher.Invoke(() => Update3DPosition());
                    break;
                // case nameof(GPSViewModel.FixQuality):
                //     Dispatcher.Invoke(() => txtGPSQuality.Text = $"Quality: {gps.FixQuality}");
                    break;
            }
        }

        /// <summary>
        /// ABLS ViewModel Property Changed Handler
        /// ======================================
        /// 
        /// Handles ABLS boom control and sensor data updates.
        /// </summary>
        private void ABLSViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == null) return;

            var abls = MainViewModel.ABLS;
            _logger.LogDebug($"ABLS property changed: {e.PropertyName}");

            switch (e.PropertyName)
            {
                case nameof(ABLSViewModel.IsSystemActive):
                case nameof(ABLSViewModel.SystemStatusText):
                    Dispatcher.Invoke(() => UpdateABLSStatus(abls.IsSystemActive, abls.SystemStatusText));
                    break;
                // case nameof(ABLSViewModel.CenterBoomCurrentHeight):
                // case nameof(ABLSViewModel.LeftWingCurrentHeight):
                // case nameof(ABLSViewModel.RightWingCurrentHeight):
                //     Dispatcher.Invoke(() => Update3DBoomPositions());
                    break;
                case nameof(ABLSViewModel.IsAutoModeEnabled):
                    Dispatcher.Invoke(() => txtABLSMode.Text = $"Mode: {(abls.IsAutoModeEnabled ? "Auto" : "Manual")}");
                    break;
            }
        }

        /// <summary>
        /// Terrain ViewModel Property Changed Handler
        /// =========================================
        /// 
        /// Handles terrain and DEM data updates for 3D visualization.
        /// </summary>
        private void TerrainViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == null) return;

            var terrain = MainViewModel.Terrain;
            _logger.LogDebug($"Terrain property changed: {e.PropertyName}");

            switch (e.PropertyName)
            {
                case nameof(TerrainViewModel.IsDEMLoaded):
                    Dispatcher.Invoke(() => 
                    {
                        _isDEMLoaded = terrain.IsDEMLoaded;
                        if (terrain.IsDEMLoaded)
                        {
                            Update3DTerrainMesh();
                        }
                    });
                    break;
                case nameof(TerrainViewModel.TerrainMesh):
                    Dispatcher.Invoke(() => Update3DTerrainMesh());
                    break;
            }
        }

        #endregion

        #region 3D Scene Update Methods

        /// <summary>
        /// Update 3D Position
        /// ==================
        /// 
        /// Updates the vehicle position in the 3D scene based on GPS coordinates.
        /// </summary>
        private void Update3DPosition()
        {
            // TODO: Update vehicle 3D model position
            // TODO: Update camera following if enabled
            _logger.LogDebug($"Updating 3D position: {MainViewModel.GPS.Latitude}, {MainViewModel.GPS.Longitude}");
        }

        /// <summary>
        /// Update 3D Boom Positions
        /// ========================
        /// 
        /// Updates the boom 3D models based on ABLS sensor data and target heights.
        /// </summary>
        private void Update3DBoomPositions()
        {
            // TODO: Update boom 3D model positions
            // TODO: Update boom following terrain contours
            var abls = MainViewModel.ABLS;
            _logger.LogDebug("Updating boom positions - TODO: Access correct boom height properties");
        }

        /// <summary>
        /// Update 3D Terrain Mesh
        /// ======================
        /// 
        /// Updates the terrain 3D mesh when DEM data changes.
        /// </summary>
        private void Update3DTerrainMesh()
        {
            // TODO: Update terrain mesh in 3D scene
            // TODO: Update boom positions to follow new terrain
            _logger.LogDebug("Updating 3D terrain mesh");
        }

        /// <summary>
        /// Update System Connection Status
        /// ===============================
        /// 
        /// Updates overall system connection status display.
        /// </summary>
        private void UpdateSystemConnectionStatus()
        {
            var status = MainViewModel.IsSystemReady ? "Ready" : "Not Ready";
            txtSystemStatus.Text = $"System: {status}";
        }

        #endregion
    }
}
