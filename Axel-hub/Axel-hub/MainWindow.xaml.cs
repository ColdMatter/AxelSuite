using NationalInstruments.Net;
using NationalInstruments.Analysis;
using NationalInstruments.Analysis.Conversion;
using NationalInstruments.Analysis.Dsp;
using NationalInstruments.Analysis.Dsp.Filters;
using NationalInstruments.Analysis.Math;
using NationalInstruments.Analysis.Monitoring;
using NationalInstruments.Analysis.SignalGeneration;
using NationalInstruments.Analysis.SpectralMeasurements;
using NationalInstruments;
using NationalInstruments.DAQmx;
using NationalInstruments.NetworkVariable;
using NationalInstruments.NetworkVariable.WindowsForms;
using NationalInstruments.Tdms;
using NationalInstruments.Controls;
using NationalInstruments.Controls.Rendering;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using scanNS;
using AxelMemsNS;
using AxelChartNS;
using UtilsNS;
//using DS345NS;


namespace Axel_hub
{
    public delegate void StartDelegate();
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private int nSamples = 2000;
        Stopwatch stopWatch;
       
        public MainWindow()
        {
            InitializeComponent();
            tabSecPlots.SelectedIndex = 4;
            ucScan1.Start += new scanClass.StartHandler(DoStart);
            ucScan1.Remote += new scanClass.RemoteHandler(DoRemote);
            ucScan1.FileRef += new scanClass.FileRefHandler(DoRefFile);

            stopWatch = new Stopwatch();
           // DoRefFile(@"e:\VSprojects\Axel-track\XPS\17-03-29_15-32-33.log"); // test 17-03-29_17-37-05.log
        }

        private void log(string txt) 
        {
            tbLog.AppendText(txt + "\n");
        }

        public void DoEvents()
        {
            DispatcherFrame frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,
                new DispatcherOperationCallback(ExitFrame), frame);
            Dispatcher.PushFrame(frame);
        }

        public object ExitFrame(object f)
        {
            ((DispatcherFrame)f).Continue = false;
            return null;
        }
        private AxelMems axelMems = null;

        // main ADC call
        public void DoStart(bool down, double period, bool TimeMode, bool Endless, double Limit)
        {
            if (!down) // user cancel
            {
                AxelChart1.Running = false; 
                ucScan1.Running = false; 
                return;
            }
            Random random = new Random();

            AxelChart1.Waveform.TimeMode = TimeMode;            
            if (TimeMode)
            {
                AxelChart1.Waveform.TimeLimit = Limit;
                nSamples = (int)(Limit / period);
            }
            else
            {
                AxelChart1.Waveform.SizeLimit = (int)Limit; 
                nSamples = AxelChart1.Waveform.SizeLimit;
            }
            AxelChart1.SamplingPeriod = period;
            AxelChart1.Running = true;
            if (Utils.isNull(axelMems)) axelMems = new AxelMems();
            axelMems.nSamples = nSamples;
            axelMems.sampleRate = 1 / period; // in Hz
            axelMems.configureVITask("cDAQ1Mod1/ai0", "chn0");
            AxelChart1.Clear();
            Stopwatch sw = new Stopwatch();
            sw.Start();
            TimeSpan tm = sw.Elapsed;
            int j = 0; 
            do
            {
                /*axelMems.readInVoltages();
                do
                {
                    DoEvents();
                } while (axelMems.running);

                if (axelMems.rawData.Count != nSamples) new Exception("Wrong number of points in aquisition");*/
                tm = sw.Elapsed;
                double[,] rd = axelMems.readBurst(0);
               
                for (int i = 0; i < 1500; i++)
                {
                    AxelChart1.AddPoint(/*axelMems.rawData[i]*/ rd[0,i], tm.TotalSeconds + period * i);
                }
                AxelChart1.Refresh();
            } while (Endless && ucScan1.Running);

            AxelChart1.Running = false;
            ucScan1.Running = false;
        }

        private int TotalCycleCount = 0;
        private double TotalCycleTime = 0;
        // XPS remote ADC call
        public void DoRemote(double SamplingPeriod, double CyclePeriod, double Pause, double Distance, double Accel, int CyclesLeft) // from TotalCount to 1
        {
            Random random = new Random();
            
            if (!AxelChart1.Running) // first call - only prepare
            {
                //AxelChart1.Clear();
                Dictionary<string, double> RemoteArg = new Dictionary<string, double>();
                RemoteArg.Add("SamplingPeriod", SamplingPeriod); RemoteArg.Add("CyclePeriod", CyclePeriod); RemoteArg.Add("Pause", Pause);
                RemoteArg.Add("Distance", Distance); RemoteArg.Add("Accel", Accel); RemoteArg.Add("TotalCycleCount", CyclesLeft);
                AxelChart1.SamplingPeriod = SamplingPeriod;
                TotalCycleCount = CyclesLeft;
                TotalCycleTime = TotalCycleCount * (CyclePeriod + 2 * Pause);
                nSamples = (int)((CyclePeriod + 2 * Pause) / SamplingPeriod);
                AxelChart1.Waveform.TimeMode = false;
                AxelChart1.Waveform.SizeLimit = TotalCycleCount * nSamples;
                AxelChart1.Running = true;
                AxelChart1.remoteArg = JsonConvert.SerializeObject(RemoteArg);
                            
                // ADC
                if (Utils.isNull(axelMems)) axelMems = new AxelMems();
                axelMems.nSamples = nSamples;
                axelMems.sampleRate = ucScan1.RealConvRate(1 / SamplingPeriod); // in Hz
                axelMems.configureVITask("cDAQ1Mod1/ai0", "chn0");
                log("> starting acquisition");
                log("> " + SamplingPeriod.ToString("G3") + "; " + CyclePeriod.ToString("G5") + "; " + Pause.ToString("G4") + "; " + Distance.ToString("G3") +
                    "; " + Accel.ToString("G3") + "; " + CyclesLeft.ToString());
                return;
            }

            if ((AxelChart1.Running) && (CyclesLeft == TotalCycleCount))// first real call
            {
                stopWatch.Restart();
            }
            double tm = stopWatch.Elapsed.TotalSeconds;
            //log("time = " + tm.ToString("G4"));

            axelMems.readInVoltages();
            do
            {   DoEvents();
            } while (axelMems.running);

            if (axelMems.rawData.Count != nSamples) throw new Exception("Wrong number of points in aquisition");
            for (int i = 0; i < axelMems.rawData.Count; i++)
            {
                AxelChart1.AddPoint(axelMems.rawData[i], //  random.Next()
                                    tm + SamplingPeriod * i);
            }
            AxelChart1.Refresh();
            if (AxelChart1.Running && (CyclesLeft == 1)) // last call
            {
                AxelChart1.Running = false;
                ucScan1.Running = false;
                TotalCycleCount = 0;
            }
        }
        
        // XPS log file reference 
        public void DoRefFile(string FN, bool statFlag)
        {            
        }

        public void DoCompareChart()
        {
        }
        #region File operation ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff
        public bool Open(string fn)
        {
            if (!File.Exists(fn)) throw new Exception("File <" + fn + "> does not exist.");
            AxelChart1.Open(fn);
            AxelChart1.Refresh();

            int ext = 0; tbRem.Text = "";
            foreach (string line in File.ReadLines(fn))
            {
                if (line.Contains("#Rem="))
                {
                    tbRem.Text = line.Substring(5);                    
                }
            }
            if (ext < 1) MessageBox.Show("Some internal extensions are missing in <" + fn + ">.");

            log("Open> " + fn);
            return (ext == 1);
        }

        private void btnOpen_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.FileName = ""; // Default file name
            dlg.DefaultExt = ".abf"; // Default file extension
            dlg.Filter = "Axel Boss File (.abf)|*.abf"; // Filter files by extension

            // Show save file dialog box
            Nullable<bool> result = dlg.ShowDialog();

            // Process save file dialog box results
            if (result == true) Open(dlg.FileName);
        }

        public void Save(string fn) 
        {
            System.IO.StreamWriter file = new System.IO.StreamWriter(fn);
            if (AxelChart1.remoteArg == string.Empty) throw new Exception("No remote arguments in upper chart");
            file.WriteLine("#" + AxelChart1.remoteArg);
            
            if (!String.IsNullOrEmpty(tbRem.Text)) file.WriteLine("#Rem=" + tbRem.Text);
            for (int i = 0; i < AxelChart1.Waveform.Count; i++)
                file.WriteLine(AxelChart1.Waveform[i].X.ToString() + "\t" + AxelChart1.Waveform[i].Y.ToString());
            file.Close();
            log("Save> " + fn);
        }

        private void btnSaveAs_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.FileName = ""; // Default file name
            dlg.DefaultExt = ".abf"; // Default file extension
            dlg.Filter = "Axel Boss File (.abf)|*.abf"; // Filter files by extension

            // Show save file dialog box
            Nullable<bool> result = dlg.ShowDialog();
            if (result == true) Save(dlg.FileName);
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            tbRem.Text = "";
            AxelChart1.Clear();
            AxelChart1.Refresh();
        }
        #endregion

        private void splitDown_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            frmAxelHub.Top = 0;
            frmAxelHub.Height = SystemParameters.WorkArea.Height;  
            frmAxelHub.Left = SystemParameters.WorkArea.Width * 0.3;
            frmAxelHub.Width = SystemParameters.WorkArea.Width * 0.7;
            tabSecPlots.SelectedIndex = 0;
        }

        private void tabSecPlots_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            double d = ActualHeight / 2 - 6; 
            if (d < 25) return;
            if ((tabSecPlots.SelectedIndex == 0) || (Utils.isNull(sender)))
            {
                rowUpperChart.Height = new GridLength(d, GridUnitType.Star);
                rowMiddleChart.Height = new GridLength(30, GridUnitType.Star);
                rowLowerChart.Height = new GridLength(d, GridUnitType.Star);
            }
            else
            {
                int mh = 150;
                rowUpperChart.Height = new GridLength(d-mh/2, GridUnitType.Star);
                rowMiddleChart.Height = new GridLength(mh, GridUnitType.Star);
                rowLowerChart.Height = new GridLength(d-mh/2, GridUnitType.Star);                
            }
        }
    }
}