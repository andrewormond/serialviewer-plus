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

        public double sqrWave(double frequency, double time) => (Math.Sin(2*Math.PI*frequency*time) >= 0) ? 1 : 0;


        public EmulatedCom()
        {
            Random r = new Random();
            Observable.Timer(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(15))
                      .ObserveOn(RxApp.TaskpoolScheduler)
                      .TimeInterval()
                      .Subscribe(iv =>
                      {
                          t += iv.Interval.TotalSeconds;

                          double y = sqrWave(1, t) + sqrWave(25, t) + Math.Sin(2*Math.PI*t*15);
                          incomingBuffer.Post($"{t}, {y}\r\n");
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
