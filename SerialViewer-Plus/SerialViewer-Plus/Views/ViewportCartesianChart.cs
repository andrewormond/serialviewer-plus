using LiveChartsCore;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Drawing;
using LiveChartsCore.SkiaSharpView.Drawing.Geometries;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WPF;
using ReactiveUI.Fody.Helpers;
using Serilog;
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
            MouseRightButtonDown += (object sender, MouseButtonEventArgs e) => ResetAxis();
        }

        protected RectangularSection selection = null;

        public delegate void SelectionHandler(Rect section);
        public event SelectionHandler OnSelection;
        public event Action OnSelectionReset;

        public SKColor SelectionColor { get; set; } = SKColors.Black;

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
                    Fill = new SolidColorPaint(SelectionColor.WithAlpha(0x40)),
                    Stroke = new SolidColorPaint(SelectionColor.WithAlpha(255), 1.5f),
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
        
        public void SetAxis(double? MaxXLimit, double? MinXLimit, double? MaxYLimit, double? MinYLimit)
        {
            if (XAxes.FirstOrDefault() is IAxis xaxis)
            {
                xaxis.MinLimit = MinXLimit;
                xaxis.MaxLimit = MaxXLimit;
            }
            if (YAxes.FirstOrDefault() is IAxis yaxis)
            {
                yaxis.MinLimit = MinYLimit;
                yaxis.MaxLimit = MaxYLimit;
            }
        }

        public void ResetAxis()
        {
            SetAxis(null, null, null, null);
            OnSelectionReset?.Invoke();
        }

        protected void OnSelectionComplete(object sender, MouseButtonEventArgs e)
        {
            if (selection == null) return;

            Point dataPoint = this.GetDataPosition(e);
            selection.Xj = dataPoint.X;
            selection.Yj = dataPoint.Y;

            if (selection.Xi != selection.Xj && selection.Yi != selection.Yj)
            {
                double MaxXLimit = Math.Max(selection.Xi.Value, selection.Xj.Value);
                double MinXLimit = Math.Min(selection.Xi.Value, selection.Xj.Value);
                double MaxYLimit = Math.Max(selection.Yi.Value, selection.Yj.Value);
                double MinYLimit = Math.Min(selection.Yi.Value, selection.Yj.Value);
                Log.Information($"Selection is: X:({MinXLimit}->{MaxXLimit}), Y:({MinYLimit}->{MaxYLimit}");
                SetAxis(MaxXLimit, MinXLimit, MaxYLimit, MinYLimit);
                OnSelection?.Invoke(new Rect(MinXLimit, MinYLimit, MaxXLimit - MinXLimit, MaxYLimit - MinYLimit));
            }


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
