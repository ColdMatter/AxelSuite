using System;
using System.IO;
using System.Windows;
using System.Timers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Threading;

using NationalInstruments;
using NationalInstruments.DAQmx;
using UtilsNS;

namespace AxelHMemsNS
{     
    public class AxelMems
    {
        
        private Stopwatch sw = null;
        public bool AdjustTimelineToStopwatch = false;  // false - use the set time interval between points
                                                        // true - adjust the time interval to stopwatch markers
        public readonly double[] FixConvRate = { 102400, 51200, 34133, 25600, 20480, 17067, 14629, 12800, 11378,
            10240, 9309, 8533, 7314, 6400, 5689, 5120, 4655, 4267, 3657, 3200, 2844, 2560, 2327, 2133, 1829,
            1600, 1422, 1280, 1164, 1067, 914, 800, 711, 640, 582, 533, 457, 400, 356, 320, 291, 267 }; // [Hz]

        public enum TimingModes { byNone, byADCtimer, byStopwatch, byBoth };
        public TimingModes TimingMode = TimingModes.byNone;

//        private const string NI9251 = "cDAQ1Mod1_1"; // ADC24 box
//        private readonly string physicalChannel0 = NI9251+"/ai0"; 
//        private readonly string physicalChannel1 = "/ai1";
        private const string PXIe = "Dev4";         // in PXIe
        private readonly string physicalChannel2 = PXIe + "/ai1";

        public Dictionary<string, string> hw = new Dictionary<string, string>();

        public int nSamples { get; private set; }
        public double sampleRate { get; private set; }
        public int Timeout = -1; // [sec] ; -1 - no timeout
        public List<double> rawData = null;
        
        private Task voltageInputTask = null;
        private AnalogMultiChannelReader VIReader = null;
        private AIChannel axelAIChannel;

        private AnalogMultiChannelReader analogInReader = null;
        private Task myTask;
        private Task runningTask;
        private AsyncCallback analogCallback;
        private AnalogWaveform<double>[] waveform;
        DispatcherTimer dTimer;

        public AxelMems(string hwFile = "")
        {
            sw = new Stopwatch();
            activeChannel = 0;
            sampleRate = 2133;
            nSamples = 1500;
            dTimer = new DispatcherTimer();
            dTimer.Tick += new EventHandler(dTimer_Tick);
            dTimer.Interval = new TimeSpan(10*10000); // 10ms

            // default hardware
            hw["device"] = "cDAQ1Mod1_1";
            hw["channel1"] = "/ai0";
            hw["channel2"] = "/ai1";
            hw["min"] = "-3.5";
            hw["max"] = "3.5";
            string fn = Utils.configPath + hwFile + ".hw";
            if (File.Exists(fn)) hw = Utils.readDict(fn);
            Reset();
        }

        public void StartStopwatch()
        {
            if (Utils.isNull(sw)) sw = new Stopwatch();
            sw.Restart();
        }
        public void SetStopwatch(Stopwatch ext_sw)
        {
            if(!Utils.isNull(ext_sw)) sw = ext_sw;
        }

        public double TimeElapsed() // [sec]
        {
            if(!sw.IsRunning) return double.NaN;
            if (Stopwatch.IsHighResolution) return sw.ElapsedTicks / Stopwatch.Frequency;
            else return sw.ElapsedMilliseconds/1000;
        }

        private bool _running = false;
        public bool running // wait for running to end in order to read rawData
        {
            get { return _running; }
        }

        public int activeChannel { get; set; } // 0,1 or 2 for both; 3 for chn.1 of PXIe

        public double RealConvRate(double wantedCR)
        {   
            int found = -1;
            if (wantedCR > FixConvRate[0]) found = 0;
            int len = FixConvRate.Length;
            if (wantedCR <= FixConvRate[len - 1]) found = len - 1;
            if (found == -1)
                for (int i = 0; i < len - 1; i++)
                {
                    if ((FixConvRate[i] >= wantedCR) && (wantedCR > FixConvRate[i + 1]))
                    {
                        found = i; break;
                    }
                }
            RealSamplingEvent(FixConvRate[found]); // let the host know the last one
            return FixConvRate[found];
        }

        public delegate void RealSamplingHandler(double realSampling);
        public event RealSamplingHandler OnRealSampling;

        protected void RealSamplingEvent(double realSampling) // 
        {
            if (OnRealSampling != null) OnRealSampling(realSampling);
        }

        #region async aqcuisition (working) 
        public double[,] readBurst(int nPoints) // synchro read
        {   
            int np = nSamples;
            if (nPoints > 0) np = nPoints;
            double[,] aiData = new double[1, np];

            voltageInputTask.Control(TaskAction.Abort); // reset the device
            voltageInputTask.Start();
            aiData = VIReader.ReadMultiSample(np);
            voltageInputTask.Stop();
            return aiData;
        }

        public void configureVITask(string physicalChn, int numbSamples, double samplingRate) // obsolete, but good to have as how to configure acquisition task
        {
            if(Utils.isNull(voltageInputTask)) voltageInputTask = new Task();

            if(Utils.isNull(axelAIChannel)) axelAIChannel = voltageInputTask.AIChannels.CreateVoltageChannel(physicalChn, "", AITerminalConfiguration.Differential,
                (double)-3.5, (double)3.5, AIVoltageUnits.Volts);

            voltageInputTask.Timing.ConfigureSampleClock("", sampleRate, SampleClockActiveEdge.Rising, SampleQuantityMode.FiniteSamples);
            voltageInputTask.Timing.SamplesPerChannel = numbSamples;
            voltageInputTask.Stream.Timeout = Timeout;
            voltageInputTask.Control(TaskAction.Commit);

            if(Utils.isNull(VIReader)) VIReader = new AnalogMultiChannelReader(voltageInputTask.Stream);
        }
        #endregion

        public bool isDevicePlugged()
        {
            if (hw.ContainsKey("device")) return isDevicePresent(hw["device"]);
            else return false;
        }

        private bool isDevicePresent(string dev)
        {
            bool rslt = false;
            foreach (string dv in DaqSystem.Local.Devices)
            {
                rslt |= dev.Equals(dv);
            }
            return rslt;
        }

        public void Reset() // for all purposes
        {
            if (!Utils.isNull(axelAIChannel)) axelAIChannel.Dispose();
            if (!Utils.isNull(VIReader)) VIReader = null;
            if (!Utils.isNull(voltageInputTask)) voltageInputTask.Dispose();

            runningTask = null;
            if (!Utils.isNull(myTask)) myTask.Dispose();
            //if (sw.IsRunning) sw.Reset();

            if (((activeChannel == 0) || (activeChannel == 2)) && isDevicePresent(hw["device"]))
            {
                Device dev1 = DaqSystem.Local.LoadDevice(hw["device"]);
                dev1.Reset();
            }

            if ((activeChannel == 3) && isDevicePresent(PXIe))
            {
                Device dev = DaqSystem.Local.LoadDevice(PXIe);
                dev.Reset();
            }
            _running = false;          
        }

        #region sync (separate thread) Aqcuisition 
        public delegate void AcquireHandler(List<Point> data, out bool next);
        public event AcquireHandler OnAcquire;

        protected void AcquireEvent(List<Point> data, out bool next)
        {
            next = false;
            if (OnAcquire != null) OnAcquire(data, out next);
        }

        public void StartAcquisition(int samplesPerChannel, double samplingRate) // acquisition
        {
            try
            {
                Reset();
                if (Utils.isNull(runningTask))
                {
                    // Create a new task
                    myTask = new Task();

                    // Create a virtual channel
                    if ((activeChannel == 0) || (activeChannel == 2))
                        myTask.AIChannels.CreateVoltageChannel(hw["device"] + hw["channel1"], "", AITerminalConfiguration.Differential,
                                                                Convert.ToDouble(hw["min"]), Convert.ToDouble(hw["max"]), AIVoltageUnits.Volts);
                    if ((activeChannel == 1) || (activeChannel == 2))
                        myTask.AIChannels.CreateVoltageChannel(hw["device"] + hw["channel2"], "", AITerminalConfiguration.Differential, 
                                                                Convert.ToDouble(hw["min"]), Convert.ToDouble(hw["max"]), AIVoltageUnits.Volts); 
                    if (activeChannel == 3)
                    {
                        myTask.AIChannels.CreateVoltageChannel(physicalChannel2, "", AITerminalConfiguration.Differential, 
                                                                Convert.ToDouble(hw["min"]), Convert.ToDouble(hw["max"]), AIVoltageUnits.Volts);
                    }
                }
                // Configure the timing parameters
                myTask.Stop();
                myTask.Timing.ConfigureSampleClock("", samplingRate, SampleClockActiveEdge.Rising, SampleQuantityMode.ContinuousSamples, samplesPerChannel);
                myTask.Timing.SamplesPerChannel = samplesPerChannel;
                myTask.Stream.Timeout = Timeout;
                myTask.Control(TaskAction.Commit);
                // Verify the Task   
                myTask.Control(TaskAction.Verify);

                if (runningTask == null)
                {
                    runningTask = myTask;
                    analogInReader = new AnalogMultiChannelReader(myTask.Stream);
                    analogCallback = new AsyncCallback(AnalogInCallback);

                    // Use SynchronizeCallbacks to specify that the object 
                    // marshals callbacks across threads appropriately.
                    analogInReader.SynchronizeCallbacks = true;
                }
                nSamples = samplesPerChannel;
                sampleRate = RealConvRate(samplingRate);
 
                // sw must be started manually from StartTime
                if (!sw.IsRunning) throw new Exception("The stopwatch has not been started");
                lastTime = 0.0;
                lastCount = 0;
                
                _running = true;
                analogInReader.BeginReadWaveform(samplesPerChannel, analogCallback, myTask);
            }
            catch (DaqException exception)
            {
                // Display Errors
                MessageBox.Show(exception.Message);
                Reset();
            }
        }

        List<Point> dataBuffer = new List<Point>();
        private void dTimer_Tick(object sender, EventArgs e)
        {
            dTimer.Stop();
            bool next;
            AcquireEvent(dataBuffer, out next); // when 2 channels data list has them both: odd - chn0; even - chn1
            _running = next;
        }

        double lastTime;
        long lastCount;
        private void AnalogInCallback(IAsyncResult ar)
        {
            if (!running) return;
            try
            {
                if (!Utils.isNull(runningTask) && runningTask.Equals(ar.AsyncState))
                {
                    // Read the available data from the channels
                    waveform = analogInReader.EndReadWaveform(ar);

                    const int actChn = 0; // for single channel (any)
                    if((activeChannel == 2) && (waveform.Length < 2)) throw new Exception("Set for two channels but only one appears!");                   
                    List<Point> data = new List<Point>();
                    double ts, prd;
                    switch (TimingMode)
                    {
                        case (TimingModes.byNone): // time markers (x values) from counter (0, 1, 2, ....)
                            for (int sample = 0; sample < waveform[actChn].Samples.Count; sample++)
                            {
                                data.Add(new Point(sample + lastCount, waveform[actChn].Samples[sample].Value));
                                if(activeChannel == 2)
                                    data.Add(new Point(sample + lastCount, waveform[actChn+1].Samples[sample].Value));
                            }
                            lastCount += waveform[actChn].Samples.Count; // total count marker
                            break;
                       case (TimingModes.byStopwatch): // time markers from Stopwatch, samplePer -> (time makers diff) / nSamples (recommended)
                            ts = sw.ElapsedMilliseconds/1000.0;
                            prd = (ts - lastTime) / waveform[actChn].Samples.Count;
                            for (int sample = 0; sample < waveform[actChn].Samples.Count; sample++)
                            {
                                data.Add(new Point(lastTime + sample * prd, waveform[actChn].Samples[sample].Value));
                                if(activeChannel == 2)
                                    data.Add(new Point(lastTime + sample * prd, waveform[actChn+1].Samples[sample].Value));
                            }
                            lastTime = ts;
                            break;
                        case (TimingModes.byADCtimer): // samplePer -> 1 / , time markers calculated from samplePer and nSamples 
                            ts = lastTime;
                            for (int sample = 0; sample < waveform[actChn].Samples.Count; sample++)
                            {   
                                lastTime = ts + sample / sampleRate;
                                data.Add(new Point(lastTime, waveform[actChn].Samples[sample].Value));
                                if(activeChannel == 2)
                                    data.Add(new Point(lastTime, waveform[actChn+1].Samples[sample].Value));
                            }
                            break;
                        case (TimingModes.byBoth): // time markers from Stopwatch, sampleRate from ADC setting
                            for (int sample = 0; sample < waveform[activeChannel].Samples.Count; sample++)
                            {
                                data.Add(new Point(lastTime + sample / sampleRate, waveform[actChn].Samples[sample].Value));
                                if(activeChannel == 2)
                                    data.Add(new Point(lastTime + sample / sampleRate, waveform[actChn+1].Samples[sample].Value));
                            }
                            lastTime = sw.ElapsedMilliseconds/1000.0;
                            break;
                    }
                    dataBuffer = new List<Point>(data);
                    if (!dTimer.IsEnabled) dTimer.Start();
                    else Console.WriteLine("Timing problem: skip a buffer");
                    analogInReader.BeginMemoryOptimizedReadWaveform(nSamples, analogCallback, myTask, waveform);
                }
            }
            catch (DaqException exception)
            {
                // Display Errors
                MessageBox.Show(exception.Message);
                Reset();
            }
        }

        public void StopAqcuisition()
        {
            Reset();
        }
        #endregion
    }
    public class AxelMemsTemperature
    { 
        public Dictionary<string, string> hw = new Dictionary<string, string>();
        private Task tmpTask = null;
        public AxelMemsTemperature(string hwFile = "")
        {
            // default hardware
            hw["device"] = "Dev2";
            hw["channel1"] = "/ai1";
            hw["channel2"] = "/ai2"; 
            hw["min"] = "-3.5";
            hw["max"] = "3.5";
            string fn = Utils.configPath + hwFile + ".hw";
            if (File.Exists(fn)) hw = Utils.readDict(fn);
        }

        public double [] TakeTheTemperature()
        {
            double[] rslt = null;
            try 
            {
                //Create a new task
                using (tmpTask = new Task())
                {
                    //Create a virtual channel
                    tmpTask.AIChannels.CreateVoltageChannel(hw["device"]+hw["channel1"],"",AITerminalConfiguration.Differential,
                                                        Convert.ToDouble(hw["min"]), Convert.ToDouble(hw["max"]),AIVoltageUnits.Volts);
                    tmpTask.AIChannels.CreateVoltageChannel(hw["device"] + hw["channel2"], "", AITerminalConfiguration.Differential,
                                                        Convert.ToDouble(hw["min"]), Convert.ToDouble(hw["max"]), AIVoltageUnits.Volts);  

                    AnalogMultiChannelReader reader = new AnalogMultiChannelReader(tmpTask.Stream);
                
                    //Verify the Task
                    tmpTask.Control(TaskAction.Verify);
                                            
                    //Plot Multiple Channels to the table
                    rslt = reader.ReadSingleSample(); 
                }
            }
            catch (DaqException exception)
            {
                MessageBox.Show(exception.Message);
            }
            finally 
            {
                    
            }
            return rslt;
        }
    }
/*
        #region flag (running) synchronized aqcuisition -> OBSOLETE !!!
        public void readInVoltages()
        {
            //int nSamplesOut;
            _running = true;
            //voltageInputTask.Start();
            //aiData =  VIReader.ReadMultiSample(nSamples);           
            //VIReader.MemoryOptimizedReadMultiSample(nSamples, ref aiData, out nSamplesOut);
            VIReader.BeginReadMultiSample(nSamples, ReadComplete, null);
            //if (!nSamplesOut.Equals(nSamples)) Utils.errorMessage("Less than required number of points in aquisition");
            //voltageInputTask.Stop();
            //return aiData;
        }

        private void ReadComplete(IAsyncResult result)
        {
            // Because the UI thread calls BeginReadMultiSample,
            // this callback will execute in the UI thread.
            double[,] dt = VIReader.EndReadMultiSample(result); // waits the end of acquisition
            rawData = new List<double>();
            rawData.Clear();
            int nSamples = dt.GetLength(1);
            for (int i = 0; i < nSamples; i++)
            {
                rawData.Add(-dt[0, i]); // the minus is to match accelertion by sign, same effect as switching the diff. input over
            }
            _running = false;
        }
        #endregion
*/    
}