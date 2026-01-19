using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using AfterCopy.Models;

namespace AfterCopy.Services
{
    public interface IClipboardService
    {
        event Action<ClipboardItem> ClipboardChanged;
        void StartMonitoring(IntPtr windowHandle);
        void StopMonitoring(IntPtr windowHandle);
        void SetContent(ClipboardItem item);
    }

    public class ClipboardService : IClipboardService
    {
        public event Action<ClipboardItem> ClipboardChanged;

        // Win32 Hooks handled in MainWindow, this service processes the data
        // when notified.
        
        // Retry policy constants
        private const int MaxRetries = 5;
        private const int RetryDelayMs = 50;

        public void OnClipboardUpdate()
        {
            // Run in background to not block UI, then marshal back if needed
            // But Clipboard access must be on STA thread (UI thread usually).
            // We use a robust retry mechanism.
            
            ClipboardItem item = null;

            for (int i = 0; i < MaxRetries; i++)
            {
                try
                {
                    if (Clipboard.ContainsText())
                    {
                        string text = Clipboard.GetText();
                        item = new ClipboardItem
                        {
                            Type = ClipboardContentType.Text,
                            Content = text,
                            DisplayPreview = text,
                            IsCode = DetectCode(text)
                        };
                        break; 
                    }
                    else if (Clipboard.ContainsImage())
                    {
                        // Handle Image
                        var image = Clipboard.GetImage();
                        if (image != null)
                        {
                            // We don't save the bitmap directly here to avoid memory bloat
                            // We signal it's an image. The View/Storage will handle saving/displaying.
                            item = new ClipboardItem
                            {
                                Type = ClipboardContentType.Image,
                                Content = "[Image Content]", 
                                DisplayPreview = "Image Snapshot captured"
                            };
                            // In a real app, we might cache this bitmap to temp disk immediately
                            // to persist it. For now, we pass the "event" that an image exists.
                        }
                        break;
                    }
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                    // Clipboard locked by another process
                    Thread.Sleep(RetryDelayMs);
                }
                catch (Exception)
                {
                    // Unknown error
                    break;
                }
            }

            if (item != null)
            {
                ClipboardChanged?.Invoke(item);
            }
        }

        public void SetContent(ClipboardItem item)
        {
            try
            {
                if (item.Type == ClipboardContentType.Text)
                {
                    Clipboard.SetText(item.Content);
                }
                else if (item.Type == ClipboardContentType.Image)
                {
                    // Requires reloading image from disk if we stored it
                    // For this V1 refactor, we focus on text restore. 
                    // Image restore is complex without full caching.
                }
            }
            catch { /* Ignore set errors */ }
        }

        // Simple heuristic to detect code
        private bool DetectCode(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            string t = text.Trim();
            return t.StartsWith("public ") || t.StartsWith("class ") || 
                   t.StartsWith("def ") || t.StartsWith("import ") ||
                   t.StartsWith("{") || t.StartsWith("<") || 
                   t.Contains(";") && t.Contains("=") ||
                   t.Length > 50 && t.Contains("    "); // Indentation
        }

        public void StartMonitoring(IntPtr windowHandle) { /* Logic remains in Window Interop for simplicity */ }
        public void StopMonitoring(IntPtr windowHandle) { /* Logic remains in Window Interop for simplicity */ }
    }
}
