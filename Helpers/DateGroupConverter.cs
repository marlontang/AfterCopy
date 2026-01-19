using System;
using System.Globalization;
using System.Windows.Data;

namespace AfterCopy.Helpers
{
    public class DateGroupConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime date)
            {
                if (date.Date == DateTime.Today) return "Today";
                if (date.Date == DateTime.Today.AddDays(-1)) return "Yesterday";
                return date.ToString("MMMM dd, yyyy");
            }
            return "Older";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
