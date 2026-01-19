using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using AfterCopy.Models;
using AfterCopy.Helpers;

namespace AfterCopy.Services
{
    public class StorageService
    {
        private const string HistoryFileName = "history_v2.json";
        private readonly string _appDataPath;
        private readonly string _imagesPath;
        private readonly SemaphoreSlim _fileLock = new SemaphoreSlim(1, 1);

        public StorageService()
        {
            _appDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            _imagesPath = Path.Combine(_appDataPath, "Images");

            Directory.CreateDirectory(_appDataPath);
            Directory.CreateDirectory(_imagesPath);
        }

        public async Task<List<ClipboardItem>> LoadHistoryAsync()
        {
            string filePath = Path.Combine(_appDataPath, HistoryFileName);
            if (!File.Exists(filePath)) return new List<ClipboardItem>();

            await _fileLock.WaitAsync();
            try
            {
                using var stream = File.OpenRead(filePath);
                return await JsonSerializer.DeserializeAsync<List<ClipboardItem>>(stream) 
                       ?? new List<ClipboardItem>();
            }
            catch
            {
                return new List<ClipboardItem>();
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task SaveItemAsync(ClipboardItem item, BitmapSource? imageSource = null)
        {
            // Validate: Don't save empty text
            if (item.Type == ClipboardContentType.Text && string.IsNullOrWhiteSpace(item.Content))
            {
                return;
            }

            Logger.Log($"[StorageService] SaveItemAsync: Type={item.Type}, Content={item.Content}, HasImageSource={imageSource != null}");

            // 1. If it's an image, save to disk first
            if (item.Type == ClipboardContentType.Image && imageSource != null)
            {
                string imageName = $"{item.Id}.png";
                string fullPath = Path.Combine(_imagesPath, imageName);

                Logger.Log($"[StorageService] Saving image to: {fullPath}");

                try
                {
                    using (var fileStream = new FileStream(fullPath, FileMode.Create))
                    {
                        BitmapEncoder encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(imageSource));
                        encoder.Save(fileStream);
                    }
                    item.Content = imageName; // Store filename in JSON
                    Logger.Log($"[StorageService] Image saved successfully: {imageName}");
                }
                catch (Exception ex)
                {
                    Logger.LogError("[StorageService] Failed to save image", ex);
                    return;
                }
            }
            else if (item.Type == ClipboardContentType.Image && imageSource == null)
            {
                Logger.Log($"[StorageService] WARNING: Image item but imageSource is null, Content={item.Content}");
            }

            var history = await LoadHistoryAsync();

            // De-duplication
            var existing = history.FirstOrDefault(h => h.Content == item.Content && h.Type == item.Type);
            if (existing != null)
            {
                history.Remove(existing);
            }

            history.Insert(0, item);

            if (history.Count > 100) history = history.Take(100).ToList();

            await SaveHistoryToFile(history);
        }

        public async Task DeleteItemAsync(string id)
        {
            var history = await LoadHistoryAsync();
            var itemToDelete = history.FirstOrDefault(x => x.Id == id);
            
            if (itemToDelete != null)
            {
                // If it's an image, try to delete the file too
                if (itemToDelete.Type == ClipboardContentType.Image)
                {
                    try
                    {
                        string path = GetFullImagePath(itemToDelete.Content);
                        if (File.Exists(path)) File.Delete(path);
                    }
                    catch { /* Best effort */ }
                }

                history.Remove(itemToDelete);
                await SaveHistoryToFile(history);
            }
        }

        public async Task ClearAllAsync()
        {
            // Delete all image files
            try
            {
                var files = Directory.GetFiles(_imagesPath);
                foreach (var file in files) File.Delete(file);
            }
            catch { }

            // Save empty list
            await SaveHistoryToFile(new List<ClipboardItem>());
        }

        // Public method to force save current state (used after editing items)
        public async Task SaveChangesAsync(List<ClipboardItem> currentFullHistory)
        {
            await SaveHistoryToFile(currentFullHistory);
        }

        private async Task SaveHistoryToFile(List<ClipboardItem> history)
        {
            await _fileLock.WaitAsync();
            try
            {
                string filePath = Path.Combine(_appDataPath, HistoryFileName);
                string tempPath = filePath + ".tmp";
                
                // 1. Write to temp file first (Atomic Write pattern)
                using (var stream = File.Create(tempPath))
                {
                    await JsonSerializer.SerializeAsync(stream, history, new JsonSerializerOptions { WriteIndented = true });
                }

                // 2. Move/Replace original file
                if (File.Exists(filePath))
                {
                    File.Move(tempPath, filePath, overwrite: true);
                }
                else
                {
                    File.Move(tempPath, filePath);
                }
            }
            catch 
            {
                // Log error or ignore
            }
            finally
            {
                _fileLock.Release();
            }
        }
        
        public string GetFullImagePath(string imageName)
        {
            return Path.Combine(_imagesPath, imageName);
        }
    }
}