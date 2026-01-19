using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;
using AfterCopy.Models;
using AfterCopy.Services;
using AfterCopy.Helpers;
using System.IO;
using System.Windows.Threading;

namespace AfterCopy
{
    public partial class HistoryWindow : Window
    {
        public event Action<ClipboardItem>? OnItemSelected;
        private readonly StorageService _storageService;
        private List<ClipboardItem> _fullHistory = new List<ClipboardItem>();
        private ICollectionView? _groupedView;
        private DispatcherTimer _autoRefreshTimer;
        private DetailsWindow? _detailsWindow;

        public HistoryWindow()
        {
            InitializeComponent();
            _storageService = new StorageService();
            LoadData();

            _autoRefreshTimer = new DispatcherTimer();
            _autoRefreshTimer.Interval = TimeSpan.FromSeconds(3);
            _autoRefreshTimer.Tick += (s, e) => LoadData(checkDiff: true);
            _autoRefreshTimer.Start();
        }

        protected override void OnClosed(EventArgs e)
        {
            _autoRefreshTimer?.Stop();

            // Close DetailsWindow if open
            if (_detailsWindow != null && _detailsWindow.IsLoaded)
            {
                _detailsWindow.Close();
                _detailsWindow = null;
            }

            base.OnClosed(e);
        }

        private async void LoadData(bool checkDiff = false)
        {
            var newData = await _storageService.LoadHistoryAsync();
            
            if (checkDiff && _fullHistory != null)
            {
                // Basic check: Count and Top Item ID
                if (newData.Count == _fullHistory.Count)
                {
                    var newTop = newData.OrderByDescending(x => x.Timestamp).FirstOrDefault();
                    var oldTop = _fullHistory.OrderByDescending(x => x.Timestamp).FirstOrDefault();
                    if (newTop?.Id == oldTop?.Id) return; // No apparent change
                }
            }

            _fullHistory = newData;
            RefreshView();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadData(checkDiff: false);
        }

        private void RefreshView()
        {
            _groupedView = CollectionViewSource.GetDefaultView(_fullHistory);
            
            // Grouping
            _groupedView.GroupDescriptions.Clear();
            _groupedView.GroupDescriptions.Add(new PropertyGroupDescription("Timestamp", new DateGroupConverter()));
            
            // Sorting
            _groupedView.SortDescriptions.Clear();
            _groupedView.SortDescriptions.Add(new SortDescription("Timestamp", ListSortDirection.Descending));

            // Filtering
            string query = SearchBox.Text.ToLower();
            if (!string.IsNullOrWhiteSpace(query))
            {
                _groupedView.Filter = obj =>
                {
                    if (obj is ClipboardItem item)
                    {
                        return item.Content != null && item.Content.ToLower().Contains(query);
                    }
                    return false;
                };
            }
            else
            {
                _groupedView.Filter = null;
            }

            HistoryList.ItemsSource = _groupedView;
            _groupedView.Refresh();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_fullHistory == null) return;
            RefreshView(); // Simplified logic to just re-apply filter on view
        }

        private async void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string id)
            {
                // Stop the event from bubbling to the List Item selection
                e.Handled = true; 
                
                var itemToRemove = _fullHistory.FirstOrDefault(x => x.Id == id);
                if (itemToRemove != null)
                {
                    // 1. Update UI List immediately for responsiveness
                    _fullHistory.Remove(itemToRemove);
                    RefreshView();

                    // 2. Persist change
                    await _storageService.DeleteItemAsync(id);
                }
            }
        }

        private async void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to delete ALL history? This cannot be undone.", 
                "Clear History", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _fullHistory.Clear();
                RefreshView();
                await _storageService.ClearAllAsync();
            }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            ContextMenu cm = new ContextMenu();
            MenuItem allItem = new MenuItem { Header = "Export All History" };
            allItem.Click += (s, args) => PerformExport(false);
            MenuItem todayItem = new MenuItem { Header = "Export Today Only" };
            todayItem.Click += (s, args) => PerformExport(true);
            cm.Items.Add(allItem);
            cm.Items.Add(todayItem);
            cm.PlacementTarget = sender as Button;
            cm.IsOpen = true;
        }

        private void PerformExport(bool todayOnly)
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "JSON Data (*.json)|*.json|Text File (*.txt)|*.txt",
                FileName = todayOnly ? $"AfterCopy_Export_{DateTime.Now:yyyyMMdd}" : "AfterCopy_Export_All"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    IEnumerable<ClipboardItem> dataToExport = _fullHistory;
                    if (todayOnly)
                    {
                        dataToExport = dataToExport.Where(x => x.Timestamp.Date == DateTime.Today).ToList();
                    }

                    if (saveDialog.FilterIndex == 1) // JSON
                    {
                        string json = JsonSerializer.Serialize(dataToExport, new JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText(saveDialog.FileName, json);
                    }
                    else // TXT
                    {
                        using (var writer = new StreamWriter(saveDialog.FileName))
                        {
                            foreach (var item in dataToExport)
                            {
                                writer.WriteLine($"--- {item.Timestamp} ---");
                                writer.WriteLine(item.Content);
                                writer.WriteLine();
                            }
                        }
                    }
                    MessageBox.Show("Export successful!", "AfterCopy", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void ListViewItem_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Check if we clicked the delete button
            if (e.OriginalSource is DependencyObject dep)
            {
                var btn = FindParent<Button>(dep);
                if (btn != null && btn.Name != "") return; // Assuming delete button might be distinguishable, but standard buttons consume click.
                // Actually, Button Click logic usually suppresses MouseUp bubbling if handled.
                // But Preview happens before.
                // If I use Preview, I hijack the delete button.
                // So I MUST use MouseLeftButtonUp (bubbling) or check source carefully.
            }
             
            // Let's use MouseLeftButtonUp in the code-behind signature, but XAML had Preview. 
            // I'll stick to what I wrote in XAML: PreviewMouseLeftButtonUp.
            // Wait, if I use Preview, I block the delete button unless I check.
            
            // Re-strategy: In XAML I used EventSetter Event="PreviewMouseLeftButtonUp". 
            // I should change XAML to "MouseLeftButtonUp" to be safe? 
            // Or just check here. The Delete button has a specific Style.
            
            if (sender is ListViewItem item && item.DataContext is ClipboardItem clipboardItem)
            {
                 // Check if the click target is the delete button or part of it
                 if (IsDescendantOfDeleteButton(e.OriginalSource as DependencyObject)) return;

                 // Use single instance of DetailsWindow (non-modal)
                 if (_detailsWindow == null || !_detailsWindow.IsLoaded)
                 {
                     _detailsWindow = new DetailsWindow(clipboardItem);
                     _detailsWindow.Closed += (s, args) => _detailsWindow = null;
                     _detailsWindow.Show();
                 }
                 else
                 {
                     // Update content in existing window
                     _detailsWindow.UpdateContent(clipboardItem);

                     // Bring to front if minimized
                     if (_detailsWindow.WindowState == WindowState.Minimized)
                         _detailsWindow.WindowState = WindowState.Normal;
                     _detailsWindow.Activate();
                 }
            }
        }

        private bool IsDescendantOfDeleteButton(DependencyObject? current)
        {
            while (current != null)
            {
                if (current is Button btn && btn.Style != null)
                {
                     // Check if it's our delete button style? Or just any button?
                     // We have Export/Clear buttons but they are not in the ListViewItem.
                     // The only button in ListViewItem is Delete.
                     return true;
                }
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            return false;
        }

        private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent) return parent;
                child = System.Windows.Media.VisualTreeHelper.GetParent(child);
            }
            return null;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void GroupHeader_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string groupName)
            {
                // Parse the group name back to date
                DateTime targetDate = DateTime.MinValue;
                if (groupName == "Today") targetDate = DateTime.Today;
                else if (groupName == "Yesterday") targetDate = DateTime.Today.AddDays(-1);
                else DateTime.TryParse(groupName, out targetDate);

                if (targetDate != DateTime.MinValue)
                {
                    // Filter items for this date
                    var dayItems = _fullHistory.Where(x => x.Timestamp.Date == targetDate).ToList();
                    
                    if (dayItems.Any())
                    {
                        var logWindow = new DailyLogWindow(targetDate, dayItems);
                        logWindow.ShowDialog(); // Modal, keeps focus
                    }
                }
            }
        }
    }
}