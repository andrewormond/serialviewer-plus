using DynamicData;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
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
using System.Linq;
using System.Numerics;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text.RegularExpressions;

namespace SerialViewer_Plus.ViewModels
{
    public class TerminalViewModel : ReactiveObject, IActivatableViewModel
    {
        public ObservableCollection<string> Logs = new();
        private static readonly Regex LineRegex = new("\\(?[\\+\\-]?\\d+\\.?\\d*\\)?", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly SKColor[] SeriesColors = new SKColor[]
        {
            SKColors.AliceBlue,
            SKColors.Crimson,
            SKColors.Green,
            SKColors.Magenta
        };

        private int graphUpdateCount = 0;
        private int SampleCount = 0;

        public ObservableCollection<int> FftSizeOptions { get; } = new()
        {
            16, 32, 64, 128, 256, 512, 1024
        };

        public TerminalViewModel()
        {
            FftSize = 256;
            BufferSize = 512;
            EnableFft = true;
            Com = new EmulatedCom(EmulatedCom.EmulationType.Emulated_Auto_Multi_Series_With_Common_X);

            ClearPointsCommand = ReactiveCommand.Create(() =>
            {
                Series.Select(s => s.Values).Cast<ObservableCollection<ObservablePoint>>().ToList().ForEach(ps => ps?.Clear());
                foreach (var ls in Series)
                {
                }
            }, null, RxApp.MainThreadScheduler);

            this.WhenActivated((CompositeDisposable registration) =>
            {
                Com.Open();
                Com.DisposeWith(registration);
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
                        foreach (var points in Series.Select(s => s.Values as ObservableCollection<ObservablePoint>).Where(ps => ps != null))
                        {
                            if (points.Count > bs)
                            {
                                points.RemoveMany(points.SkipLast(bs));
                            }
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
                    if (values.Length > 0)
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
                .Subscribe(obj =>
                {
                    if (obj is AutoMessage[] autoValues)
                    {
                        OnAutoValues(autoValues);
                    }

                    graphUpdateCount++;
                }).DisposeWith(registration);

                Com.IncomingStream
                    .Where(_ => !IsPaused && EnableFft)
                    .Sample(TimeSpan.FromSeconds(1.0))
                    .Merge(this.WhenAnyValue(vm => vm.FftSize).Select(_ => ""))
                    .ObserveOn(RxApp.TaskpoolScheduler)
                    .Subscribe(_ => UpdateFFTs())
                    .DisposeWith(registration);

                this.WhenAnyValue(vm => vm.FftCalculations)
                    .Where(calculations => calculations != null)
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(calculation =>
                    {
                        for (int i = 0; i < calculation.Length; i++)
                        {
                            if (i >= FftSeries.Count)
                            {
                                FftSeries.Add(new LineSeries<ObservablePoint>()
                                {
                                    LineSmoothness = 0,
                                    GeometryStroke = null,
                                    GeometrySize = 2,
                                    Values = new ObservableCollection<ObservablePoint>(),
                                });
                                Log.Information("Created a new line series for FFT");
                            }
                            if (FftSeries[i] is LineSeries<ObservablePoint> fs
                               && fs.Values is ObservableCollection<ObservablePoint> fPoints)
                            {
                                fPoints.Clear();
                                fPoints.AddRange(calculation[i]);
                            }
                        }
                    })
                    .DisposeWith(registration);
            });
        }

        public ViewModelActivator Activator { get; } = new();
        [Reactive] public int BufferSize { get; set; }
        public ReactiveCommand<Unit, Unit> ClearPointsCommand { get; init; }
        [Reactive] public BaseCom Com { get; set; }
        [Reactive] public bool EnableFft { get; set; }

        public ObservableCollection<LineSeries<ObservablePoint>> FftSeries { get; set; } = new();

        [Reactive] public int FftSize { get; set; }

        [Reactive] public double GraphUPS { get; set; }

        [Reactive] public bool IsPaused { get; set; }

        public ObservableCollection<ISeries> Series { get; } = new ObservableCollection<ISeries>();

        [Reactive] public int ViewPortSize { get; set; }

        [Reactive] private ObservablePoint[][] FftCalculations { get; set; }

        public static int FindNextPowerOf2(int n)
        {
            // initialize result by 1
            int k = 1;

            // double `k` and divide `n` in half till it becomes 0
            while (n > 0)
            {
                k <<= 1;    // double `k`
                n >>= 1;
            }
            return k;
        }

        public static int FindPreviousPowerOf2(int n) => FindNextPowerOf2(n) / 2;

        public static AutoMessage[] ParseAutoLine(string line)
        {
            double y;
            line = line.Trim();
            List<AutoMessage> values = new();
            string[] entries = line.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            for (int i = 0; i < entries.Length; i++)
            {
                string s = entries[i];
                if (s.StartsWith("("))
                {
                    if (double.TryParse(s[1..], out double x))
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
                            }
                            else if (double.TryParse(s, out y))
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
                else if (double.TryParse(s, out y))
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

        private void OnAutoValues(AutoMessage[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (i >= Series.Count)
                {
                    Series.Add(new LineSeries<ObservablePoint>()
                    {
                        LineSmoothness = 0,
                        Fill = null,
                        GeometryStroke = null,
                        GeometrySize = 0,
                        AnimationsSpeed = TimeSpan.Zero,
                        Values = new ObservableCollection<ObservablePoint>(),
                    });
                    Log.Information("Created a new Series for AutoValue: " + i);
                }
                if (Series[i] is LineSeries<ObservablePoint> ls && ls.Values is ObservableCollection<ObservablePoint> points)
                {
                    if (values[i].ContainsX())
                    {
                        points.Add(new(values[i].X, values[i].Y));
                    }
                    else
                    {
                        points.Add(new(SampleCount, values[i].Y));
                    }

                    if (points.Count > BufferSize)
                    {
                        points.RemoveMany(points.SkipLast(BufferSize));
                    }
                }
                else
                {
                    Log.Error($"Unknown series: {Series[i].GetType().FullName}");
                }
            }
            SampleCount++;
        }

        private void UpdateFFTs()
        {
            var seriesPoints = Series.Select(s => s as LineSeries<ObservablePoint>)
                                     .ToArray()
                                     .Where(ls => ls != null)
                                     .Select(ls => ls.Values as ObservableCollection<ObservablePoint>)
                                     .Where(ps => ps != null)
                                     .ToArray();
            FftCalculations = seriesPoints.Select(sPoints =>
            {
                if (sPoints.Count > 4)
                {
                    DSPLib.FFT fft = new();
                    uint sampleSize = sPoints.Count <= FftSize ? (uint)sPoints.Count : (uint)FftSize;

                    fft.Initialize(sampleSize, (uint)Math.Max(0, (FftSize - sPoints.Count)));

                    var sampleTime = (sPoints[^1].X - sPoints[0].X) ?? 0.01;
                    sampleTime /= sPoints.Count;
                    var parray = sPoints.Select(op => op.Y ?? 0.0).TakeLast((int)sampleSize - 1).ToArray();
                    Complex[] cSpectrum = fft.Execute(parray);
                    double[] lmSpectrum = DSPLib.DSP.ConvertComplex.ToMagnitude(cSpectrum);
                    double[] freqSpan = fft.FrequencySpan(1.0 / sampleTime);

                    return freqSpan.Zip(lmSpectrum, (f, l) => new ObservablePoint(f, l)).ToArray();
                }
                else
                {
                    return Array.Empty<ObservablePoint>();
                }
            }).ToArray();
        }
    }
}