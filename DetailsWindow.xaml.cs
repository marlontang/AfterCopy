using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using AfterCopy.Models;
using AfterCopy.Helpers;

namespace AfterCopy
{
    public partial class DetailsWindow : Window
    {
        private static readonly string ImagesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Images");

        public DetailsWindow()
        {
            InitializeComponent();
        }

        public DetailsWindow(ClipboardItem item) : this()
        {
            UpdateContent(item);
        }

        /// <summary>
        /// Update the displayed content without creating a new window
        /// </summary>
        public void UpdateContent(ClipboardItem item)
        {
            if (item == null) return;

            if (item.Type == ClipboardContentType.Image)
            {
                // Show image
                ContentBox.Visibility = Visibility.Collapsed;
                ImageScrollViewer.Visibility = Visibility.Visible;

                try
                {
                    string imagePath = Path.IsPathRooted(item.Content)
                        ? item.Content
                        : Path.Combine(ImagesPath, item.Content);

                    Logger.Log($"[DetailsWindow] Loading image: ItemId={item.Id}, Content={item.Content}, FullPath={imagePath}");

                    if (File.Exists(imagePath))
                    {
                        Logger.Log($"[DetailsWindow] Image file exists, loading...");
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        ContentImage.Source = bitmap;
                        Logger.Log($"[DetailsWindow] Image loaded successfully");
                    }
                    else
                    {
                        // Image file not found, show error text
                        Logger.Log($"[DetailsWindow] Image file NOT found at: {imagePath}");
                        Logger.Log($"[DetailsWindow] ImagesPath base: {ImagesPath}");
                        Logger.Log($"[DetailsWindow] Directory exists: {Directory.Exists(ImagesPath)}");
                        if (Directory.Exists(ImagesPath))
                        {
                            var files = Directory.GetFiles(ImagesPath);
                            Logger.Log($"[DetailsWindow] Files in Images folder: {string.Join(", ", files)}");
                        }
                        ContentBox.Visibility = Visibility.Visible;
                        ImageScrollViewer.Visibility = Visibility.Collapsed;
                        ContentBox.Text = "[Image file not found]";
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("[DetailsWindow] Failed to load image", ex);
                    ContentBox.Visibility = Visibility.Visible;
                    ImageScrollViewer.Visibility = Visibility.Collapsed;
                    ContentBox.Text = "[Failed to load image]";
                }
            }
            else
            {
                // Show text
                ContentBox.Visibility = Visibility.Visible;
                ImageScrollViewer.Visibility = Visibility.Collapsed;
                ContentBox.Text = item.Content;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }
    }
}