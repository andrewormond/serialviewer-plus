using ReactiveUI;
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
    public class EmulatedCom : ICom
    {
        private readonly CompositeDisposable registration = new();
        private readonly BufferBlock<string> incomingBuffer = new();

        private double t = 0;

        private const double SinMultiplier = 2 * Math.PI;

        public double sinWave(double frequency, double time) => Math.Sin(2 * Math.PI * frequency * time);
        public double sqrWave(double frequency, double time) => (sinWave(frequency,time) >= 0) ? 1 : -1;

        public double PollingFrequency { get; init; } = 200;
        public const double CutoffFrequency = 50;

        public EmulatedCom()
        {

            int div = 1;
            TimeSpan interval;
            while(PollingFrequency / div > CutoffFrequency)
            {
                div++;
            }
            interval = TimeSpan.FromSeconds(div / PollingFrequency);

            List<Double> frequencies = new();
            int nm1 = 1;
            int nm2 = 0;
            int n = 0;
            do
            {
                n = nm1 + nm2;
                nm2 = nm1;
                nm1 = n;
                if(n <= PollingFrequency/2)
                {
                    frequencies.Add(n);
                }
            }
            while (n <= PollingFrequency/4);

            Log.Debug($"To achieve a polling rate of {PollingFrequency:0.0} Hz, using {PollingFrequency / div:0.0}Hz ~= {1000.0*div/PollingFrequency:0.0}ms with a multiplier of {div}");
            Log.Information($"Frequencies: [{string.Join(", ", frequencies.Select(f => $"{f:0.0}"))}]");

            Observable.Timer(interval, interval, RxApp.TaskpoolScheduler)
                      .TimeInterval(RxApp.TaskpoolScheduler)
                      .Subscribe(iv =>
                      {
                          for (int i = 0; i < div; i++)
                          {
                              t += iv.Interval.TotalSeconds / div;

                              double y = frequencies.Select(f => sinWave(f, t)).Sum();
                              incomingBuffer.Post($"{t}, {y}\r\n");
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
