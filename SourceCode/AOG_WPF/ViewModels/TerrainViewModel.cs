using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace AOG_WPF.ViewModels
{
    /// <summary>
    /// Terrain and DEM (Digital Elevation Model) ViewModel
    /// ===================================================
    /// 
    /// Manages terrain data, DEM loading, and 3D surface generation for the WPF 3D migration.
    /// This ViewModel is critical for ABLS functionality as it provides the ground elevation
    /// data needed for accurate boom height calculations and terrain following.
    /// 
    /// Migration Notes:
    /// - Replaces the flat grid rendering from the original OpenGL implementation
    /// - Provides 3D terrain surface based on real elevation data
    /// - Enables accurate boom positioning relative to actual ground contours
    /// - Supports multiple DEM formats and coordinate systems
    /// 
    /// ABLS Integration:
    /// - Provides ground elevation at any GPS coordinate for boom height calculations
    /// - Enables terrain-following mode where booms adjust to ground contours
    /// - Supports real-time elevation queries for guidance line projection
    /// - Integrates with GPS coordinates for accurate positioning
    /// </summary>
    public class TerrainViewModel : BaseViewModel
    {
        #region Private Fields

        /// <summary>
        /// DEM data and terrain properties
        /// </summary>
        private bool _isDEMLoaded;
        private string _demFileName = "";
        private double _demResolution;
        private double _minElevation;
        private double _maxElevation;
        private double _terrainWidth;
        private double _terrainHeight;

        /// <summary>
        /// Current position and elevation data
        /// </summary>
        private double _currentElevation;
        private double _centerBoomElevation;
        private double _leftWingElevation;
        private double _rightWingElevation;

        /// <summary>
        /// 3D mesh and rendering data
        /// </summary>
        private MeshGeometry3D? _terrainMesh;
        private int _meshResolution = 100; // Default mesh resolution
        private bool _isTerrainVisible = true;

        /// <summary>
        /// DEM data storage
        /// Grid of elevation values indexed by [x,y] coordinates
        /// </summary>
        private double[,]? _elevationGrid;
        private double _gridOriginX;
        private double _gridOriginY;
        private double _gridSpacingX;
        private double _gridSpacingY;
        private int _gridWidth;
        private int _gridHeight;

        #endregion

        #region DEM Status Properties

        /// <summary>
        /// Is DEM Loaded Property
        /// =====================
        /// 
        /// Indicates whether a Digital Elevation Model has been successfully loaded.
        /// When true, terrain elevation data is available for ABLS calculations.
        /// </summary>
        public bool IsDEMLoaded
        {
            get => _isDEMLoaded;
            private set
            {
                if (SetProperty(ref _isDEMLoaded, value))
                {
                    OnPropertyChanged(nameof(DEMStatusText));
                    OnPropertyChanged(nameof(DEMResolutionText));
                }
            }
        }

        /// <summary>
        /// DEM File Name Property
        /// =====================
        /// 
        /// Name of the currently loaded DEM file.
        /// </summary>
        public string DEMFileName
        {
            get => _demFileName;
            private set => SetProperty(ref _demFileName, value);
        }

        /// <summary>
        /// DEM Resolution Property (meters)
        /// ===============================
        /// 
        /// Spatial resolution of the DEM data in meters per pixel.
        /// Lower values indicate higher resolution and more detailed terrain data.
        /// </summary>
        public double DEMResolution
        {
            get => _demResolution;
            private set => SetProperty(ref _demResolution, value);
        }

        /// <summary>
        /// Minimum Elevation Property (meters)
        /// ==================================
        /// 
        /// Lowest elevation value in the loaded DEM data.
        /// </summary>
        public double MinElevation
        {
            get => _minElevation;
            private set => SetProperty(ref _minElevation, value);
        }

        /// <summary>
        /// Maximum Elevation Property (meters)
        /// ==================================
        /// 
        /// Highest elevation value in the loaded DEM data.
        /// </summary>
        public double MaxElevation
        {
            get => _maxElevation;
            private set => SetProperty(ref _maxElevation, value);
        }

        /// <summary>
        /// Terrain Width Property (meters)
        /// ==============================
        /// 
        /// Width of the terrain area covered by the DEM data.
        /// </summary>
        public double TerrainWidth
        {
            get => _terrainWidth;
            private set => SetProperty(ref _terrainWidth, value);
        }

        /// <summary>
        /// Terrain Height Property (meters)
        /// ===============================
        /// 
        /// Height of the terrain area covered by the DEM data.
        /// </summary>
        public double TerrainHeight
        {
            get => _terrainHeight;
            private set => SetProperty(ref _terrainHeight, value);
        }

        #endregion

        #region Current Elevation Properties

        /// <summary>
        /// Current Elevation Property (meters)
        /// ==================================
        /// 
        /// Ground elevation at the current GPS position.
        /// Updated in real-time as the vehicle moves across the terrain.
        /// </summary>
        public double CurrentElevation
        {
            get => _currentElevation;
            set
            {
                if (SetProperty(ref _currentElevation, value))
                {
                    OnPropertyChanged(nameof(CurrentElevationText));
                }
            }
        }

        /// <summary>
        /// Center Boom Elevation Property (meters)
        /// ======================================
        /// 
        /// Ground elevation at the center boom GPS position.
        /// Used for ABLS center boom height calculations.
        /// </summary>
        public double CenterBoomElevation
        {
            get => _centerBoomElevation;
            set => SetProperty(ref _centerBoomElevation, value);
        }

        /// <summary>
        /// Left Wing Elevation Property (meters)
        /// ====================================
        /// 
        /// Ground elevation at the left wing boom GPS position.
        /// Used for ABLS left wing height calculations.
        /// </summary>
        public double LeftWingElevation
        {
            get => _leftWingElevation;
            set => SetProperty(ref _leftWingElevation, value);
        }

        /// <summary>
        /// Right Wing Elevation Property (meters)
        /// =====================================
        /// 
        /// Ground elevation at the right wing boom GPS position.
        /// Used for ABLS right wing height calculations.
        /// </summary>
        public double RightWingElevation
        {
            get => _rightWingElevation;
            set => SetProperty(ref _rightWingElevation, value);
        }

        #endregion

        #region 3D Rendering Properties

        /// <summary>
        /// Terrain Mesh Property
        /// =====================
        /// 
        /// 3D mesh geometry representing the terrain surface.
        /// This mesh is rendered in the WPF 3D viewport to show ground contours.
        /// </summary>
        public MeshGeometry3D? TerrainMesh
        {
            get => _terrainMesh;
            private set => SetProperty(ref _terrainMesh, value);
        }

        /// <summary>
        /// Mesh Resolution Property
        /// =======================
        /// 
        /// Resolution of the 3D mesh used for terrain rendering.
        /// Higher values create more detailed but slower-rendering terrain.
        /// Range: 50-500 (typical values for agricultural applications)
        /// </summary>
        public int MeshResolution
        {
            get => _meshResolution;
            set
            {
                var clampedValue = Math.Max(50, Math.Min(500, value));
                if (SetProperty(ref _meshResolution, clampedValue))
                {
                    // Regenerate mesh with new resolution if DEM is loaded
                    if (IsDEMLoaded)
                    {
                        GenerateTerrainMesh();
                    }
                }
            }
        }

        /// <summary>
        /// Is Terrain Visible Property
        /// ===========================
        /// 
        /// Controls whether the terrain surface is visible in the 3D viewport.
        /// Useful for toggling terrain display for different viewing modes.
        /// </summary>
        public bool IsTerrainVisible
        {
            get => _isTerrainVisible;
            set => SetProperty(ref _isTerrainVisible, value);
        }

        #endregion

        #region UI Display Properties

        /// <summary>
        /// DEM Status Text for UI Display
        /// ==============================
        /// </summary>
        public string DEMStatusText => IsDEMLoaded ? $"DEM: {DEMFileName}" : "DEM: Not loaded";

        /// <summary>
        /// DEM Resolution Text for UI Display
        /// ==================================
        /// </summary>
        public string DEMResolutionText => IsDEMLoaded ? $"Resolution: {DEMResolution:F1}m" : "Resolution: --";

        /// <summary>
        /// Current Elevation Text for UI Display
        /// =====================================
        /// </summary>
        public string CurrentElevationText => IsDEMLoaded ? $"Ground Elevation: {CurrentElevation:F1} m" : "Ground Elevation: -- m";

        #endregion

        #region Constructor

        /// <summary>
        /// Terrain ViewModel Constructor
        /// ============================
        /// 
        /// Initializes the terrain ViewModel with default values.
        /// Sets up initial state for no DEM data loaded.
        /// </summary>
        public TerrainViewModel()
        {
            // Initialize DEM status
            _isDEMLoaded = false;
            _demFileName = "";
            _demResolution = 0.0;
            _minElevation = 0.0;
            _maxElevation = 0.0;
            _terrainWidth = 0.0;
            _terrainHeight = 0.0;

            // Initialize current elevations
            _currentElevation = 0.0;
            _centerBoomElevation = 0.0;
            _leftWingElevation = 0.0;
            _rightWingElevation = 0.0;

            // Initialize 3D rendering
            _terrainMesh = null;
            _meshResolution = 100;
            _isTerrainVisible = true;

            // Initialize DEM grid
            _elevationGrid = null;
            _gridOriginX = 0.0;
            _gridOriginY = 0.0;
            _gridSpacingX = 1.0;
            _gridSpacingY = 1.0;
            _gridWidth = 0;
            _gridHeight = 0;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Load DEM Data
        /// =============
        /// 
        /// Loads Digital Elevation Model data from a file.
        /// Supports common DEM formats used in agriculture.
        /// 
        /// Parameters:
        /// - filePath: Path to the DEM file
        /// - originX: X coordinate of grid origin (easting)
        /// - originY: Y coordinate of grid origin (northing)
        /// - spacingX: Grid spacing in X direction (meters)
        /// - spacingY: Grid spacing in Y direction (meters)
        /// - elevationData: 2D array of elevation values
        /// 
        /// Returns:
        /// - true if DEM was loaded successfully
        /// - false if loading failed
        /// </summary>
        public bool LoadDEMData(string filePath, double originX, double originY, 
                               double spacingX, double spacingY, double[,] elevationData)
        {
            try
            {
                IsLoading = true;
                ClearError();

                // Validate input parameters
                if (string.IsNullOrEmpty(filePath))
                {
                    SetError("Invalid file path");
                    return false;
                }

                if (elevationData == null || elevationData.GetLength(0) == 0 || elevationData.GetLength(1) == 0)
                {
                    SetError("Invalid elevation data");
                    return false;
                }

                // Store DEM data
                _elevationGrid = elevationData;
                _gridOriginX = originX;
                _gridOriginY = originY;
                _gridSpacingX = spacingX;
                _gridSpacingY = spacingY;
                _gridWidth = elevationData.GetLength(0);
                _gridHeight = elevationData.GetLength(1);

                // Calculate DEM properties
                DEMFileName = System.IO.Path.GetFileName(filePath);
                DEMResolution = Math.Min(spacingX, spacingY);
                TerrainWidth = _gridWidth * spacingX;
                TerrainHeight = _gridHeight * spacingY;

                // Calculate elevation range
                CalculateElevationRange();

                // Generate 3D terrain mesh
                GenerateTerrainMesh();

                // Mark DEM as loaded
                IsDEMLoaded = true;

                return true;
            }
            catch (Exception ex)
            {
                SetError($"Failed to load DEM: {ex.Message}");
                return false;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Get Elevation at GPS Coordinate
        /// ==============================
        /// 
        /// Returns the ground elevation at a specific GPS coordinate.
        /// Uses bilinear interpolation for smooth elevation values between grid points.
        /// 
        /// Parameters:
        /// - easting: X coordinate (easting) in meters
        /// - northing: Y coordinate (northing) in meters
        /// 
        /// Returns:
        /// - Ground elevation in meters, or 0.0 if coordinate is outside DEM area
        /// </summary>
        public double GetElevationAtCoordinate(double easting, double northing)
        {
            if (!IsDEMLoaded || _elevationGrid == null)
            {
                return 0.0;
            }

            // Convert GPS coordinates to grid indices
            double gridX = (easting - _gridOriginX) / _gridSpacingX;
            double gridY = (northing - _gridOriginY) / _gridSpacingY;

            // Check if coordinate is within grid bounds
            if (gridX < 0 || gridX >= _gridWidth - 1 || gridY < 0 || gridY >= _gridHeight - 1)
            {
                return 0.0; // Outside DEM area
            }

            // Get integer grid indices for bilinear interpolation
            int x0 = (int)Math.Floor(gridX);
            int y0 = (int)Math.Floor(gridY);
            int x1 = Math.Min(x0 + 1, _gridWidth - 1);
            int y1 = Math.Min(y0 + 1, _gridHeight - 1);

            // Get fractional parts for interpolation
            double fx = gridX - x0;
            double fy = gridY - y0;

            // Get elevation values at grid corners
            double z00 = _elevationGrid[x0, y0];
            double z10 = _elevationGrid[x1, y0];
            double z01 = _elevationGrid[x0, y1];
            double z11 = _elevationGrid[x1, y1];

            // Perform bilinear interpolation
            double z0 = z00 * (1 - fx) + z10 * fx;
            double z1 = z01 * (1 - fx) + z11 * fx;
            double elevation = z0 * (1 - fy) + z1 * fy;

            return elevation;
        }

        /// <summary>
        /// Update Current Position Elevation
        /// ================================
        /// 
        /// Updates the current elevation based on GPS position.
        /// Should be called when GPS position changes.
        /// 
        /// Parameters:
        /// - easting: Current X coordinate (easting) in meters
        /// - northing: Current Y coordinate (northing) in meters
        /// </summary>
        public void UpdateCurrentPositionElevation(double easting, double northing)
        {
            CurrentElevation = GetElevationAtCoordinate(easting, northing);
        }

        /// <summary>
        /// Update ABLS Boom Elevations
        /// ===========================
        /// 
        /// Updates ground elevations for all ABLS boom positions.
        /// Critical for accurate boom height calculations.
        /// 
        /// Parameters:
        /// - centerEasting: Center boom X coordinate
        /// - centerNorthing: Center boom Y coordinate
        /// - leftEasting: Left wing X coordinate
        /// - leftNorthing: Left wing Y coordinate
        /// - rightEasting: Right wing X coordinate
        /// - rightNorthing: Right wing Y coordinate
        /// </summary>
        public void UpdateABLSBoomElevations(double centerEasting, double centerNorthing,
                                           double leftEasting, double leftNorthing,
                                           double rightEasting, double rightNorthing)
        {
            CenterBoomElevation = GetElevationAtCoordinate(centerEasting, centerNorthing);
            LeftWingElevation = GetElevationAtCoordinate(leftEasting, leftNorthing);
            RightWingElevation = GetElevationAtCoordinate(rightEasting, rightNorthing);
        }

        /// <summary>
        /// Clear DEM Data
        /// ==============
        /// 
        /// Clears all loaded DEM data and resets to default state.
        /// </summary>
        public void ClearDEMData()
        {
            _elevationGrid = null;
            TerrainMesh = null;
            IsDEMLoaded = false;
            DEMFileName = "";
            DEMResolution = 0.0;
            MinElevation = 0.0;
            MaxElevation = 0.0;
            TerrainWidth = 0.0;
            TerrainHeight = 0.0;
            CurrentElevation = 0.0;
            CenterBoomElevation = 0.0;
            LeftWingElevation = 0.0;
            RightWingElevation = 0.0;
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Calculate Elevation Range
        /// ========================
        /// 
        /// Calculates the minimum and maximum elevation values in the DEM data.
        /// </summary>
        private void CalculateElevationRange()
        {
            if (_elevationGrid == null)
                return;

            double min = double.MaxValue;
            double max = double.MinValue;

            for (int x = 0; x < _gridWidth; x++)
            {
                for (int y = 0; y < _gridHeight; y++)
                {
                    double elevation = _elevationGrid[x, y];
                    if (elevation < min) min = elevation;
                    if (elevation > max) max = elevation;
                }
            }

            MinElevation = min;
            MaxElevation = max;
        }

        /// <summary>
        /// Generate Terrain Mesh
        /// =====================
        /// 
        /// Generates a 3D mesh from the DEM data for rendering in the WPF viewport.
        /// Creates a triangle mesh with vertices positioned according to elevation data.
        /// </summary>
        private void GenerateTerrainMesh()
        {
            if (_elevationGrid == null || !IsDEMLoaded)
            {
                TerrainMesh = null;
                return;
            }

            try
            {
                var mesh = new MeshGeometry3D();
                var positions = new Point3DCollection();
                var triangleIndices = new Int32Collection();
                var textureCoordinates = new PointCollection();

                // Calculate step size based on mesh resolution
                int stepX = Math.Max(1, _gridWidth / _meshResolution);
                int stepY = Math.Max(1, _gridHeight / _meshResolution);

                // Generate vertices
                for (int x = 0; x < _gridWidth; x += stepX)
                {
                    for (int y = 0; y < _gridHeight; y += stepY)
                    {
                        // Calculate world coordinates
                        double worldX = _gridOriginX + x * _gridSpacingX;
                        double worldY = _gridOriginY + y * _gridSpacingY;
                        double worldZ = _elevationGrid[x, y];

                        // Add vertex position
                        positions.Add(new Point3D(worldX, worldY, worldZ));

                        // Add texture coordinate
                        double u = (double)x / (_gridWidth - 1);
                        double v = (double)y / (_gridHeight - 1);
                        textureCoordinates.Add(new System.Windows.Point(u, v));
                    }
                }

                // Generate triangle indices
                int verticesPerRow = (_gridHeight + stepY - 1) / stepY;
                for (int x = 0; x < (_gridWidth - stepX); x += stepX)
                {
                    for (int y = 0; y < (_gridHeight - stepY); y += stepY)
                    {
                        int row = x / stepX;
                        int col = y / stepY;

                        // Calculate vertex indices for current quad
                        int v0 = row * verticesPerRow + col;
                        int v1 = v0 + 1;
                        int v2 = (row + 1) * verticesPerRow + col;
                        int v3 = v2 + 1;

                        // Add two triangles for the quad
                        // Triangle 1: v0, v1, v2
                        triangleIndices.Add(v0);
                        triangleIndices.Add(v1);
                        triangleIndices.Add(v2);

                        // Triangle 2: v1, v3, v2
                        triangleIndices.Add(v1);
                        triangleIndices.Add(v3);
                        triangleIndices.Add(v2);
                    }
                }

                // Assign collections to mesh
                mesh.Positions = positions;
                mesh.TriangleIndices = triangleIndices;
                mesh.TextureCoordinates = textureCoordinates;

                // Set the terrain mesh
                TerrainMesh = mesh;
            }
            catch (Exception ex)
            {
                SetError($"Failed to generate terrain mesh: {ex.Message}");
                TerrainMesh = null;
            }
        }

        #endregion
    }
}
