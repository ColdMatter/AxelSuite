using System;
using System.Timers;
using System.Collections.Generic;

using NationalInstruments;
using NationalInstruments.DAQmx;
//using DS345NS;
using UtilsNS;

namespace AxelBMemsNS
{
    public class AxelMems
    {
        public Timer mTimer = null; // start/stop

        private bool _running = false;
        public int nSamples = 20000; // default
        public int Timeout = 1000; // [sec]
        public double sampleRate = 2200;  
        public List<double> rawData = null;
        //double[,] aiData = new double[1, nSamples];
        
        private Task voltageInputTask = null;
        private AnalogMultiChannelReader VIReader = null;
        private AIChannel axelAIChannel;

        public AxelMems()
        {
        }

        public bool running // wait for running to end in order to read rawData
        {
            get { return _running; }
        }

        public double[,] readBurst(int nPoints) // synchro read
        {
            double[,] aiData = new double[1, nPoints];

            voltageInputTask.Control(TaskAction.Abort); // reset the device
            voltageInputTask.Start();
            aiData = VIReader.ReadMultiSample(nPoints);
            voltageInputTask.Stop();
            return aiData;
        }

        public void configureVITask(string physicalChn, string taskName) // nSamples and sampleRate must be set beforehand
        {
            if(Utils.isNull(voltageInputTask)) voltageInputTask = new Task();

            if(Utils.isNull(axelAIChannel)) axelAIChannel = voltageInputTask.AIChannels.CreateVoltageChannel(physicalChn, taskName, AITerminalConfiguration.Differential,
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

    }
}