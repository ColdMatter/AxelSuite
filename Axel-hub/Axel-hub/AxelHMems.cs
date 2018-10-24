using System;
using System.Windows;
using System.Timers;
using System.Collections.Generic;
using System.Diagnostics;

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

        
        private readonly string physicalChannel0 = "cDAQ1Mod1_1/ai0"; //ADC24
        private readonly string physicalChannel1 = "cDAQ1Mod1_1/ai1";
        private readonly string physicalChannel2 = "Dev4/ai1"; //PXIe

        public int nSamples { get; private set; }
        public double sampleRate { get; private set; }
        public int Timeout = -1; // [sec] ; -1 - no timeout
        public List<double> rawData = null;
        
        private Task voltageInputTask = null;
        private AnalogMultiChannelReader VIReader = null;
        private AIChannel axelAIChannel;

        private AnalogMultiChannelReader analogInReader;
        private Task myTask;
        private Task runningTask;
        private AsyncCallback analogCallback;
        private AnalogWaveform<double>[] waveform;
        
        public AxelMems()
        {
            sw = new Stopwatch();
            activeChannel = 0;
            sampleRate = 2133;
            nSamples = 1500;
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
            OnRealSampling(FixConvRate[found]); // let the host know the last one
            return FixConvRate[found];
        }

        public delegate void RealSamplingHandler(double realSampling);
        public event RealSamplingHandler RealSampling;

        protected void OnRealSampling(double realSampling) // 
        {
            if (RealSampling != null) RealSampling(realSampling);
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

        public void Reset() // for all procs
        {
            if (!Utils.isNull(axelAIChannel)) axelAIChannel.Dispose();
            if (!Utils.isNull(VIReader)) VIReader = null;
            if (!Utils.isNull(voltageInputTask)) voltageInputTask.Dispose();

            runningTask = null;
            if (!Utils.isNull(myTask)) myTask.Dispose();
            if (sw.IsRunning) sw.Reset();
            _running = false;          
        }

        #region sync (separate thread) Aqcuisition 
        public delegate void AcquireHandler(List<Point> data, out bool next);
        public event AcquireHandler Acquire;

        protected void OnAcquire(List<Point> data, out bool next)
        {
            next = false;
            if (Acquire != null) Acquire(data, out next);
        }

        public void StartAqcuisition(int samplesPerChannel, double samplingRate)
        {
            try
            {
                if (Utils.isNull(runningTask))
                {
                    // Create a new task
                    myTask = new Task();                
                   
                    // Create a virtual channel
                    if ((activeChannel == 0) || (activeChannel == 2))
                        myTask.AIChannels.CreateVoltageChannel(physicalChannel0, "", AITerminalConfiguration.Differential, -3.5, 3.5, AIVoltageUnits.Volts);
                    if ((activeChannel == 1) || (activeChannel == 2))
                        myTask.AIChannels.CreateVoltageChannel(physicalChannel1, "", AITerminalConfiguration.Differential, -3.5, 3.5, AIVoltageUnits.Volts);
                    if (activeChannel == 3)
                        myTask.AIChannels.CreateVoltageChannel(physicalChannel2, "", AITerminalConfiguration.Differential, -3.5, 3.5, AIVoltageUnits.Volts);
                }
                // Configure the timing parameters
                myTask.Stop();
                myTask.Timing.ConfigureSampleClock("", samplingRate, SampleClockActiveEdge.Rising, SampleQuantityMode.ContinuousSamples, samplesPerChannel);
                myTask.Timing.SamplesPerChannel = samplesPerChannel;
                myTask.Stream.Timeout = Timeout;

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
                if (!sw.IsRunning)
                {
                    sw.Start(); // first time
                    lastTime = 0.0;
                    lastCount = 0;
                }
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

        double lastTime;
        long lastCount;
        private void AnalogInCallback(IAsyncResult ar)
        {
            try
            {
                if (runningTask != null && runningTask == ar.AsyncState)
                {
                    // Read the available data from the channels
                    waveform = analogInReader.EndReadWaveform(ar);
                    int actChn = 0; // for single channel (any)
                    if((activeChannel == 2) && (waveform.Length < 2)) throw new Exception("Set for two channels but only one appears!");                   
                    List<Point> data = new List<Point>();
                    double ts, prd;
                    switch (TimingMode)
                    {
                        case (TimingModes.byNone): // time markers (x values) from counter (0, 1, 2, ....)
                            for (int sample = 0; sample < waveform[actChn].Samples.Count; ++sample)
                            {
                                data.Add(new Point(sample + lastCount, waveform[actChn].Samples[sample].Value));
                                if(activeChannel == 2)
                                    data.Add(new Point(sample + lastCount, waveform[actChn+1].Samples[sample].Value));
                            }
                            lastCount += waveform[actChn].Samples.Count; // total count marker
                            break;
                       case (TimingModes.byStopwatch): // time markers from Stopwatch, samplePer -> (time makers diff) / nSamples (recommended)
                            ts = sw.Elapsed.TotalSeconds;
                            prd = (ts - lastTime) / waveform[actChn].Samples.Count;
                            for (int sample = 0; sample < waveform[actChn].Samples.Count; ++sample)
                            {
                                data.Add(new Point(lastTime + sample * prd, waveform[actChn].Samples[sample].Value));
                                if(activeChannel == 2)
                                    data.Add(new Point(lastTime + sample * prd, waveform[actChn+1].Samples[sample].Value));
                            }
                            lastTime = ts;
                            break;
                        case (TimingModes.byADCtimer): // samplePer -> 1 / , time markers calculated from samplePer and nSamples 
                            ts = lastTime;
                            for (int sample = 0; sample < waveform[actChn].Samples.Count; ++sample)
                            {   
                                lastTime = ts + sample / sampleRate;
                                data.Add(new Point(lastTime, waveform[actChn].Samples[sample].Value));
                                if(activeChannel == 2)
                                    data.Add(new Point(lastTime, waveform[actChn+1].Samples[sample].Value));
                            }
                            break;
                        case (TimingModes.byBoth): // time markers from Stopwatch, sampleRate from ADC setting
                            for (int sample = 0; sample < waveform[activeChannel].Samples.Count; ++sample)
                            {
                                data.Add(new Point(lastTime + sample / sampleRate, waveform[actChn].Samples[sample].Value));
                                if(activeChannel == 2)
                                    data.Add(new Point(lastTime + sample / sampleRate, waveform[actChn+1].Samples[sample].Value));
                            }
                            lastTime = sw.Elapsed.TotalSeconds;
                            break;
                    }
                    bool next = false;
                    OnAcquire(data, out next); // when 2 channels data list has them both: odd - chn0; even - chn1
                    if (next) analogInReader.BeginMemoryOptimizedReadWaveform(nSamples, analogCallback, myTask, waveform);
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
    }
}