using DynamicData;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using SerialViewer_Plus.Com;
using SerialViewer_Plus.Models;
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
        [Reactive] public BaseCom Com { get; set; }

        [Reactive] public int FftSize { get; set; }
        [Reactive] public int BufferSize { get; set; }
        [Reactive] public int ViewPortSize { get; set; }
        [Reactive] public bool IsPaused { get; set; }


        public ReactiveCommand<Unit,Unit> ClearPointsCommand { get; init; }
        public ObservableCollection<ISeries> Series { get; set; } = new ObservableCollection<ISeries>();
        public ObservableCollection<ISeries> FFTs { get; set; } = new ObservableCollection<ISeries>();

        public ObservableCollection<string> Logs = new();

        private static readonly Regex LineRegex = new("\\(?[\\+\\-]?\\d+\\.?\\d*\\)?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static AutoMessage[] ParseAutoLine(string line)
        {
            double y;
            line = line.Trim();
            List<AutoMessage> values = new();
            string[] entries = line.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            for(int i = 0; i < entries.Length; i++)
            {
                string s = entries[i];
                if (s.StartsWith("("))
                {
                    if(double.TryParse(s[1..], out double x))
                    {
                        i++;
                        while (i < entries.Length)
                        {
                            s = entries[i];
                            if (s.EndsWith(")"))
                            {
                                if (s.Length > 1 && double.TryParse(s[..^1], out y))
                                {
                                    values.Add(new(x, y));
                                }
                                else if (s.Length != 1)
                                {
                                    Log.Error($"Unable to End ')' parse: \"{s}\" from \"{line}\"");
                                }
                                break;
                            }else if(double.TryParse(s, out y))
                            {
                                values.Add(new(x, y));
                            }
                            else
                            {
                                Log.Error($"Unable to parse looping double: \"{s}\" from \"{line}\"");
                            }
                            i++;
                        }
                    }
                }
                else if(double.TryParse(s, out y))
                {
                    values.Add(new(y));
                }
                else
                {
                    Log.Error($"Unable to parse normal double: \"{s}\" from \"{line}\"");
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
        private void OnAutoValues(AutoMessage[] values)
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
                    if (values[i].ContainsX())
                    {
                        points.Add(new(values[i].X, values[i].Y));
                    }
                    else
                    {
                        points.Add(new(SampleCount, values[i].Y));
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
            Com = new EmulatedCom(EmulatedCom.EmulationType.Emulated_Auto_Multi_Series_With_Common_X);



            List<ObservableCollection<ObservablePoint>> points = new();
            List<ISeries<ObservablePoint>> signalSeries = new();



            ClearPointsCommand = ReactiveCommand.Create(() => points.Clear(), null, RxApp.MainThreadScheduler);

            DSPLib.FFT fft = new();
            this.WhenActivated((CompositeDisposable registration) =>
            {
                Com.Open();
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
                    AutoMessage[] values = ParseAutoLine(s);
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
                    if(obj is AutoMessage[] autoValues)
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
