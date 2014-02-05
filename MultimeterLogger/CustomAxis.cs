using System.Linq;
using System.Collections.Generic;
using System;
using OxyPlot.Axes;

namespace MultimeterLogger
{
    public class CustomAxis : LinearAxis
    {
        private MeasurementUnit _unit;
        public new MeasurementUnit Unit
        {
            get { return _unit; }
            set
            {
                if (_unit == value) return;
                _unit = value;
                base.Unit = Measurement.ConvertUnit(value);
                OnAxisChanged(new AxisChangedEventArgs(AxisChangeTypes.Reset));
            }
        }

        public CustomAxis()
            : base(AxisPosition.Left)
        { }

        public override string FormatValue(double x)
        {
            return Measurement.GetFriendlyValue(x, _unit);
        }
    }
}