using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using AfterCopy.Models;
using AfterCopy.Services;
using System.ComponentModel;

namespace AfterCopy.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly ClipboardService _clipboardService;
        private readonly StorageService _storageService;
        private ConfigService _configService;
        
        private ClipboardItem? _currentItem;
        private string _statusMessage = "Live";
        private bool _isImageVisible;
        private BitmapSource? _currentImageSource;
        private bool _isVisible = true;
        private bool _isSaveButtonVisible;
        private bool _isMonitoring = true;

        public ObservableCollection<ClipboardItem> RecentHistory { get; } = new ObservableCollection<ClipboardItem>();

        public event PropertyChangedEventHandler? PropertyChanged;
        public event Action? RequestFocus;
        public event Action<bool>? RequestVisibilityChange;

        public MainViewModel()
        {
            _clipboardService = new ClipboardService();
            _storageService = new StorageService();
            _configService = new ConfigService(); 
            
            _isSaveButtonVisible = !_configService.Current.IsAutoSaveEnabled;

            _clipboardService.ClipboardChanged += OnClipboardChanged;

            CopyCommand = new RelayCommand(ExecuteCopy);
            SaveCommand = new RelayCommand(ExecuteSave);
            ToggleVisibilityCommand = new RelayCommand(ExecuteToggleVisibility);
            LoadItemCommand = new RelayCommand(ExecuteLoadItem);

            ToggleMonitoringCommand = new RelayCommand(ExecuteToggleMonitoring);
            TogglePinCommand = new RelayCommand(ExecuteTogglePin);

            // Initial Data Load
            LoadInitialData();
        }

        public ICommand CopyCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand ToggleVisibilityCommand { get; }
        public ICommand LoadItemCommand { get; }
        public ICommand ToggleMonitoringCommand { get; }
        public ICommand TogglePinCommand { get; }

        public ClipboardItem? CurrentItem
        {
            get => _currentItem;
            set
            {
                if (_currentItem != value)
                {
                    _currentItem = value;
                    OnPropertyChanged();
                }
            }
        }

        private int TabCount => Math.Max(1, Math.Min(8, _configService.Current.TabCount));

        private async void LoadInitialData()
        {
            try
            {
                var history = await _storageService.LoadHistoryAsync();
                var recent = history.OrderByDescending(x => x.Timestamp).Take(TabCount).ToList();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    RecentHistory.Clear();
                    foreach (var item in recent) RecentHistory.Add(item);

                    EnsurePlaceholders();
                    RefreshQuickKeys();

                    if (RecentHistory.Count > 0)
                    {
                        // Select the first item (real or placeholder)
                        CurrentItem = RecentHistory[0];
                        UpdateViewForContent(CurrentItem);
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading initial data: {ex.Message}");
                // Fallback to placeholders if load fails
                Application.Current.Dispatcher.Invoke(() =>
                {
                    EnsurePlaceholders();
                    if (RecentHistory.Count > 0) CurrentItem = RecentHistory[0];
                });
            }
        }

        private void EnsurePlaceholders()
        {
            int targetCount = TabCount;

            // Remove excess items if TabCount decreased
            while (RecentHistory.Count > targetCount)
            {
                RecentHistory.RemoveAt(RecentHistory.Count - 1);
            }

            // Add placeholders if needed
            while (RecentHistory.Count < targetCount)
            {
                RecentHistory.Add(new ClipboardItem
                {
                    Content = "Waiting Copy",
                    Type = ClipboardContentType.Text,
                    Timestamp = DateTime.Now,
                    Id = "PLACEHOLDER"
                });
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage != value)
                {
                    _statusMessage = value;
                    OnPropertyChanged();
                }
            }
        }
        
        public bool IsImageVisible
        {
            get => _isImageVisible;
            set
            {
                if (_isImageVisible != value)
                {
                    _isImageVisible = value;
                    OnPropertyChanged();
                }
            }
        }

        public BitmapSource? CurrentImageSource
        {
            get => _currentImageSource;
            set
            {
                if (_currentImageSource != value)
                {
                    _currentImageSource = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (_isVisible != value)
                {
                    _isVisible = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsSaveButtonVisible
        {
            get => _isSaveButtonVisible;
            set
            {
                if (_isSaveButtonVisible != value)
                {
                    _isSaveButtonVisible = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsMonitoring
        {
            get => _isMonitoring;
            set
            {
                if (_isMonitoring != value)
                {
                    _isMonitoring = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(MonitoringButtonText));
                }
            }
        }

        public string MonitoringButtonText => IsMonitoring ? "STOP COPY" : "START COPY";

        public void ProcessClipboardUpdate()
        {
            _clipboardService.OnClipboardUpdate();
        }

        public void RefreshSettings()
        {
            _configService.LoadConfig();
            IsSaveButtonVisible = !_configService.Current.IsAutoSaveEnabled;

            // Update status based on mode
            StatusMessage = _configService.Current.IsAutoSaveEnabled ? "AUTO-SAVE" : "MANUAL-SAVE";

            // Note: Window size is controlled by user dragging, not auto-adjusted here
            // Only adjust tab count (partial refresh of left sidebar)
            EnsurePlaceholders();
            RefreshQuickKeys();
        }

        private void OnClipboardChanged(ClipboardItem item)
        {
            // Ignore empty text
            if (item.Type == ClipboardContentType.Text && string.IsNullOrWhiteSpace(item.Content)) return;

            // For images, get the image source from clipboard and prepare filename
            BitmapSource? imageSource = null;
            if (item.Type == ClipboardContentType.Image)
            {
                try
                {
                    if (Clipboard.ContainsImage())
                    {
                        imageSource = Clipboard.GetImage();
                        // Freeze the bitmap so it can be used across threads
                        if (imageSource != null && imageSource.CanFreeze)
                        {
                            imageSource.Freeze();
                        }
                        // Set the filename immediately so it's correct in history
                        item.Content = $"{item.Id}.png";
                    }
                }
                catch { }
            }

            if (_configService.Current.IsAutoSaveEnabled)
            {
                // Fire and forget save (pass imageSource for images)
                var imgCopy = imageSource; // Capture for closure
                Task.Run(() => _storageService.SaveItemAsync(item, imgCopy));
            }

            // Update UI on UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                CurrentItem = item;

                // Store image source for display
                if (item.Type == ClipboardContentType.Image && imageSource != null)
                {
                    CurrentImageSource = imageSource;
                }

                UpdateViewForContent(item);
                UpdateRecentHistory(item);
            });
        }

        private void UpdateRecentHistory(ClipboardItem newItem)
        {
            int maxTabs = TabCount;

            // Remove placeholders first so they don't interfere with logic
            var placeholders = RecentHistory.Where(x => x.Id == "PLACEHOLDER").ToList();
            foreach (var p in placeholders) RecentHistory.Remove(p);

            var existing = RecentHistory.FirstOrDefault(x => x.Content == newItem.Content && x.Type == newItem.Type);
            if (existing != null)
            {
                if (existing.IsPinned)
                {
                    EnsurePlaceholders();
                    return;
                }
                RecentHistory.Remove(existing);
            }

            int insertIndex = 0;
            while (insertIndex < RecentHistory.Count && RecentHistory[insertIndex].IsPinned)
            {
                insertIndex++;
            }

            RecentHistory.Insert(insertIndex, newItem);

            // Trim to max tab count
            int unpinnedCount = RecentHistory.Count(x => !x.IsPinned);
            while (unpinnedCount > maxTabs)
            {
                var lastUnpinned = RecentHistory.LastOrDefault(x => !x.IsPinned);
                if (lastUnpinned != null)
                {
                    RecentHistory.Remove(lastUnpinned);
                    unpinnedCount--;
                }
                else break;
            }

            EnsurePlaceholders();
            RefreshQuickKeys();
        }

        private void RefreshQuickKeys()
        {
            int maxTabs = TabCount;
            for (int i = 0; i < RecentHistory.Count; i++)
            {
                RecentHistory[i].QuickKey = (i < maxTabs) ? (i + 1).ToString() : null;
            }
        }

        private void ExecuteClearClipboard(object? obj)
        {
            Clipboard.Clear();
            CurrentItem = null;
            StatusMessage = "Cleared";
            Task.Delay(2000).ContinueWith(_ => StatusMessage = _configService.Current.IsAutoSaveEnabled ? "AUTO-SAVE" : "MANUAL-SAVE");
            IsImageVisible = false;
            CurrentImageSource = null;
        }

        private void ExecuteTogglePin(object? obj)
        {
            if (obj is ClipboardItem item)
            {
                item.IsPinned = !item.IsPinned;
                var sorted = new ObservableCollection<ClipboardItem>(
                    RecentHistory.OrderByDescending(x => x.IsPinned).ThenByDescending(x => x.Timestamp)
                );
                
                RecentHistory.Clear();
                foreach (var i in sorted) RecentHistory.Add(i);
            }
        }

        private void UpdateViewForContent(ClipboardItem item)
        {
            string modeStatus = _configService.Current.IsAutoSaveEnabled ? "AUTO-SAVE" : "MANUAL-SAVE";

            // 1. Content Processing
            if (item.Type == ClipboardContentType.Image)
            {
                if (Clipboard.ContainsImage())
                {
                    CurrentImageSource = Clipboard.GetImage();
                }
                
                IsImageVisible = _configService.Current.IsImagePreviewEnabled;
                StatusMessage = $"IMG - {modeStatus}";
            }
            else
            {
                IsImageVisible = false;
                CurrentImageSource = null;
                
                string typeLabel = item.IsCode ? "CODE" : "TEXT";
                StatusMessage = $"{typeLabel} - {modeStatus}";

                string displayContent = item.Content;
                if (!string.IsNullOrEmpty(item.Content) && (item.Content.TrimStart().StartsWith("{") || item.Content.TrimStart().StartsWith("[")))
                {
                    try
                    {
                        var jsonElement = System.Text.Json.JsonDocument.Parse(item.Content).RootElement;
                        displayContent = System.Text.Json.JsonSerializer.Serialize(jsonElement, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                        StatusMessage = $"JSON - {modeStatus}";
                    }
                    catch { }
                }

                if (displayContent != item.Content)
                {
                     item.Content = displayContent;
                }
            }

            // Note: Window size is now fully controlled by user dragging
            // No automatic resize here to prevent unexpected size changes
        }

        private void ExecuteCopy(object? obj)
        {
            if (CurrentItem != null)
            {
                _clipboardService.SetContent(CurrentItem);
                StatusMessage = "Copied";
                Task.Delay(2000).ContinueWith(_ => StatusMessage = _configService.Current.IsAutoSaveEnabled ? "AUTO-SAVE" : "MANUAL-SAVE");
            }
        }

        private void ExecuteSave(object? obj)
        {
            if (CurrentItem != null)
            {
                _storageService.SaveItemAsync(CurrentItem, CurrentImageSource).ContinueWith(t => 
                {
                    Application.Current.Dispatcher.Invoke(() => 
                    {
                        if (!t.IsFaulted && obj != null) StatusMessage = "Saved";
                    });
                });
            }
        }
        
        private void ExecuteToggleVisibility(object? obj)
        {
            IsVisible = !IsVisible;
            RequestVisibilityChange?.Invoke(IsVisible);
        }

        private void ExecuteLoadItem(object? obj)
        {
            if (obj is ClipboardItem item)
            {
                CurrentItem = item;
                UpdateViewForContent(item); 
                // Preview only: Do not write to clipboard or close window
                RequestFocus?.Invoke();
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}