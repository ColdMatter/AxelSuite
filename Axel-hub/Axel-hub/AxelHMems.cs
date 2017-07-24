using System;
using System.Windows;
using System.Timers;
using System.Collections.Generic;
using System.Diagnostics;

using NationalInstruments;
using NationalInstruments.DAQmx;
//using DS345NS;
using UtilsNS;

namespace AxelHMemsNS
{
    public class AxelMems
    {
        public Timer mTimer = null; // start/stop
        private Stopwatch sw = null; 
        
        private bool _running = false;
        private int activeChannel = 0;
        private string physicalChannel = "cDAQ1Mod1/ai0"; 
        private int _nSamples = 1500; // default
        public int nSamples {get { return _nSamples; }  }

        private double _sampleRate = 2200;
        public double sampleRate { get { return _sampleRate; } }
        public int Timeout = 1000; // [sec]
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
        }

        public bool running // wait for running to end in order to read rawData
        {
            get { return _running; }
        }

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

        public void configureVITask(string physicalChn, int numbSamples, double samplingRate) 
        {
            if(Utils.isNull(voltageInputTask)) voltageInputTask = new Task();

            if(Utils.isNull(axelAIChannel)) axelAIChannel = voltageInputTask.AIChannels.CreateVoltageChannel(physicalChn, "", AITerminalConfiguration.Differential,
                (double)-3.5, (double)3.5, AIVoltageUnits.Volts);

            voltageInputTask.Timing.ConfigureSampleClock("", sampleRate, SampleClockActiveEdge.Rising, SampleQuantityMode.FiniteSamples);
            voltageInputTask.Timing.SamplesPerChannel = nSamples;
            voltageInputTask.Stream.Timeout = 1000 * Timeout;
            voltageInputTask.Control(TaskAction.Commit);

            if(Utils.isNull(VIReader)) VIReader = new AnalogMultiChannelReader(voltageInputTask.Stream);
        }

        public void Reset()
        {
            if (!Utils.isNull(axelAIChannel)) axelAIChannel.Dispose();
            if (!Utils.isNull(VIReader)) VIReader = null;
            if (!Utils.isNull(voltageInputTask)) voltageInputTask.Dispose();
        }

        #region 
        public delegate void AcquireHandler(List<Point> data, out bool next);
        public event AcquireHandler Acquire;

        protected void OnAcquire(List<Point> data, out bool next)
        {
            next = false;
            if (Acquire != null) Acquire(data, out next);
        }

        public void StartAqcuisition(int samplesPerChannel, double samplingRate)
        {
            if (runningTask == null)
            {
                try
                {
                    // Create a new task
                    myTask = new Task();

                    // Create a virtual channel
                    myTask.AIChannels.CreateVoltageChannel(physicalChannel, "", (AITerminalConfiguration)(-1), -3.5, 3.5, AIVoltageUnits.Volts);

                    // Configure the timing parameters
                    myTask.Timing.ConfigureSampleClock("", samplingRate, SampleClockActiveEdge.Rising, SampleQuantityMode.ContinuousSamples, 1000);

                    // Verify the Task
                    myTask.Control(TaskAction.Verify);

                    runningTask = myTask;
                    analogInReader = new AnalogMultiChannelReader(myTask.Stream);
                    analogCallback = new AsyncCallback(AnalogInCallback);
                    
                    // Use SynchronizeCallbacks to specify that the object 
                    // marshals callbacks across threads appropriately.
                    analogInReader.SynchronizeCallbacks = true;
                    _nSamples = samplesPerChannel;
                    analogInReader.BeginReadWaveform(samplesPerChannel, analogCallback, myTask);
                }
                catch (DaqException exception)
                {
                    // Display Errors
                    MessageBox.Show(exception.Message);
                    runningTask = null;
                    myTask.Dispose();
                }
            }

        }

        private void AnalogInCallback(IAsyncResult ar)
        {
            try
            {
                if (runningTask != null && runningTask == ar.AsyncState)
                {
                    // Read the available data from the channels
                    waveform = analogInReader.EndReadWaveform(ar);
                    List<Point> data = new List<Point>();

                    if (!sw.IsRunning) sw.Start(); 
                    double ts = sw.Elapsed.TotalSeconds;

                    for (int sample = 0; sample < waveform[activeChannel].Samples.Count; ++sample)
                    {
                        data.Add(new Point( ts + sample/sampleRate, waveform[activeChannel].Samples[sample].Value));
                    }
                    bool next = false;
                    if (Acquire != null) Acquire(data, out next);
                    if (next) analogInReader.BeginMemoryOptimizedReadWaveform(nSamples,  analogCallback, myTask, waveform);
                }
            }
            catch (DaqException exception)
            {
                // Display Errors
                MessageBox.Show(exception.Message);
                runningTask = null;
                myTask.Dispose();
            }
        }

        public void StopAqcuisition()
        {
            if (myTask != null)
            {
                 runningTask = null;
                 myTask.Dispose();
                 sw.Reset();
            }
        }
        #endregion

        #region flag (running) synchronized aqcuisition
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