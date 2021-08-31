using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SerialViewer_Plus.Com
{
    public interface ICom : IDisposable
    {
        public IObservable<string> IncomingStream { get; }

        public bool Post(char c);
    }

    public static class IComHelper
    {
        public static bool Post(this ICom com, string s)
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
