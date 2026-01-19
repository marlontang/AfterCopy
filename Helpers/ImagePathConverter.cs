using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace AfterCopy.Helpers
{
    public class ImagePathConverter : IValueConverter
    {
        private static readonly string ImagesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Images");

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string filename && !string.IsNullOrEmpty(filename))
            {
                // If it's already a full path, return it
                if (Path.IsPathRooted(filename)) return filename;
                
                // Otherwise combine with app data path
                return Path.Combine(ImagesPath, filename);
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}