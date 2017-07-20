using NationalInstruments.Net;
using NationalInstruments;
using NationalInstruments.NetworkVariable;
using NationalInstruments.NetworkVariable.WindowsForms;
using NationalInstruments.Tdms;
using NationalInstruments.Controls;
using NationalInstruments.Controls.Rendering;
using NationalInstruments.Analysis;
using NationalInstruments.Analysis.Math;
using NationalInstruments.Analysis.SpectralMeasurements;
using NationalInstruments.Analysis.Monitoring;
using NationalInstruments.Analysis.Dsp;
using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Diagnostics;
using System.Windows.Controls.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UtilsNS;

namespace AxelChartNS
{
    struct movePrmStr
    {
        public double TimeElapsed;
        public double CurrentPosition;
        public double CalcVelocity;
        public double CalcAcceleration;
        public double CurrentVelocity;
        public double CurrentAcceleration;
        public double SetpointPosition;
        public double SetpointVelocity;
        public double SetpointAcceleration;
        public double FollowingError;
    }

    public struct SingleStatStr 
    {
        public void clear()
        {
            level = 0;
            sd = 0;
        }
        public double level;
        public double sd;
    }

    public struct StatsStr 
    {
        public bool ok;
        public void clear()
        {
            upper.clear();
            lower.clear();
        }
        public SingleStatStr upper;
        public SingleStatStr lower;
        public SingleStatStr mean() 
        {
            SingleStatStr rslt;
            rslt.level = (Math.Abs(upper.level) + Math.Abs(lower.level))/2;
            rslt.sd = (upper.sd + lower.sd)/2; 
            return rslt;
        }
    } 

    #region DataStack - data storage for AxelChart
    /// <summary>
    /// You (developer) need to set TimeMode and one of SizeLimit or TimeLimit
    /// When (before) you start using the stack you have to set Running = true, ResetTimer will start timer
    /// Point.X is acommodating increasing value of double, if TimeMode -> sec, else - custom (incl. point number)
    /// TimeMode is about the way DataStack limits its size, only exception is if AddPoint has no second param and TimeMode is ON
    /// The output is from standart List method ToArray in order to set DataSource of Graph
    /// </summary>
    public class DataStack : List<System.Windows.Point>
    {
        public DataStack(bool stackMode = false) : base() 
        {
            _stackMode = stackMode;
            SizeLimit = 100000;
            TimeLimit = 1;
            RefFileStats = new Dictionary<string, double>();
            stopWatch = new Stopwatch();
        }
        public Dictionary<string, double> RefFileStats;

        private bool _stackMode = false;
        public bool StackMode
        {
            get { return _stackMode; }
        }
        public Stopwatch stopWatch;
        private bool _running; 
        public bool Running
        {
            get { return _running; }
            set 
            {
                _running = value;
                if (value) stopWatch.Restart();
                else stopWatch.Stop();
            }
        }
        public int SizeLimit { get; set; }
        private bool timeMode = true;

        public bool TimeMode  // if true TimeLimit is valid and vice versa for SizeLimit 
        {
            get
            {
                return timeMode;
            }
            set 
            {
                if(Running) throw new Exception("Cannot change mode while the DataStack is Running");
                timeMode = value;
            }
        }
        public double TimeLimit { get; set; }

        public int Fit2Limit()
        {
            if (StackMode)
            {
                if (TimeMode) // TimeLimit is valid
                {
                    if ((TimeLimit <= 0) || (TimeLimit > 1000)) throw new Exception("Invalid TimeLimit in TimeMode");
                    while ((this[Count-1].X - this[0].X) > TimeLimit)
                        RemoveAt(0);
                }
                else // SizeLimit is valid
                {
                    if ((SizeLimit <= 0) || (SizeLimit > 1E6)) throw new Exception("Invalid SizeLimit in SizeMode (TimeMode = false)");
                    while (Count > SizeLimit)
                        RemoveAt(0);
                }
            }
            return Count;
        }

        public int AddPoint(double dt, double tm = Double.NaN) // in order of Y,X 
        {
            double t = 0;
            if (TimeMode) 
            {
                if (Double.IsNaN(tm)) 
                {
                    TimeSpan ts = stopWatch.Elapsed; // use this with caution !
                    t = ts.TotalSeconds;
                }
                else t = tm;
            }
            else 
            {
                if (Double.IsNaN(tm)) t = Count;
                else t = tm;
            }
            Add(new System.Windows.Point(t, dt));
            return Fit2Limit();
        }

        public void CopyEach(int each, out System.Windows.Point[] pntsArr) // skip some points for speed
        {
            if (each == 1)
            {
                pntsArr = ToArray(); 
                return;
            }
            int japrx = (int)(Count / each) + 1;
            pntsArr = new System.Windows.Point[japrx];
            int j = 0;
            for( int i = 0; i < Count; i = i + each)
            {
                pntsArr[j] = this[i];
                j++;
            }
            if (j < japrx) Array.Resize<System.Windows.Point>(ref pntsArr, j);
        }

        public DataStack Clone(double offsetX = 0, double offsetY = 0)
        {
            DataStack rslt = new DataStack(StackMode);
            for (int i = 0; i < Count; i++)
                rslt.AddPoint(this[i].Y + offsetY, this[i].X + offsetX);
            rslt.TimeMode = TimeMode;
            rslt.Running = Running;
            rslt.TimeLimit = TimeLimit;
            rslt.SizeLimit = SizeLimit;
            return rslt;
        }

        public int indexByX(double X)
        {
            int idx = -1;
            for (int i = 0; i < Count; i++)
            {
                if (this[i].X > X)
                {
                    idx = i;
                    break;
                }
            }
            return idx;     
        }

        public double[] pointXs()
        {
            double[] pnts = new double[Count];
            for (int i = 0; i < Count; i++)
            {
                pnts[i] = this[i].X;
            }
            return pnts;
        }
        public void Rescale(double[] newXs, double offsetX = 0)
        {
            if (newXs.Length < 2) throw new Exception("Not enough new X points to rescale");
            DataStack cln = Clone(offsetX);
            double[] secondDev = CurveFit.SplineInterpolant(cln.pointXs(),cln.pointYs(),0,0);
            Clear();
            for(int i=0; i<newXs.Length; i++)
            {
                AddPoint(CurveFit.SplineInterpolation(cln.pointXs(), cln.pointYs(), secondDev, newXs[i]), newXs[i]);
            }           
        }

        public double[] pointYs()
        {
            double[] pnts = new double[Count];
            for (int i = 0; i < Count; i++)
            {
                pnts[i] = this[i].Y;
            }
            return pnts;
        }

        public double pointSDevY(bool relative = false) // in %
        {
            double average = pointYs().Average();
            double sumOfSquaresOfDifferences = pointYs().Select(val => (val - average) * (val - average)).Sum();
            double disp = Math.Sqrt(sumOfSquaresOfDifferences / pointYs().Length);
            if (relative) return 100 * disp / Math.Abs(average);
            return disp;            
        }
 
        #region File operations in DataStack
        // standard tab separated x,y file 
        public void OpenPair(string fn)
        {
            Clear();
            int j = 0;
            double X, Y;
            string[] pair;
            foreach (string line in File.ReadLines(fn))
            {
                if (line[0] == '#') continue; //skip comments/service info
                pair = line.Split('\t');
                if (!double.TryParse(pair[0], out X)) throw new Exception("Wrong double at line " + j.ToString());
                if (!double.TryParse(pair[1], out Y)) throw new Exception("Wrong double at line " + j.ToString());
                AddPoint(Y, X);
                j++;
            }
        }

        public void OpenFileRef(string fn)
        {
            //Group1.Pos.CurrentPosition	Group1.Pos.CurrentVelocity	Group1.Pos.CurrentAcceleration	
            //Group1.Pos.SetpointPosition	Group1.Pos.SetpointVelocity	Group1.Pos.SetpointAcceleration	Group1.Pos.FollowingError
            int j = -1;
            movePrmStr mp;
            string[] prms;
            double timeInterval = 1;
            List<movePrmStr> mList = new List<movePrmStr>();
            foreach (string line in File.ReadLines(fn))
            {
                j++;
                if (j == 0) // first row
                {
                    prms = line.Split('\t');
                    double.TryParse(prms[0], out timeInterval);
                }
                if (j < 2) continue; // skip first two rows
                prms = line.Split('\t');
                if (prms.Length != 8) break; // end of list or broken (incomplete) row 
                mp.TimeElapsed = j * timeInterval;
                if (!double.TryParse(prms[0], out mp.CurrentPosition)) throw new Exception("Wrong double at line " + j.ToString());
                if (!double.TryParse(prms[1], out mp.CurrentVelocity)) throw new Exception("Wrong double at line " + j.ToString());
                if (!double.TryParse(prms[2], out mp.CurrentAcceleration)) throw new Exception("Wrong double at line " + j.ToString());
                //if (prms[3].Equals("0") || prms[4].Equals("0") || prms[5].Equals("0")) continue; // ??? clean or raw
                if (!double.TryParse(prms[3], out mp.SetpointPosition)) throw new Exception("Wrong double at line " + j.ToString());
                if (!double.TryParse(prms[4], out mp.SetpointVelocity)) throw new Exception("Wrong double at line " + j.ToString());
                if (!double.TryParse(prms[5], out mp.SetpointAcceleration)) throw new Exception("Wrong double at line " + j.ToString());
                if (!double.TryParse(prms[6], out mp.FollowingError)) throw new Exception("Wrong double at line " + j.ToString());
                mp.CalcVelocity = 0; mp.CalcAcceleration = 0;
                mList.Add(mp);
            }
            j = 0;           
            Clear();
            TimeMode = false;
            SizeLimit = mList.Count;
            List<double> feList = new List<double>(); List<double> afeList = new List<double>();
            List<double> posList = new List<double>();
            for (int i = 0; i < mList.Count; i++)
            {
                posList.Add(mList[i].CurrentPosition);
            }
            double[] iniCond = {0.0, 0.0};
            double[] velArr = SignalProcessing.Differentiate(posList.ToArray(), timeInterval, iniCond, iniCond, DifferentiationMethod.Forward);
            double[] accelArr = SignalProcessing.Differentiate(velArr, timeInterval, iniCond, iniCond, DifferentiationMethod.Forward);
            for (int i = 4; i < mList.Count - 4; i++)
            {
                mp = mList[i];
                //mp.CalcVelocity = velArr[i];
                mp.CalcAcceleration = accelArr[i];
                feList.Add(mp.FollowingError); afeList.Add(Math.Abs(mp.FollowingError));
                AddPoint(mp.CalcAcceleration, mp.TimeElapsed);
                mList[i] = mp;
            }
            Double average = feList.Average(); RefFileStats["Mean_FE"] = average;
            double sumOfSquaresOfDifferences = feList.Select(val => (val - average) * (val - average)).Sum();
            RefFileStats["SDev_FE"] = Math.Sqrt(sumOfSquaresOfDifferences / feList.Count);
            RefFileStats["Min_FE"] = feList.Min(); RefFileStats["Max_FE"] = feList.Max();
            RefFileStats["Mean_abs_FE"] = afeList.Average();
        }
        
        public void Save(string fn)
        {
            System.IO.StreamWriter file = new System.IO.StreamWriter(fn);
            for (int i = 0; i < Count; i++ )
                file.WriteLine(this[i].X.ToString() + "\t" + this[i].Y.ToString());
            file.Close();
        }
        #endregion
    }
    #endregion
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class AxelChartClass : UserControl
    {
        private DataStack FirstDerv = null;
        private DataStack CleanSeries = null;
        private DataStack OriginalWaveform = null;

        public AxelChartClass()
        {
            InitializeComponent();
            Waveform = new DataStack();
            FirstDerv = new DataStack();
            CleanSeries = new DataStack();

            Running = false;
            tabSecPlots.SelectedIndex = 1;
            //
            lblRange.Content = "Range = " + curRange.ToString() + " pnts";
            Refresh();
        }
        public void Clear(bool andRefresh = true) 
        {
            Waveform.Clear();
            FirstDerv.Clear();
            CleanSeries.Clear();
            if (!Utils.isNull(OriginalWaveform)) OriginalWaveform.Clear();

            remoteArg = String.Empty;
            refFile = String.Empty;
            lbResult.Content = "";
            lbLvlP.Content = "level+ = ";
            lvDspP.Content = "SDev+ = ";
            lbLvlM.Content = "level- = ";
            lvDspM.Content = "SDev- = ";
            lbInfo.Content = "Info:";
            tbLog.Clear();
            if (andRefresh) Refresh();
        }

        private string _remoteArg  = String.Empty;
        public string remoteArg
        {
            get
            {
                return (string)GetValue(remoteArgProperty);
            }
            set
            {
                SetValue(remoteArgProperty, value);
                //if (!String.IsNullOrEmpty(value)) 
                    lbInfo.Content = "remArg> " + value;
            }
        }
        // Using a DependencyProperty as the backing store for remoteArg.  
        public static readonly DependencyProperty remoteArgProperty
            = DependencyProperty.Register(
                  "remoteArg",
                  typeof(string),
                  typeof(AxelChartClass),
                  new PropertyMetadata("")
              );

        private string _refFile = String.Empty;
        public string refFile
        {
            get
            {
                return (string)GetValue(refFileProperty);
            }
            set
            {
                SetValue(refFileProperty, value);
                if (!String.IsNullOrEmpty(value)) lbInfo.Content = "RefFile> " + value;
            }
        }
        // Using a DependencyProperty as the backing store for refFile.  
        public static readonly DependencyProperty refFileProperty
            = DependencyProperty.Register(
                  "refFile",
                  typeof(string),
                  typeof(AxelChartClass),
                  new PropertyMetadata("")
              );

        public double SamplingPeriod
        {
            get { return (double)GetValue(SamplingPeriodProperty); }
            set { SetValue(SamplingPeriodProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Running.  
        public static readonly DependencyProperty SamplingPeriodProperty
            = DependencyProperty.Register(
                  "SamplingPeriod",
                  typeof(double),
                  typeof(AxelChartClass),
                  new PropertyMetadata(0.0)
              );       

        public string customX
        {
            get
            {
                return (string)GetValue(customXProperty);
            }
            set
            {
                if ((value == string.Empty) || (value == "[sec]"))
                {
                    rbSec.Content = "[sec]";
                    rbMiliSec.Visibility = System.Windows.Visibility.Visible;
                    rbMicroSec.Visibility = System.Windows.Visibility.Visible;
                }
                else
                {
                    rbSec.Content = value;
                    rbMiliSec.Visibility = System.Windows.Visibility.Hidden;
                    rbMicroSec.Visibility = System.Windows.Visibility.Hidden;
                }
                SetValue(customXProperty, value);
            }
        }
        // Using a DependencyProperty as the backing store for customX.  
        public static readonly DependencyProperty customXProperty
            = DependencyProperty.Register(
                  "customX",
                  typeof(string),
                  typeof(AxelChartClass),
                  new PropertyMetadata("")
              );

        public DataStack Waveform
        {
            get
            {
                return (DataStack)GetValue(WaveformProperty);
            }
            set
            {
                SetValue(WaveformProperty, value);
            }
        }

        // Using a DependencyProperty as the backing store for Waveform.  
        public static readonly DependencyProperty WaveformProperty
          = DependencyProperty.Register(
          "Waveform",
          typeof(DataStack),
          typeof(AxelChartClass),
          new PropertyMetadata(null)
        );

        public bool Running
        {
            get { return (bool)GetValue(RunningProperty); }
            set 
            {
                Waveform.Running = value;
                if (value)
                {
                    btnPause.Visibility = Visibility.Visible;
                    Clear();
                }
                else
                {
                    btnPause.Visibility = Visibility.Hidden;
                    Refresh(null, null);
                }
                if (Running == value) return;
                SetValue(RunningProperty, value);
            }
        }

        // Using a DependencyProperty as the backing store for Running.  
        public static readonly DependencyProperty RunningProperty
            = DependencyProperty.Register(
                  "Running",
                  typeof(bool),
                  typeof(AxelChartClass),
                  new PropertyMetadata(false)
              );
        
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

        public int AddPoint(double pnt, double tm = Double.NaN)
        {
            return Waveform.AddPoint(pnt,tm);          
        }

        private void rescaleX(System.Windows.Point[] pntsIn, out System.Windows.Point[] pntsOut)
        {
            pntsOut = pntsIn; 
            if (rbPoints.IsChecked.Value)
            {               
                for (int i = 0; i < Waveform.Count; i++)
                {
                    pntsOut[i].X = i;
                }
                return;
            }
            if (rbSec.IsChecked.Value) return; // default X (sec or custom)
            if (rbMiliSec.IsChecked.Value)
            {
                for (int i = 0; i < Waveform.Count; i++)
                {
                    pntsOut[i].X = 1000 * pntsOut[i].X;
                }
                return;
            }
            if (rbMicroSec.IsChecked.Value)
            {
                for (int i = 0; i < Waveform.Count; i++)
                {
                    pntsOut[i].X = 1000000 * pntsOut[i].X;
                }
                return;
            }
        }

        private int curRange = 4096;
        private bool pauseFlag = false;
        public void Refresh()
        {
            Refresh(null, null);
        }

        private void Refresh(object sender, RoutedEventArgs e)
        {
            if (Utils.isNull(Waveform)) return;
            if (Waveform.Count == 0)
            {
                graphScroll.DataSource = null; graphOverview.DataSource = null; graphPower.DataSource = null; graphHisto.DataSource = null;
                return;
            }
            System.Windows.Point[] pA, pB;
            int each = (int)seShowFreq.Value;
            Waveform.CopyEach(each, out pA);
            rescaleX(pA, out pB);

            if (rbPoints.IsChecked.Value)
            {
                if (curRange >= Waveform.Count)
                {                
                    ((AxisDouble)graphScroll.Axes[0]).Range = new Range<double>(0, curRange);
                }
                else
                {
                    double l = Waveform.Count; 
                    ((AxisDouble)graphScroll.Axes[0]).Range = new Range<double>(l - curRange, l);
                }
            }
            else
            {
                int k = 0;
                if (rbSec.IsChecked.Value) k = 1;
                if (rbMiliSec.IsChecked.Value) k = 1000;
                if (rbMicroSec.IsChecked.Value) k = 1000000; 
 
                if (curRange >= Waveform.Count)
                {
                    if (Waveform.Count > 0)
                        ((AxisDouble)graphScroll.Axes[0]).Range = new Range<double>(k * Waveform[0].X, k * (Waveform[0].X + curRange * SamplingPeriod));
                }
                else
                {
                    double l = Waveform[Waveform.Count - 1].X;
                    ((AxisDouble)graphScroll.Axes[0]).Range = new Range<double>(k * (l - curRange * SamplingPeriod), k * l);
                }
            }

            graphScroll.DataSource = pB;
            double[] Ys;
            List<System.Windows.Point> pl = new List<System.Windows.Point>();
            switch (tabSecPlots.SelectedIndex) 
            {
                case 0: break; // disable
                case 1: graphOverview.DataSource = pB;
                        break;
                case 2: Ys = Waveform.pointYs();
                        double df;
                        double[] ps; //, ampl, phase; 
                        if (SamplingPeriod == 0) SamplingPeriod = (Waveform.pointXs()[99] - Waveform.pointXs()[0]) / 100;
                        //Measurements.AmplitudePhaseSpectrum(Ys, false, SamplingPeriod , out ampl, out phase, out df); 
                        ps = Measurements.AutoPowerSpectrum(Ys, SamplingPeriod, out df);  
                        int len = Math.Min(ps.Length, 12000);
                        for (int i = 1; i < len; i++) // skip DC component at position 0
                        {
                            if ((i * df) < 0.5) continue; // cut off level
                            pl.Add(new System.Windows.Point(i * df, ps[i])); 
                        }                        
                        graphPower.DataSource = pl;
                        break;
                case 3: graphHisto.DataSource = Histogram(Waveform);
                        break;
                case 4: break; // opts / stats
            }
            DoEvents();
            while (pauseFlag) 
            {
                DoEvents();
                System.Threading.Thread.Sleep(100);
            }
        }
  
        private double defaultRowRatio = 0;
        private double hiddenHeight = 22;

        private void tabSecPlots_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (tabSecPlots.SelectedIndex == 0)
            {
                defaultRowRatio = gridRight.RowDefinitions[1].ActualHeight / gridRight.ActualHeight;
                gridRight.RowDefinitions[1].Height = new GridLength(hiddenHeight);
            }
            else
            {
                if ((defaultRowRatio > 0) && (gridRight.RowDefinitions[1].ActualHeight < (hiddenHeight+5)))
                {
                    gridRight.RowDefinitions[1].Height = new GridLength(gridRight.ActualHeight * defaultRowRatio); 
                }
            }
            Refresh();
        }

        private void btnZoomOut_Click(object sender, RoutedEventArgs e)
        {
            curRange = (int)(curRange * 2);
            lblRange.Content = "Range = " + curRange.ToString() + " pnts";
            Refresh();
        }

        private void btnZoomIn_Click(object sender, RoutedEventArgs e)
        {
            if (curRange > 2) curRange = (int)(curRange / 2);
            lblRange.Content = "Range = " + curRange.ToString() + " pnts";
            Refresh();
        }

        private void btnPause_Click(object sender, RoutedEventArgs e)
        {
            pauseFlag = !pauseFlag; 
            if (pauseFlag) 
            {
                btnPause.Content = "Cont...";
            }
            else 
            {
                btnPause.Content = "Pause";
            }    
        }
        #region File Operations
        //ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff
        private void btnSaveAs_Click(object sender, RoutedEventArgs e)
        {
            if (Waveform.Count == 0) throw new Exception("No data to be saved");
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.FileName = ""; // Default file name
            dlg.DefaultExt = ".abf"; // Default file extension
            dlg.Filter = "Axel Boss File (.abf)|*.abf"; // Filter files by extension

            // Show save file dialog box
            Nullable<bool> result = dlg.ShowDialog();

            // Process save file dialog box results
            if (result == true)
            {
                // Save file                
                Waveform.Save(dlg.FileName);
                lbInfo.Content = "Saved: " + dlg.FileName; 
            }
        }

        public void Open(string fn)
        {
            if (!File.Exists(fn)) throw new Exception("File <" + fn + "> does not exist.");
            Clear();
            string ext = System.IO.Path.GetExtension(fn);
            if (ext.Equals(".abf"))
            {
                remoteArg = "";
                foreach (string line in File.ReadLines(fn))
                {
                    if (line.Contains("#{"))
                    {
                        remoteArg = line.Substring(1);                    
                    }
                }
                Waveform.OpenPair(fn);
                if (String.IsNullOrEmpty(remoteArg)) SamplingPeriod = (Waveform.pointXs()[99] - Waveform.pointXs()[0]) / 100;
                else
                {
                    Dictionary<string, object> remotePrms = JsonConvert.DeserializeObject < Dictionary<string, object>>(remoteArg);
                    SamplingPeriod = (double)remotePrms["SamplingPeriod"];
                }
                tbLog.AppendText("Count= " + Waveform.Count.ToString() + "\n");
                tbLog.AppendText("Sampling period= " + SamplingPeriod.ToString("G3") + "\n");
                tbLog.AppendText("^^^^^^^^^^^^^^^^^^^\n");
            }
            if (ext.Equals(".log"))
            {
                Waveform.OpenFileRef(fn);
                refFile = fn;
                SamplingPeriod = (Waveform.pointXs()[99] - Waveform.pointXs()[0]) / 100;
                tbLog.AppendText("Count= " + Waveform.Count.ToString() + "\n");
                tbLog.AppendText("Sampling period= " + SamplingPeriod.ToString("G3") + "\n");
                foreach (var st in Waveform.RefFileStats)
                {
                    tbLog.AppendText(st.Key + "= " + st.Value.ToString("G4") + "\n");
                }
                tbLog.AppendText("^^^^^^^^^^^^^^^^^^^\n");
            }
            OriginalWaveform = Waveform.Clone();
        }

        private void btnOpen_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.FileName = ""; // Default file name
            dlg.DefaultExt = ".abf"; // Default file extension
            dlg.Filter = "Axel Boss File (.abf)|*.abf|Axel Track File (.log)|*.log"; // Filter files by extension

            // Show save file dialog box
            Nullable<bool> result = dlg.ShowDialog();

            // Process save file dialog box results
            if (result == true)
            {
                // Open file                
                Open(dlg.FileName);
                Refresh();
                lbInfo.Content = "Opened: " + dlg.FileName; 
            }
        }
        #endregion
        //+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++

        #region Statistical routines

        private DataStack CalcFirstDerv(DataStack orig, int deg = 2) // 2-4
        {
            DataStack rslt = new DataStack();
            deg = Utils.EnsureRange(deg, 2, 4);
            double y;
            switch(deg) 
            {
               case 2: 
                    for (int i = 2; i < orig.Count-2; i++)
                    {
                        y = - 2 * orig[i - 2].Y - orig[i - 1].Y +  orig[i + 1].Y + 2 * orig[i + 2].Y ;
                        rslt.AddPoint(y / 10, orig[i].X);
                    }
                    break;
                case 3:
                    for (int i = 3; i < orig.Count - 3; i++)
                    {
                        y = - 3 * orig[i - 3].Y - 2 * orig[i - 2].Y - orig[i - 1].Y +
                            orig[i + 1].Y + 2 * orig[i + 2].Y + 3 * orig[i + 3].Y;
                        rslt.AddPoint(y / 28, orig[i].X);
                    }
                    break;
                case 4:
                    for (int i = 4; i < orig.Count - 4; i++)
                    {
                        y = -4 * orig[i - 4].Y - 3 * orig[i - 3].Y - 2 * orig[i - 2].Y - orig[i - 1].Y +
                            orig[i + 1].Y + 2 * orig[i + 2].Y + 3 * orig[i + 3].Y + 4 * orig[i + 4].Y;
                        rslt.AddPoint(y / 60, orig[i].X);
                    }
                    break;
            }
            return rslt;
        }

        private DataStack Histogram(DataStack src, int numBins = 100)
        {
            if (src.Count == 0) throw new Exception("No data for the histogram");
            DataStack rslt = new DataStack();
            double[] centerValues,Ys;
            Ys = src.pointYs();
            int[] histo = Statistics.Histogram(Ys, Ys.Min(), Ys.Max(), numBins, out centerValues);
            if (histo.Length != centerValues.Length) throw new Exception("histogram trouble!");
            for (int i = 0; i < histo.Length; i++)
            {
                rslt.AddPoint(histo[i], centerValues[i]);
            }    
            return rslt;
        }

        private DataStack statsByHistoExt(DataStack src, double thresholdAmpl, double thresholdHisto, bool centered, bool upper, out SingleStatStr stats) 
                                         // threshold is the level of distrimination as % of the max
        {
            Func<string, bool> log = new Func<string, bool>((txt) =>
            {
                //Console.WriteLine(txt); 
                tbLog.AppendText(txt + "\n");
                return true;
            });
            DataStack rslt = new DataStack();
            stats.level = 0; stats.sd = 0;
            try
            {
                if (src.Count == 0) throw new Exception("No data to be processed");
                if (upper) log(">-- calculating upper part --<");
                else log(">-- calculating lower part --<");            
                DataStack offmd;
                if (centered)
                {
                    double offsetY = src.pointYs().Sum() / src.Count;
                    log("offset by Y= " + offsetY.ToString("G3"));
                    offmd = src.Clone(0, -offsetY);
                }  
                else offmd = src.Clone();

                double minY = src.pointYs().Min(); double maxY = src.pointYs().Max(); 
                DataStack md = new DataStack();
                for (int i = 0; i < src.Count; i++)
                {
                    if (upper) 
                    {
                        if (offmd[i].Y < (thresholdAmpl / 100) * maxY) continue;
                        md.AddPoint(offmd[i].Y, offmd[i].X);
                    }
                    else
                    {
                        if (offmd[i].Y > (thresholdAmpl / 100) * minY) continue;
                        md.AddPoint(-offmd[i].Y, offmd[i].X);
                    } 
                }

                DataStack hst = Histogram(md);
                // peak detection
                double[] amplitudes, locations, secondDerivatives;
                double initialThreshold;
                int initialWidth;
                bool endOfData;

                PeakDetector peakDetector = new PeakDetector();
                // Set initial state of peakDetector
                initialThreshold = 0.0;
                initialWidth = 20; 
                peakDetector.Reset(initialThreshold, initialWidth, PeakPolarity.Peaks);
                endOfData = true;

                // Find location of amplitude, locations and second derivates of peaks in signalIn array
                peakDetector.Detect(hst.pointYs(), endOfData, out amplitudes, out locations, out secondDerivatives);
                int pkIdx = 0; double mx = 0;
                if (amplitudes.Length == 0) throw new Exception("No peaks found in histogram");
                if (locations.Length > 1) log(locations.Length.ToString() + " peaks found, searching for the highest one");
                for (int i = 0; i < amplitudes.Length; i++)
                {
                    if (mx > amplitudes[i]) continue;
                    mx = amplitudes[i];
                    pkIdx = i; 
                }
                //log("peakPosIdx in histo= " + locations[pkIdx].ToString("G3"));
                // fit Gauss
                double ampl, res; 
                double iAmpl =  hst.pointYs().Max();
                int cIdx = (int)Math.Round(locations[pkIdx]); double iCentre = hst.pointXs()[cIdx];
                double iSDev = md.pointSDevY();                
                double[] fitted = CurveFit.GaussianFit(hst.pointXs(), hst.pointYs(), FitMethod.Bisquare, iAmpl, iCentre, iSDev,
                                                       out ampl, out stats.level, out stats.sd, out res);
                cIdx = hst.indexByX(stats.level);
                if (cIdx == -1) throw new Exception("No level index found");
                log("ampl= " + ampl.ToString("G4") + "; level= " + stats.level.ToString("G4"));
                log("SDev= " + stats.sd.ToString("G4") + "; res= " + res.ToString("G4")); //+ "; cIdx= " + cIdx.ToString());
                int li = 0; int ri = hst.Count - 1;
                double lvl = (thresholdHisto / 100) * ampl;
                for (int i = cIdx; i < hst.Count; i++)
                {
                    if (hst[i].Y < lvl)
                    {
                        ri = i; break; 
                    }
                }
                for (int i = cIdx; i>0 ; i--)
                {
                    if (hst[i].Y < lvl)
                    {
                        li = i; break;
                    }
                }
                log("left Idx= " + li.ToString() + "; level= " + hst[li].X.ToString("G4"));   
                log("right Idx= " + ri.ToString() + "; level= " + hst[ri].X.ToString("G4"));
            
                for (int i = 0; i < md.Count; i++)
                {
                    if (Utils.InRange(md[i].Y, hst[li].X, hst[ri].X)) rslt.AddPoint(md[i].Y, md[i].X);
                }
                log("sp.mean= "+rslt.pointYs().Average().ToString("G3")+"; sp.SDev= "+rslt.pointSDevY(false).ToString("G3")); //+"%"
                log("src.Cnt= " + src.Count.ToString()+"; rslt.Cnt= "+rslt.Count.ToString());
                
                return rslt;    
                }
            catch (Exception e)

            {
                rslt.Clear();
                log("Error: " + e.Message);
                return rslt; 
            }
        }

        // same as above but params from GUI
        public DataStack statsByHistoExt(DataStack src, bool upper, out SingleStatStr stats)
        {
            return statsByHistoExt(src, ntbAmplThre.Value, ntbHistoThre.Value, chkCentralize.IsChecked.Value, upper, out stats);
        }

        public List<DataStack> SplitCycles(DataStack src) // split upper or lower result of statsByHisto
        {
            double intervalThre = 0.2; int minCount = 10;
            List<DataStack> rslt = new List<DataStack>();
            rslt.Add(new DataStack());
            for (int i=0; i<src.Count-1; i++)
            {
                if((src[i+1].X-src[i].X)<intervalThre) 
                {
                    rslt.Last().Add(src[i]);
                }
                else
                {
                    if (rslt.Last().Count < minCount) rslt.Last().Clear();
                    else rslt.Add(new DataStack());
                }                    
            }
            return rslt;
        } 

        public StatsStr statsByHisto()
        {
            StatsStr rslt;
            //if (Utils.isNull(OriginalWaveform)) 
            OriginalWaveform = Waveform.Clone();
            DataStack wu = statsByHistoExt(OriginalWaveform, true, out rslt.upper);
            DataStack wl = statsByHistoExt(OriginalWaveform, false, out rslt.lower);
            rslt.ok = (wu.Count>0) && (wl.Count>0);
            return rslt;
        }

        private double myResidue(DataStack src, double slope, double intercept)
        {
            double kai = 0;
            for (int i = 0; i < src.Count; i++)
            {
                kai += Math.Pow(src.pointYs()[i] - (src.pointXs()[i]*slope + intercept),2);
            }
            return Math.Sqrt(kai/ src.Count);
        }

        private StatsStr statsByLinRegr(DataStack src)
        {
            StatsStr mr, rslt; 
            double slope, intercept, residue;
            DataStack wu = statsByHistoExt(src, true, out mr.upper);
            CurveFit.LinearFit(wu.pointXs(), wu.pointYs(), FitMethod.LeastSquare, out slope, out intercept, out residue);
            rslt.upper.level = intercept; rslt.upper.sd = residue; ; // myResidue(src, slope, intercept);

            DataStack wl = statsByHistoExt(src, false, out mr.lower);
            CurveFit.LinearFit(wl.pointXs(), wl.pointYs(), FitMethod.LeastSquare, out slope, out intercept, out residue);
            rslt.lower.level = intercept; rslt.lower.sd = residue;
            rslt.ok = (wu.Count > 0) && (wl.Count > 0);
            return rslt;
        }
        #endregion

        public void reportRslt(StatsStr stats)
        {
            if (!stats.ok)
            {
                lbResult.Content = "Problem...!";
                return;
            }
            lbResult.Content = "Aver.SDev = " + (100 * stats.mean().sd / stats.mean().level).ToString("G3")+"%";
            lbLvlP.Content = "level+ = " + stats.upper.level.ToString("G4");
            lvDspP.Content = "SDev+ = " + stats.upper.sd.ToString("G4");
            lbLvlM.Content = "level- = " + stats.lower.level.ToString("G4");
            lvDspM.Content = "SDev- = " + stats.lower.sd.ToString("G4");
            Refresh();
        }

         private void btnShowDerv_Click(object sender, RoutedEventArgs e)
        {
            OriginalWaveform = Waveform.Clone();
            Waveform = CalcFirstDerv(Waveform);
            Refresh();
        }

        private void btnOriginal_Click(object sender, RoutedEventArgs e)
        {
            Waveform = OriginalWaveform.Clone();
            Refresh();
        }

        private void btnShowCleared_Click(object sender, RoutedEventArgs e)
        {
            if (Utils.isNull(OriginalWaveform)) OriginalWaveform = Waveform.Clone();
            StatsStr stats;
            Waveform = statsByHistoExt(OriginalWaveform, true, out stats.upper);
            DataStack wl = statsByHistoExt(OriginalWaveform, false, out stats.lower);
            stats.ok = (Waveform.Count > 0) && (wl.Count > 0);
            reportRslt(stats);
        }

        private void btnSimul_Click(object sender, RoutedEventArgs e)
        {
            Waveform.Clear(); SamplingPeriod = 0.001; double d, w;
            for (int i = 0; i < 20000; i++)
            {
                d = SamplingPeriod * i;
                /*w =  Math.Cos(6.28 * 10 *d) + Math.Cos(6.28 * 50 *d);
                for (int j = 1; j < 5; j++)
                {
                    w += Math.Cos(6.28 * 100 * j *d);
                }*/
                //Waveform.AddPoint(5*(0.5 * Math.Cos(6.28 * 30 * d) + Math.Cos(6.28 * 2 * d)), d);

                Waveform.AddPoint(Math.Exp(d),d);
            }
            Refresh();
        }

        private void graphOverview_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ((Graph)sender).ResetZoomPan();
        }

        private void btnLinRegr_Click(object sender, RoutedEventArgs e)
        {
            if (Utils.isNull(OriginalWaveform)) OriginalWaveform = Waveform.Clone();
            /*  StatsStr stats = statsByLinRegr(OriginalWaveform);
              reportRslt(stats);*/
         /*    double v, va,vb, axel;
            Waveform.Clear();
            for (int i = 1; i < OriginalWaveform.Count - 1; i++)
            {

                v = (OriginalWaveform[i + 1].Y - OriginalWaveform[i - 1].Y) / (2 * SamplingPeriod);
                va = (OriginalWaveform[i].Y - OriginalWaveform[i - 1].Y) / SamplingPeriod;
                vb = (OriginalWaveform[i + 1].Y - OriginalWaveform[i].Y) / SamplingPeriod;
                axel = (vb - va) / SamplingPeriod;
         
                AddPoint(axel, OriginalWaveform[i].X);
            } 
           feList.Clear(); afeList.Clear(); 
            List<double> posList = new List<double>();
            double[] iniCond = { 0.0, 0.0 };
            double[] velArr = SignalProcessing.Differentiate(OriginalWaveform.pointYs(), SamplingPeriod, iniCond, iniCond, DifferentiationMethod.Forward);
            double[] accelArr = SignalProcessing.Differentiate(velArr, SamplingPeriod, iniCond, iniCond, DifferentiationMethod.Forward);
            for (int i = 4; i < velArr.Length - 4; i++)
            {
                AddPoint(accelArr[i], SamplingPeriod*i);             
            } */
            double Xmin = Math.Max(((AxisDouble)graphScroll.Axes[0]).Range.Minimum, Waveform[0].X);
            double Xmax = Math.Min(((AxisDouble)graphScroll.Axes[0]).Range.Maximum, Waveform[Waveform.Count-2].X);

            int X0 = Waveform.indexByX(Xmin);
            int X1 = Waveform.indexByX(Xmax);

            if((X0 == -1) || (X1 == -1)) throw new Exception("Problem with X range of scroll graph");
            Console.WriteLine(X0.ToString() + " / " + X1.ToString());
            DataStack partial = new DataStack();
            
            for (int i = X0; i < X1; i++)
            {
                partial.Add(Waveform[i]);
            }
            tbLog.AppendText("top part: M= " + partial.pointYs().Average().ToString("G3") + "; SD= " + partial.pointSDevY(false).ToString("G3")+ 
                                " ("+partial.pointSDevY(true).ToString("G3") + "%)\n");
        }

        private void btnCpyPic_Click(object sender, RoutedEventArgs e)
        {
            Button btn = (Button)sender; Graph graph = null;
            if(sender == btnCpyPic1) graph = graphOverview;
            if(sender == btnCpyPic2) graph = graphPower;
            if (sender == btnCpyPic3) graph = graphHisto;
            Rect bounds = LayoutInformation.GetLayoutSlot(tabSecPlots);//graph ); 
            var bitmap = new RenderTargetBitmap( (int)bounds.Width, (int)bounds.Height, 96, 96, PixelFormats.Pbgra32 ); 
            bitmap.Render(tabSecPlots);
            Clipboard.SetImage(bitmap);
            Utils.TimedMessageBox("The image is in the clipboard");
        }
    }
}
