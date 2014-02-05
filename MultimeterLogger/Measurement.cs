using System.Linq;
using System.Collections.Generic;
using System;
using OxyPlot;
using OxyPlot.Axes;

namespace MultimeterLogger
{
    public enum MeasurementUnit
    {
        Volt,
        Ampere,
        Farad,
        Ohm,
        Hertz,
        Percent
    }

    public class Measurement : IDataPoint
    {
        private readonly Lazy<string> _friendlyValue;

        public Measurement(DateTime startTime, TimeSpan difference, double value, MeasurementUnit unit)
        {
            Unit = unit;
            Value = value;

            X = TimeSpanAxis.ToDouble(difference);
            Y = Value;
            AbsoluteTime = startTime.Add(difference);
            Time = difference;
            StartTime = startTime;

            _friendlyValue = new Lazy<string>(() => GetFriendlyValue(Value, Unit));
        }

        public static string GetFriendlyValue(double value, MeasurementUnit? unit = null)
        {
            var auxValue = value;
            var power = 0;

            while (Math.Abs(auxValue) < 1 && power >= -12)
            {
                auxValue *= 1000;
                power -= 3;
            }
            while (Math.Abs(auxValue) >= 1000)
            {
                auxValue /= 1000;
                power += 3;
            }

            return (power < -12 ? "0" : auxValue.ToString("0.####") + " " + PowerToMultiple(power)) + (unit != null ? ConvertUnit(unit.Value) : string.Empty);
        }

        public static string ConvertUnit(MeasurementUnit unit)
        {
            switch (unit)
            {
                case MeasurementUnit.Ampere:
                    return "A";
                case MeasurementUnit.Farad:
                    return "F";
                case MeasurementUnit.Hertz:
                    return "Hz";
                case MeasurementUnit.Ohm:
                    return "Ω";
                case MeasurementUnit.Percent:
                    return "%";
                case MeasurementUnit.Volt:
                    return "V";
                default:
                    return string.Empty;
            }
        }

        public static string PowerToMultiple(int power)
        {
            switch (power)
            {
                case 9:
                    return "G";
                case 6:
                    return "M";
                case 3:
                    return "k";
                case 0:
                    return string.Empty;
                case -3:
                    return "m";
                case -6:
                    return "μ";
                case -9:
                    return "η";
                case -12:
                    return "ρ";
                default:
                    return "(Invalid:" + power + ")";
            }
        }

        public MeasurementUnit Unit { get; private set; }
        public double Value { get; private set; }
        public DateTime AbsoluteTime { get; private set; }
        public TimeSpan Time { get; private set; }
        public DateTime StartTime { get; private set; }

        public double X { get; set; }
        public double Y { get; set; }

        public string FriendlyValue { get { return _friendlyValue.Value; } }
    }
}