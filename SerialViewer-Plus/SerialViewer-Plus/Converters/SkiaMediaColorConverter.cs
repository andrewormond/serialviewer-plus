using LiveChartsCore.SkiaSharpView.Painting;
using Serilog;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace SerialViewer_Plus.Converters
{
    public class SkiaMediaColorConverter : IValueConverter
    {
        public static SKColor MediatoSK(Color c) => new SKColor(c.R, c.G, c.B, c.A);

        public  static Color SKtoMedia(SKColor c) =>new()
            {
                R = c.Red,
                G = c.Green,
                B = c.Blue,
                A = c.Alpha,
            };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is null)
            {
                return null;
            }
            else if(value is SolidColorPaint paint)
            {
                return Convert(paint.Color, targetType, parameter, culture);
            }
            else if (value is SKColor skC && (targetType == typeof(Color) || targetType == typeof(Color?)))
            {
                return SKtoMedia(skC);
            }
            else if (value is Color c && targetType == typeof(SKColor))
            {
                return MediatoSK(c);
            }
            else if (value is Color cl && targetType.IsAssignableFrom(typeof(SolidColorPaint)))
            {
                return new SolidColorPaint( MediatoSK(cl));
            }
            else
            {
                Log.Error($"Unable to convert: {value?.GetType().FullName} to {targetType.FullName}");
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Convert(value, targetType, parameter, culture);
    }
}
