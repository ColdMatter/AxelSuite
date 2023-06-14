using System;
using System.IO;
using System.Windows;
using System.Timers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Threading;

using NationalInstruments;
using NationalInstruments.DAQmx;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UtilsNS;
using OptionsNS;
using System.Windows.Navigation;

namespace Axel_hub
{
    public static class MEMSmodel
    {
        /// <summary>
        /// NI9251 support fixed sampling freq listed here
        /// </summary>
        public readonly static double[] FixConvRate = { 102400, 51200, 34133, 25600, 20480, 17067, 14629, 12800, 11378,
            10240, 9309, 8533, 7314, 6400, 5689, 5120, 4655, 4267, 3657, 3200, 2844, 2560, 2327, 2133, 1829,
            1600, 1422, 1280, 1164, 1067, 914, 800, 711, 640, 582, 533, 457, 400, 356, 320, 291, 267 }; // [Hz]

        /// <summary>
        /// Find nearest up sampling freq
        /// </summary>
        /// <param name="wantedCR">Desired freq</param>
        /// <returns></returns>
        public static double RealConvRate(double wantedCR)
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
            return FixConvRate[found];
        }
    }

    /// <summary>
    /// Acceleration calibration with optional temperature compensation 
    /// particular to each MEMS device
    /// </summary>
    public struct accelCalibr
    {
        public string model;
        public string SN;
        public double rAccel; // in Omhs
        public double rTemper;
        public double cK0; // ug
        public double cK1; // mA/g
        public double[] pK0;
        public double[] pK1;

        public string IdString()
        {
            Dictionary<string, string> dt = new Dictionary<string, string>();
            dt["model"] = model; dt["SN"] = SN;
            return JsonConvert.SerializeObject(dt);
        }
        /// <summary>
        /// The actual calibration from [V] to [mg] with optional temperature compensation 
        /// </summary>
        /// <param name="accelV"></param>
        /// <param name="temperV"></param>
        /// <param name="tempComp"></param>
        /// <returns></returns>
        public double accelMg(double accelV, double temperV, bool tempComp = false) // in [V] ; out [mg]
        {
            double K0 = 0; double K1 = 0;
            if (tempComp)
            {
                if (!pK0.Length.Equals(pK1.Length)) throw new Exception("Wrong coeff arrays lenghts");
                double degC = ((temperV / rTemper) + 1E6) - 272.3;
                for (int i = 0; i < pK0.Length; i++)
                {
                    K0 += pK0[i] + Math.Pow(degC, i);
                    K1 += pK1[i] + Math.Pow(degC, i);
                }
            }
            else
            {
                K0 = cK0; K1 = cK1;
            }
            return K0 / 1000.0 +                  // bias in mg
                   ((accelV / rAccel) * 1000.0) * // mA
                   K1 * 1000.0;                   // mg
        }
        public double accelV(double accelMg)
        {
            return ((accelMg - cK0 / 1000) * rAccel) / (1e6 * cK1);
        }
    }

    
    public enum tracerStage { neutral, readAcq, startComb, endComb }
    public struct AcqTracer
    {
        public AcqTracer(bool isEnabled)
        {
            enabled = isEnabled;
            trouble = false;
            stopWatch = new Stopwatch();
            _stage = tracerStage.neutral;
            period = -1;
            fulfillment = -1;
            d1 = -1; d2 = -1;
        }
        Stopwatch stopWatch; long d1, d2;
        public bool trouble;
        tracerStage _stage;
        public tracerStage stage
        {
            get { return _stage; }
            set
            {
                if (!enabled) return;
                switch (value)
                {
                    case tracerStage.neutral:
                        _stage = value; trouble = false;
                        break;
                    case tracerStage.readAcq:
                        if ((_stage == tracerStage.neutral) || (_stage == tracerStage.endComb))
                        {
                            _stage = value; stopWatch.Restart(); trouble = false;
                        }
                        else trouble = true;
                        break;
                    case tracerStage.startComb:
                        if (_stage == tracerStage.readAcq)
                        {
                            _stage = value; d1 = stopWatch.ElapsedMilliseconds;
                        }
                        else trouble = true;
                        break;
                    case tracerStage.endComb:
                        if (_stage == tracerStage.startComb)
                        {
                            _stage = value; d2 = stopWatch.ElapsedMilliseconds;
                            if (stopWatch.IsRunning) fulfillment = d2 / (period * 1000.0);
                            else fulfillment = -1;
                            stopWatch.Stop();
                        }
                        else trouble = true;
                        break;
                }
            }
        }
        public bool enabled;
        public double period; // [s]

        public double fulfillment; // 0-1
        public string message()
        {
            if (trouble) return "Error status: MEMS timing overflow !";
            else return "Info: acq.cycle " + ((int)(fulfillment * 100)).ToString() + "% busy";
                //return "Info [ms]: p=" + ((int)(period * 1000.0)).ToString() + "; d1=" + d1.ToString() + "; d2=" + d2.ToString();
        }
    }

    /// <summary>
    /// The hardware abstraction for MEMS with ADC24 (NI9251) device
    /// </summary>
    public class AxelMems
    {
        /// <summary>
        /// false - use the set time interval between points
        /// true - adjust the time interval to stopwatch markers
        /// </summary>
        public bool AdjustTimelineToStopwatch = false;

        public enum TimingModes { byNone, byADCtimer, byStopwatch, byBoth };
        public TimingModes TimingMode = TimingModes.byNone;
        public bool intClock { get; private set; } 

        //        private const string NI9251 = "cDAQ1Mod1_1"; // ADC24 box
        //        private readonly string physicalChannel0 = NI9251+"/ai0"; 
        //        private readonly string physicalChannel1 = "/ai1";
        private const string PXIe = "Dev4";         // in PXIe
        private readonly string physicalChannel2 = PXIe + "/ai1";
        public accelCalibr memsX, memsY;

        public Dictionary<string, string> hw = new Dictionary<string, string>();

        public int nSamples { get; private set; }
        public double sampleRate { get; private set; }
        public int Timeout = -1; // [sec] ; -1 - no timeout
        public List<double> rawData = null;

        /// <summary>
        /// Inner makings of continious (no gaps) data acquisition 
        /// refer. NI9251 and related documentation
        /// </summary>
        private Task voltageInputTask = null;
        private AnalogMultiChannelReader VIReader = null;
        private AIChannel axelAIChannel;

        private AnalogMultiChannelReader analogInReader = null;
        private Task myTask;
        private Task runningTask;
        private AsyncCallback analogCallback;
        private AnalogWaveform<double>[] waveform;
        private DispatcherTimer dTimer; // delay timer
        public AcqTracer tracer;

        /// <summary>
        /// Class contructor
        /// </summary>
        /// <param name="hwFile">Hardware file (NI9251 settings)</param>
        /// <param name="memsFile">Mems calibration and temperature compensation</param>
        public AxelMems(string hwFile = "", string memsFile = "", bool _intClock = false) // memsFile - no ext
        {
            activeChannel = 0;
            sampleRate = 2133;
            nSamples = 1500;
            dTimer = new DispatcherTimer();
            dTimer.Tick += new EventHandler(dTimer_Tick);
            dTimer.Interval = new TimeSpan(100 * 10000); // 100 ms

            // default hardware
            hw["device"] = "cDAQ1Mod1_2";
            hw["channel1"] = "/ai0";
            hw["channel2"] = "/ai1";
            hw["min"] = "-3.5";
            hw["max"] = "3.5";
            string fn = Utils.configPath + hwFile + ".hw";
            if (File.Exists(fn)) hw = Utils.readDict(fn);

            string mFN = (memsFile.Equals("")) ? "mems" : memsFile;
            string FN = Utils.configPath + mFN + ".X";
            if (!File.Exists(FN)) throw new Exception("File " + FN + " not found.");
            string fileJson = File.ReadAllText(FN);
            memsX = JsonConvert.DeserializeObject<accelCalibr>(fileJson);
            FN = Utils.configPath + mFN + ".Y";
            if (!File.Exists(FN)) throw new Exception("File " + FN + " not found.");
            fileJson = File.ReadAllText(FN);
            memsY = JsonConvert.DeserializeObject<accelCalibr>(fileJson);
            Reset();
            tracer = new AcqTracer(true);
            intClock = _intClock;
        }

        private bool _running = false;
        public bool running // wait for running to end in order to read rawData
        {
            get { return _running; }
        }

        public int activeChannel { get; set; } // 0,1 or 2 for both; 3 for chn.1 of PXIe

        public delegate void RealSamplingHandler(double realSampling);
        public event RealSamplingHandler OnRealSampling;

        protected void RealSamplingEvent(double realSampling) // 
        {
            if (OnRealSampling != null) OnRealSampling(realSampling);
        }

        #region async aqcuisition (working) 
        /// <summary>
        /// Inner methods of continious (no gaps) data acquisition 
        /// refer. NI9251 and related documentation
        /// </summary>
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
            if (Utils.isNull(voltageInputTask)) voltageInputTask = new Task();
            if (Utils.isNull(axelAIChannel)) axelAIChannel = voltageInputTask.AIChannels.CreateVoltageChannel(physicalChn, "", AITerminalConfiguration.Differential,
                 (double)-3.5, (double)3.5, AIVoltageUnits.Volts);
            voltageInputTask.Timing.ConfigureSampleClock("", sampleRate, SampleClockActiveEdge.Rising, SampleQuantityMode.FiniteSamples);
            voltageInputTask.Timing.SamplesPerChannel = numbSamples;
            voltageInputTask.Stream.Timeout = Timeout;
            voltageInputTask.Control(TaskAction.Commit);

            if (Utils.isNull(VIReader)) VIReader = new AnalogMultiChannelReader(voltageInputTask.Stream);
        }
        #endregion

        /// <summary>
        /// Check for device presence
        /// </summary>
        /// <returns></returns>
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

        /// <summary>
        /// Reset before new series of measurements
        /// </summary>
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
            tracer.stage = tracerStage.neutral;
        }

        #region sync (separate thread) Aqcuisition 
        public delegate void AcquireHandler(List<Point> data, out bool next);
        public event AcquireHandler OnAcquire;

        protected void AcquireEvent(List<Point> data, out bool next)
        {
            next = false;
            if (OnAcquire != null) OnAcquire(data, out next);
        }
        FileLogger timeLog = new FileLogger();
        /// <summary>
        /// Set conditions for new data acquisition series
        /// </summary>
        /// <param name="samplesPerChannel"></param>
        /// <param name="samplingRate"></param>
        public void StartAcquisition(int samplesPerChannel, double samplingRate) // acquisition
        {
            try
            {
                Reset(); timeLog.Enabled = true;
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
                if (intClock)
                {
                    // Configure the timing parameters
                    myTask.Timing.ConfigureSampleClock("", samplingRate,
                        SampleClockActiveEdge.Rising, SampleQuantityMode.ContinuousSamples, samplesPerChannel * 10);

                    // Configure the Every N Samples Event
                    myTask.EveryNSamplesReadEventInterval = samplesPerChannel;
                    myTask.EveryNSamplesRead += new EveryNSamplesReadEventHandler(myTask_EveryNSamplesRead);
                }
                else
                {
                    myTask.Timing.ConfigureSampleClock("", samplingRate, SampleClockActiveEdge.Rising, SampleQuantityMode.ContinuousSamples, samplesPerChannel);
                    myTask.Timing.SamplesPerChannel = samplesPerChannel;                   
                }
                myTask.Stream.Timeout = Timeout;
                myTask.Control(TaskAction.Commit);
                // Verify the Task   
                myTask.Control(TaskAction.Verify);

                if (runningTask == null)
                {
                    runningTask = myTask;
                    analogInReader = new AnalogMultiChannelReader(myTask.Stream);
                    if (!intClock)
                        analogCallback = new AsyncCallback(AnalogInCallback);                   
                    analogInReader.SynchronizeCallbacks = true;
                }
                nSamples = samplesPerChannel;
                sampleRate = MEMSmodel.RealConvRate(samplingRate);
                tracer.period = samplesPerChannel / sampleRate;
                lastTime = 0.0;
                lastCount = 0;

                _running = true;
                if (intClock) runningTask.Start();
                else analogInReader.BeginReadWaveform(samplesPerChannel, analogCallback, myTask);
            }
            catch (DaqException exception)
            {
                // Display Errors
                MessageBox.Show(exception.Message);
                Reset();
            }
        }
        // internal sample clock
        void myTask_EveryNSamplesRead(object sender, EveryNSamplesReadEventArgs e)
        {
            try
            {
                tracer.stage = tracerStage.readAcq;
                // Read the available data from the channels
                waveform = analogInReader.ReadWaveform(nSamples);

                const int actChn = 0; // for single channel (any)
                List<Point> data = new List<Point>();
                double ts, prd;
                if (theTime.isTimeRunning && TimingMode == TimingModes.byStopwatch)
                {
                    ts = theTime.elapsedTime;
                    prd = (ts - lastTime) / waveform[actChn].Samples.Count;
                    for (int sample = 0; sample < waveform[actChn].Samples.Count; sample++)
                    {
                        data.Add(new Point(lastTime + sample * prd, waveform[actChn].Samples[sample].Value));
                        if (activeChannel == 2)
                            data.Add(new Point(lastTime + sample * prd, waveform[actChn + 1].Samples[sample].Value));
                    }
                    lastTime = ts;
                }
                else throw new Exception("Wrong timing");

                dataBuffer = new List<Point>(data);
                if (!dTimer.IsEnabled) dTimer.Start();
                else Utils.TimedMessageBox("Timing problem: skip a buffer of data");
                if (tracer.trouble)
                {
                    Utils.TimedMessageBox("Timing sequence problem");
                    tracer.trouble = false;
                }
            }
            catch (DaqException exception)
            {
                // Display Errors
                Utils.TimedMessageBox(exception.Message);
                runningTask = null;
                myTask.Dispose();
            }
        }

        List<Point> dataBuffer = new List<Point>();
        private void dTimer_Tick(object sender, EventArgs e) // delay timer
        {
            dTimer.Stop();
            bool next;
            tracer.stage = tracerStage.startComb;
            AcquireEvent(dataBuffer, out next); // when 2 channels data list has them both: odd - chn0; even - chn1
            tracer.stage = tracerStage.endComb;
            _running = next;
        }

        double lastTime; long lastCount;
        /// <summary>
        /// The smart part of how to avoid the gaps in data-acquisition
        /// and different way to stitch buffers together with flowless (or almost) time scale
        /// </summary>
        /// <param name="ar"></param>
        private void AnalogInCallback(IAsyncResult ar)
        {
            if (!running) return;
            try
            {
                if (!Utils.isNull(runningTask) && runningTask.Equals(ar.AsyncState))
                {
                    if (theTime.isTimeRunning) timeLog.log(theTime.elapsedTime.ToString());
                    tracer.stage = tracerStage.readAcq;
                    // Read the available data from the channels
                    waveform = analogInReader.EndReadWaveform(ar);

                    const int actChn = 0; // for single channel (any)
                    if ((activeChannel == 2) && (waveform.Length < 2)) throw new Exception("Set for two channels but only one appears!");
                    List<Point> data = new List<Point>();
                    double ts, prd;
                    switch (TimingMode)
                    {
                        case (TimingModes.byNone): // time markers (x values) from counter (0, 1, 2, ....)
                            for (int sample = 0; sample < waveform[actChn].Samples.Count; sample++)
                            {
                                data.Add(new Point(sample + lastCount, waveform[actChn].Samples[sample].Value));
                                if (activeChannel == 2)
                                    data.Add(new Point(sample + lastCount, waveform[actChn + 1].Samples[sample].Value));
                            }
                            lastCount += waveform[actChn].Samples.Count; // total count marker
                            break;
                        case (TimingModes.byStopwatch): // time markers from theTime, samplePer -> (time makers diff) / nSamples (recommended)
                            if (theTime.isTimeRunning)
                            {
                                ts = theTime.elapsedTime;
                                prd = (ts - lastTime) / waveform[actChn].Samples.Count;
                                for (int sample = 0; sample < waveform[actChn].Samples.Count; sample++)
                                {
                                    data.Add(new Point(lastTime + sample * prd, waveform[actChn].Samples[sample].Value));
                                    if (activeChannel == 2)
                                        data.Add(new Point(lastTime + sample * prd, waveform[actChn + 1].Samples[sample].Value));
                                }
                                lastTime = ts;
                            }
                            break;
                        case (TimingModes.byADCtimer): // samplePer -> 1 / , time markers calculated from samplePer and nSamples 
                            ts = lastTime;
                            for (int sample = 0; sample < waveform[actChn].Samples.Count; sample++)
                            {
                                lastTime = ts + sample / sampleRate;
                                data.Add(new Point(lastTime, waveform[actChn].Samples[sample].Value));
                                if (activeChannel == 2)
                                    data.Add(new Point(lastTime, waveform[actChn + 1].Samples[sample].Value));
                            }
                            lastTime += 1 / sampleRate;
                            break;
                        case (TimingModes.byBoth): // time markers from Stopwatch, sampleRate from ADC setting
                            for (int sample = 0; sample < waveform[activeChannel].Samples.Count; sample++)
                            {
                                data.Add(new Point(lastTime + sample / sampleRate, waveform[actChn].Samples[sample].Value));
                                if (activeChannel == 2)
                                    data.Add(new Point(lastTime + sample / sampleRate, waveform[actChn + 1].Samples[sample].Value));
                            }
                            if (theTime.isTimeRunning) lastTime = theTime.elapsedTime;
                            break;
                    }
                    dataBuffer = new List<Point>(data);
                    if (!dTimer.IsEnabled) dTimer.Start();
                    else Utils.TimedMessageBox("Timing problem: skip a buffer of data");
                    if (tracer.trouble)
                    {
                        Utils.TimedMessageBox("Timing sequence problem"); 
                        tracer.trouble = false;
                    }                       
                    analogInReader.BeginMemoryOptimizedReadWaveform(nSamples, analogCallback, myTask, waveform);
                }
            }
            catch (DaqException exception)
            {
                // Display Errors
                Utils.TimedMessageBox(exception.Message);
                Reset();
            }
        }
        public void StopAcquisition()
        {
            Reset();
        }
        #endregion
    }

    /// <summary>
    /// The temperature in a class abstraction
    /// </summary>
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
        public void StartAcquisition(int samplesPerChannel = 100, double samplingRate = 267)
        {
            if (!Utils.isNull(tmpTask)) return; // fix later !!!
            try
            {
                //Create a new task locally
                tmpTask = new Task();
                
                //Create a virtual channel
                tmpTask.AIChannels.CreateVoltageChannel(hw["device"] + hw["channel1"], "", AITerminalConfiguration.Differential,
                                                    Convert.ToDouble(hw["min"]), Convert.ToDouble(hw["max"]), AIVoltageUnits.Volts);
                tmpTask.AIChannels.CreateVoltageChannel(hw["device"] + hw["channel2"], "", AITerminalConfiguration.Differential,
                                                    Convert.ToDouble(hw["min"]), Convert.ToDouble(hw["max"]), AIVoltageUnits.Volts);

                tmpTask.Stop();
                tmpTask.Timing.ConfigureSampleClock("", samplingRate, SampleClockActiveEdge.Rising, SampleQuantityMode.FiniteSamples, samplesPerChannel);
                tmpTask.Timing.SamplesPerChannel = samplesPerChannel;   //tmpTask.Stream.Timeout = Timeout;
                tmpTask.Control(TaskAction.Commit);

                //Verify the Task
                tmpTask.Control(TaskAction.Verify);               
            }
            catch (DaqException exception)
            {
                MessageBox.Show(exception.Message);
            }
            finally
            {

            }
        }
        public void StopAcquisition()
        {
            return; // fix later !!!
            if (!Utils.isNull(tmpTask)) tmpTask.Dispose();
            if (isDevicePresent(hw["device"]))
            {
                Device dev = DaqSystem.Local.LoadDevice(hw["device"]);
                //dev.Reset();
            }
        }
        /// <summary>
        /// The actual temperature measurement by channel 
        /// </summary>
        /// <returns></returns>
        public double[] TakeTheTemperature()
        {
            double[] rslt = null;
            if (Utils.isNull(tmpTask)) { StartAcquisition(); }
            try
            {
                 AnalogMultiChannelReader reader = new AnalogMultiChannelReader(tmpTask.Stream);
                 //Plot Multiple Channels to the table
                 rslt = reader.ReadSingleSample();               
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
}