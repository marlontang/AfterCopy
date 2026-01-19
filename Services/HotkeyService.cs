using System;
using System.Runtime.InteropServices;
using System.Windows.Input; // For KeyInterop
using System.Windows.Interop;

namespace AfterCopy.Services
{
    public class HotkeyService
    {
        private const int MOD_ALT = 0x0001;
        private const int MOD_CONTROL = 0x0002;
        private const int MOD_SHIFT = 0x0004;
        private const int HOTKEY_ID = 9000;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private IntPtr _windowHandle;
        private Action _onHotkeyAction;

        public void Register(IntPtr windowHandle, string keyString, string modifierString, Action onHotkey)
        {
            _windowHandle = windowHandle;
            _onHotkeyAction = onHotkey;
            
            // Unregister first to be safe
            UnregisterHotKey(_windowHandle, HOTKEY_ID);

            int modifiers = 0;
            if (modifierString.Contains("Alt")) modifiers |= MOD_ALT;
            if (modifierString.Contains("Ctrl")) modifiers |= MOD_CONTROL;
            if (modifierString.Contains("Shift")) modifiers |= MOD_SHIFT;

            int virtualKey = 0;
            
            // Basic Key Mapping
            if (keyString == "OemTilde") virtualKey = 0xC0; // `~`
            else if (keyString == "Space") virtualKey = 0x20;
            else if (keyString == "V") virtualKey = 0x56; // Explicitly map V
            else 
            {
                // Try to parse Enum
                if (Enum.TryParse(typeof(Key), keyString, out object result))
                {
                    virtualKey = KeyInterop.VirtualKeyFromKey((Key)result);
                }
                else
                {
                     // Fallback default
                     virtualKey = 0xC0; 
                }
            }

            RegisterHotKey(_windowHandle, HOTKEY_ID, modifiers, virtualKey);
        }

        public void ProcessMessage(int msg, IntPtr wParam)
        {
            if (msg == 0x0312 && wParam.ToInt32() == HOTKEY_ID) // WM_HOTKEY
            {
                _onHotkeyAction?.Invoke();
            }
        }

        public void Unregister()
        {
            if (_windowHandle != IntPtr.Zero)
            {
                UnregisterHotKey(_windowHandle, HOTKEY_ID);
            }
        }
    }
}