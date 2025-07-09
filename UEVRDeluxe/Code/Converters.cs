using System;
using Microsoft.UI.Xaml.Data;

namespace UEVRDeluxe.Code;

public partial class NullableIntToDoubleConverter : IValueConverter {
	public object Convert(object value, Type targetType, object parameter, string language) {
		if (value == null) return double.NaN;
		if (value is int i) return (double)i;
		if (value is double) return value;
		if (value.GetType() == typeof(int?)) {
			int? nullableInt = (int?)value;
			if (nullableInt.HasValue) return (double)nullableInt.Value;
			return double.NaN;
		}
		return double.NaN;
	}

	public object ConvertBack(object value, Type targetType, object parameter, string language) {
		if (value == null) return null;
		if (value is double d) {
			if (double.IsNaN(d)) return null;
			return (int?)d;
		}
		if (double.TryParse(value.ToString(), out double parsed)) {
			if (double.IsNaN(parsed)) return null;
			return (int?)parsed;
		}
		return null;
	}
}

public partial class DateTimeToDateTimeOffsetConverter : IValueConverter {
	public object Convert(object value, Type targetType, object parameter, string language) {
		if (value is DateTime dt) return new DateTimeOffset(dt);
		return DateTimeOffset.Now;
	}

	public object ConvertBack(object value, Type targetType, object parameter, string language) {
		if (value is DateTimeOffset dto) return dto.DateTime;
		return DateTime.Now;
	}
}
