using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using AfterCopy.Models;
using AfterCopy.Services;

namespace AfterCopy
{
    public class TimelineGroup : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        
        public string HourLabel { get; set; }
        public string Name { get; set; }
        public string CountLabel { get; set; }
        
        private ClipboardItem? _firstItem;
        
        public TimelineGroup(string hourLabel, string name, string countLabel, ClipboardItem? firstItem)
        {
            HourLabel = hourLabel;
            Name = name;
            CountLabel = countLabel;
            _firstItem = firstItem;
            
            // Listen to changes in the underlying item
            if (_firstItem != null)
            {
                _firstItem.PropertyChanged += (s, e) => 
                {
                    if (e.PropertyName == nameof(ClipboardItem.DisplaySummary))
                    {
                        OnPropertyChanged(nameof(Summary));
                    }
                };
            }
        }

        public string Summary => _firstItem?.DisplaySummary ?? "";

        protected void OnPropertyChanged(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class DailyLogWindow : Window
    {
        private readonly List<ClipboardItem> _dayItems;
        private readonly ClipboardService _clipboardService;
        private readonly StorageService _storageService; // Add storage service

        public DailyLogWindow(DateTime date, List<ClipboardItem> dayItems)
        {
            InitializeComponent();
            _clipboardService = new ClipboardService();
            _storageService = new StorageService(); // Init
            _dayItems = dayItems;
            DateHeader.Text = date.ToString("MMMM dd, yyyy");

            LoadData();
        }

        private void LoadData()
        {
            // 1. Setup Right Side (Content)
            var view = CollectionViewSource.GetDefaultView(_dayItems);
            view.GroupDescriptions.Clear();
            view.GroupDescriptions.Add(new PropertyGroupDescription("Timestamp", new HourGroupConverter()));
            view.SortDescriptions.Add(new SortDescription("Timestamp", ListSortDirection.Ascending));
            
            ContentList.ItemsSource = view;

            // 2. Setup Left Side (Navigation)
            var groups = _dayItems
                .GroupBy(x => x.Timestamp.ToString("HH:00"))
                .OrderBy(g => g.Key)
                .Select(g => new TimelineGroup(
                    g.Key,
                    g.Key,
                    $"{g.Count()} items",
                    g.First() // Pass the first item to track summary
                ))
                .ToList();

            TimelineList.ItemsSource = groups;
        }

        private void TimelineList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TimelineList.SelectedItem is TimelineGroup group)
            {
                var firstItem = _dayItems.Where(x => new HourGroupConverter().Convert(x.Timestamp, null, null, null).ToString() == group.Name)
                                         .OrderBy(x => x.Timestamp)
                                         .FirstOrDefault();
                
                if (firstItem != null)
                {
                    ContentList.ScrollIntoView(firstItem);
                }
            }
        }

        private void CopyItem_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is ClipboardItem item)
            {
                _clipboardService.SetContent(item);
                MessageBox.Show("Copied to clipboard!", "AfterCopy", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // Auto-save on lost focus (edit done)
        private async void Item_LostFocus(object sender, RoutedEventArgs e)
        {
            // We need to save the WHOLE history list back to disk, not just this day's items.
            // But _dayItems is just a subset.
            // To do this correctly without re-loading the whole file, we should have passed the FULL history reference 
            // or we accept that we need to load->merge->save.
            // Simplified approach for MVP:
            // 1. Load full history from disk.
            // 2. Find the items we edited (by ID) and update them.
            // 3. Save back.
            // BETTER: Since StorageService is singleton-ish or stateless file writer, 
            // we can just re-load, update our subset, and save.
            
            // Actually, to avoid race conditions or overwriting other days:
            // The cleanest way is to trigger an update by ID in StorageService.
            // But StorageService currently overwrites the whole file.
            
            // Let's assume HistoryWindow passed us a REFERENCE to objects that are part of the full list?
            // Yes, in HistoryWindow we did: var dayItems = _fullHistory.Where(...).ToList();
            // Since objects are references, modifying _dayItems[i] also modifies the object inside _fullHistory!
            // So we just need to ask StorageService to save _fullHistory.
            // But wait, we don't have access to _fullHistory here.
            
            // Fix: Let's just rely on a "Fire and Update" approach where we load fresh, update, save.
            // Or better: Let's pass the Service or a Callback?
            // For now, let's load full, update, save. It's safe enough for single user.
            
            var fullHistory = await _storageService.LoadHistoryAsync();
            bool changed = false;
            
            foreach (var item in _dayItems)
            {
                var existing = fullHistory.FirstOrDefault(x => x.Id == item.Id);
                if (existing != null)
                {
                    if (existing.Title != item.Title || existing.Content != item.Content)
                    {
                        existing.Title = item.Title;
                        existing.Content = item.Content;
                        changed = true;
                    }
                }
            }
            
            if (changed)
            {
                await _storageService.SaveChangesAsync(fullHistory);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    public class HourGroupConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is DateTime dt)
            {
                return dt.ToString("HH:00");
            }
            return "Unknown";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
