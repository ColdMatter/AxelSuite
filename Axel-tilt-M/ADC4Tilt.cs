using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NationalInstruments;
using NationalInstruments.DAQmx;
using UtilsNS;
using Task = NationalInstruments.DAQmx.Task;
using System.Windows.Media;

namespace Axel_tilt
{
    class ADC4Tilt
    {
        string physicalChannel = "Dev4/ai0:3";
        private double freq; //Hz
        private int samplesPerChannel = 1000; // buffer size
        private int totalSamples; // total number for the file
        public ADC4Tilt()
        {

        }
        // ContAcqVoltageSamples_IntClk.2013 from NI examples AnalogIn
        private AnalogMultiChannelReader analogInReader;
        private Task myTask;
        private Task runningTask;
        private AsyncCallback analogCallback;
        private AnalogWaveform<double>[] data;

        public bool MeasureADC(double _freq, int _totalSamples) // kHz, n
        {
            freq = _freq*1000;
            if (!Utils.InRange(freq, 10, 10000)) { Log("Err: freq out of range"); return false; }
            if (!Utils.InRange(_totalSamples, 10, 1000000)) { Log("Err: samples out of range"); return false; }
            totalSamples = _totalSamples;
            dataList = new List<string>();
            try
            {
                // Create a new task
                myTask = new Task();

                double rangeMinimum = -10;
                double rangeMaximum = 10;

                // Create a channel
                myTask.AIChannels.CreateVoltageChannel(physicalChannel, "",
                    (AITerminalConfiguration)(-1), rangeMinimum, rangeMaximum, AIVoltageUnits.Volts);

                // Configure timing specs    
                myTask.Timing.ConfigureSampleClock("", freq, SampleClockActiveEdge.Rising,
                    SampleQuantityMode.ContinuousSamples, samplesPerChannel);

                // Verify the task
                myTask.Control(TaskAction.Verify);

                runningTask = myTask;
                analogInReader = new AnalogMultiChannelReader(myTask.Stream);
                analogCallback = new AsyncCallback(AnalogInCallback);

                // Use SynchronizeCallbacks to specify that the object 
                // marshals callbacks across threads appropriately.
                analogInReader.SynchronizeCallbacks = true;
                analogInReader.BeginReadWaveform(samplesPerChannel,  analogCallback, myTask);               
            }
            catch (DaqException exception)
            {
                System.Windows.MessageBox.Show(exception.Message);
                runningTask = null;
                myTask.Dispose();

                return false;
            }
            Log("Start data acquisition");
            return true;
        }

        private void AnalogInCallback(IAsyncResult ar)
        {
            try
            {
                if (runningTask != null && runningTask == ar.AsyncState)
                {
                    // Read the available data from the channels
                    data = analogInReader.EndReadWaveform(ar);
                                       
                    AddData(data);
                    if (dataList.Count < totalSamples) analogInReader.BeginMemoryOptimizedReadWaveform(samplesPerChannel, analogCallback, myTask, data);
                    else { Log("End of data acquisition"); SaveData(); }
                }
            }
            catch (DaqException exception)
            {
                // Display Errors
                MessageBox.Show(exception.Message);
                Finish();              
            }
        }
        public void Finish()
        {
            if (runningTask != null)
            {
                // Dispose of the task
                runningTask = null;
                myTask.Dispose();
            }
        }
        List<string> dataList;
        private void AddData(AnalogWaveform<double>[] data)
        {
            for (int sample = 0; sample < samplesPerChannel; sample++)
            {
                string ss = "";
                for (int chn = 0; chn < 4; chn++)
                {
                    ss += data[chn].Samples[sample].Value.ToString("G5")+'\t';
                }
                dataList.Add(ss);
            }       
        }
        private void SaveData()
        {
            dataList.Insert(0,"# frequency[Hz] = " + freq.ToString("G4") + " ; samples = " + totalSamples.ToString());
            string fn = Path.Combine(Utils.dataPath, Path.ChangeExtension(Utils.timeName(), ".ahm"));
            Utils.writeList(fn, dataList); Log("File " + fn + " saved.");
        }
        public event Utils.LogHandler OnLog;
        protected void Log(string txt, SolidColorBrush clr = null)
        {
            if (OnLog != null) OnLog(txt, clr);
        }

        /*private void dataToDataTable(AnalogWaveform<double>[] sourceArray, ref DataTable dataTable)
        {
            // Iterate over channels
            int currentLineIndex = 0;
            foreach (AnalogWaveform<double> waveform in sourceArray)
            {
                for (int sample = 0; sample < waveform.Samples.Count; ++sample)
                {
                    if (sample == 10)
                        break;

                    dataTable.Rows[sample][currentLineIndex] = waveform.Samples[sample].Value;
                }
                currentLineIndex++;
            }
        }*/

    }
}
