using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Windows;

namespace MultimeterLogger
{
    public class MeasurementDataReceiverModel : BaseModel, IDisposable
    {
        private readonly Subject<Measurement> _received;
        private readonly SerialPort _serialPort;
        private string _port;

        public string Port
        {
            get { return _port; }
            set
            {
                _port = value;
                OnPropertyChanged();

                PortChanged();
            }
        }

        public IEnumerable<string> AvailablePorts { get { return SerialPort.GetPortNames(); } }

        public IObservable<Measurement> Received { get { return _received; } }

        public MeasurementDataReceiverModel()
        {
            _serialPort = new SerialPort {BaudRate = 2400, DataBits = 8, StopBits = StopBits.One, Parity = Parity.None};
            _received = new Subject<Measurement>();

            var ctx = new ReceiveContext();

            //ProcessReceivedData(ctx);

            Observable.FromEvent<SerialDataReceivedEventHandler, SerialDataReceivedEventArgs>(
                action =>
                {
                    SerialDataReceivedEventHandler handler = (sender, args) => action(args);
                    return handler;
                }, handler => _serialPort.DataReceived += handler, handler => _serialPort.DataReceived -= handler)
                      .Subscribe(args => ProcessReceivedData(ctx));
        }

        private class ReceiveContext
        {
            public readonly byte[] RxBuffer = new byte[1024];
            public readonly byte[] SegmentData = new byte[4];
            public int LastCategory, Steps;
            public Multiple SelectedMultiple = Multiple.None;
            public MeasurementUnit? Unit;

            private static readonly Dictionary<int, double> SegmentToDigit =
                new Dictionary<int, double>
                    {
                        {0x00, double.PositiveInfinity},
                        {0x7D, 0},
                        {0x05, 1},
                        {0x5B, 2},
                        {0x1F, 3},
                        {0x27, 4},
                        {0x3E, 5},
                        {0x7E, 6},
                        {0x15, 7},
                        {0x7F, 8},
                        {0x3F, 9},
                        {0x68, double.PositiveInfinity},
                    };

            public enum Multiple
            {
                None = 0,
                Mili = -3,
                Micro = -6,
                Nano = -9,
                Kilo = 3,
                Mega = 6
            }

            public double GetValue()
            {
                var value = SegmentToDigit[SegmentData[0] & 0x7F] * 1000 +
                            SegmentToDigit[SegmentData[1] & 0x7F] * 100 +
                            SegmentToDigit[SegmentData[2] & 0x7F] * 10 +
                            SegmentToDigit[SegmentData[3] & 0x7F];

                if ((SegmentData[3] & 0x80) != 0) value /= 10.0;
                if ((SegmentData[2] & 0x80) != 0) value /= 100.0;
                if ((SegmentData[1] & 0x80) != 0) value /= 1000.0;
                if ((SegmentData[0] & 0x80) != 0) value = -value;

                value *= Math.Pow(10, (int)SelectedMultiple);

                return value;
            }
        }

        private DateTime? _startRecordingTime;

        private void ProcessReceivedData(ReceiveContext ctx)
        {
            int rxData;
            do
            {
                rxData = _serialPort.Read(ctx.RxBuffer, 0, ctx.RxBuffer.Length);

                //var aux = new byte[]
                //              {
                //                  0x17, 0x2F, 0x3D, 0x47, 0x5D, 0x67, 0x7E, 0x8F, 0x9E, 0xA0, 0xB8, 0xC0, 0xD4, 0xE8
                //                  //0x11, 0x27, 0x3D, 0x47, 0x5D, 0x67, 0x7D, 0x8F, 0x9D, 0xA0, 0xB0, 0xC0, 0xD2, 0xE8 //0.000 Hz
                //                  //0x11, 0x27, 0x3D, 0x47, 0x5D, 0x67, 0x7D, 0x8F, 0x9D, 0xA0, 0xB4, 0xC0, 0xD0, 0xE8 //0.000 %		
                //                  //0x17, 0x2F, 0x3D, 0x47, 0x5D, 0x67, 0x7D, 0x8A, 0x97, 0xA0, 0xB8, 0xC0, 0xD4, 0xE8 //-000.4 mV
                //                  //0x13, 0x20, 0x35, 0x47, 0x5D, 0x69, 0x75, 0x85, 0x9B, 0xA4, 0xB0, 0xC9, 0xD0, 0xE8 //10.72 nF
                //                  //0x11, 0x20, 0x30, 0x4F, 0x5D, 0x66, 0x78, 0x80, 0x90, 0xA1, 0xB0, 0xC0, 0xD4, 0xE8 //.0L V Diode
                //              };
                //Array.Copy(aux, ctx.RxBuffer, aux.Length);
                //rxData = aux.Length;

                for (var i = 0; i < rxData; i++)
                {
                    var cByte = ctx.RxBuffer[i];
                    var cat = cByte & 0xF0;
                    var data = (byte)(cByte & 0x0F);

                    if (cat == 0x10)
                    {
                        //do a reset
                        ctx.Steps = 0;
                        ctx.SelectedMultiple = ReceiveContext.Multiple.None;
                        ctx.Unit = null;
                    }
                    else
                        if (cat - ctx.LastCategory != 0x10)
                            continue;

                    ctx.Steps++;

                    switch (cat)
                    {
                        case 0x10:
                            break;
                        case 0x20: ctx.SegmentData[0] = (byte)(data << 4); break;
                        case 0x30: ctx.SegmentData[0] |= data; break;
                        case 0x40: ctx.SegmentData[1] = (byte)(data << 4); break;
                        case 0x50: ctx.SegmentData[1] |= data; break;
                        case 0x60: ctx.SegmentData[2] = (byte)(data << 4); break;
                        case 0x70: ctx.SegmentData[2] |= data; break;
                        case 0x80: ctx.SegmentData[3] = (byte)(data << 4); break;
                        case 0x90: ctx.SegmentData[3] |= data; break;
                        case 0xA0:
                            if ((data & 0x08) != 0)
                                ctx.SelectedMultiple = ReceiveContext.Multiple.Micro;
                            if ((data & 0x04) != 0)
                                ctx.SelectedMultiple = ReceiveContext.Multiple.Nano;
                            if ((data & 0x02) != 0)
                                ctx.SelectedMultiple = ReceiveContext.Multiple.Kilo;
                            break;
                        case 0xB0:
                            if ((data & 0x08) != 0)
                                ctx.SelectedMultiple = ReceiveContext.Multiple.Mili;
                            if ((data & 0x04) != 0)
                                ctx.Unit = MeasurementUnit.Percent;
                            if ((data & 0x02) != 0)
                                ctx.SelectedMultiple = ReceiveContext.Multiple.Mega;
                            break;
                        case 0xC0:
                            if ((data & 0x08) != 0)
                                ctx.Unit = MeasurementUnit.Farad;
                            if ((data & 0x04) != 0)
                                ctx.Unit = MeasurementUnit.Ohm;
                            break;
                        case 0xD0:
                            if ((data & 0x08) != 0)
                                ctx.Unit = MeasurementUnit.Ampere;
                            if ((data & 0x04) != 0)
                                ctx.Unit = MeasurementUnit.Volt;
                            if ((data & 0x02) != 0)
                                ctx.Unit = MeasurementUnit.Hertz;
                            break;
                        case 0xE0:
                            if (ctx.Steps != 14) break;
                            if (_startRecordingTime == null) _startRecordingTime = DateTime.Now;

                            var value = ctx.GetValue();
                            if (ctx.Unit == null) return;

                            _received.OnNext(new Measurement(_startRecordingTime.Value, DateTime.Now - _startRecordingTime.Value, value, ctx.Unit.Value));
                            break;
                    }

                    ctx.LastCategory = cat;
                }
            } while (rxData != 0);
        }

        private void PortChanged()
        {
            if (string.IsNullOrEmpty(Port)) return;

            try
            {
                if (_serialPort.IsOpen)
                    _serialPort.Close();
                _serialPort.PortName = Port;
                _serialPort.Open();
            }
            catch (Exception ex)
            {
                Port = string.Empty;
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void Dispose()
        {
            try
            {
                if (_serialPort.IsOpen)
                    _serialPort.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
