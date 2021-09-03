using DynamicData;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using SerialViewer_Plus.Com;
using Serilog;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
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

        [Reactive] public int FftSize { get; set; }
        [Reactive] public int BufferSize { get; set; }
        [Reactive] public int ViewPortSize { get; set; }
        [Reactive] public bool IsPaused { get; set; }


        public ReactiveCommand<Unit,Unit> ClearPointsCommand { get; init; }
        public ObservableCollection<ISeries> Series { get; set; } = new ObservableCollection<ISeries>();
        public ObservableCollection<ISeries> FFTs { get; set; } = new ObservableCollection<ISeries>();

        public ObservableCollection<string> Logs = new();

        private static readonly Regex LineRegex = new("\\(?[\\+\\-]?\\d+\\.?\\d*\\)?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static double[][] ParseAutoLine(string s)
        {
            Match[] matches = LineRegex.Matches(s).Where(m => m.Success).ToArray();
            List<double[]> values = new();
            for(int i = 0; i < matches.Length; i++)
            {
                string ms = matches[i].Value;
                if(i < matches.Length - 1 && ms.StartsWith("(") && matches[i+1].Value.EndsWith(")"))
                {
                    if(double.TryParse(ms.Substring(1), out double x) && double.TryParse(matches[i + 1].Value[0..^1], out double y))
                    {
                        values.Add(new double[] { x, y });
                    }
                    else
                    {
                        Log.Error($"Unable to parse: {ms} and {matches[i + 1].Value}");
                    }
                }
                if(double.TryParse(ms, out double val))
                {
                    values.Add(new double[] { val });
                }
            }

            return values.ToArray();
        }
        

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

        private readonly SKColor[] SeriesColors = new SKColor[]
        {
            SKColors.AliceBlue,
            SKColors.Crimson,
            SKColors.Green,
            SKColors.Magenta
        };

        private int SampleCount = 0;
        private void OnAutoValues(double[][] values)
        {
            for(int i = 0; i < values.Length; i++)
            {
                if(i <= Series.Count)
                {
                    Series.Add(new LineSeries<ObservablePoint>()
                    {
                        LineSmoothness = 0,
                        Fill = null,
                        GeometryStroke = null,
                        GeometrySize = 0,
                        Values = new ObservableCollection<ObservablePoint>(),
                    });
                }
                if(Series[i] is LineSeries<ObservablePoint> ls && ls.Values is ObservableCollection<ObservablePoint> points)
                {
                    if(values[i].Length == 1)
                    {
                        points.Add(new(SampleCount, values[i][0]));
                    }
                    else if(values[i].Length == 2)
                    {
                        points.Add(new(values[i][0], values[i][1]));
                    }

                    while (points.Count > BufferSize)
                    {
                        points.RemoveAt(0);
                    }
                }
                else
                {
                    Log.Error($"Unknown series: {Series[i].GetType().FullName}");
                }
            }
            SampleCount++;
        }

        public TerminalViewModel()
        {
            BufferSize = 512;
            var fftPoints = new ObservableCollection<ObservablePoint>();
            FFTs.Add(new LineSeries<ObservablePoint>()
            {
                Values = fftPoints,
                GeometrySize = 1,
            });
            Com = new EmulatedCom(EmulatedCom.EmulationType.Emulated_Auto_XY_Series);



            List<ObservableCollection<ObservablePoint>> points = new();
            List<ISeries<ObservablePoint>> signalSeries = new();



            ClearPointsCommand = ReactiveCommand.Create(() => points.Clear(), null, RxApp.MainThreadScheduler);

            DSPLib.FFT fft = new();
            this.WhenActivated((CompositeDisposable registration) =>
            {
                Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(handle => Logs.CollectionChanged += handle, handle => Logs.CollectionChanged -= handle)
                       .Select(pattern => pattern?.EventArgs)
                       .Where(args => args.Action == NotifyCollectionChangedAction.Add || args.Action == NotifyCollectionChangedAction.Replace)
                       .SelectMany(args => args.NewItems.Cast<string>())
                       .ObserveOn(RxApp.MainThreadScheduler)
                       .Subscribe(log => Log.Debug($"Unhandled message: \"{log}\""))
                       .DisposeWith(registration);

                TimeSpan StatsInterval = TimeSpan.FromSeconds(0.5);
                Observable.Timer(TimeSpan.Zero, StatsInterval)
                          .ObserveOn(RxApp.MainThreadScheduler)
                          .Subscribe(_ =>
                          {
                              GraphUPS = graphUpdateCount / StatsInterval.TotalSeconds;
                              graphUpdateCount = 0;
                          })
                          .DisposeWith(registration);

                this.WhenAnyValue(vm => vm.BufferSize)
                    .Throttle(TimeSpan.FromSeconds(1))
                    .DistinctUntilChanged()
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(bs =>
                    {
                        while (points.Count > bs)
                        {
                            points.RemoveAt(0);
                        }
                    })
                    .DisposeWith(registration);

                var startTime = DateTime.Now;
                Com.IncomingStream
                .Where(_ => !IsPaused)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Select<string, object>(s =>
                {
                    s = s.Trim();
                    double[][] values = ParseAutoLine(s);
                    if(values.Length > 0)
                    {
                        return values;
                    }
                    else
                    {
                        Logs.Add(s);
                        return null;
                    }
                })
                .Where(obj => obj != null)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Do(obj =>
                {
                    if(obj is double[][] autoValues)
                    {
                        OnAutoValues(autoValues);
                    }

                    
                    graphUpdateCount++;

                    
                })
                .Sample(TimeSpan.FromSeconds(0.5))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ =>
                {
                    if (points.Count >= 16)//points.Count == SampleWindow)
                    {
                        FftSize = FindPreviousPowerOf2(points.Count);

                        fft.Initialize((uint)FftSize);

                        var sampleTime = (points[0][^1].X - points[0][^FftSize].X) ?? 0.01;
                        sampleTime /= FftSize;
                        Complex[] cSpectrum = fft.Execute(points[0].Select(op => op.Y ?? 0.0).TakeLast(FftSize).ToArray());
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
