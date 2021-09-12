using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SerialViewer_Plus.Models
{
    public class PlotSeries : LineSeries<ObservablePoint>
    {



        public PlotSeries() : base()
        {
        }

        protected override void OnPaintChanged(string propertyName)
        {
            base.OnPaintChanged(propertyName);
            if(propertyName == nameof(Stroke))
            {
                GeometryFill = Stroke.CloneTask();
                GeometryStroke = Stroke.CloneTask();
                if(Fill != null && Fill is SolidColorPaint prevFill && Stroke is SolidColorPaint pnt)
                {
                    Fill = new SolidColorPaint(pnt.Color.WithAlpha(prevFill.Color.Alpha));
                }
                
                PaintHasChanged?.Invoke();
            }
        }

        public event Action PaintHasChanged;
    }
}
