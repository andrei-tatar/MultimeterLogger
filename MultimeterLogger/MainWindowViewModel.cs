using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Wpf;
using AreaSeries = OxyPlot.Series.AreaSeries;
using LineSeries = OxyPlot.Series.LineSeries;
using TimeSpanAxis = OxyPlot.Axes.TimeSpanAxis;

namespace MultimeterLogger
{
    public class MainWindowViewModel : BaseModel
    {
        private readonly AreaSeries _dataSeries;

        private readonly LineSeries _averageSeries, _minSeries, _maxSeries;

        private readonly CustomAxis _yAxis;
        private readonly List<Measurement> _measurements;

        private double? _minX, _maxX, _minY, _maxY;
        private Measurement _lastMeasurement;

        public bool ShowAverage
        {
            get { return _averageSeries.IsVisible; }
            set
            {
                _averageSeries.IsVisible = value;
                OnPropertyChanged();
                Model.RefreshPlot(false);
            }
        }

        public bool ShowMinimum
        {
            get { return _minSeries.IsVisible; }
            set
            {
                _minSeries.IsVisible = value;
                OnPropertyChanged();
                Model.RefreshPlot(false);
            }
        }

        public bool ShowMaximum
        {
            get { return _maxSeries.IsVisible; }
            set
            {
                _maxSeries.IsVisible = value;
                OnPropertyChanged();
                Model.RefreshPlot(false);
            }
        }

        public PlotModel Model { get; private set; }
        public MeasurementDataReceiverModel DataReceiver { get; private set; }

        public ICommand SaveAsCommand { get; private set; }
        public ICommand ClearCommand { get; private set; }
        public Measurement LastMeasurement
        {
            get { return _lastMeasurement; }
            private set
            {
                _lastMeasurement = value;
                OnPropertyChanged();
            }
        }

        public MainWindowViewModel()
        {
            _measurements = new List<Measurement>();

            DataReceiver = new MeasurementDataReceiverModel();

            Model = new PlotModel();
            _yAxis = new CustomAxis
            {
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Solid
            };
            Model.PlotAreaBackground = OxyColors.White;
            Model.Axes.Add(_yAxis);
            Model.Axes.Add(new TimeSpanAxis(AxisPosition.Bottom, "Time")
            {
                AbsoluteMinimum = 0,
                Unit = "min:sec",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Solid
            });

            _dataSeries =
                new AreaSeries
                    {
                        CanTrackerInterpolatePoints = true,
                        LabelFormatString = "0.##",
                        TrackerFormatString = "{FriendlyValue}\nAt: {Time}"
                    };

            _averageSeries =
                new LineSeries
                    {
                        Color = OxyColors.Orange,
                        TrackerFormatString = "Average: {FriendlyValue}"
                    };

            _minSeries =
                new LineSeries
                    {
                        Color = OxyColors.Blue,
                        TrackerFormatString = "Min: {FriendlyValue}"
                    };

            _maxSeries =
                new LineSeries
                    {
                        Color = OxyColors.Red,
                        TrackerFormatString = "Max: {FriendlyValue}"
                    };

            Model.Series.Add(_dataSeries);
            Model.Series.Add(_averageSeries);
            Model.Series.Add(_minSeries);
            Model.Series.Add(_maxSeries);

            DataReceiver.Received += AddMeasurement;

            //var startTime = DateTime.Now;
            //var random = new Random();
            //var prev = 0.0000001;
            //var units = Enum.GetValues(typeof(MeasurementUnit)).OfType<MeasurementUnit>().Take(1).ToArray();
            //var l = 0;
            //Application.Current.MainWindow.Tag = new System.Threading.Timer(state =>
            //{
            //    var newPoint = new Measurement(startTime, TimeSpan.FromSeconds(l++), prev + (random.NextDouble() - 0.5) * 0.1 * prev, units[(l / units.Length) % units.Length]);
            //    prev = newPoint.Value;
            //    AddMeasurement(newPoint);

            //}, null, TimeSpan.Zero, TimeSpan.FromSeconds(0.5));

            SaveAsCommand = new DelegateCommand<object>(
                o =>
                {
                    var sfd = new SaveFileDialog { Filter = "Vector drawing|*.svg|Image file|*.png|CSV File|*.csv" };
                    if (sfd.ShowDialog() != true) return;

                    switch (sfd.FilterIndex)
                    {
                        case 1:
                            var svg = Model.ToSvg(Model.Width, Model.Height, true);
                            File.WriteAllText(sfd.FileName, svg);
                            break;

                        case 2:
                            var canvas = new Canvas { Width = Model.Width, Height = Model.Height };
                            Model.Render(new ShapesRenderContext(canvas), Model.Width, Model.Height);

                            canvas.Measure(new Size(Model.Width, Model.Height));
                            canvas.Arrange(new Rect(0, 0, Model.Width, Model.Height));

                            var rtb = new RenderTargetBitmap((int)Model.Width, (int)Model.Height, 96d, 96d, System.Windows.Media.PixelFormats.Default);
                            rtb.Render(canvas);

                            var pngEncoder = new PngBitmapEncoder();
                            pngEncoder.Frames.Add(BitmapFrame.Create(rtb));
                            using (var fs = File.OpenWrite(sfd.FileName))
                                pngEncoder.Save(fs);
                            break;

                        case 3:
                            File.WriteAllLines(sfd.FileName, _measurements.Select(m => m.AbsoluteTime + "," + m.Value + "," + Measurement.ConvertUnit(m.Unit) + "," + m.FriendlyValue));
                            break;
                    }
                });

            ClearCommand = new DelegateCommand<string>(s =>
            {
                Clear(bool.Parse(s)); Model.RefreshPlot(true);
                LastMeasurement = null;
            });
        }

        public void Clear(bool onlyUi)
        {
            if (!onlyUi)
                _measurements.Clear();

            _dataSeries.Points.Clear();
            _dataSeries.Points2.Clear();
            _minX = null;
            _maxX = null;
            _minY = null;
            _maxY = null;
        }

        private void AddMeasurement(Measurement measurement)
        {
            LastMeasurement = measurement;
            if (double.IsInfinity(measurement.Value))
                return;

            if (_yAxis.Unit != measurement.Unit)
            {
                Clear(true);
                _yAxis.Unit = measurement.Unit;
            }

            _measurements.Add(measurement);

            _dataSeries.Points.Add(measurement);
            _dataSeries.Points2.Clear();

            if (_minX == null || measurement.X < _minX) _minX = measurement.X;
            if (_maxX == null || measurement.X > _maxX) _maxX = measurement.X;
            if (_minY == null || measurement.Y < _minY) _minY = measurement.Y;
            if (_maxY == null || measurement.Y > _maxY) _maxY = measurement.Y;

            _dataSeries.Points2.Add(new DataPoint(_minX.Value, _minY.Value));
            _dataSeries.Points2.Add(new DataPoint(_maxX.Value, _minY.Value));

            var average = _dataSeries.Points.Average(p => p.Y);
            _averageSeries.Points.Clear();
            _averageSeries.Points.Add(new Measurement(measurement.StartTime, TimeSpanAxis.ToTimeSpan(_minX.Value), average, measurement.Unit));
            _averageSeries.Points.Add(new Measurement(measurement.StartTime, measurement.Time, average, measurement.Unit));

            _minSeries.Points.Clear();
            _minSeries.Points.Add(new Measurement(measurement.StartTime, TimeSpanAxis.ToTimeSpan(_minX.Value), _minY.Value, measurement.Unit));
            _minSeries.Points.Add(new Measurement(measurement.StartTime, measurement.Time, _minY.Value, measurement.Unit));

            _maxSeries.Points.Clear();
            _maxSeries.Points.Add(new Measurement(measurement.StartTime, TimeSpanAxis.ToTimeSpan(_minX.Value), _maxY.Value, measurement.Unit));
            _maxSeries.Points.Add(new Measurement(measurement.StartTime, measurement.Time, _maxY.Value, measurement.Unit));

            Model.RefreshPlot(true);
        }
    }
}
