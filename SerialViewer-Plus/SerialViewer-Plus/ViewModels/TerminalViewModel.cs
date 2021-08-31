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
using System.Reactive;
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

        [Reactive] public int WindowSize { get; set; }
        [Reactive] public bool IsPaused { get; set; }

        public ReactiveCommand<Unit,Unit> ClearPointsCommand { get; init; }
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

        public const int SampleWindow = 512;

        public static int FindPreviousPowerOf2(int n)
        {
            // initialize result by 1
            int k = 1;

            // double `k` and divide `n` in half till it becomes 0
            while (n > 0)
            {
                k <<= 1;    // double `k`
                n >>= 1;
            }

            return k/2;
        }

        [Reactive] public double GraphUPS { get; set; }
        private int graphUpdateCount = 0;

        public TerminalViewModel()
        {
            Com = new EmulatedCom();
            var points = new ObservableCollection<ObservablePoint>();
            var s1 = new ScatterSeries<ObservablePoint>()
            {
                Values = points,
                GeometrySize = 1,
            };
            Series.Add(s1);
            var fftPoints = new ObservableCollection<ObservablePoint>();
            FFTs.Add(new LineSeries<ObservablePoint>()
            {
                Values = fftPoints,
                GeometrySize = 1,
            });

            ClearPointsCommand = ReactiveCommand.Create(() => points.Clear(), null, RxApp.MainThreadScheduler);

            DSPLib.FFT fft = new DSPLib.FFT();
            this.WhenActivated((CompositeDisposable registration) =>
            {
                TimeSpan StatsInterval = TimeSpan.FromSeconds(0.5);
                Observable.Timer(TimeSpan.Zero, StatsInterval)
                          .ObserveOn(RxApp.MainThreadScheduler)
                          .Subscribe(_ =>
                          {
                              GraphUPS = graphUpdateCount / StatsInterval.TotalSeconds;
                              graphUpdateCount = 0;
                          })
                          .DisposeWith(registration);


                Com.IncomingStream
                .Where(_ => !IsPaused)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Select(s =>
                {
                    s = s.Trim();
                    var values = ParseString(s);
                    if (values.Length == 2)
                    {
                        return new ObservablePoint(values[0], values[1]);
                    }
                    return null;
                })
                .Where(p => p != null)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Do(p =>
                {
                    points.Add(p);
                    graphUpdateCount++;
                    while(points.Count > SampleWindow)
                    {
                        points.RemoveAt(0);
                    }

                    
                })
                .Sample(TimeSpan.FromSeconds(0.5))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(p =>
                {
                    if (points.Count >= 16)//points.Count == SampleWindow)
                    {
                        WindowSize = FindPreviousPowerOf2(points.Count);

                        fft.Initialize((uint)WindowSize);

                        var sampleTime = (points[points.Count - 1].X - points[points.Count - WindowSize].X) ?? 0.01;
                        sampleTime /= WindowSize;
                        Complex[] cSpectrum = fft.Execute(points.Select(op => op.Y ?? 0.0).TakeLast(WindowSize).ToArray());
                        double[] lmSpectrum = DSPLib.DSP.ConvertComplex.ToMagnitude(cSpectrum);
                        double[] freqSpan = fft.FrequencySpan(1.0 / sampleTime);
                        fftPoints.Clear();
                        for (int i = 0; i < lmSpectrum.Length; i++)
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
