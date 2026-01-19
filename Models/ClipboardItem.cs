using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AfterCopy.Models
{
    public enum ClipboardContentType
    {
        Text,
        Image,
        File
    }

    public class ClipboardItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public string Id { get; set; } = Guid.NewGuid().ToString();
        public ClipboardContentType Type { get; set; }
        
        private string _content = string.Empty;
        public string Content 
        { 
            get => _content;
            set { _content = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplaySummary)); }
        }
        
        public string DisplayPreview { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string DisplayTime => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");

        public bool IsCode { get; set; } 
        public bool IsPinned { get; set; } = false;
        
        private string? _quickKey;
        public string? QuickKey 
        { 
            get => _quickKey;
            set { _quickKey = value; OnPropertyChanged(); }
        }

        private string _title = string.Empty;
        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplaySummary)); }
        }

        public string DisplaySummary
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Title)) return Title;
                if (Type == ClipboardContentType.Image) return "[COPY IMAGE]";
                if (string.IsNullOrWhiteSpace(Content)) return "(Empty)";
                var summary = Content.Replace(Environment.NewLine, " ").Trim();
                return summary.Length > 50 ? summary.Substring(0, 50) + "..." : summary;
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
