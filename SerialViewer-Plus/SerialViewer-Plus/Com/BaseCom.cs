using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Text;
using System.Threading.Tasks;

namespace SerialViewer_Plus.Com
{
    public abstract class BaseCom : ReactiveObject, IDisposable
    {
        protected readonly CompositeDisposable registration = new();
        public abstract IObservable<string> IncomingStream { get; }

        public abstract bool Post(char c);

        public abstract bool Open();

        public abstract bool Close();

        public virtual void Dispose() => registration.Dispose();

        [Reactive] public virtual bool IsOpen { get; set; }
    }

    public static class IComHelper
    {
        public static bool Post(this BaseCom com, string s)
        {
            foreach (char c in s)
            {
                if (!com.Post(c))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
