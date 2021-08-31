using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using SerialViewer_Plus.Com;
using Serilog;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SerialViewer_Plus.ViewModels
{
    public class TerminalViewModel : ReactiveObject, IActivatableViewModel
    {
        public ViewModelActivator Activator { get; } = new();
        [Reactive] public ICom Com { get; set; }
        public ObservableCollection<ISeries> Series { get; set; } = new ObservableCollection<ISeries>();
        public ObservableCollection<ISeries> FFTs { get; set; } = new ObservableCollection<ISeries>();

        private static readonly Regex LineRegex = new Regex("[\\+\\-]?\\d+\\.?\\d*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public double[] ParseString(string s)
        {
            IEnumerable<Match> matches = LineRegex.Matches(s).Where(m => m.Success);
            List<double> values = new();
            foreach(Match m in matches)
            {
                if(double.TryParse(m.Value, out double val))
                {
                    values.Add(val);
                }
            }

            return values.ToArray();
        }

        

        public TerminalViewModel()
        {
            Com = new EmulatedCom();
            var points = new ObservableCollection<ObservablePoint>();
            var s1 = new ScatterSeries<ObservablePoint>()
            {
                Values = points,
                GeometrySize = 4,
            };
            Series.Add(s1);
            this.WhenActivated((CompositeDisposable registration) =>
            {

                Com.IncomingStream
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Select(s =>
                {
                    s = s.Trim();
                    var values = ParseString(s);
                    if (values.Length == 2)
                    {
                        Log.Information($"X: {values[0]}, Y: {values[1]}");
                        return new ObservablePoint(values[0], values[1]);
                    }
                    return null;
                })
                .Where(p => p != null)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(p =>
                {
                    points.Add(p);
                    if(points.Count > 500)
                    {
                        points.RemoveAt(0);
                    }
                })
                .DisposeWith(registration);
            });

        }
    }
}
