using LiveChartsCore.Defaults;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView.WPF;
using Newtonsoft.Json.Linq;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace SerialViewer_Plus
{
    public static class Extensions
    {

        public static Point GetDataPosition(this CartesianChart chart, Point mousePoint)
        {
            var fp = chart.ScaleUIPoint(new((float)mousePoint.X , (float)mousePoint.Y));
            return new(fp[0], fp[1]);
        }

        public static Point GetDataPosition(this CartesianChart chart, MouseButtonEventArgs e)
        {
            return chart.GetDataPosition(e.GetPosition(chart));
        }

        public static Point GetDataPosition(this CartesianChart chart, MouseEventArgs e)
        {
            return chart.GetDataPosition(e.GetPosition(chart));
        }
        
        public static void ForEach<T>(this IEnumerable<T> en, Action<T> handler)
        {
            foreach(T t in en)
            {
                handler?.Invoke(t);
            }
        }

        public static string PrettyPrint(this Point p) => $"({p.X:0.00}, {p.Y:0.00})";


        private const string KEY_RED = "R";
        private const string KEY_GREEN = "G";
        private const string KEY_BLUE = "B";
        private const string KEY_ALPHA = "A";
        public static JObject ToJObject(this SKColor color) => new JObject()
        {
            [KEY_RED] = color.Red,
            [KEY_GREEN] = color.Green,
            [KEY_BLUE] = color.Blue,
            [KEY_ALPHA] = color.Alpha,
        };

        public static SKColor ToSKColor(this JToken token)
        {
            byte red = token.Value<byte>(KEY_RED);
            byte green = token.Value<byte>(KEY_GREEN);
            byte blue = token.Value<byte>(KEY_BLUE);
            byte alpha = token.Value<byte>(KEY_ALPHA);
            return new SKColor(red, green, blue, alpha);
        }

        public static Point ToPoint(this ObservablePoint op) => new Point(op.X ?? 0, op.Y ?? 0);
    }
}
