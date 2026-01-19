using System;
using System.Windows;
using System.Windows.Controls;
using AfterCopy.Models;
using AfterCopy.Services;

namespace AfterCopy
{
    public partial class SettingsWindow : Window
    {
        public event Action OnSettingsSaved;
        private readonly ConfigService _configService;
        private AppConfig _tempConfig;

        public SettingsWindow()
        {
            InitializeComponent();
            _configService = new ConfigService(); // Logic: Should ideally rely on DI, but simple instantiation is fine here
            LoadCurrentSettings();
        }

        private void LoadCurrentSettings()
        {
            // Reload config from file to get latest window size (may have been changed by dragging)
            _configService.LoadConfig();
            _tempConfig = _configService.Current;

            // Map Config to UI
            AutoSaveToggle.IsChecked = _tempConfig.IsAutoSaveEnabled;
            ImagePreviewToggle.IsChecked = _tempConfig.IsImagePreviewEnabled;

            // Map Hotkey
            // Simple mapping logic for our ComboBox presets
            foreach (ComboBoxItem item in HotkeyCombo.Items)
            {
                if (item.Tag.ToString() == _tempConfig.HotkeyKey)
                {
                    // Basic check, currently ignores modifier diffs for simplicity in MVP
                    HotkeyCombo.SelectedItem = item;
                    break;
                }
            }
            if (HotkeyCombo.SelectedIndex == -1) HotkeyCombo.SelectedIndex = 0;

            // Display Window Size (read-only)
            SizeDisplay.Text = $"{_tempConfig.LockedWindowWidth:F0} x {_tempConfig.LockedWindowHeight:F0}";

            // Map Tab Count
            int tabCount = Math.Max(1, Math.Min(8, _tempConfig.TabCount));
            TabCountCombo.SelectedIndex = tabCount - 1; // Index is 0-based
        }

        private void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Setting_Changed(object sender, RoutedEventArgs e)
        {
            SaveSettings();
        }

        private void TabCountCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SaveSettings();
        }

        private void SaveSettings()
        {
            if (_tempConfig == null) return;

            // Map UI to Config
            _tempConfig.IsAutoSaveEnabled = AutoSaveToggle.IsChecked == true;
            _tempConfig.IsImagePreviewEnabled = ImagePreviewToggle.IsChecked == true;

            // Tab Count
            if (TabCountCombo.SelectedItem is ComboBoxItem tabItem && int.TryParse(tabItem.Tag.ToString(), out int tabCount))
            {
                _tempConfig.TabCount = tabCount;
            }

            // Hotkey
            if (HotkeyCombo.SelectedItem is ComboBoxItem selectedItem)
            {
                _tempConfig.HotkeyKey = selectedItem.Tag.ToString();
                if (_tempConfig.HotkeyKey == "V_CtrlShift")
                {
                    _tempConfig.HotkeyKey = "V";
                    _tempConfig.HotkeyModifier = "CtrlShift";
                }
                else
                {
                    _tempConfig.HotkeyModifier = "Alt";
                }
            }

            _configService.SaveConfig(_tempConfig);
            OnSettingsSaved?.Invoke();
        }
    }
}
