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
using scanHub;
using AxelHMemsNS;
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
        private bool EndlessMode = false;
        int nSamples = 1500; 
        private AxelMems axelMems = null;     

        public MainWindow()
        {
            InitializeComponent();
            tabSecPlots.SelectedIndex = 4;
            ucScan1.Start += new scanClass.StartHandler(DoStart);
            ucScan1.Remote += new scanClass.RemoteHandler(DoRemote);
            ucScan1.FileRef += new scanClass.FileRefHandler(DoRefFile);
            axelMems = new AxelMems();
            axelMems.Acquire += new AxelMems.AcquireHandler(DoAcquire);
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

        // main ADC call
        public void DoStart(bool down, double period, bool TimeMode, bool Endless, double Limit)
        {
            EndlessMode = Endless;
            
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
            AxelChart1.Clear();

            axelMems.StartAqcuisition(nSamples, 1 / period);
        }

        public void DoAcquire(List<Point> dt, out bool next)
        {
            next = (EndlessMode && ucScan1.Running);
            for (int i = 0; i < nSamples; i++)
            {
                AxelChart1.Waveform.AddPoint(dt[i].Y);
            }
            //AxelChart1.Refresh();
            DoEvents();
            if (!next)
            {
                AxelChart1.Running = false;
                ucScan1.Running = false;
            }
        }

        // remote ADC call
        public void DoRemote(double SamplingPeriod, double CyclePeriod, double Pause, double Distance, double Accel, int CyclesLeft) // from TotalCount to 1
        {
        }
        
        // XPS log file reference .....
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