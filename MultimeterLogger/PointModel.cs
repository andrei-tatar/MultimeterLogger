using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MultimeterLogger
{
    public class PointModel : BaseModel
    {
        public double X { get; private set; }
        public double Y { get; private set; }

        public PointModel(double x, double y)
        {
            X = x;
            Y = y;
        }
    }
}
