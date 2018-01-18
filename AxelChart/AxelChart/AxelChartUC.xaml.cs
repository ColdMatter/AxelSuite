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
using OptionsTypeNS;

namespace AxelChartNS
{
    #region DataStack - data storage for AxelChart
    /// <summary>
    /// You (developer) need to set TimeMode and one of SizeLimit or TimeLimit
    /// TimeMode is about the way DataStack limits its size
    /// The output is from standart List method ToArray in order to set DataSource of Graph
    /// </summary>
    public class DataStack : List<System.Windows.Point>
    {
        public DataStack(int depth = 1000): base() // -1 for Non-Stack mode
        {
            _stackMode = (depth>0);
            TimeSeriesMode = false;
            if (StackMode) Depth = depth;
            else Depth = 1000;
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
        public const int maxDepth = 15000000;
        public int Depth { get; set; }
        
        public bool TimeSeriesMode { get; set; }

        public int Fit2Limit()
        {
            if ((Depth <= 0) || (Depth > maxDepth)) throw new Exception("Error: Invalid Depth("+Depth.ToString()+") in StackMode!");
            while ((Count+5) > Depth)
                RemoveRange(0,10);
            return Count;
        }

        public new void Clear()
        {
            base.Clear();
            generalIdx = 0;
            rem = "";
            Depth = maxDepth;
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

        public DataStack CopyEach(int each) // skip some points for visual speed
        {
            DataStack pntsList;
            if (each < 2)
            {
                pntsList = this.Clone() as DataStack;
                return pntsList;
            }
            pntsList = new DataStack(DataStack.maxDepth);
            for( int i = 0; i < Count; i += each)
            {
                pntsList.Add(this[i]);
            }
            return pntsList;
        }

        public DataStack Clone(double offsetX = 0, double offsetY = 0)
        {
            int dp;
            if (StackMode) dp = Depth;
            else dp = -1;
            DataStack rslt = new DataStack(dp);
            rslt.TimeSeriesMode = TimeSeriesMode;
            rslt.Running = Running;
            for (int i = 0; i < Count; i++)
                rslt.Add(new Point(this[i].X + offsetX, this[i].Y + offsetY));
            return rslt;
        }

        public DataStack Portion(int lastNPoints)
        {
            DataStack rslt = new DataStack(lastNPoints);
            rslt.TimeSeriesMode = TimeSeriesMode;
            rslt.Running = Running;
            int k = Math.Max(0, Count - lastNPoints);
            for (int i = k; i < Count; i++)
                rslt.Add(new Point(this[i].X, this[i].Y));
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
            if (Count == 0) return idx; 
            if (!Utils.InRange(X, First.X, Last.X)) return idx;
            if (smart && (Last.X > First.X))// assuming equidistant and increasing seq.
            {
                double prd = (Last.X - First.X) / Count;
                int j = (int)Math.Round((X - First.X) / prd);
                return Utils.EnsureRange(j, 0, Count - 1);   
            }
            for (int i = 0; i < Count; i++)
            {
                if (this[i].X >= X)
                {
                    idx = i;
                    break;
                }
            }
            return Utils.EnsureRange(idx,-1,Count-1);     
        }

        public bool statsByIdx(int FromIdx, int ToIdx, out double Mean, out double stDev)
        {
            Mean = double.NaN; stDev = double.NaN;
            if (!Utils.InRange(FromIdx, 0, Count-1) || !Utils.InRange(ToIdx, 0, Count-1) || (FromIdx >= ToIdx)) return false;
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
            if (Count == 0) return false;
            if ((double.IsNaN(startingPoint)) && (double.IsNaN(duration)))
            {
                return statsByIdx(0, Count-1, out Mean, out stDev);
            }
            double moment;
            if (double.IsNaN(startingPoint)) moment = Last.X; // the last recorded - make sense for short buffers
            else moment = startingPoint;
            if(startingPoint > Last.X) return false;
            int ToIdx = indexByX(moment); if (ToIdx == -1) return false;

            if (double.IsNaN(duration)) return false;
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
            double disp = Statistics.StandardDeviation(pointYs());
            //double sumOfSquaresOfDifferences = pointYs().Select(val => (val - average) * (val - average)).Sum();
            //double disp = Math.Sqrt(sumOfSquaresOfDifferences / pointYs().Length);
            if (relative) return 100 * disp / Math.Abs(pointYs().Average());
            return disp;            
        }

        public void importFromArrays(double[] xs, double[] ys)
        {
            Clear();
            if ((xs.Length == 0) || (ys.Length == 0) || (xs.Length != ys.Length))
            {
                Utils.TimedMessageBox("Incorrect data!", "Error: ", 2500);
                return;
            }
            Clear();
            for(int i=0; i<xs.Length; i++)
            {
                AddPoint(ys[i],xs[i]);
            }
        }
 
        #region File operations in DataStack
        // standard tab separated x,y file 
        public bool OpenPair(string fn, ref GroupBox header, int rm = 1) // rm - RollMean
        {
            bool rslt = true;
            Clear();
            // TODO: to implement Y-only file import
            int j = 0;
            double X, Y, od = 0;
            string[] pair;
            List<double> ld = new List<double>();

            foreach (string line in File.ReadLines(fn))
            {
                j++;
                if (((j % 1000) == 0) && (header != null))
                {
                    header.Header = "Reading: " + (j / 1000).ToString() + " k";
                    if (!Utils.isNull(Application.Current)) 
                       Application.Current.Dispatcher.Invoke(DispatcherPriority.Background,
                                                  new Action(delegate { }));
                }
                if (line[0] == '#') continue; //skip comments/service info
                pair = line.Split('\t');
                if (pair.Length < 2) continue;
                bool bx = (double.TryParse(pair[0], out X));
                bool by = (double.TryParse(pair[1], out Y));
                if (bx && by)
                {
                    if(rm < 2) Add(new Point(X, Y));
                    else
                    {
                        if (ld.Count == 0) od = X;
                        ld.Add(Y);
                        if (ld.Count == rm) 
                        {
                            Add(new Point(od, ld.Average()));
                            ld.Clear();
                        }                       
                    }
                }
                rslt = rslt && bx && by;                               
            }
            if(Count == 0) throw new Exception("No data !!!");
            TimeSeriesMode = !((int)Math.Round((Last.X - First.X) / Count) == 1);
            return rslt;
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
        public AxelChartClass( )
        {
            InitializeComponent();
            Waveform = new DataStack(DataStack.maxDepth);
            Waveform.Clear();

            Running = false;
            tabSecPlots.SelectedIndex = 1;
            //
            lblRange.Content = "Vis.Range = " + curRange.ToString() + " pnts";
            Waveform.DoRefresh += new DataStack.RefreshHandler(Refresh);
            
            Waveform.TimeSeriesMode = !rbPoints.IsChecked.Value;
            resultStack = new DataStack(DataStack.maxDepth);
            Refresh();
        }
        GeneralOptions genOptions = null;
        Modes genModes = null;
        public void InitOptions(ref GeneralOptions _genOptions, ref Modes _genModes)
        {
            genOptions = _genOptions;

            seRollMean.Value = _genModes.RollMean;
            seShowFreq.Value = _genModes.ShowFreq;
            seStackDepth.Value = _genModes.StackDepth;
            genModes = _genModes;
        }
        private DataStack resultStack;
        public int GetStackDepth() { return (int)seStackDepth.Value; }

        public void Clear() 
        {
            Waveform.Clear();
            graphScroll.Data[0] = null; graphOverview.Data[0] = null; graphPower.Data[0] = null; graphHisto.Data[0] = null; graphHisto.Data[1] = null;
            lboxGaussFit.Items.Clear();
            grpData.Header = "Data";
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
                resultStack.logger.Enabled = value && chkResultsLog.IsChecked.Value;
                if (value)
                {
                    if (resultStack.logger.Enabled) resultStack.logger.log("#StDev\tmean");
                    resultStack.Clear();
                } 
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

        public void DoEvents() // use it with caution (or better not), risk to introduce GUI freezing
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

        private DataStack rescaleX(DataStack pntsIn) 
        {
            // internally x must be in sec !!!
            if (rbSec.IsChecked.Value) return pntsIn;
            DataStack pntsOut = new DataStack(DataStack.maxDepth);
            if (rbPoints.IsChecked.Value)
            {
                if (!Waveform.TimeSeriesMode) return pntsIn;
                else
                {
                    for (int i = 0; i < pntsIn.Count; i++)
                    {
                        pntsOut.Add(new Point(i, pntsIn[i].Y));
                    }
                    return pntsOut;
                }
            }
            double factor = 1;
            if (rbMiliSec.IsChecked.Value) factor = 1000;
            if (rbMicroSec.IsChecked.Value) factor = 1000000;
            for (int i = 0; i < pntsIn.Count; i++)
            {
                pntsOut.Add(new Point(factor * pntsIn[i].X, pntsIn[i].Y));
            }
            return pntsOut;
        }

        private int curRange = 256; // number of points shown
        private const int maxVisual = 10000;
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
                Clear();
                return;
            }
            //Console.WriteLine("refresh at " + Waveform.stopWatch.Elapsed.Seconds.ToString());

            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                DataStack pA, pB;
                pA = Waveform.CopyEach((int)seShowFreq.Value);
                pB = rescaleX(pA);

                if (!rbPoints.IsChecked.Value) // time
                {
                    int k = 0;
                    if (rbSec.IsChecked.Value) k = 1;
                    if (rbMiliSec.IsChecked.Value) k = 1000;
                    if (rbMicroSec.IsChecked.Value) k = 1000000;

                    if (curRange >= pB.Count)
                    {
                        if (pB.Count > 0)
                            ((AxisDouble)graphScroll.Axes[0]).Range = new Range<double>(k * pB[0].X, k * (pB[0].X + curRange * SamplingPeriod));
                    }
                    else
                    {
                        double xEnd = pB[pB.Count - 1].X;
                        ((AxisDouble)graphScroll.Axes[0]).Range = new Range<double>(k * (xEnd - curRange * SamplingPeriod), k * xEnd);
                    }
                }
                else // points
                {
                    double xEnd = pB[pB.Count-1].X; 
                    ((AxisDouble)graphScroll.Axes[0]).Range = new Range<double>(Math.Max(0,xEnd - curRange), xEnd);
                }
                Application.Current.Dispatcher.BeginInvoke(
                  DispatcherPriority.Background, new Action(() => { graphScroll.Data[0] = pB; }));
            
                switch (tabSecPlots.SelectedIndex) 
                {
                    case 0: if(!Utils.isNull(graphOverview.Data[0])) graphOverview.Data[0] = null; // disable
                            break;
                    case 1: if (pB.Count > maxVisual) //overview
                            {
                                Utils.TimedMessageBox("The data length is too high (" + pB.Count.ToString() + "). Showing the last "+maxVisual.ToString()+" points.");
                                while (pB.Count > maxVisual) pB.RemoveAt(0);
                            }
                            graphOverview.Data[0] = pB;
                            break;
                    case 2: double[] Ys = Waveform.pointYs(); // 
                            double df;
                            double[] ps; //, ampl, phase; 
                            if (SamplingPeriod == 0) SamplingPeriod = (Waveform.pointXs()[99] - Waveform.pointXs()[0]) / 100;
                            //Measurements.AmplitudePhaseSpectrum(Ys, false, SamplingPeriod , out ampl, out phase, out df); 
                            ps = Measurements.AutoPowerSpectrum(Ys, SamplingPeriod, out df);  
                            DataStack pl = new DataStack(DataStack.maxDepth);
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
                    case 4: if (chkVisualUpdate.IsChecked.Value) // opts / stats
                            {
                                double mn; double dsp;
                                //mn = Waveform.pointYs().Average(); dsp = Waveform.pointSDevY();
                                if (Waveform.statsByTime(double.NaN, ntbTimeSlice.Value / 1000, out mn, out dsp))
                                {
                                    Application.Current.Dispatcher.BeginInvoke(
                                     DispatcherPriority.Background,
                                     new Action(() =>
                                     {
                                         lbMean.Items[0] = mn.ToString("G7"); lbStDev.Items[0] = dsp.ToString("G7"); // V
                                         double k = 1000;
                                         lbMean.Items[1] = (k * mn).ToString("G7"); lbStDev.Items[1] = (k * dsp).ToString("G7"); // mV
                                         k = 1e6 / 6000.12;
                                         lbMean.Items[2] = (k * mn).ToString("G7"); lbStDev.Items[2] = (k * dsp).ToString("G7"); // uA
                                         k = 1.235976e6 / 6000.12;
                                         lbMean.Items[3] = (k * mn).ToString("G7"); lbStDev.Items[3] = (k * dsp).ToString("G7"); // mg
                                         resultStack.AddPoint(k * mn, k * dsp);
                                         lbMean.Items[4] = "# " + resultStack.Count.ToString(); lbStDev.Items[4] = resultStack.pointSDevY().ToString("G7");
                                         ntbTimeSlice.Background = Brushes.White;
                                     }));
                                }
                                else ntbTimeSlice.Background = Brushes.Red;
                            }
                            break; 
                }
                //DoEvents();
                if (btnPause.Foreground == Brushes.Black) btnPause.Foreground = Brushes.Navy;
                else btnPause.Foreground = Brushes.Black;;
                while (pauseFlag) 
                {
                    DoEvents();
                    System.Threading.Thread.Sleep(10);
                }
            }
            finally
            {
                Mouse.OverrideCursor = null;
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
            Clear();
            string ext = System.IO.Path.GetExtension(fn);
            //if (ext.Equals(".abf") || ext.Equals(".ahf"))
            {
                remoteArg = "";
                foreach (string line in File.ReadLines(fn))
                {
                    if (line.Contains("#{"))
                    {
                        remoteArg = line.Substring(1);                    
                    }
                }
                if (!Waveform.OpenPair(fn, ref grpData, genModes.RollMean)) Utils.TimedMessageBox("Some data might be missing");
                grpData.Header = "Data pnts: " + Waveform.Count.ToString();
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
                
        }

        private void btnOpen_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.FileName = ""; // Default file name
            dlg.DefaultExt = ".ahf"; // Default file extension
            dlg.Filter = "Axel Hub File (.ahf)|*.ahf|" + "All files (*.*)|*.*"; ; // Filter files by extension

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

        public DataStack Histogram(DataStack src, int numBins = 200)
        {
            if (src.Count == 0) throw new Exception("No data for the histogram");
            DataStack rslt = new DataStack(DataStack.maxDepth);
            double[] centerValues, Ys;
            Ys = src.pointYs();
            double mean = Ys.Average(); double stDev = src.pointSDevY();
            int[] histo = Statistics.Histogram(Ys, mean - 2.5 * stDev, mean + 2.5 * stDev, numBins, out centerValues);
            if (histo.Length != centerValues.Length) throw new Exception("histogram trouble!");
            for (int i = 0; i < histo.Length; i++)
            {
                rslt.Add(new Point(centerValues[i], histo[i]));
            }
            return rslt;
        }

        private void seStackDepth_ValueChanged(object sender, ValueChangedEventArgs<double> e)
        {   
            if(Utils.isNull(genModes)) return;

            genModes.RollMean = (int)seRollMean.Value;
            genModes.ShowFreq = (int)seShowFreq.Value;
            genModes.StackDepth = (int)seStackDepth.Value;

            if (Utils.isNull(seStackDepth) || Utils.isNull(Waveform)) return;
            Waveform.Depth = (int)seStackDepth.Value;            
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            Clear();
        }

        private void graphOverview_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                if (e.Key == Key.C) 
                { btnCpyPic_Click(sender, null); }
            }
        }

        private void btnCpyDta3_Click(object sender, RoutedEventArgs e)
        {
            List<Point> ds = graphHisto.Data[0] as List<Point>;
            if (ds.Count == 0)
            {
                Utils.TimedMessageBox("No data to process.");
                return;
            }
            string ss = "";
            foreach (Point pnt in ds) 
            {
                ss += pnt.X.ToString("G5") + "\t" + pnt.Y.ToString("G5")+"\r";
            }
            Clipboard.SetText(ss);
            Utils.TimedMessageBox("The data is in the clipboard");
        }

        private void btnCpyDta2_Click(object sender, RoutedEventArgs e)
        {
            List<Point> ds = graphPower.Data[0] as List<Point>;
            if (ds.Count == 0)
            {
                Utils.TimedMessageBox("No data to process.");
                return;
            }
            string ss = "";
            foreach (Point pnt in ds)
            {
                ss += pnt.X.ToString("G5") + "\t" + pnt.Y.ToString("G5") + "\r";
            }
            Clipboard.SetText(ss);
            Utils.TimedMessageBox("The data is in the clipboard");
        }

        private void btnGaussFit_Click(object sender, RoutedEventArgs e)
        {
            if ((graphHisto.Data[0] as DataStack) == null) 
            {
                Utils.TimedMessageBox("No data to process");
                return;
            }
            DataStack ds = (graphHisto.Data[0] as DataStack);            
            double[] wg = new double[ds.Count];
            for (int i = 0; i < wg.Length; i++) wg[i] = 1;
            double ampl,center,stdDev,res;
            double[] fitYs = CurveFit.GaussianFit(ds.pointXs(), ds.pointYs(), FitMethod.Bisquare, wg, 0, out ampl, out center, out stdDev, out res);

            lboxGaussFit.Items.Clear();
            string format = "G7";
            ListBoxItem lbi = new ListBoxItem(); lbi.Content = "Center= " + center.ToString(format); lbi.Foreground = Brushes.Navy;
            lboxGaussFit.Items.Add(lbi);
            lbi = new ListBoxItem(); lbi.Content = "stdDev= " + stdDev.ToString(format); lbi.Foreground = Brushes.Navy;
            lboxGaussFit.Items.Add(lbi);
            lbi = new ListBoxItem(); lbi.Content = "Amplitude= " + ampl.ToString(format); lbi.Foreground = Brushes.Navy;
            lboxGaussFit.Items.Add(lbi);
            lbi = new ListBoxItem(); lbi.Content = "Residuals= " + res.ToString(format); lbi.Foreground = Brushes.Navy;
            lboxGaussFit.Items.Add(lbi);
            lbi = new ListBoxItem(); lbi.Content = "Raw Mean= " + Waveform.pointYs().Average().ToString(format); lbi.Foreground = Brushes.DarkGreen;
            lboxGaussFit.Items.Add(lbi);
            lbi = new ListBoxItem(); lbi.Content = "Raw SDev= " + Waveform.pointSDevY().ToString(format); lbi.Foreground = Brushes.DarkGreen;
            lboxGaussFit.Items.Add(lbi);

            DataStack fit = new DataStack(DataStack.maxDepth);
            fit.importFromArrays(ds.pointXs(), fitYs);
            graphHisto.Data[1] = fit;
        }

        private void horAxisScroll_RangeChanged(object sender, ValueChangedEventArgs<Range<double>> e)
        {
            Range<double> oldVal = e.OldValue;
            Range<double> newVal = e.NewValue;            
        }
     }
}
