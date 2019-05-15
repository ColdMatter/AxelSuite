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

using NationalInstruments.Analysis.Math;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UtilsNS;

namespace Axel_hub
{
    #region DataStack - data storage for AxelChart
    /// <summary>
    /// You (developer) need to set TimeMode and one of SizeLimit or TimeLimit
    /// TimeMode is about the way DataStack limits its size
    /// The output is from standart List method ToArray in order to set DataSource of Graph
    /// </summary>
    public class DataStack : List<System.Windows.Point>
    {
        public DataStack(int depth = 1000, string _prefix = "")
            : base() // -1 for Non-Stack modes
        {
            _stackMode = (depth > 0);
            TimeSeriesMode = false;
            if (StackMode) Depth = depth;
            else Depth = 1000;
            stopWatch = new Stopwatch();
            prefix = _prefix;
            logger = new FileLogger(prefix);
            random = new Random((int)(DateTime.Now.Ticks & 0xFFFFFFFF));
            Clear();
            Thread.Sleep(1);
        }
        private Random random;
        public string prefix { get; private set; }
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

        public FileLogger logger;
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
            if ((Depth <= 0) || (Depth > maxDepth)) throw new Exception("Error: Invalid Depth(" + Depth.ToString() + ") in StackMode!");
            while ((Count + 5) > Depth)
                RemoveRange(0, 10);
            return Count;
        }

        public new void Clear()
        {
            base.Clear();
            generalIdx = 0;
            rem = ""; lastError = "";
            if (Depth <= 1) Depth = 1000;
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
            logger.log((logger.stw.ElapsedMilliseconds / 1000.0).ToString("F1") + "\t" + pnt.X.ToString("G9") + '\t' + pnt.Y.ToString("G9"));
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
                logger.log(pnts[i].Y.ToString("G9") + '\t' + pnts[i].X.ToString("G9"));
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
            for (int i = 0; i < Count; i += each)
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
                Console.WriteLine("Index problem in TimePortion " + fromTime.ToString("G7") + " / " + toTime.ToString("G7"));
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
            int bf = Utils.EnsureRange(backFrom, 1, Count - 1);
            int k = Math.Max(0, bf - lastNPoints);
            for (int i = k; i < k + lastNPoints; i++)
            {
                if (Utils.InRange(i, 0, Count - 1)) rslt.Add(new Point(this[i].X, this[i].Y));
            }
            return rslt;
        }

        public double[,] ExportToArray() // first index - point data, second one -  X/Y index
        {
            double[,] da = new double[Count, 2];
            for (int i = 0; i < Count; i++)
            {
                da[i, 0] = this[i].X; da[i, 1] = this[i].Y;
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
                Add(new Point(da[i, 0], da[i, 1]));
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
                double prd = (Last.X - First.X) / (Count - 1);
                j = (int)Math.Round((X - First.X) / prd);
                if (j < 0) j = 0;
            }
            for (int i = j - 2; i < Count; i++)
            {
                if (this[i].X >= X)
                {
                    idx = i;
                    break;
                }
            }
            return Utils.EnsureRange(idx, -1, Count - 1);
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
            for (int i = fromIdx; i <= toIdx; i++)
            {
                j = i - fromIdx;
                if (weightMean)
                {
                    if (j < (len / 2))
                    {
                        a = 4 / len; b = 0;
                    }
                    else
                    {
                        a = -4 / len; b = 4;
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
                lastError = "no rigth edge index > " + moment.ToString("G6") + "\r" + " <" + First.X.ToString("G6") + "<->" + Last.X.ToString("G6") + ">";
                return false;
            }

            if (double.IsNaN(duration))
            {
                lastError = "no duration argument";
                return false;
            }

            int FromIdx = indexByX(moment - duration);
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
            double[] secondDev = CurveFit.SplineInterpolant(cln.pointXs(), cln.pointYs(), 0, 0);
            Clear();
            for (int i = 0; i < newXs.Length; i++)
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
            for (int i = 0; i < xs.Length; i++)
            {
                AddPoint(ys[i], xs[i]);
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
                    if (rm < 2) Add(new Point(X, Y));
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
            if (Count == 0) throw new Exception("No data !!!");
            TimeSeriesMode = !((int)Math.Round((Last.X - First.X) / Count) == 1);
            return rslt;
        }

        public void SavePair(string fn, string rem = "", string format = "")
        {
            System.IO.StreamWriter file = new System.IO.StreamWriter(fn);
            if (rem != "") file.WriteLine("#Rem=" + rem);
            for (int i = 0; i < Count; i++)
                file.WriteLine(this[i].X.ToString(format) + "\t" + this[i].Y.ToString(format));
            file.Close();
        }
        #endregion
    }
    #endregion

}
