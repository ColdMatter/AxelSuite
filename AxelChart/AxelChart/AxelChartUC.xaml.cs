﻿using NationalInstruments.Net;
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
using System.Threading;
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
using System.Threading.Tasks.Dataflow;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UtilsNS;

namespace AxelChartNS
{
    #region async file logger
    /// <summary>
    /// Async data storage device 
    /// first you set the full path of the file, if - not, it will save in data dir under date-time filename
    /// when you want the logging to start you set Enabled to true
    /// at the end you set Enabled to false (that will flush the buffer to HD)
    /// </summary>
    public class AutoFileLogger 
    {
        public string header = ""; // that will be put as a file first line with # in front of it
        List<string> buffer;
        public int bufferLimit = 2560;
        public int bufferSize { get { return buffer.Count; } }
        public bool writing { get; private set; }
        public bool missingData { get; private set; }
        Stopwatch stw;

        public AutoFileLogger(string Filename = "")
        {
            _AutoSaveFileName = Filename;
            buffer = new List<string>();
            stw = new Stopwatch();
        }

        public int log(List<string> newItems)
        {
            if (!Enabled) return buffer.Count;
            buffer.AddRange(newItems);
            if (buffer.Count > bufferLimit) Flush();
            return buffer.Count;
        }

        public int log(string newItem)
        {
            if (!Enabled) return buffer.Count;
            buffer.Add(newItem);
            if (buffer.Count > bufferLimit) Flush();
            return buffer.Count;
        }

        private void ConsoleLine(string txt)
        {
            #if DEBUG
            Console.WriteLine(txt);
            #endif
        }
 
        public Task Flush() // do not forget to flush when exit (OR switch Enabled Off)
        {
            if (buffer.Count == 0) return null;
            string strBuffer = ""; 
            for (int i=0; i<buffer.Count; i++)
            {
                strBuffer += buffer[i] + "\n";
            }           
            buffer.Clear();
            ConsoleLine("0h: " + stw.ElapsedMilliseconds.ToString());
            var task = Task.Run(() => FileWriteAsync(AutoSaveFileName, strBuffer, true));
            return task;
        }

        private async Task FileWriteAsync(string filePath, string messaage, bool append = true)
        {
            try 
            {
                using (FileStream stream = new FileStream(filePath, append ? FileMode.Append : FileMode.Create, FileAccess.Write,
                                                                                               FileShare.None, 65536, true))
                using (StreamWriter sw = new StreamWriter(stream))
                {
                    writing = true;
                    ConsoleLine("1k: "+stw.ElapsedMilliseconds.ToString());
                    await sw.WriteAsync(messaage);
                    ConsoleLine("2p: " + stw.ElapsedMilliseconds.ToString());
                    writing = false;
                }
            }
            catch (IOException e)
            {
                Console.WriteLine(">> IOException - "+e.Message);
                missingData = true; 
            }
        }

        private bool _Enabled = false;
        public bool Enabled 
        {
            get { return _Enabled; }       
            set
            {
                if (value == _Enabled) return;
                if (value && !_Enabled) // when it goes from false to true
                {
                    string dir = "";
                    if (!_AutoSaveFileName.Equals("")) dir = Directory.GetParent(_AutoSaveFileName).FullName;
                    if (!Directory.Exists(dir)) 
                        _AutoSaveFileName = Utils.dataPath + DateTime.Now.ToString("yy-MM-dd_H-mm-ss") + ".ahf"; //axel hub file

                    string hdr = "";
                    if (header != "") hdr = "# " + header+"\n";
                    var task = Task.Run(() => FileWriteAsync(AutoSaveFileName, hdr, false));

                    task.Wait();
                    writing = false;
                    missingData = false;
                    stw.Start();
                    _Enabled = true;
                }
                if (!value && _Enabled) // when it goes from true to false
                {
                    while (writing)
                    {
                        Thread.Sleep(100);
                    }
                    Task task = Flush();
                    if(task != null) task.Wait();
                    if (missingData) System.Windows.MessageBox.Show("Some data maybe missing from the log");
                    stw.Reset();
                    header = "";
                    _Enabled = false;
                }   
             }     
        }
        
        private string _AutoSaveFileName = "";
        public string AutoSaveFileName
        {
            get 
            {
                return _AutoSaveFileName;
            }
            set 
            { 
                if(_Enabled) throw new Exception("Logger.Enabled must be Off when you set AutoSaveFileName.");
                _AutoSaveFileName = value; 
            }
        }
    }
    #endregion

    #region DataStack - data storage for AxelChart
    /// <summary>
    /// You (developer) need to set TimeMode and one of SizeLimit or TimeLimit
    /// TimeMode is about the way DataStack limits its size
    /// The output is from standart List method ToArray in order to set DataSource of Graph
    /// </summary>
    public class DataStack : List<System.Windows.Point>
    {
        public DataStack(bool stackMode = true): base() 
        {
            _stackMode = stackMode;
            TimeSeriesMode = false;
            Depth = 1000;
            stopWatch = new Stopwatch();
            logger = new AutoFileLogger();
        }
        public string rem { get; set; }
        public Dictionary<string, double> RefFileStats;
        private int visualCounter = 0;
        public int visualCountLimit = -1;
        public int generalIdx = 0; // for adding doubles only !!! 

        private bool _stackMode = false;
        public bool StackMode
        {
            get { return _stackMode; }
            set { _stackMode = value; }
        }

        public AutoFileLogger logger;
        public Stopwatch stopWatch;

        public delegate void RefreshHandler();
        public event RefreshHandler DoRefresh;

        protected void OnRefresh()
        {
            if (DoRefresh != null) DoRefresh();
        }

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
        public int Depth 
        { 
            get; set; 
        }
        
        public bool TimeSeriesMode
        {
            get; set; 
         }

        public int Fit2Limit()
        {
            if ((Depth <= 0) || (Depth > 1E6)) throw new Exception("Invalid SizeLimit in SizeMode (TimeMode = false)");
            while (Count > Depth)
                RemoveAt(0);
            return Count;
        }

        public new void Clear()
        {
            base.Clear();
            generalIdx = 0;
            rem = "";
            System.GC.Collect();
        }

        private void OnAddPoint(int pnt_count = 1)
        {
            if (StackMode)
            {
                Fit2Limit();
            }
            if (visualCountLimit == -1) return; // skip Refreshing
            visualCounter += pnt_count;
            if (visualCounter >= visualCountLimit)
            {
                visualCounter = 0;
                OnRefresh();
            }
        }

        public new int Add(Point pnt) 
        {
            base.Add(pnt);
            if (logger.Enabled) logger.log(pnt.X.ToString("G6")+'\t'+pnt.Y.ToString("G5"));  
            OnAddPoint(1);
            return Count;
        }

        public int AddPoint(double Y, double X = double.NaN) 
        {
            double x = X;
            if (double.IsNaN(X))
            {
                x = generalIdx; generalIdx += 1;
                TimeSeriesMode = false;
            }
            return Add(new Point(x, Y));
        }

        public new int AddRange(List<Point> pnts)
        {
            base.AddRange(pnts);
            if (logger.Enabled)
                for (int i = 0; i < pnts.Count; i++)
                {
                    logger.log(pnts[i].X.ToString("G6") + '\t' + pnts[i].Y.ToString("G5")); 
                }
            OnAddPoint(pnts.Count);
            return Count;
        }

        public List<Point> CopyEach(int each) // skip some points for visual speed
        {
            List<Point> pntsList;
            if (each < 2)
            {
                pntsList = this.Clone() as List<Point>;
                return pntsList;
            }
            pntsList = new List<Point>(); 
            for( int i = 0; i < Count; i = i + each)
            {
                pntsList.Add(this[i]);
            }
            return pntsList;
        }

        public DataStack Clone(double offsetX = 0, double offsetY = 0)
        {
            DataStack rslt = new DataStack(StackMode);
            for (int i = 0; i < Count; i++)
                rslt.Add(new Point(this[i].X + offsetX, this[i].Y + offsetY));
            rslt.TimeSeriesMode = TimeSeriesMode;
            rslt.Running = Running;
            rslt.Depth = Depth;
            return rslt;
        }

        public Point First
        {
            get { return this[0]; }
        }
        public Point Last
        {
            get {  return this[Count - 1]; }
        }

        public int indexByX(double X, bool smart = true)
        {
            int idx = -1;
            if ((Count == 0) || !Utils.InRange(X, First.X, Last.X)) return idx;
            if (smart && (Last.X > First.X))// assuming equidistant and increasing seq.
            {
                double prd = (Last.X - First.X) / Count;
                return (int)Math.Round((X - First.X) / prd);
            }
            for (int i = 0; i < Count; i++)
            {
                if (this[i].X >= X)
                {
                    idx = i;
                    break;
                }
            }
            return idx;     
        }

        public bool statsByIdx(int FromIdx, int ToIdx, out double Mean, out double stDev)
        {
            Mean = double.NaN; stDev = double.NaN;
            if (!Utils.InRange(FromIdx, 0, Count) || !Utils.InRange(ToIdx, 0, Count) || (FromIdx >= ToIdx)) return false;
            double[] arr = new double[ToIdx - FromIdx +1];
            for (int i=FromIdx; i<=ToIdx; i++)
            {
                arr[i-FromIdx] = this[i].Y; 
            }
            Mean = arr.Average();
            stDev = Statistics.StandardDeviation(arr); // correspond to STDEV.P from Excel
            return true;
        }

        public bool statsByTime(double startingPoint, double duration, out double Mean, out double stDev) 
            // moment is last, duration is backwards
        {
            Mean = double.NaN; stDev = double.NaN;
            //if (!TimeSeriesMode) return false;
            if(startingPoint > Last.X) return false;
            double moment;
            if (double.IsNaN(startingPoint)) moment = Last.X; // the last recorded - make sense for short buffers
            else moment = startingPoint;
            int ToIdx = indexByX(moment); if (ToIdx == -1) return false;
            int FromIdx = indexByX(moment-duration); if (FromIdx == -1) return false;
            return statsByIdx(FromIdx, ToIdx, out Mean, out stDev);
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
                Add(new Point(newXs[i], CurveFit.SplineInterpolation(cln.pointXs(), cln.pointYs(), secondDev, newXs[i])));
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
                Add(new Point(X, Y));
                j++;
            }
            TimeSeriesMode = !((int)Math.Round((Last.X - First.X) / Count) == 1);
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
        public AxelChartClass()
        {
            InitializeComponent();
            Waveform = new DataStack(true);
            Waveform.Depth = (int)seStackDepth.Value;

            Running = false;
            tabSecPlots.SelectedIndex = 1;
            //
            lblRange.Content = "Vis.Range = " + curRange.ToString() + " pnts";
            Waveform.DoRefresh += new DataStack.RefreshHandler(Refresh);
            //rbPoints.IsChecked = true;
            Waveform.TimeSeriesMode = !rbPoints.IsChecked.Value;

            Refresh();
        }
        public int GetStackDepth() { return (int)seStackDepth.Value; }

        public void Clear(bool andRefresh = true) 
        {
            Waveform.Clear();
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
                Waveform.logger.header = value;
                if (String.IsNullOrEmpty(value)) lbInfo.Content = "Info: ";
                else lbInfo.Content = "Info: " + value;
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
             /*   if ((value == string.Empty) || (value == "[sec]"))
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
                }*/
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
                    Clear();
                    btnPause.Visibility = Visibility.Visible;
                    Waveform.TimeSeriesMode = !rbPoints.IsChecked.Value;
                    if (chkVisualUpdate.IsChecked.Value) Waveform.visualCountLimit = (int)seStackDepth.Value;
                    else Waveform.visualCountLimit = -1;
                    Waveform.Depth = (int)seStackDepth.Value;
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

        private List<Point> rescaleX(List<Point> pntsIn)
        {
            if (rbPoints.IsChecked.Value)
            {
                if (Waveform.TimeSeriesMode) throw new Exception("Wrong (time series) mode for natural point series");
                return Waveform.Clone() as List<Point>;
            }
            // internally x must be in sec
            if (!Waveform.TimeSeriesMode) throw new Exception("Wrong (natural) mode for time series");
            if (rbSec.IsChecked.Value) return Waveform.Clone() as List<Point>;
            
            List<Point> pntsOut = Waveform.Clone() as List<Point>;
            double factor = 1;
            if (rbMiliSec.IsChecked.Value) factor = 1000;
            if (rbMicroSec.IsChecked.Value) factor = 1000000;
            for (int i = 0; i < Waveform.Count; i++)
            {
                pntsOut.Add(new Point(factor * pntsIn[i].X, pntsIn[i].Y));
            }
            return pntsOut;
        }

        private int curRange = 256;
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
                graphScroll.Data[0] = null; graphOverview.Data[0] = null; graphPower.Data[0] = null; graphHisto.Data[0] = null;
                return;
            }
            //Console.WriteLine("refresh at " + Waveform.stopWatch.Elapsed.Seconds.ToString());
            List<Point> pA, pB;
            pA = Waveform.CopyEach((int)seShowFreq.Value);
            pB = rescaleX(pA);

            Waveform.TimeSeriesMode = !rbPoints.IsChecked.Value;
            if (Waveform.TimeSeriesMode)
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
            else
            {
                double x = Waveform[Waveform.Count-1].X; 
                ((AxisDouble)graphScroll.Axes[0]).Range = new Range<double>(x - curRange, x);
            }
            Application.Current.Dispatcher.BeginInvoke(
              DispatcherPriority.ApplicationIdle, new Action(() => { graphScroll.Data[0] = pB; }));
            double[] Ys;
            List<System.Windows.Point> pl = new List<System.Windows.Point>();
            switch (tabSecPlots.SelectedIndex) 
            {
                case 0: if(!Utils.isNull(graphOverview.Data[0])) graphOverview.Data[0] = null; // disable
                        break; 
                case 1: graphOverview.Data[0] = pB;
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
                            pl.Add(new Point(i * df, ps[i])); 
                        }                        
                        graphPower.Data[0] = pl;
                        break;
                case 3: graphHisto.Data[0] = Histogram(Waveform);
                        break;
                case 4: break; // opts / stats
            }
            DoEvents();
            if (btnPause.Foreground == Brushes.Black) btnPause.Foreground = Brushes.Navy;
            else btnPause.Foreground = Brushes.Black;;
            while (pauseFlag) 
            {
                DoEvents();
                System.Threading.Thread.Sleep(10);
            }
        }
  
        private double defaultRowRatio = 0;
        private double hiddenHeight = 28;

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
            lblRange.Content = "Vis.Range = " + curRange.ToString() + " pnts";
            Refresh();
        }

        private void btnZoomIn_Click(object sender, RoutedEventArgs e)
        {
            if (curRange > 2) curRange = (int)(curRange / 2);
            lblRange.Content = "Vis.Range = " + curRange.ToString() + " pnts";
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
            dlg.DefaultExt = ".ahf"; // Default file extension
            dlg.Filter = "Axel Hib File (.ahf)|*.ahf|"+"Axel Boss File (.abf)|*.abf"; // Filter files by extension

            // Show save file dialog box
            Nullable<bool> result = dlg.ShowDialog();

            // Process save file dialog box results
            if (result == true)
            {
                // Save file                
                Waveform.Save(dlg.FileName);
               // lbInfo.Content = "Saved: " + dlg.FileName; 
            }
        }

        public void Open(string fn)
        {
            if (!File.Exists(fn)) throw new Exception("File <" + fn + "> does not exist.");
            //Clear();
            string ext = System.IO.Path.GetExtension(fn);
            if (ext.Equals(".abf") || ext.Equals(".ahf"))
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
                if (String.IsNullOrEmpty(remoteArg))
                {
                    SamplingPeriod = (Waveform.Last.X - Waveform.First.X) / Waveform.Count;
                }
                else
                {
                    Dictionary<string, object> remotePrms = JsonConvert.DeserializeObject<Dictionary<string, object>>(remoteArg);
                    SamplingPeriod = (double)remotePrms["SamplingPeriod"];
                }
                Waveform.TimeSeriesMode = !(Utils.InRange(SamplingPeriod, 0.99, 1.01));  // sampling with 1 Hz is reserved for 
                if (Waveform.TimeSeriesMode) 
                {
                    if (!rbSec.IsChecked.Value && !rbMiliSec.IsChecked.Value && !rbMicroSec.IsChecked.Value) rbSec.IsChecked = true;
                }
                rbPoints.IsChecked = !Waveform.TimeSeriesMode;

              //  tbLog.AppendText("Count= " + Waveform.Count.ToString() + "\n");
              //  tbLog.AppendText("Sampling period= " + SamplingPeriod.ToString("G3") + "\n");
              //  tbLog.AppendText("^^^^^^^^^^^^^^^^^^^\n");
            }
         //OriginalWaveform = Waveform.Clone();
        }

        private void btnOpen_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.FileName = ""; // Default file name
            dlg.DefaultExt = ".ahf"; // Default file extension
            dlg.Filter = "Axel Hub File (.ahf)|*.ahf|" + "Axel Boss File (.abf)|*.abf"; ; // Filter files by extension

            // Show save file dialog box
            Nullable<bool> result = dlg.ShowDialog();

            // Process save file dialog box results
            if (result == true)
            {
                // Open file                
                Open(dlg.FileName);
                Refresh();
                //lbInfo.Content = "Opened: " + dlg.FileName; 
            }
        }
        #endregion
        //+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        private void graphOverview_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ((Graph)sender).ResetZoomPan();
        }

        public void btnCpyPic_Click(object sender, RoutedEventArgs e)
        {
            Graph graph = null; Rect bounds; RenderTargetBitmap bitmap;
            if (sender is Button)
            {
                Button btn = sender as Button;
                if (sender == btnCpyPic1) graph = graphOverview;
                if (sender == btnCpyPic2) graph = graphPower;
                if (sender == btnCpyPic3) graph = graphHisto;
                if (Utils.isNull(graph)) return;
                bounds = LayoutInformation.GetLayoutSlot(tabSecPlots);//graph ); 
                bitmap = new RenderTargetBitmap((int)bounds.Width, (int)bounds.Height, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(tabSecPlots);
                Clipboard.SetImage(bitmap);
                Utils.TimedMessageBox("The image is in the clipboard");
            }
            if (sender is Graph)
            {
                graph = (sender as Graph);
                if (Utils.isNull(graph)) return;
                bounds = LayoutInformation.GetLayoutSlot(graph);
                bitmap = new RenderTargetBitmap((int)bounds.Width, (int)bounds.Height, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(graph);
                Clipboard.SetImage(bitmap);
                Utils.TimedMessageBox("The image is in the clipboard");
            }
        }

        public DataStack Histogram(DataStack src, int numBins = 100)
        {
            if (src.Count == 0) throw new Exception("No data for the histogram");
            DataStack rslt = new DataStack();
            double[] centerValues, Ys;
            Ys = src.pointYs();
            int[] histo = Statistics.Histogram(Ys, Ys.Min(), Ys.Max(), numBins, out centerValues);
            if (histo.Length != centerValues.Length) throw new Exception("histogram trouble!");
            for (int i = 0; i < histo.Length; i++)
            {
                rslt.Add(new Point(centerValues[i], histo[i]));
            }
            return rslt;
        }

        private void seStackDepth_ValueChanged(object sender, ValueChangedEventArgs<double> e)
        {
            if ((seStackDepth == null) || (Waveform == null)) return;
            Waveform.Depth = (int)seStackDepth.Value;
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            //Clear(true);
            double m, d;
            Waveform.statsByTime(4,0.9,out m,out d);
            MessageBox.Show(m.ToString() + " / " + d.ToString());
        }

        private void graphOverview_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                if (e.Key == Key.C) 
                { btnCpyPic_Click(sender, null); }
            }
        }
    }
}
