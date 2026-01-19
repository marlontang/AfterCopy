using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace AfterCopy.Helpers
{
    public static class ScreenHelper
    {
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

        public static Rect GetMouseScreenWorkArea()
        {
            POINT mousePos;
            GetCursorPos(out mousePos);

            IntPtr hMonitor = MonitorFromPoint(mousePos, MONITOR_DEFAULTTONEAREST);

            MONITORINFO mi = new MONITORINFO();
            mi.cbSize = Marshal.SizeOf(mi);

            if (GetMonitorInfo(hMonitor, ref mi))
            {
                return new Rect(
                    mi.rcWork.Left,
                    mi.rcWork.Top,
                    mi.rcWork.Right - mi.rcWork.Left,
                    mi.rcWork.Bottom - mi.rcWork.Top);
            }

            return SystemParameters.WorkArea; // Fallback
        }
    }
}
