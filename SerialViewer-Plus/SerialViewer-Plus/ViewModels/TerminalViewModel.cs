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
using System.Numerics;
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

        public uint SampleWindow = 128;

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
            var fftPoints = new ObservableCollection<ObservablePoint>();
            FFTs.Add(new LineSeries<ObservablePoint>()
            {
                Values = fftPoints,
                GeometrySize = 1,
            });

            DSPLib.FFT fft = new DSPLib.FFT();
            fft.Initialize(SampleWindow);
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
                    while(points.Count > SampleWindow)
                    {
                        points.RemoveAt(0);
                    }
                    if(points.Count == SampleWindow)
                    {
                        var sampleTime = (points[points.Count - 1].X - points[0].X) ?? 0.01;
                        sampleTime /= points.Count;
                        Complex[] cSpectrum = fft.Execute(points.Select(op => op.Y ?? 0.0).ToArray());
                        double[] lmSpectrum = DSPLib.DSP.ConvertComplex.ToMagnitude(cSpectrum);
                        double[] freqSpan = fft.FrequencySpan(1.0/sampleTime);
                        fftPoints.Clear();
                        for(int i = 0; i < lmSpectrum.Length; i++)
                        {
                            fftPoints.Add(new ObservablePoint(freqSpan[i], lmSpectrum[i]));
                        }
                    }
                })
                .DisposeWith(registration);
            });

        }
    }
}
