using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SerialViewer_Plus.Models
{
    public class AutoMessage
    {
        public double Y { get; init; }
        public double? X { get; init; }

        public bool ContainsX() => X.HasValue;

        public AutoMessage(double? x, double y)
        {
            Y = y;
            X = x;
        }

        public AutoMessage(double y) : this(null, y) { }

        public override string ToString()
        {
            if (ContainsX())
            {
                return $"({X},{Y})";
            }
            else
            {
                return $"{Y}";
            }
        }
    }
}
