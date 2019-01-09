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
using NationalInstruments.Controls.Primitives;
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
            random = new Random((int)(DateTime.Now.Ticks & 0xFFFFFFFF));
            Clear();
            Thread.Sleep(1); 
        }
        private Random random;
        public string rem { get; set; }
        public string lastError { get; set; }
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
        public event RefreshHandler OnRefresh;

        protected void RefreshEvent()
        {
            if (OnRefresh != null) OnRefresh();
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
            rem = ""; lastError = "";
            if(Depth <= 1) Depth = 1000;
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
                RefreshEvent();
            }
        }

        public new int Add(Point pnt) 
        {
            base.Add(pnt); 
            logger.log((logger.stw.ElapsedMilliseconds/1000.0).ToString("F1")+"\t"+pnt.X.ToString("G6")+'\t'+pnt.Y.ToString("G5"));  
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

        public int AddRange(List<Point> pnts)
        {
            base.AddRange(pnts);
            for (int i = 0; i < pnts.Count; i++)
            {
                logger.log(pnts[i].Y.ToString("G6") + '\t' + pnts[i].X.ToString("G5")); 
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

        public DataStack TimePortion(double fromTime, double toTime)
        {
            DataStack rslt = new DataStack();
            rslt.TimeSeriesMode = TimeSeriesMode;
            rslt.Running = Running;
            if (Count == 0) return rslt;

            int ti0 = indexByX(fromTime); int ti1 = indexByX(toTime);
            if (ti0 == -1 || ti1 == -1)
            {
                Console.WriteLine("Index problem in TimePortion "+fromTime.ToString("G5")+" / "+toTime.ToString("G5"));
                return rslt;
                //throw new Exception("Index problem in TimePortion");
            }                  
            for (int i = ti0; i <= ti1; i++)
            {
                if (Utils.InRange(i, 0, Count - 1)) rslt.Add(new Point(this[i].X, this[i].Y));
            }
            return rslt;
        }

        public DataStack Portion(int lastNPoints, int backFrom = 0)
        {
            DataStack rslt = new DataStack(lastNPoints);           
            rslt.TimeSeriesMode = TimeSeriesMode;
            rslt.Running = Running;
            if (Count == 0) return rslt;
            int bf = (Utils.InRange(backFrom,1,Count - 1)) ? backFrom : Count - 1 ;
            int k = Math.Max(0, bf - lastNPoints);
            for (int i = k; i < k + lastNPoints; i++)
            {
                if(Utils.InRange(i,0,Count-1)) rslt.Add(new Point(this[i].X, this[i].Y));
            }                
            return rslt;
        }

        public double[,] ExportToArray() // first index - point data, second one -  X/Y index
        {
            double[,] da = new double[Count, 2];
            for (int i = 0; i < Count; i++)
            {
                da[i,0] = this[i].X; da[i,1] = this[i].Y;
            }
            return da;
        }

        public bool ImportFromArray(double[,] da) // first index - point data, second one -  X/Y index
        {
            if (!da.GetLength(1).Equals(2)) throw new Exception("Wrong array size (1)");
            int k = da.GetLength(0);
            if (k < 2) throw new Exception("Wrong array size (0)");
            Clear();
            for (int i = 0; i < k; i++)
            {
                Add(new Point(da[i, 0],da[i, 1]));
            }
            return true;
        }

        public Point First
        {
            get { return this[0]; }
        }
        public Point Last
        {
            get { return this[Count - 1]; }
        }

        public int indexByX(double X, bool smart = true)
        {
            int idx = -1;
            if (Count == 0) return idx; 
            if (!Utils.InRange(X, First.X, Last.X)) return idx;
            int j = 0;
            if (smart && (Last.X > First.X))// assuming equidistant and increasing seq.
            {
                double prd = (Last.X - First.X) / (Count-1);
                j = (int)Math.Round((X - First.X) / prd);
                if (j < 0) j = 0;
            }
            for (int i = j-2; i < Count; i++)
            {
                if (this[i].X >= X)
                {
                    idx = i;
                    break;
                }
            }
            return Utils.EnsureRange(idx,-1,Count-1);     
        }

        public bool statsByIdx(int FromIdx, int ToIdx, bool weightMean, out double Mean, out double stDev) // WgMean - triangle weight (is callibration valid ?)
        {
            Mean = double.NaN; stDev = double.NaN;
            if (Count == 0) return false;
            int fromIdx = Utils.EnsureRange(FromIdx, 0, Count - 2); int toIdx = Utils.EnsureRange(ToIdx, 1, Count - 1);     
            if (fromIdx >= toIdx) return false;
            int j, len = toIdx - fromIdx + 1;
            double a, b, w = 0;
            double[] arr = new double[len];
            for (int i=fromIdx; i<=toIdx; i++)
            {
                j = i-fromIdx;
                if (weightMean)
                {
                    if( j < (len/2)) 
                    {
                        a = 4 / len; b = 0;
                    }
                    else 
                    {
                        a = - 4 / len; b = 4;
                    }
                    w += (a * j + b) * this[i].Y;  
                }
                else
                {
                    arr[j] = this[i].Y;
                }
            }
            if (weightMean) { Mean = w / (2 * len); }
            else { Mean = arr.Average(); }
            stDev = Statistics.StandardDeviation(arr); // correspond to STDEV.P from Excel
            return true;
        }

        public bool statsByTime(double endOfTimeInterval, double duration, bool weightMean, out double Mean, out double stDev) 
            // startingPoint is the later edge of time interval, duration is backwards (from late to earlier)
        {
            Mean = double.NaN; stDev = double.NaN; lastError = "";
            if (Count == 0)   
            {
                lastError = "No data"; return false;
            }               
            if ((double.IsNaN(endOfTimeInterval)) && (double.IsNaN(duration))) // the whole thing
            {
                return statsByIdx(0, Count - 1, weightMean, out Mean, out stDev);
            }
            if (endOfTimeInterval > Last.X)
            {
                lastError = "the time interv. goes out of limit";
                return false;
            }
                
            double moment;
            if (double.IsNaN(endOfTimeInterval)) moment = Last.X; // the last recorded - make sense for short buffers
            else moment = endOfTimeInterval;            
            int ToIdx = indexByX(moment);
            if (ToIdx == -1)
            {
                lastError = "no rigth edge index > " + moment.ToString("G6") + "\r" + " <" + First.X.ToString("G6") + "<->" + Last.X.ToString("G6")+">";
                return false;
            }

            if (double.IsNaN(duration))
            {
                lastError = "no duration argument";
                return false;
            }
                                
            int FromIdx = indexByX(moment-duration);
            if (FromIdx == -1)             
            {
                lastError = "no left edge index > " + (moment - duration).ToString("G6") + "\r" + " <" + First.X.ToString("G6") + "<->" + Last.X.ToString("G6") + ">";
                return false;
            }
            bool bl = statsByIdx(FromIdx, ToIdx, weightMean, out Mean, out stDev);
            if (!bl) lastError = "error in statsByIdx method";
            return bl;
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

        public Point pointSDev(bool relativeY = false) // in %
        {
            Point pnt = new Point();
            pnt.X = Statistics.StandardDeviation(pointXs());
            pnt.Y = Statistics.StandardDeviation(pointYs());
            //double sumOfSquaresOfDifferences = pointYs().Select(val => (val - average) * (val - average)).Sum();
            //double disp = Math.Sqrt(sumOfSquaresOfDifferences / pointYs().Length);
            if (relativeY) pnt.Y = 100 * pnt.Y / Math.Abs(pointYs().Average());
            return pnt;            
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
        public void fillSamples(int n)
        {
            Clear();
            for (int i = 0; i < n; i++) AddPoint(random.NextDouble());
        }
 
        #region File operations in DataStack
        // standard tab separated x,y file 
        public bool OpenPair(string fn, ref GroupBox header, int rm = 1) // header - for rolling index during reading (can be null); rm - RollMean
        {
            bool rslt = true;
            Clear();
            // TODO: to implement Y-only file import
            int j = 0; rem = "";
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
                if (line.Contains("#Rem="))
                {
                    rem = line.Substring(5); 
                }
                if (line[0].Equals('#')) continue; //skip comments/service info
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
        
        public void SavePair(string fn, string rem = "", string format = "")
        {
            System.IO.StreamWriter file = new System.IO.StreamWriter(fn);
            if (rem != "") file.WriteLine("#Rem=" + rem);
            for (int i = 0; i < Count; i++ )
                file.WriteLine(this[i].X.ToString(format) + "\t" + this[i].Y.ToString(format));
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
            MEMS2 = new DataStack(DataStack.maxDepth);

            SetValue(RunningProperty, false);
            tabSecPlots.SelectedIndex = 1;
            //
            lblRange.Content = "Vis.Range = " + curRange.ToString() + " pnts";
            Waveform.OnRefresh += new DataStack.RefreshHandler(Refresh);
            
            Waveform.TimeSeriesMode = !rbPoints.IsChecked.Value;
            MEMS2.TimeSeriesMode = Waveform.TimeSeriesMode;
            resultStack = new DataStack(1000); resultStack.visualCountLimit = -1;
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
            chkChartUpdate.IsChecked = _genModes.ChartUpdate;
            chkTblUpdate.IsChecked = _genModes.TblUpdate;
            nbPowerCoeff.Value = _genModes.PowerCoeff;
            if(genOptions.saveVisuals) rowScroll.Height = new GridLength(_genModes.TopOfTopFrame);
            genModes = _genModes;
        }

        public DataStack MEMS2;
        public DataStack resultStack;
        public int GetStackDepth() { return (int)seStackDepth.Value; }

        public double convertV2mg(double V, bool M2 = false)
        {
            if (M2) return ((V - 2.5+0.056) / 3)*1000; // [mg]
            else return V / (6000.12 * 1.235976e-6);
        }

        public void Clear() 
        {
            if(!Utils.isNull(Waveform)) Waveform.Clear(); 
            if(!Utils.isNull(MEMS2)) MEMS2.Clear(); 
            if(!Utils.isNull(resultStack)) resultStack.Clear();

            graphScroll.Data[0] = null; graphOverview.Data[0] = null; graphPower.Data[0] = null; graphHisto.Data[0] = null; graphHisto.Data[1] = null;
            lboxGaussFit.Items.Clear();
            grpData.Header = "Data";
            tbRemFile.Text = "";
            lbInfo.Content = "Info: ";
            if (curRange > 1024) curRange = 1024;
            chkWindowMode.IsChecked = false; chkVisWindow_Checked(null, null);
        }

        public void SetInfo(string info = "")
        {
            Waveform.logger.header = info;
            lbInfo.Content = "Info: " + info;
        }

        public void SetWaveformDepth(int depth)
        {
            if(seStackDepth.Value < (depth*1.2)) seStackDepth.Value = (int)(depth*1.2);
        }

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
                    Waveform.visualCountLimit = (int)seStackDepth.Value;
                    Waveform.Depth = (int)seStackDepth.Value;

                }
                else
                {
                    btnPause.Visibility = Visibility.Hidden;
                    Refresh(null, null);
                }
                if (Running == value) return;
                bool bb = value && chkResultsLog.IsChecked.Value;
                if (bb || !resultStack.logger.Enabled) resultStack.logger.AutoSaveFileName = ""; // reset name to a new log file
                resultStack.logger.Enabled = bb;
                if (value)
                {
                    if (genOptions.MemsChannels == 2) resultStack.logger.log("#time\tMEMS\tMEMS2");
                    else resultStack.logger.log("#time\tMEMS\tStDsp");
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
            if (chkTblUpdate.IsChecked.Value && (tabSecPlots.SelectedIndex == 4)) // opts / stats
            {
                double mn,mn2 = 0, dsp,dsp2 = 0; bool bb,bc = true;
                //mn = Waveform.pointYs().Average(); dsp = Waveform.pointSDevY();
                if (numTimeSlice.Value < 0) bb = Waveform.statsByIdx(Waveform.Count - 1001, Waveform.Count - 1, false, out mn, out dsp);
                else bb = Waveform.statsByTime(double.NaN, numTimeSlice.Value / 1000, false, out mn, out dsp);
                if (genOptions.MemsChannels == 2)
                {
                    if (numTimeSlice.Value < 0) bc = MEMS2.statsByIdx(Waveform.Count - 1001, Waveform.Count - 1, false, out mn2, out dsp2);
                    else bc = MEMS2.statsByTime(double.NaN, numTimeSlice.Value / 1000, false, out mn2, out dsp2);
                }
                if(bb && bc)
                {
                    Application.Current.Dispatcher.BeginInvoke(
                        DispatcherPriority.Background,
                        new Action(() =>
                        {
                            ListBoxItem lbi;                            
                            double k = 1000;
                            lbi = (ListBoxItem)lbMean.Items[0]; lbi.Content = (k * mn).ToString("G7");
                            lbi = (ListBoxItem)lbStDev.Items[0]; lbi.Content = (k * dsp).ToString("G7"); // mV

                            k = 1e6 / 6000.12;
                            lbi = (ListBoxItem)lbMean.Items[1]; lbi.Content = (k * mn).ToString("G7");
                            lbi = (ListBoxItem)lbStDev.Items[1]; lbi.Content = (k * dsp).ToString("G7"); // uA

                            lbi = (ListBoxItem)lbMean.Items[2]; lbi.Content = convertV2mg(mn).ToString("G7");
                            lbi = (ListBoxItem)lbStDev.Items[2]; lbi.Content = convertV2mg(dsp).ToString("G7"); // mg

                            resultStack.AddPoint(convertV2mg(mn)); 
                            lbi = (ListBoxItem)lbMean.Items[3]; lbi.Content = "# " + resultStack.Count.ToString();
                            lbi = (ListBoxItem)lbStDev.Items[3]; lbi.Content = (Waveform.stopWatch.ElapsedMilliseconds / 1000.0).ToString("G5"); //resultStack.pointSDev().Y.ToString("G7"); // # and StDev
                            
                            if (genOptions.MemsChannels == 2)
                            {
                                lbi = (ListBoxItem)lbMean.Items[4]; lbi.Content = convertV2mg(mn2, true).ToString("G7");
                                lbi = (ListBoxItem)lbStDev.Items[4]; lbi.Content = convertV2mg(dsp2, true).ToString("G7"); // mg2
                            }
                            else
                            {
                                lbi = (ListBoxItem)lbMean.Items[4]; lbi.Content = "NaN"; 
                                lbi = (ListBoxItem)lbStDev.Items[4]; lbi.Content = "NaN"; // mg2
                            }
                            numTimeSlice.Background = Brushes.White;
                        }));
                }
                else
                {
                    numTimeSlice.Background = Brushes.Red;
                }
                lbErrorStatus.Content = "Error status: "+Waveform.lastError;    
            }
            if (!chkChartUpdate.IsChecked.Value) return;
            //Console.WriteLine("refresh at " + (Waveform.stopWatch.ElapsedMillseconds/1000.0).ToString());
            lblRange.Content = "Vis.Range = " + curRange.ToString() + " pnts";

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
                    case 2: double df;
                            if (SamplingPeriod == 0) SamplingPeriod = (Waveform.pointXs()[Waveform.Count - 1] - Waveform.pointXs()[0]) / Waveform.Count;
                            //Measurements.AmplitudePhaseSpectrum(Ys, false, SamplingPeriod , out ampl, out phase, out df); 
                            double[] ps = Measurements.AutoPowerSpectrum(Waveform.pointYs(), SamplingPeriod, out df);  
                            DataStack pl = new DataStack(DataStack.maxDepth);
                            int len = Math.Min(ps.Length, 300000); // limit the lenght for visual speed
                            for (int i = 1; i < len; i++) // skip DC component at position 0
                            {
                                if (ps[i] < 0.0) continue; // skip negatives
                                pl.Add(new Point(i * df, nbPowerCoeff.Value * Math.Sqrt(ps[i]))); 
                            }                        
                            graphPower.Data[0] = pl;
                            break;
                    case 3: graphHisto.Data[0] = Histogram(Waveform);
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
            Refresh();
        }

        private void btnZoomIn_Click(object sender, RoutedEventArgs e)
        {
            if (curRange > 2) curRange = (int)(curRange / 2);
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
                Waveform.SavePair(dlg.FileName, tbRemFile.Text);
                lbInfo.Content = "Saved: " + dlg.FileName; 
            }
        }

        public void Open(string fn)
        {
            if (!File.Exists(fn)) throw new Exception("File <" + fn + "> does not exist.");
            Clear();
            string ext = System.IO.Path.GetExtension(fn);
            //if (ext.Equals(".abf") || ext.Equals(".ahf"))
            {
                
                foreach (string line in File.ReadLines(fn))
                {
                    if (line.Contains("#{"))
                    {
                        SetInfo(line.Substring(1));                    
                    }
                    if (line.Contains("#Rem="))
                    {
                        tbRemFile.Text = line.Substring(5); 
                    }
                }
                if (!Waveform.OpenPair(fn, ref grpData, genModes.RollMean)) Utils.TimedMessageBox("Some data might be missing");
                grpData.Header = "Data pnts: " + Waveform.Count.ToString();
                string info = (string)lbInfo.Content; info = info.Replace("Info: ", "");
                if (String.IsNullOrEmpty(info))
                {
                    SamplingPeriod = (Waveform.Last.X - Waveform.First.X) / Waveform.Count;
                }
                else
                {
                    Dictionary<string, object> remotePrms = JsonConvert.DeserializeObject<Dictionary<string, object>>(info);
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
                lbInfo.Content = "Opened: " + dlg.FileName; 
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

        public DataStack Histogram(DataStack src, int numBins = 300)
        {
            if (src.Count == 0) throw new Exception("No data for the histogram");
            DataStack rslt = new DataStack(DataStack.maxDepth);
            double[] centerValues, Ys;
            Ys = src.pointYs();
            double mean = Ys.Average(); double stDev = src.pointSDev().Y;
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
            genModes.ChartUpdate = (bool)chkChartUpdate.IsChecked.Value;
            genModes.TblUpdate = (bool)chkTblUpdate.IsChecked.Value;
            genModes.PowerCoeff = nbPowerCoeff.Value;

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
            DataStack wave = Waveform.Clone();

            if (chkWindowMode.IsChecked.Value)
            {
                DataStack dt = new DataStack(ds.Depth);
                double low = Convert.ToDouble(rangeCursorHisto.ActualHorizontalRange.GetMinimum().ToString());
                double high = Convert.ToDouble(rangeCursorHisto.ActualHorizontalRange.GetMaximum().ToString());  
                foreach(Point pnt in ds)
                {
                    if (Utils.InRange(pnt.X, low, high)) dt.Add(pnt);
                }
                ds = dt.Clone();
                wave.Clear();
                foreach (Point pnt in Waveform)
                {
                    if (Utils.InRange(pnt.Y, low, high)) wave.Add(pnt);
                }
            }
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
            lbi = new ListBoxItem(); lbi.Content = "Raw Mean= " + wave.pointYs().Average().ToString(format); lbi.Foreground = Brushes.DarkGreen;
            lboxGaussFit.Items.Add(lbi);
            lbi = new ListBoxItem(); lbi.Content = "Raw SDev= " + wave.pointSDev().Y.ToString(format); lbi.Foreground = Brushes.DarkGreen;
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

        private void btnSplit_Click(object sender, RoutedEventArgs e)
        {
            DataStack stack = new DataStack(DataStack.maxDepth); int i = 0; bool bl;
            double thre = Convert.ToDouble(tbSplitLevel.Text);
            int loose = Convert.ToInt16(tbSplitEdges.Text); ;
            int front = -1; 
            foreach (Point p in Waveform)
            {  
                if(chkUpperSplit.IsChecked.Value) bl = (p.Y > thre);
                else bl = (p.Y < thre); 
                if (bl)
                {
                    if (front == -1) front = i;
                    if ((front > -1) && ((i - front) > loose))
                    {
                        stack.Add(p);
                    }
                }
                else
                {
                    if ((front > -1) && ((i - front) > loose))
                    {
                        front = -1;
                        for (int j = 0; j < loose; j++)
                            if (stack.Count>0)
                                stack.RemoveAt(stack.Count-1);
                    }
                }
                i += 1;
            }
            Waveform = stack.Clone();
            if (stack.Count == 0) Utils.TimedMessageBox("No data left !");
            Refresh();
        }

        private void chkVisualUpdate_Checked(object sender, RoutedEventArgs e)
        {
            seStackDepth_ValueChanged(sender, null);
        }

        private void btnExtractPart_Click(object sender, RoutedEventArgs e)
        {
            int highI = Waveform.indexByX(horAxisScroll.Range.Maximum);
            if (highI > -1) Waveform.RemoveRange(highI, Waveform.Count - highI);
            int lowI = Waveform.indexByX(horAxisScroll.Range.Minimum); 
            if (lowI > -1) Waveform.RemoveRange(0, lowI);
            Refresh();
        }

        private void chkVisWindow_Checked(object sender, RoutedEventArgs e)
        {
            if (chkWindowMode.IsChecked.Value) 
            {
                rangeCursorHisto.Visibility = System.Windows.Visibility.Visible;
                double low = ((AxisDouble)graphHisto.Axes[0]).Range.Minimum;
                double high = ((AxisDouble)graphHisto.Axes[0]).Range.Maximum;
                double rng = high - low;
                rangeCursorHisto.HorizontalRange = new Range<double>(low + 0.25*rng, high - 0.25*rng);
            }               
            else rangeCursorHisto.Visibility = System.Windows.Visibility.Collapsed;
        }

        private void splitter1_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!Utils.isNull(genModes))
                genModes.TopOfTopFrame = rowScroll.Height.Value;
        }

        private void splitter1_LayoutUpdated(object sender, EventArgs e)
        {
            if (!Utils.isNull(genModes))
                genModes.TopOfTopFrame = rowScroll.Height.Value;
        }

        private void UserControl_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5) Refresh(); 
        }

        private void nbPowerCoeff_ValueChanging(object sender, ValueChangingEventArgs<double> e)
        {
            seStackDepth_ValueChanged(null, null);
        }

        private void nbPowerCoeff_KeyUp(object sender, KeyEventArgs e)
        {
            seStackDepth_ValueChanged(null, null);
        }
    }
}
