using System;
using System.Threading;
using System.Windows;
using AfterCopy.Helpers;

namespace AfterCopy
{
    public partial class App : Application
    {
        private const string MutexName = "AfterCopy_SingleInstance_Mutex";
        private const string EventName = "AfterCopy_ShowWindow_Event";
        private Mutex? _mutex;
        private EventWaitHandle? _showWindowEvent;
        private Thread? _eventListenerThread;
        private bool _isFirstInstance;

        public App()
        {
            // Log immediately to confirm process started
            Logger.Log("Application Initializing...");

            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                Logger.LogError("App.xaml parsing failed", ex);
                throw;
            }

            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            // Check for single instance
            _mutex = new Mutex(true, MutexName, out _isFirstInstance);

            if (!_isFirstInstance)
            {
                // Another instance is running, signal it to show window and exit
                Logger.Log("Another instance detected. Signaling to show window...");
                try
                {
                    var showEvent = EventWaitHandle.OpenExisting(EventName);
                    showEvent.Set();
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to signal existing instance", ex);
                }

                // Exit this instance
                Shutdown();
                return;
            }

            // First instance - create the event and start listening
            _showWindowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
            StartEventListener();

            base.OnStartup(e);
            Logger.Log("Application Started (OnStartup) - First Instance.");
        }

        private void StartEventListener()
        {
            _eventListenerThread = new Thread(() =>
            {
                while (_showWindowEvent != null)
                {
                    try
                    {
                        // Wait for signal from another instance
                        if (_showWindowEvent.WaitOne(1000))
                        {
                            // Signal received, show the main window
                            Logger.Log("Received signal to show window from another instance.");
                            Dispatcher.Invoke(() =>
                            {
                                if (MainWindow is MainWindow mainWindow)
                                {
                                    var vm = mainWindow.DataContext as ViewModels.MainViewModel;
                                    if (vm != null && !vm.IsVisible)
                                    {
                                        vm.ToggleVisibilityCommand.Execute(null);
                                    }
                                    mainWindow.Show();
                                    mainWindow.Activate();
                                }
                            });
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        // Event was disposed, exit the loop
                        break;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Event listener error", ex);
                    }
                }
            })
            {
                IsBackground = true,
                Name = "SingleInstanceEventListener"
            };
            _eventListenerThread.Start();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Logger.Log("Application Exiting normally.");

            // Clean up - only release mutex if we are the first instance (owner)
            _showWindowEvent?.Dispose();
            _showWindowEvent = null;

            if (_isFirstInstance && _mutex != null)
            {
                try
                {
                    _mutex.ReleaseMutex();
                }
                catch { /* Ignore if already released */ }
            }
            _mutex?.Dispose();

            base.OnExit(e);
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Logger.LogError("Dispatcher Unhandled Exception (UI Thread)", e.Exception);
            
            // Show user a friendly message
            MessageBox.Show($"An unexpected error occurred.\nDetails check 'debug.log'.\n\nError: {e.Exception.Message}", 
                            "AfterCopy Error", 
                            MessageBoxButton.OK, 
                            MessageBoxImage.Error);
            
            e.Handled = true; // Attempt to keep app alive or allow graceful shutdown
            Shutdown();
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                Logger.LogError("CurrentDomain Unhandled Exception (Non-UI Thread)", ex);
                MessageBox.Show($"Critical Error.\nDetails check 'debug.log'.\n\nError: {ex.Message}", 
                                "AfterCopy Critical", 
                                MessageBoxButton.OK, 
                                MessageBoxImage.Error);
            }
        }
    }
}