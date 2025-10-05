using System.Globalization;

namespace PageRankApp.Maui.Converters;

public class OpacityConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return (bool)value ? 1.0 : 0.5;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
