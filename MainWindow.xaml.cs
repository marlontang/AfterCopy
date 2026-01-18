using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace AfterCopy
{
    public partial class MainWindow : Window
    {
        // Win32 Constants
        private const int WM_CLIPBOARDUPDATE = 0x031D;
        
        // Extended Window Styles
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080; 

        // P/Invoke Declarations
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Position the window at the bottom-right of the working area
            var workArea = SystemParameters.WorkArea;
            this.Left = workArea.Right - this.Width;
            this.Top = workArea.Bottom - this.Height;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Get Window Handle
            var windowHandle = new WindowInteropHelper(this).Handle;

            // 1. Add Clipboard Listener
            AddClipboardFormatListener(windowHandle);

            // 2. Set WS_EX_NOACTIVATE to prevent focus stealing
            // Get current styles
            int exStyle = GetWindowLong(windowHandle, GWL_EXSTYLE);
            // Add NOACTIVATE and TOOLWINDOW (hides from Alt-Tab typically)
            SetWindowLong(windowHandle, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);

            // Hook into the message loop
            HwndSource source = HwndSource.FromHwnd(windowHandle);
            source.AddHook(WndProc);
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                var windowHandle = new WindowInteropHelper(this).Handle;
                RemoveClipboardFormatListener(windowHandle);
                HwndSource.FromHwnd(windowHandle)?.RemoveHook(WndProc);
            }
            catch { /* Best effort cleanup */ }
            
            base.OnClosed(e);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_CLIPBOARDUPDATE)
            {
                UpdateClipboardContent();
                // We handled the message, but usually the chain should continue, 
                // strictly speaking return 0 if processed. 
                // However, listener just observes.
            }

            return IntPtr.Zero;
        }

        private void UpdateClipboardContent()
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    string text = Clipboard.GetText();
                    // Basic truncation or formatting if text is massive could go here
                    ClipboardText.Text = text;
                }
                else
                {
                    ClipboardText.Text = "Non-text content copied.";
                }
            }
            catch (Exception ex)
            {
                ClipboardText.Text = $"Error reading clipboard: {ex.Message}";
            }
        }
    }
}