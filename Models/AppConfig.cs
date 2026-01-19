using System;

namespace AfterCopy.Models
{
    public class AppConfig
    {
        public bool IsAutoSaveEnabled { get; set; } = true;
        public bool IsImagePreviewEnabled { get; set; } = true;
        
        // Hotkey Settings
        public string HotkeyKey { get; set; } = "OemTilde"; // The `~` key
        public string HotkeyModifier { get; set; } = "Alt";

        // Window Size Settings (always locked, not editable)
        public bool IsWindowSizeLocked { get; set; } = true;
        public double LockedWindowWidth { get; set; } = 250;
        public double LockedWindowHeight { get; set; } = 200;

        // Window Position Settings
        public bool IsWindowPositionLocked { get; set; } = false;
        public double WindowLeft { get; set; } = -1; // -1 means use default (bottom-right)
        public double WindowTop { get; set; } = -1;

        // Tab Count Settings (1-8)
        public int TabCount { get; set; } = 3;
    }
}
