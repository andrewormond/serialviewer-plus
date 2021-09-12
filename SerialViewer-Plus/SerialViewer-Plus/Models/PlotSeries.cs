using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReactiveUI;
using SkiaSharp;
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

        private const string KEY_NAME = "Name";
        private const string KEY_COLOR = "Color";

        public JObject ToJObject()
        {
            return new JObject()
            {
                [KEY_NAME] = Name,
                [KEY_COLOR] = (Stroke as SolidColorPaint)?.Color.ToJObject(),
            };
        }


        public void FromJObject(JObject jobj)
        {
            Name = jobj.Value<string>(KEY_NAME);
            SKColor color = jobj.Value<JObject>(KEY_COLOR).ToSKColor();
            Stroke = new SolidColorPaint(color);
        }

        public event Action PaintHasChanged;
    }
}
