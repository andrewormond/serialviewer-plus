using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Drawing;
using LiveChartsCore.SkiaSharpView.Drawing.Geometries;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WPF;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace SerialViewer_Plus.Views
{
    public class ViewportCartesianChart : CartesianChart
    {

        public ViewportCartesianChart() : base()
        {
            MouseLeftButtonDown += OnSelectionStart;
            MouseMove += OnSelectionChange;
            MouseLeftButtonUp += OnSelectionComplete;
            MouseLeave += (object sender, MouseEventArgs e) => OnSelectionCancel();
        }

        protected RectangularSection selection = null;

        public delegate void SelectionHandler(RectangularSection section);
        public event SelectionHandler OnSelection;


        protected void OnSelectionStart(object sender, MouseButtonEventArgs e)
        {
            if(Sections == null || Sections.Count() == 0)
            {
                Sections = new ObservableCollection<RectangularSection>();
            }

            OnSelectionCancel();
            if(Sections is ICollection<RectangularSection> coll)
            {
                Point dataPoint = this.GetDataPosition(e);
                selection = new()
                {
                    Fill = new SolidColorPaint(SKColors.Gray.WithAlpha(128)),
                    Xi = dataPoint.X,
                    Yi = dataPoint.Y,
                    Xj = dataPoint.X,
                    Yj = dataPoint.Y,
                };
                coll.Add(selection);
            }
            else
            {
                Debugger.Break();
            }
        }

        protected void OnSelectionChange(object sender, MouseEventArgs e)
        {
            if(selection != null)
            {
                Point dataPoint = this.GetDataPosition(e);
                selection.Xj = dataPoint.X;
                selection.Yj = dataPoint.Y;
            }
        }

        protected void OnSelectionComplete(object sender, MouseButtonEventArgs e)
        {
            Point dataPoint = this.GetDataPosition(e);
            selection.Xj = dataPoint.X;
            selection.Yj = dataPoint.Y;

            OnSelection?.Invoke(selection);

            OnSelectionCancel();
        }

        protected void OnSelectionCancel()
        {
            if (selection != null && Sections is ICollection<RectangularSection> coll)
                {
                coll.Remove(selection);
                selection = null;
            }
        }
    }
}
