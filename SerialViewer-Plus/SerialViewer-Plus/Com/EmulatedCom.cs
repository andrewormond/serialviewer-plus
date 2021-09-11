using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace SerialViewer_Plus.Com
{
    public class EmulatedCom : BaseCom
    {
        private readonly BufferBlock<string> incomingBuffer = new();


        public static double SinWave(double frequency, double time) => Math.Sin(2 * Math.PI * frequency * time);
        public static double SqrWave(double frequency, double time) => (SinWave(frequency, time) >= 0) ? 1 : -1;

        public double PollingFrequency { get; init; } = 120;

        public enum EmulationType
        {
            Emulated_NO_DATA = 0,
            Emulated_Auto_Single_Series,
            Emulated_Auto_XY_Series,
            Emulated_Auto_Multi_series,
            Emulated_Auto_Multi_Series_With_Common_X,
            Emulated_Auto_Mixed_Single_And_Pair, //This one doesn't make too much sense and probably should show a warning to the user
            NumberOfEmulations
        }

        [Reactive] public EmulationType Emulation { get; set; }

        public delegate string SignalHandler(double time);

        private readonly Dictionary<EmulationType, SignalHandler> Handlers = new()
        {
            [EmulationType.Emulated_Auto_Single_Series] = (t) => $"{SqrWave(1, t):0.000000}",
            [EmulationType.Emulated_Auto_Multi_series] = (t) => $"{SqrWave(1, t):0.000000}, {SinWave(1, t):0.000000}",
            [EmulationType.Emulated_Auto_XY_Series] = (t) => $"({t},{SinWave(1, t):0.000000})",
            [EmulationType.Emulated_Auto_Multi_Series_With_Common_X] = (t) => $"({t},{SinWave(10, t):0.000000}, {SqrWave(25,t):.00000}",
            [EmulationType.Emulated_Auto_Mixed_Single_And_Pair] = (t) => $"({t},{SinWave(1, t):0.000000}), {SqrWave(0.5,t):.00000}",
        };

        public EmulatedCom(EmulationType emulationType)
        {
            Emulation = emulationType;

            //int div = 1;
            //TimeSpan interval;
            //while(PollingFrequency / div > CutoffFrequency)
            //{
            //    div++;
            //}
            //interval = TimeSpan.FromSeconds(div / PollingFrequency);

            
            //Log.Debug($"To achieve a polling rate of {PollingFrequency:0.0} Hz, using {PollingFrequency / div:0.0}Hz ~= {1000.0*div/PollingFrequency:0.0}ms with a multiplier of {div}");

            //Observable.Timer(interval, interval, RxApp.TaskpoolScheduler)
            //          .TimeInterval(RxApp.TaskpoolScheduler)
            //          .Subscribe(iv =>
            //          {
            //              for (int i = 0; i < div; i++)
            //              {
            //                  t += iv.Interval.TotalSeconds / div;

            //                  if (Handlers.ContainsKey(Emulation))
            //                  {
            //                      incomingBuffer.Post(Handlers[Emulation]?.Invoke(t));
            //                  }
            //              }
            //          })
            //          .DisposeWith(registration);

        }

        public override IObservable<string> IncomingStream => incomingBuffer.AsObservable();


        public override void Dispose()
        {
            base.Dispose();
            Close();
        }

        public override bool Post(char c)
        {
            throw new NotImplementedException();
        }

        protected Thread signalThread = null;

        private void SignalLoop()
        {
            try
            {
                double t = 0;
                double pollingInterval = Math.Max(1000 / PollingFrequency, 0);
                pollingInterval -= 1;
                while (true)
                {
                    t += pollingInterval / 1000.0;

                    if (Handlers.ContainsKey(Emulation))
                    {
                        incomingBuffer.Post(Handlers[Emulation]?.Invoke(t));
                    }
                    Thread.Sleep((int)pollingInterval);
                }
            }
            catch (ThreadInterruptedException)
            {
                
            }
        }




        public override bool Open()
        {
            Close();
            signalThread = new Thread(SignalLoop);
            signalThread.Start();
            return true;
        }

        public override bool Close()
        {
            if(signalThread != null)
            {
                signalThread.Interrupt();
            }
            signalThread = null;

            IsOpen = false;
            return true;
        }
    }
}
