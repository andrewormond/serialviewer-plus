using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView.WPF;
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
    }
}
