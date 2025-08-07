using System;
using System.Windows;
using Microsoft.Extensions.Logging;

namespace AOG_WPF
{
    /// <summary>
    /// WPF Application Entry Point - Code Behind
    /// ==========================================
    /// 
    /// This class handles the application lifecycle for the WPF 3D migration of AgOpenGPS.
    /// 
    /// Migration Notes:
    /// - Replaces the Program.cs + Application.Run() pattern from Windows Forms
    /// - Provides centralized error handling and logging for the entire application
    /// - Manages application startup, shutdown, and global state
    /// 
    /// ABLS Integration:
    /// - Initializes logging for agricultural sensor data processing
    /// - Sets up global error handling for GPS/IMU/radar sensor failures
    /// - Configures application for real-time data processing requirements
    /// </summary>
    public partial class App : Application
    {
        #region Private Fields
        
        /// <summary>
        /// Application-wide logger for debugging and monitoring
        /// Critical for troubleshooting GPS, sensor, and 3D rendering issues
        /// </summary>
        private ILogger<App>? _logger;
        
        /// <summary>
        /// Logger factory for creating loggers throughout the application
        /// Enables consistent logging across all migration components
        /// </summary>
        private ILoggerFactory? _loggerFactory;
        
        #endregion

        #region Application Lifecycle Events

        /// <summary>
        /// Application Startup Event Handler
        /// =================================
        /// 
        /// Called when the WPF application starts up, before the main window is shown.
        /// This replaces the initialization logic that was previously in Program.Main()
        /// in the Windows Forms version.
        /// 
        /// Migration Tasks:
        /// 1. Initialize logging system for debugging migration issues
        /// 2. Set up global error handling for unhandled exceptions
        /// 3. Configure application settings and preferences
        /// 4. Initialize GPS and sensor communication systems
        /// </summary>
        /// <param name="sender">The application instance</param>
        /// <param name="e">Startup event arguments</param>
        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                // Initialize logging system first for debugging migration issues
                InitializeLogging();
                
                _logger?.LogInformation("=== AgOpenGPS WPF 3D Migration Starting ===");
                _logger?.LogInformation("Application startup initiated");
                
                // Set up global exception handling
                // Critical for catching and logging 3D rendering and sensor errors
                SetupGlobalExceptionHandling();
                
                // Log startup parameters for debugging
                _logger?.LogInformation("Command line arguments: {Args}", string.Join(" ", e.Args));
                
                // Initialize application configuration
                // This will eventually load field data, GPS settings, and ABLS configuration
                InitializeApplicationConfiguration();
                
                // Call base implementation to continue WPF startup process
                base.OnStartup(e);
                
                _logger?.LogInformation("WPF application startup completed successfully");
            }
            catch (Exception ex)
            {
                // Critical error during startup - log and show error dialog
                _logger?.LogCritical(ex, "Fatal error during application startup");
                
                MessageBox.Show(
                    $"Failed to start AgOpenGPS WPF:\n\n{ex.Message}\n\nSee logs for details.",
                    "Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                
                // Shutdown application on critical startup failure
                Shutdown(1);
            }
        }

        /// <summary>
        /// Application Exit Event Handler
        /// ==============================
        /// 
        /// Called when the application is shutting down.
        /// Ensures proper cleanup of resources, especially important for:
        /// - GPS/sensor communication cleanup
        /// - 3D rendering context disposal
        /// - File handles and data saving
        /// </summary>
        /// <param name="sender">The application instance</param>
        /// <param name="e">Exit event arguments</param>
        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                _logger?.LogInformation("=== AgOpenGPS WPF 3D Migration Shutting Down ===");
                
                // TODO: Add cleanup for GPS/sensor connections
                // TODO: Save application state and field data
                // TODO: Dispose 3D rendering resources
                
                _logger?.LogInformation("Application shutdown completed with exit code: {ExitCode}", e.ApplicationExitCode);
                
                // Dispose logging resources
                _loggerFactory?.Dispose();
            }
            catch (Exception ex)
            {
                // Log shutdown errors but don't prevent application exit
                _logger?.LogError(ex, "Error during application shutdown");
            }
            finally
            {
                // Always call base implementation
                base.OnExit(e);
            }
        }

        #endregion

        #region Private Initialization Methods

        /// <summary>
        /// Initialize Logging System
        /// =========================
        /// 
        /// Sets up comprehensive logging for the WPF migration.
        /// Essential for debugging the complex migration from OpenGL to WPF 3D.
        /// 
        /// Logging Categories:
        /// - Application lifecycle events
        /// - 3D rendering and HelixToolkit operations
        /// - GPS and sensor data processing
        /// - ABLS boom control operations
        /// - DEM data loading and terrain generation
        /// </summary>
        private void InitializeLogging()
        {
            _loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddConsole()  // Console output for development
                    .SetMinimumLevel(LogLevel.Information);  // Adjust as needed
                
                // TODO: Add file logging for production use
                // TODO: Add structured logging for GPS/sensor data analysis
            });
            
            _logger = _loggerFactory.CreateLogger<App>();
        }

        /// <summary>
        /// Setup Global Exception Handling
        /// ===============================
        /// 
        /// Configures application-wide exception handling to catch and log
        /// unhandled exceptions that could occur during:
        /// - 3D rendering operations (HelixToolkit/DirectX)
        /// - GPS/sensor data processing
        /// - DEM data loading and mesh generation
        /// - Real-time boom positioning calculations
        /// </summary>
        private void SetupGlobalExceptionHandling()
        {
            // Handle unhandled exceptions in the main UI thread
            DispatcherUnhandledException += (sender, e) =>
            {
                _logger?.LogError(e.Exception, "Unhandled exception in UI thread");
                
                // Show user-friendly error message
                MessageBox.Show(
                    $"An unexpected error occurred:\n\n{e.Exception.Message}\n\nThe application will continue running.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                
                // Mark as handled to prevent application crash
                e.Handled = true;
            };

            // Handle unhandled exceptions in background threads
            // Important for GPS/sensor processing threads
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var exception = e.ExceptionObject as Exception;
                _logger?.LogCritical(exception, "Unhandled exception in background thread. IsTerminating: {IsTerminating}", e.IsTerminating);
                
                if (e.IsTerminating)
                {
                    // Application is terminating - try to save critical data
                    MessageBox.Show(
                        $"A critical error occurred and the application must close:\n\n{exception?.Message}",
                        "Critical Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            };
        }

        /// <summary>
        /// Initialize Application Configuration
        /// ===================================
        /// 
        /// Sets up application-wide configuration and settings.
        /// This will eventually include:
        /// - GPS receiver settings and communication parameters
        /// - 3D rendering preferences and performance settings
        /// - ABLS configuration (boom dimensions, sensor calibration)
        /// - Field data and DEM file locations
        /// - User interface preferences and layout
        /// </summary>
        private void InitializeApplicationConfiguration()
        {
            _logger?.LogInformation("Initializing application configuration");
            
            // TODO: Load GPS settings from original AOG configuration
            // TODO: Initialize 3D rendering preferences
            // TODO: Load ABLS boom configuration
            // TODO: Set up DEM data directory paths
            // TODO: Initialize user interface preferences
            
            _logger?.LogInformation("Application configuration initialized");
        }

        #endregion
    }
}
