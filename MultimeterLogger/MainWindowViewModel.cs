using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

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
            : base(AxisPosition.Left, "Value")
        { }

        public override string FormatValue(double x)
        {
            return Measurement.GetFriendlyValue(x, _unit);
        }
    }

    public class MainWindowViewModel : BaseModel
    {
        private readonly List<Measurement> _measurements;

        public PlotModel Model { get; private set; }
        public MeasurementDataReceiverModel DataReceiver { get; private set; }

        public MainWindowViewModel()
        {
            _measurements = new List<Measurement>();

            DataReceiver = new MeasurementDataReceiverModel();

            Model = new PlotModel();
            var axis = new CustomAxis();
            Model.Axes.Add(axis);
            Model.Axes.Add(new TimeSpanAxis(AxisPosition.Bottom, "Time") { AbsoluteMinimum = 0, Unit = "sec" });

            var dataSeries =
                new AreaSeries
                    {
                        CanTrackerInterpolatePoints = true,
                        LabelFormatString = "0.##",
                        TrackerFormatString = "{FriendlyValue}\nAt: {Time}"
                    };

            var meanSeries =
                new LineSeries
                    {
                        TrackerFormatString = "Average: {FriendlyValue}"
                    };

            var startTime = DateTime.Now;

            Model.Series.Add(dataSeries);
            Model.Series.Add(meanSeries);

            DataReceiver.Received.Subscribe(measurement => AddMeasurement(measurement, dataSeries, meanSeries, axis));

            var random = new Random();
            var prev = 0.0000001;
            Observable.Interval(TimeSpan.FromSeconds(1)).ObserveOnDispatcher().Subscribe(
                l =>
                {
                    var newPoint = new Measurement(startTime, TimeSpan.FromSeconds(l), prev + (random.NextDouble() - 0.5) * 0.1 * prev, MeasurementUnit.Hertz); prev = newPoint.Value;
                    AddMeasurement(newPoint, dataSeries, meanSeries, axis);
                });
        }

        private void AddMeasurement(Measurement measurement, AreaSeries dataSeries, DataPointSeries meanSeries, CustomAxis axis)
        {
            axis.Unit = measurement.Unit;

            _measurements.Add(measurement);
            double minX = double.MaxValue, maxX = double.MinValue;
            _measurements.ForEach(m =>
            {
                minX = Math.Min(m.X, minX);
                maxX = Math.Max(m.X, maxX);
            });

            dataSeries.Points.Add(measurement);
            dataSeries.Points2.Clear();
            dataSeries.Points2.Add(new DataPoint(minX, 0));
            dataSeries.Points2.Add(new DataPoint(maxX, 0));

            var average = _measurements.Average(p => p.Value);
            meanSeries.Points.Clear();
            meanSeries.Points.Add(new Measurement(measurement.StartTime, TimeSpan.Zero, average, measurement.Unit));
            meanSeries.Points.Add(new Measurement(measurement.StartTime, measurement.Time, average, measurement.Unit));

            Model.RefreshPlot(true);
        }
    }
}
