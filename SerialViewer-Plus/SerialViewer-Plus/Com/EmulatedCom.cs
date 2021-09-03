using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace SerialViewer_Plus.Com
{
    public class EmulatedCom : ReactiveObject, ICom
    {
        private readonly CompositeDisposable registration = new();
        private readonly BufferBlock<string> incomingBuffer = new();

        private double t = 0;

        public static double SinWave(double frequency, double time) => Math.Sin(2 * Math.PI * frequency * time);
        public static double SqrWave(double frequency, double time) => (SinWave(frequency, time) >= 0) ? 1 : -1;

        public double PollingFrequency { get; init; } = 200;
        public const double CutoffFrequency = 50;

        public enum EmulationType
        {
            Emulated_NO_DATA = 0,
            Emulated_Auto_Single_Series,
            Emulated_Auto_XY_Series,
            Emulated_Auto_Multi_series,
            Emulated_Auto_Multi_series_with_x,
            NumberOfEmulations
        }

        [Reactive] public EmulationType Emulation { get; set; }

        public delegate string SignalHandler(double time);

        private readonly Dictionary<EmulationType, SignalHandler> Handlers = new()
        {
            [EmulationType.Emulated_Auto_Single_Series] = (t) => $"{SqrWave(1, t):0.000000}",
            [EmulationType.Emulated_Auto_Multi_series] = (t) => $"{SqrWave(1, t):0.000000}, {SinWave(1, t):0.000000}",
            [EmulationType.Emulated_Auto_XY_Series] = (t) => $"({t},{SqrWave(1, t):0.000000})",
        };

        public EmulatedCom(EmulationType emulationType)
        {
            Emulation = emulationType;

            int div = 1;
            TimeSpan interval;
            while(PollingFrequency / div > CutoffFrequency)
            {
                div++;
            }
            interval = TimeSpan.FromSeconds(div / PollingFrequency);

            
            Log.Debug($"To achieve a polling rate of {PollingFrequency:0.0} Hz, using {PollingFrequency / div:0.0}Hz ~= {1000.0*div/PollingFrequency:0.0}ms with a multiplier of {div}");

            Observable.Timer(interval, interval, RxApp.TaskpoolScheduler)
                      .TimeInterval(RxApp.TaskpoolScheduler)
                      .Subscribe(iv =>
                      {
                          for (int i = 0; i < div; i++)
                          {
                              t += iv.Interval.TotalSeconds / div;

                              if (Handlers.ContainsKey(Emulation))
                              {
                                  incomingBuffer.Post(Handlers[Emulation]?.Invoke(t));
                              }
                          }
                      })
                      .DisposeWith(registration);

        }

        public IObservable<string> IncomingStream => incomingBuffer.AsObservable();

        public void Dispose() => registration.Dispose();

        public bool Post(char c)
        {
            throw new NotImplementedException();
        }
    }
}
