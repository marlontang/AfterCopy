using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using AfterCopy.Models;
using AfterCopy.Services;
using AfterCopy.ViewModels;
using AfterCopy.Helpers;

namespace AfterCopy
{
    public partial class MainWindow : Window
    {
        private ConfigService _configService;
        private HotkeyService _hotkeyService;
        private bool _isReallyClosing = false; // Flag to allow real close when quitting app

        // Clipboard Listener P/Invoke
        private const int WM_CLIPBOARDUPDATE = 0x031D;

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                _configService = new ConfigService();
                _hotkeyService = new HotkeyService();
                
                var vm = new MainViewModel();
                this.DataContext = vm;

                vm.RequestFocus += OnRequestFocus;
                vm.RequestVisibilityChange += OnRequestVisibilityChange;

                // Apply initial settings (size, etc.)
                vm.RefreshSettings();
            }
            catch (Exception ex)
            {
                Logger.LogError("MainWindow initialization failed", ex);
                throw;
            }
        }

        private void OnRequestFocus()
        {
            // Ensure UI is ready
            this.Dispatcher.InvokeAsync(() =>
            {
                ContentTextBox.Focus();
                ContentTextBox.CaretIndex = 0;
            }, System.Windows.Threading.DispatcherPriority.Input);
        }

        private void OnRequestVisibilityChange(bool isVisible)
        {
            if (isVisible)
            {
                this.Show();
                this.Activate();
                UpdatePosition();
            }
            else
            {
                CloseAllChildWindows();
                this.Hide();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Logger.Log("MainWindow Loaded successfully.");

            // Restore window size from config on startup
            var config = _configService.Current;
            if (config.LockedWindowWidth > 0 && config.LockedWindowHeight > 0)
            {
                this.Width = Math.Max(this.MinWidth, Math.Min(config.LockedWindowWidth, this.MaxWidth));
                this.Height = Math.Max(this.MinHeight, Math.Min(config.LockedWindowHeight, this.MaxHeight));
                Logger.Log($"Restored window size: {this.Width}x{this.Height}");
            }

            UpdatePosition();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // If not really closing (just hiding), cancel the close and hide instead
            if (!_isReallyClosing)
            {
                e.Cancel = true;

                // Hide window instead of closing
                if (DataContext is MainViewModel vm && vm.IsVisible)
                {
                    vm.ToggleVisibilityCommand.Execute(null);
                }
            }
        }

        /// <summary>
        /// Call this method to really close the window and exit the app
        /// </summary>
        public void ForceClose()
        {
            _isReallyClosing = true;
            this.Close();
        }
        
        private void UpdatePosition()
        {
            var config = _configService?.Current;

            // If user has locked position (by dragging), use saved position
            if (config != null && config.IsWindowPositionLocked && config.WindowLeft >= 0 && config.WindowTop >= 0)
            {
                // Ensure the window is still visible on screen
                var workArea = SystemParameters.WorkArea;
                double left = config.WindowLeft;
                double top = config.WindowTop;

                // Clamp to screen bounds
                if (left + this.Width > workArea.Right) left = workArea.Right - this.Width;
                if (top + this.Height > workArea.Bottom) top = workArea.Bottom - this.Height;
                if (left < workArea.Left) left = workArea.Left;
                if (top < workArea.Top) top = workArea.Top;

                this.Left = left;
                this.Top = top;
            }
            else
            {
                // Default: Position the window in the bottom-right corner with some margin
                var workArea = SystemParameters.WorkArea;
                this.Left = workArea.Right - this.Width - 20;
                this.Top = workArea.Bottom - this.Height - 20;
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var windowHandle = new WindowInteropHelper(this).Handle;
            AddClipboardFormatListener(windowHandle);
            
            HwndSource source = HwndSource.FromHwnd(windowHandle);
            source.AddHook(WndProc);

            RegisterHotkey(windowHandle);
        }

        private void RegisterHotkey(IntPtr windowHandle)
        {
            var vm = DataContext as MainViewModel;
            var config = _configService.Current;
            
            _hotkeyService.Register(windowHandle, config.HotkeyKey, config.HotkeyModifier, () => 
            {
                vm?.ToggleVisibilityCommand.Execute(null);
                
                if (vm?.IsVisible == true)
                {
                    this.Activate(); 
                }
            });
        }

        protected override void OnClosed(EventArgs e)
        {
            var windowHandle = new WindowInteropHelper(this).Handle;
            RemoveClipboardFormatListener(windowHandle);
            
            if (_hotkeyService != null)
            {
                _hotkeyService.Unregister();
            }

            // Save window size/position
            if (_configService != null)
            {
                _configService.SaveConfig(_configService.Current);
            }
            
            base.OnClosed(e);
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Only update config if the window is fully loaded (user interaction)
            // preventing startup default size from overwriting saved config.
            if (this.IsLoaded && _configService?.Current != null)
            {
                // User manually dragged to resize, so enable Lock Window Size
                _configService.Current.IsWindowSizeLocked = true;
                _configService.Current.LockedWindowWidth = this.Width;
                _configService.Current.LockedWindowHeight = this.Height;

                // Save immediately to persist changes
                _configService.SaveConfig(_configService.Current);
            }
        }

        private void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
            {
                this.DragMove();

                // After drag completes, save the new position
                if (_configService?.Current != null)
                {
                    _configService.Current.IsWindowPositionLocked = true;
                    _configService.Current.WindowLeft = this.Left;
                    _configService.Current.WindowTop = this.Top;
                    _configService.SaveConfig(_configService.Current);
                }
            }
        }

        private void MinimizeWindow_Click(object sender, RoutedEventArgs e)
        {
            // Minimize to taskbar instead of hiding
            this.WindowState = WindowState.Minimized;
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            // Really close the application
            ForceClose();
        }

        private void CloseAllChildWindows()
        {
            // Close history window if open
            if (_historyWindow != null && _historyWindow.IsLoaded)
            {
                _historyWindow.Close();
                _historyWindow = null;
            }

            // Close settings window if open
            if (_settingsWindow != null && _settingsWindow.IsLoaded)
            {
                _settingsWindow.Close();
                _settingsWindow = null;
            }

            // Close any other owned windows
            foreach (Window ownedWindow in this.OwnedWindows)
            {
                ownedWindow.Close();
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_CLIPBOARDUPDATE)
            {
                (DataContext as MainViewModel)?.ProcessClipboardUpdate();
            }
            
            _hotkeyService.ProcessMessage(msg, wParam);
            
            return IntPtr.Zero;
        }

        private HistoryWindow? _historyWindow;

        private void ViewHistory_Click(object sender, RoutedEventArgs e)
        {
            if (_historyWindow == null || !_historyWindow.IsLoaded)
            {
                _historyWindow = new HistoryWindow();
                _historyWindow.Closed += (s, args) => _historyWindow = null;
                _historyWindow.Show();
            }
            else
            {
                if (_historyWindow.WindowState == WindowState.Minimized)
                    _historyWindow.WindowState = WindowState.Normal;
                _historyWindow.Activate();
            }
        }

        private SettingsWindow? _settingsWindow;

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            // If settings window already open, just activate it
            if (_settingsWindow != null && _settingsWindow.IsLoaded)
            {
                _settingsWindow.Activate();
                return;
            }

            _settingsWindow = new SettingsWindow();
            _settingsWindow.Closed += (s, args) => _settingsWindow = null;
            _settingsWindow.OnSettingsSaved += () =>
            {
                _configService.LoadConfig();
                var vm = DataContext as MainViewModel;
                vm?.RefreshSettings();

                var windowHandle = new WindowInteropHelper(this).Handle;
                RegisterHotkey(windowHandle);
            };
            _settingsWindow.Show(); // Non-modal, doesn't block main window
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                (DataContext as MainViewModel)?.ToggleVisibilityCommand.Execute(null);
            }
            else if (e.Key == System.Windows.Input.Key.D1 || e.Key == System.Windows.Input.Key.NumPad1)
            {
                LoadRecentItem(0);
            }
            else if (e.Key == System.Windows.Input.Key.D2 || e.Key == System.Windows.Input.Key.NumPad2)
            {
                LoadRecentItem(1);
            }
            else if (e.Key == System.Windows.Input.Key.D3 || e.Key == System.Windows.Input.Key.NumPad3)
            {
                LoadRecentItem(2);
            }
        }

        private void LoadRecentItem(int index)
        {
            if (DataContext is MainViewModel vm && vm.RecentHistory.Count > index)
            {
                vm.LoadItemCommand.Execute(vm.RecentHistory[index]);
            }
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.IsVisible = false;
            }
        }
    }
}