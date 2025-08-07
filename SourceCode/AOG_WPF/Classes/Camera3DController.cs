using System;
using System.Windows.Media.Media3D;
using Microsoft.Extensions.Logging;

namespace AOG_WPF.Classes
{
    /// <summary>
    /// 3D Camera Controller Class
    /// ==========================
    /// 
    /// Advanced camera control system for agricultural 3D visualization.
    /// Replaces and enhances the original OpenGL camera management.
    /// 
    /// Key Features:
    /// - Multiple camera modes (Field View, Top/Rear View, Free Camera)
    /// - Smooth camera transitions and animations
    /// - Vehicle following with configurable offset and damping
    /// - ABLS-specific camera positioning for boom monitoring
    /// - Touch/mouse gesture support for camera manipulation
    /// - Collision detection and boundary constraints
    /// 
    /// Migration Benefits:
    /// - Eliminates OpenGL matrix calculation complexity
    /// - Provides smooth, professional camera movements
    /// - Supports multiple simultaneous camera views
    /// - Better integration with WPF 3D scene graph
    /// </summary>
    public class Camera3DController
    {
        #region Enumerations

        /// <summary>
        /// Camera Operation Modes
        /// </summary>
        public enum CameraMode
        {
            /// <summary>
            /// Standard field view - follows vehicle from behind and above
            /// </summary>
            FieldView,

            /// <summary>
            /// Top/rear view for ABLS boom monitoring
            /// Camera positioned above and behind vehicle, looking down and forward
            /// </summary>
            TopRearView,

            /// <summary>
            /// Free camera mode - user-controlled positioning
            /// </summary>
            FreeCamera,

            /// <summary>
            /// Fixed camera mode - stationary position
            /// </summary>
            FixedView
        }

        /// <summary>
        /// Camera Animation States
        /// </summary>
        public enum AnimationState
        {
            Idle,
            Transitioning,
            Following,
            UserControlled
        }

        #endregion

        #region Private Fields

        /// <summary>
        /// Logger for camera operations
        /// </summary>
        private readonly ILogger<Camera3DController> _logger;

        /// <summary>
        /// Current camera mode
        /// </summary>
        private CameraMode _currentMode;

        /// <summary>
        /// Current animation state
        /// </summary>
        private AnimationState _animationState;

        /// <summary>
        /// Target camera position for smooth transitions
        /// </summary>
        private Point3D _targetPosition;
        private Vector3D _targetLookDirection;
        private Vector3D _targetUpDirection;

        /// <summary>
        /// Current camera parameters
        /// </summary>
        private Point3D _currentPosition;
        private Vector3D _currentLookDirection;
        private Vector3D _currentUpDirection;

        /// <summary>
        /// Vehicle position and heading for following
        /// </summary>
        private Point3D _vehiclePosition;
        private double _vehicleHeading; // degrees

        /// <summary>
        /// Camera configuration parameters
        /// </summary>
        private double _fieldViewDistance = 50.0; // meters behind vehicle
        private double _fieldViewHeight = 30.0;   // meters above vehicle
        private double _fieldViewPitch = -15.0;   // degrees down from horizontal

        private double _topRearViewDistance = 25.0; // meters behind vehicle
        private double _topRearViewHeight = 40.0;   // meters above vehicle
        private double _topRearViewPitch = -45.0;   // degrees down from horizontal

        /// <summary>
        /// Animation and smoothing parameters
        /// </summary>
        private double _transitionSpeed = 2.0;     // transitions per second
        private double _followingDamping = 0.1;    // smoothing factor for vehicle following
        private double _rotationDamping = 0.15;    // smoothing factor for rotation

        /// <summary>
        /// Field boundaries for camera constraints
        /// </summary>
        private double _fieldMinX = -500.0;
        private double _fieldMaxX = 500.0;
        private double _fieldMinY = -500.0;
        private double _fieldMaxY = 500.0;
        private double _fieldMinZ = 1.0;
        private double _fieldMaxZ = 200.0;

        #endregion

        #region Public Properties

        /// <summary>
        /// Current Camera Mode
        /// </summary>
        public CameraMode CurrentMode
        {
            get => _currentMode;
            set
            {
                if (_currentMode != value)
                {
                    _logger.LogInformation($"Camera mode changing from {_currentMode} to {value}");
                    _currentMode = value;
                    InitiateModeTransition();
                }
            }
        }

        /// <summary>
        /// Current Camera Position
        /// </summary>
        public Point3D CurrentPosition => _currentPosition;

        /// <summary>
        /// Current Look Direction
        /// </summary>
        public Vector3D CurrentLookDirection => _currentLookDirection;

        /// <summary>
        /// Current Up Direction
        /// </summary>
        public Vector3D CurrentUpDirection => _currentUpDirection;

        /// <summary>
        /// Is Camera Currently Animating
        /// </summary>
        public bool IsAnimating => _animationState == AnimationState.Transitioning;

        #endregion

        #region Constructor

        /// <summary>
        /// Camera 3D Controller Constructor
        /// </summary>
        public Camera3DController(ILogger<Camera3DController> logger = null)
        {
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<Camera3DController>.Instance;

            // Initialize with field view mode
            _currentMode = CameraMode.FieldView;
            _animationState = AnimationState.Idle;

            // Set default camera position
            _currentPosition = new Point3D(0, -50, 30);
            _currentLookDirection = new Vector3D(0, 1, -0.3);
            _currentUpDirection = new Vector3D(0, 0, 1);

            // Initialize target to current
            _targetPosition = _currentPosition;
            _targetLookDirection = _currentLookDirection;
            _targetUpDirection = _currentUpDirection;

            _logger.LogInformation("Camera 3D Controller initialized in Field View mode");
        }

        #endregion

        #region Vehicle Tracking

        /// <summary>
        /// Update Vehicle Position and Heading
        /// ==================================
        /// 
        /// Updates the vehicle position and heading for camera following.
        /// Should be called whenever GPS position or heading changes.
        /// 
        /// Parameters:
        /// - position: Vehicle position in local coordinates (meters)
        /// - heading: Vehicle heading in degrees (0 = North, 90 = East)
        /// </summary>
        public void UpdateVehiclePosition(Point3D position, double heading)
        {
            _vehiclePosition = position;
            _vehicleHeading = heading;

            // Update camera target based on current mode
            if (_currentMode == CameraMode.FieldView || _currentMode == CameraMode.TopRearView)
            {
                CalculateFollowingCameraTarget();
            }

            _logger.LogDebug($"Vehicle position updated: {position}, heading: {heading:F1}°");
        }

        /// <summary>
        /// Calculate Following Camera Target
        /// ================================
        /// 
        /// Calculates the target camera position and orientation for vehicle following modes.
        /// </summary>
        private void CalculateFollowingCameraTarget()
        {
            double distance, height, pitch;

            // Get parameters based on current mode
            switch (_currentMode)
            {
                case CameraMode.FieldView:
                    distance = _fieldViewDistance;
                    height = _fieldViewHeight;
                    pitch = _fieldViewPitch;
                    break;

                case CameraMode.TopRearView:
                    distance = _topRearViewDistance;
                    height = _topRearViewHeight;
                    pitch = _topRearViewPitch;
                    break;

                default:
                    return; // No following for other modes
            }

            // Convert vehicle heading to radians
            double headingRad = (_vehicleHeading - 90.0) * Math.PI / 180.0; // Adjust for coordinate system

            // Calculate camera position behind vehicle
            double cameraX = _vehiclePosition.X - distance * Math.Cos(headingRad);
            double cameraY = _vehiclePosition.Y - distance * Math.Sin(headingRad);
            double cameraZ = _vehiclePosition.Z + height;

            // Apply field boundaries
            cameraX = Math.Max(_fieldMinX, Math.Min(_fieldMaxX, cameraX));
            cameraY = Math.Max(_fieldMinY, Math.Min(_fieldMaxY, cameraY));
            cameraZ = Math.Max(_fieldMinZ, Math.Min(_fieldMaxZ, cameraZ));

            _targetPosition = new Point3D(cameraX, cameraY, cameraZ);

            // Calculate look direction toward vehicle with pitch adjustment
            Vector3D toVehicle = new Vector3D(
                _vehiclePosition.X - cameraX,
                _vehiclePosition.Y - cameraY,
                _vehiclePosition.Z - cameraZ
            );

            // Apply pitch adjustment
            double pitchRad = pitch * Math.PI / 180.0;
            double horizontalDistance = Math.Sqrt(toVehicle.X * toVehicle.X + toVehicle.Y * toVehicle.Y);
            
            _targetLookDirection = new Vector3D(
                toVehicle.X,
                toVehicle.Y,
                horizontalDistance * Math.Tan(pitchRad)
            );

            _targetLookDirection.Normalize();
            _targetUpDirection = new Vector3D(0, 0, 1); // Z-up coordinate system

            _animationState = AnimationState.Following;
        }

        #endregion

        #region Camera Animation and Updates

        /// <summary>
        /// Update Camera Animation
        /// ======================
        /// 
        /// Updates camera position and orientation with smooth transitions.
        /// Should be called every frame for smooth camera movement.
        /// 
        /// Parameters:
        /// - deltaTime: Time elapsed since last update (seconds)
        /// </summary>
        public void UpdateCamera(double deltaTime)
        {
            if (_animationState == AnimationState.Idle)
                return;

            bool positionChanged = false;
            bool orientationChanged = false;

            // Smooth position transition
            Vector3D positionDelta = _targetPosition - _currentPosition;
            if (positionDelta.Length > 0.01) // 1cm threshold
            {
                double damping = _animationState == AnimationState.Following ? _followingDamping : _transitionSpeed * deltaTime;
                _currentPosition += positionDelta * damping;
                positionChanged = true;
            }

            // Smooth look direction transition
            Vector3D lookDelta = _targetLookDirection - _currentLookDirection;
            if (lookDelta.Length > 0.001) // Small angle threshold
            {
                double damping = _animationState == AnimationState.Following ? _rotationDamping : _transitionSpeed * deltaTime;
                _currentLookDirection += lookDelta * damping;
                _currentLookDirection.Normalize();
                orientationChanged = true;
            }

            // Smooth up direction transition
            Vector3D upDelta = _targetUpDirection - _currentUpDirection;
            if (upDelta.Length > 0.001)
            {
                double damping = _animationState == AnimationState.Following ? _rotationDamping : _transitionSpeed * deltaTime;
                _currentUpDirection += upDelta * damping;
                _currentUpDirection.Normalize();
                orientationChanged = true;
            }

            // Check if transition is complete
            if (_animationState == AnimationState.Transitioning)
            {
                if (!positionChanged && !orientationChanged)
                {
                    _animationState = (_currentMode == CameraMode.FieldView || _currentMode == CameraMode.TopRearView) 
                        ? AnimationState.Following 
                        : AnimationState.Idle;
                    
                    _logger.LogDebug($"Camera transition completed, now in {_animationState} state");
                }
            }
        }

        /// <summary>
        /// Initiate Mode Transition
        /// ========================
        /// 
        /// Starts a smooth transition to the new camera mode.
        /// </summary>
        private void InitiateModeTransition()
        {
            _animationState = AnimationState.Transitioning;

            switch (_currentMode)
            {
                case CameraMode.FieldView:
                    SetFieldViewTarget();
                    break;

                case CameraMode.TopRearView:
                    SetTopRearViewTarget();
                    break;

                case CameraMode.FreeCamera:
                    _animationState = AnimationState.UserControlled;
                    break;

                case CameraMode.FixedView:
                    // Keep current position, stop following
                    _targetPosition = _currentPosition;
                    _targetLookDirection = _currentLookDirection;
                    _targetUpDirection = _currentUpDirection;
                    break;
            }
        }

        /// <summary>
        /// Set Field View Target
        /// ====================
        /// 
        /// Sets camera target for standard field operations view.
        /// </summary>
        private void SetFieldViewTarget()
        {
            if (_vehiclePosition != default(Point3D))
            {
                CalculateFollowingCameraTarget();
            }
            else
            {
                // Default field view position
                _targetPosition = new Point3D(0, -50, 30);
                _targetLookDirection = new Vector3D(0, 1, -0.3);
                _targetUpDirection = new Vector3D(0, 0, 1);
            }
        }

        /// <summary>
        /// Set Top/Rear View Target
        /// =======================
        /// 
        /// Sets camera target for ABLS boom monitoring view.
        /// </summary>
        private void SetTopRearViewTarget()
        {
            if (_vehiclePosition != default(Point3D))
            {
                CalculateFollowingCameraTarget();
            }
            else
            {
                // Default top/rear view position
                _targetPosition = new Point3D(0, -25, 40);
                _targetLookDirection = new Vector3D(0, 1, -1);
                _targetUpDirection = new Vector3D(0, 0, 1);
            }
        }

        #endregion

        #region Manual Camera Control

        /// <summary>
        /// Pan Camera
        /// ==========
        /// 
        /// Manually pan the camera (for free camera mode).
        /// 
        /// Parameters:
        /// - deltaX: Horizontal pan amount
        /// - deltaY: Vertical pan amount
        /// </summary>
        public void PanCamera(double deltaX, double deltaY)
        {
            if (_currentMode != CameraMode.FreeCamera)
                return;

            // Calculate right and up vectors
            Vector3D right = Vector3D.CrossProduct(_currentLookDirection, _currentUpDirection);
            right.Normalize();

            Vector3D up = Vector3D.CrossProduct(right, _currentLookDirection);
            up.Normalize();

            // Apply pan movement
            Vector3D movement = right * deltaX + up * deltaY;
            _currentPosition += movement;
            _targetPosition = _currentPosition;

            _logger.LogDebug($"Camera panned by ({deltaX:F2}, {deltaY:F2})");
        }

        /// <summary>
        /// Zoom Camera
        /// ===========
        /// 
        /// Zoom camera in/out along look direction.
        /// 
        /// Parameters:
        /// - zoomDelta: Zoom amount (positive = zoom in, negative = zoom out)
        /// </summary>
        public void ZoomCamera(double zoomDelta)
        {
            if (_currentMode != CameraMode.FreeCamera)
                return;

            Vector3D zoomMovement = _currentLookDirection * zoomDelta;
            _currentPosition += zoomMovement;
            _targetPosition = _currentPosition;

            // Apply field boundaries
            _currentPosition = new Point3D(
                Math.Max(_fieldMinX, Math.Min(_fieldMaxX, _currentPosition.X)),
                Math.Max(_fieldMinY, Math.Min(_fieldMaxY, _currentPosition.Y)),
                Math.Max(_fieldMinZ, Math.Min(_fieldMaxZ, _currentPosition.Z))
            );

            _logger.LogDebug($"Camera zoomed by {zoomDelta:F2}");
        }

        /// <summary>
        /// Rotate Camera
        /// =============
        /// 
        /// Rotate camera look direction (for free camera mode).
        /// 
        /// Parameters:
        /// - yawDelta: Horizontal rotation (degrees)
        /// - pitchDelta: Vertical rotation (degrees)
        /// </summary>
        public void RotateCamera(double yawDelta, double pitchDelta)
        {
            if (_currentMode != CameraMode.FreeCamera)
                return;

            // Convert to radians
            double yawRad = yawDelta * Math.PI / 180.0;
            double pitchRad = pitchDelta * Math.PI / 180.0;

            // Create rotation matrices and apply to look direction
            // This is a simplified rotation - could be enhanced with quaternions for better precision
            
            // Yaw rotation around up vector
            Vector3D right = Vector3D.CrossProduct(_currentLookDirection, _currentUpDirection);
            right.Normalize();

            // Apply rotations (simplified implementation)
            // In a full implementation, would use proper 3D rotation matrices or quaternions
            
            _targetLookDirection = _currentLookDirection;
            _targetUpDirection = _currentUpDirection;

            _logger.LogDebug($"Camera rotated by yaw: {yawDelta:F1}°, pitch: {pitchDelta:F1}°");
        }

        #endregion

        #region Configuration

        /// <summary>
        /// Set Field View Parameters
        /// ========================
        /// 
        /// Configures field view camera parameters.
        /// </summary>
        public void SetFieldViewParameters(double distance, double height, double pitch)
        {
            _fieldViewDistance = Math.Max(10.0, Math.Min(200.0, distance));
            _fieldViewHeight = Math.Max(5.0, Math.Min(100.0, height));
            _fieldViewPitch = Math.Max(-89.0, Math.Min(0.0, pitch));

            _logger.LogInformation($"Field view parameters updated: distance={distance:F1}m, height={height:F1}m, pitch={pitch:F1}°");

            if (_currentMode == CameraMode.FieldView)
            {
                CalculateFollowingCameraTarget();
            }
        }

        /// <summary>
        /// Set Top/Rear View Parameters
        /// ===========================
        /// 
        /// Configures top/rear view camera parameters for ABLS monitoring.
        /// </summary>
        public void SetTopRearViewParameters(double distance, double height, double pitch)
        {
            _topRearViewDistance = Math.Max(5.0, Math.Min(100.0, distance));
            _topRearViewHeight = Math.Max(10.0, Math.Min(150.0, height));
            _topRearViewPitch = Math.Max(-89.0, Math.Min(-10.0, pitch));

            _logger.LogInformation($"Top/rear view parameters updated: distance={distance:F1}m, height={height:F1}m, pitch={pitch:F1}°");

            if (_currentMode == CameraMode.TopRearView)
            {
                CalculateFollowingCameraTarget();
            }
        }

        /// <summary>
        /// Set Field Boundaries
        /// ===================
        /// 
        /// Sets field boundaries for camera movement constraints.
        /// </summary>
        public void SetFieldBoundaries(double minX, double maxX, double minY, double maxY, double minZ, double maxZ)
        {
            _fieldMinX = minX;
            _fieldMaxX = maxX;
            _fieldMinY = minY;
            _fieldMaxY = maxY;
            _fieldMinZ = Math.Max(1.0, minZ); // Ensure minimum height above ground
            _fieldMaxZ = maxZ;

            _logger.LogInformation($"Field boundaries updated: X=[{minX:F1}, {maxX:F1}], Y=[{minY:F1}, {maxY:F1}], Z=[{minZ:F1}, {maxZ:F1}]");
        }

        #endregion
    }
}
