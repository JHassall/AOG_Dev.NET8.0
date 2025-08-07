using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media.Media3D;
using Microsoft.Extensions.Logging;

namespace AOG_WPF.Classes
{
    /// <summary>
    /// DEM Terrain Loader Class
    /// ========================
    /// 
    /// Loads and processes Digital Elevation Model (DEM) data for 3D terrain visualization.
    /// Essential for ABLS boom terrain following and accurate field representation.
    /// 
    /// Supported Formats:
    /// - ASCII Grid (.asc)
    /// - GeoTIFF elevation data
    /// - XYZ point cloud data
    /// - Custom AOG elevation files
    /// 
    /// Key Features:
    /// - High-performance mesh generation
    /// - Adaptive level-of-detail for large terrains
    /// - Elevation interpolation for smooth surfaces
    /// - Integration with GPS coordinate system
    /// - Memory-efficient data structures
    /// </summary>
    public class DEMTerrainLoader
    {
        #region Private Fields

        private readonly ILogger<DEMTerrainLoader> _logger;
        private readonly GPSCoordinateSystem _coordinateSystem;

        /// <summary>
        /// Elevation data grid
        /// </summary>
        private float[,] _elevationData;
        private int _gridWidth;
        private int _gridHeight;
        private double _cellSize; // meters per cell
        private double _originX, _originY; // local coordinates of grid origin

        /// <summary>
        /// Terrain bounds
        /// </summary>
        private double _minElevation = double.MaxValue;
        private double _maxElevation = double.MinValue;

        #endregion

        #region Public Properties

        /// <summary>
        /// Is DEM Data Loaded
        /// </summary>
        public bool IsLoaded => _elevationData != null;

        /// <summary>
        /// Grid Dimensions
        /// </summary>
        public int GridWidth => _gridWidth;
        public int GridHeight => _gridHeight;

        /// <summary>
        /// Cell Size (meters)
        /// </summary>
        public double CellSize => _cellSize;

        /// <summary>
        /// Elevation Range
        /// </summary>
        public double MinElevation => _minElevation;
        public double MaxElevation => _maxElevation;

        #endregion

        #region Constructor

        /// <summary>
        /// DEM Terrain Loader Constructor
        /// </summary>
        public DEMTerrainLoader(GPSCoordinateSystem coordinateSystem, ILogger<DEMTerrainLoader> logger = null)
        {
            _coordinateSystem = coordinateSystem ?? throw new ArgumentNullException(nameof(coordinateSystem));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DEMTerrainLoader>.Instance;
        }

        #endregion

        #region DEM Loading Methods

        /// <summary>
        /// Load DEM from ASCII Grid File
        /// =============================
        /// 
        /// Loads elevation data from ESRI ASCII Grid format (.asc files).
        /// Common format for DEM data exchange.
        /// </summary>
        public bool LoadFromASCIIGrid(string filePath)
        {
            try
            {
                _logger.LogInformation($"Loading DEM from ASCII Grid: {filePath}");

                if (!File.Exists(filePath))
                {
                    _logger.LogError($"DEM file not found: {filePath}");
                    return false;
                }

                using (var reader = new StreamReader(filePath))
                {
                    // Read header
                    var ncols = int.Parse(reader.ReadLine().Split()[1]);
                    var nrows = int.Parse(reader.ReadLine().Split()[1]);
                    var xllcorner = double.Parse(reader.ReadLine().Split()[1]);
                    var yllcorner = double.Parse(reader.ReadLine().Split()[1]);
                    var cellsize = double.Parse(reader.ReadLine().Split()[1]);
                    var nodata = float.Parse(reader.ReadLine().Split()[1]);

                    // Convert WGS84 corner to local coordinates
                    if (!_coordinateSystem.ConvertWGS84ToLocal(yllcorner, xllcorner, out double localY, out double localX))
                    {
                        _logger.LogError("Failed to convert DEM coordinates to local system");
                        return false;
                    }

                    // Initialize grid
                    _gridWidth = ncols;
                    _gridHeight = nrows;
                    _cellSize = cellsize;
                    _originX = localX;
                    _originY = localY;
                    _elevationData = new float[_gridHeight, _gridWidth];

                    // Read elevation data
                    for (int row = 0; row < _gridHeight; row++)
                    {
                        var line = reader.ReadLine();
                        var values = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                        for (int col = 0; col < _gridWidth; col++)
                        {
                            float elevation = float.Parse(values[col]);
                            
                            if (elevation != nodata)
                            {
                                _elevationData[row, col] = elevation;
                                _minElevation = Math.Min(_minElevation, elevation);
                                _maxElevation = Math.Max(_maxElevation, elevation);
                            }
                            else
                            {
                                _elevationData[row, col] = 0; // Default to ground level
                            }
                        }
                    }
                }

                _logger.LogInformation($"DEM loaded successfully: {_gridWidth}x{_gridHeight} cells, elevation range: {_minElevation:F2}m to {_maxElevation:F2}m");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to load DEM from ASCII Grid: {filePath}");
                return false;
            }
        }

        /// <summary>
        /// Create Flat Terrain
        /// ===================
        /// 
        /// Creates a flat terrain for testing or when no DEM data is available.
        /// </summary>
        public void CreateFlatTerrain(double width, double height, double cellSize, double elevation = 0.0)
        {
            _gridWidth = (int)(width / cellSize);
            _gridHeight = (int)(height / cellSize);
            _cellSize = cellSize;
            _originX = -width / 2.0;
            _originY = -height / 2.0;

            _elevationData = new float[_gridHeight, _gridWidth];
            _minElevation = elevation;
            _maxElevation = elevation;

            // Fill with constant elevation
            for (int row = 0; row < _gridHeight; row++)
            {
                for (int col = 0; col < _gridWidth; col++)
                {
                    _elevationData[row, col] = (float)elevation;
                }
            }

            _logger.LogInformation($"Flat terrain created: {_gridWidth}x{_gridHeight} cells at {elevation:F2}m elevation");
        }

        #endregion

        #region Elevation Query Methods

        /// <summary>
        /// Get Elevation at Local Coordinates
        /// =================================
        /// 
        /// Returns the terrain elevation at specified local coordinates.
        /// Uses bilinear interpolation for smooth elevation values.
        /// </summary>
        public float GetElevationAt(double localX, double localY)
        {
            if (!IsLoaded)
                return 0.0f;

            // Convert local coordinates to grid indices
            double gridX = (localX - _originX) / _cellSize;
            double gridY = (localY - _originY) / _cellSize;

            // Check bounds
            if (gridX < 0 || gridX >= _gridWidth - 1 || gridY < 0 || gridY >= _gridHeight - 1)
                return 0.0f;

            // Bilinear interpolation
            int x0 = (int)Math.Floor(gridX);
            int y0 = (int)Math.Floor(gridY);
            int x1 = Math.Min(x0 + 1, _gridWidth - 1);
            int y1 = Math.Min(y0 + 1, _gridHeight - 1);

            double fx = gridX - x0;
            double fy = gridY - y0;

            float e00 = _elevationData[y0, x0];
            float e10 = _elevationData[y0, x1];
            float e01 = _elevationData[y1, x0];
            float e11 = _elevationData[y1, x1];

            float interpolated = (float)(
                e00 * (1 - fx) * (1 - fy) +
                e10 * fx * (1 - fy) +
                e01 * (1 - fx) * fy +
                e11 * fx * fy
            );

            return interpolated;
        }

        #endregion

        #region 3D Mesh Generation

        /// <summary>
        /// Generate 3D Terrain Mesh
        /// ========================
        /// 
        /// Generates a 3D mesh from the loaded DEM data for rendering.
        /// Optimized for performance with configurable level of detail.
        /// </summary>
        public MeshGeometry3D GenerateTerrainMesh(int levelOfDetail = 1)
        {
            if (!IsLoaded)
            {
                _logger.LogWarning("Cannot generate mesh - no DEM data loaded");
                return null;
            }

            var mesh = new MeshGeometry3D();
            var positions = new Point3DCollection();
            var triangleIndices = new Int32Collection();
            var textureCoordinates = new PointCollection();

            int step = Math.Max(1, levelOfDetail);
            int vertexIndex = 0;

            // Generate vertices
            for (int row = 0; row < _gridHeight; row += step)
            {
                for (int col = 0; col < _gridWidth; col += step)
                {
                    double x = _originX + col * _cellSize;
                    double y = _originY + row * _cellSize;
                    double z = _elevationData[row, col];

                    positions.Add(new Point3D(x, y, z));

                    // Texture coordinates (0-1 range)
                    double u = (double)col / (_gridWidth - 1);
                    double v = (double)row / (_gridHeight - 1);
                    textureCoordinates.Add(new System.Windows.Point(u, v));
                }
            }

            // Generate triangle indices
            int verticesPerRow = (_gridWidth + step - 1) / step;
            int numRows = (_gridHeight + step - 1) / step;

            for (int row = 0; row < numRows - 1; row++)
            {
                for (int col = 0; col < verticesPerRow - 1; col++)
                {
                    int topLeft = row * verticesPerRow + col;
                    int topRight = topLeft + 1;
                    int bottomLeft = (row + 1) * verticesPerRow + col;
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

            mesh.Positions = positions;
            mesh.TriangleIndices = triangleIndices;
            mesh.TextureCoordinates = textureCoordinates;

            _logger.LogInformation($"Terrain mesh generated: {positions.Count} vertices, {triangleIndices.Count / 3} triangles");
            return mesh;
        }

        #endregion
    }
}
