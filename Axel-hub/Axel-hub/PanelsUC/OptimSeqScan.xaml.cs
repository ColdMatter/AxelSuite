using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using NationalInstruments.Controls;
using UtilsNS;

namespace Axel_hub.PanelsUC
{
    
    /// <summary>
    /// Interaction logic for OptimSeqScan.xaml
    /// </summary>
    public partial class OptimSeqScan : UserControl, IOptimization
    {
        public OptimSeqScan()
        {
            InitializeComponent();
            state = optimState.idle;
        }
        #region Common
        public Dictionary<string, double> opts { get; set; }
        public void Init(Dictionary<string, double> _opts)
        {                       
            if (Utils.isNull(_opts)) return;
            opts = new Dictionary<string, double>(_opts);
            if (opts.Count.Equals(0)) return;
            if (opts.ContainsKey("numSGdegree")) numSGdegree.Value = Convert.ToInt32(opts["numSGdegree"]);
            if (opts.ContainsKey("numSGframe"))  numSGframe.Value =  Convert.ToInt32(opts["numSGframe"]);
            if (opts.ContainsKey("numIters"))    numIters.Value =    Convert.ToInt32(opts["numIters"]);
            //IEnumerable<RadixNumericTextBoxInt32> collection = mainGrid.Children.OfType<RadixNumericTextBoxInt32>();
        }
        public void Final()
        {
            if (Utils.isNull(opts)) opts = new Dictionary<string, double>();
            opts["moduleIdx"] = 0;
            opts["numSGdegree"] = numSGdegree.Value;
            opts["numSGframe"] = numSGframe.Value; 
            opts["numIters"] = numIters.Value;
        }

        #region Events
        public event EventHandler ParamSetEvent;
        protected virtual void OnParamSet(OptimEventArgs e)
        {
            ParamSetEvent?.Invoke(this, e);
        }
        public event CostEventHandler TakeAShotEvent;
        protected virtual double OnTakeAShot(OptimEventArgs e)
        {
            if (Utils.isNull(TakeAShotEvent)) return Double.NaN;
            return TakeAShotEvent.Invoke(this, e);
        }
        public event EventHandler LogEvent;
        protected virtual void OnLog(OptimEventArgs e)
        {
            LogEvent?.Invoke(this, e);
        }
        public void log(string txt, bool detail)
        {
            OnLog(new OptimEventArgs("", detail ? -1: 1, txt));
        }
        public event EventHandler EndOptimEvent;
        protected virtual void OnEndOptim(OptimEventArgs e)
        {
            EndOptimEvent?.Invoke(this, e);
        }
        #endregion Events
        public optimState state { get; set; }
        public List<baseMMscan> scans { get; set; }
        public baseMMscan actScan { get; set; }
        public string report(bool lastIter)
        {
            string ss = "";
            int ni = optims.Count(); int np = scans.Count(); int ip = ni % np;
            if (ni.Equals(0)) return "";
            int idx = 0; int stIdx = 0; Point pt; double d = -1e6;
            if (lastIter) stIdx = ni - np;
            for (int i = stIdx; i < ni; i++)
            {
                if (optims[i].Item3 > d) { d = optims[i].Item3; idx = i; }
            }
            int iw = idx / np; // iteration winner
            int iws = iw * np; 
            if (lastIter) ss = "> iteration #"+(ni/np).ToString() + " with max cost = "+d.ToString("G5");
            else
            {
                for (int i = iws; i < (iws + np); i++)
                {
                    ss += "  "+optims[i].Item2.ToString() + ";"; 
                }
                ss = ss.Substring(0, ss.Length - 1);
            }
            return ss;
        }
        List<Tuple<string,double,double>> optims = new List<Tuple<string, double, double>>(); // log of results

        public void Optimize(bool? start, List<baseMMscan> _scans, Dictionary<string, double> opts)      
        {
            if (Utils.isNull(start))
            {
                if (state.Equals(optimState.cancelRequest)) return;
                if (state.Equals(optimState.paused)) { log("Err: Wrong state order.", false); return; }
                int ni = optims.Count(); int np = scans.Count(); int ip = ni % np; Point pt;  
                if (ip.Equals(0) && (optims.Count() != 0)) log(report(true), false);
                if (ni.Equals(np*numIters.Value)) 
                { 
                    state = optimState.idle;
                    OnEndOptim(new OptimEventArgs("", Double.NaN, report(false))); return; 
                }
                actScan = scans[ip]; state = optimState.running; 
                proc = SingleScan(actScan);
                if (state.Equals(optimState.cancelRequest)) { state = optimState.idle; return; }
                pt = maximum(proc); if (!Double.IsNaN(pt.X)) crsMaxProc.AxisValue = pt.X;
                if (bcbPause.Value) { state = optimState.paused; return; }
                OnParamSet(new OptimEventArgs(actScan.sParam, pt.X, "optimized"));
                log("param/max cost = " + pt.X.ToString("G5") + " / " + pt.Y.ToString("G5"), false);
                optims.Add(new Tuple<string, double, double>(actScan.sParam, pt.X, pt.Y));
                
                Optimize(null, null, null);
            }
            else
            {
                if ((bool)start)
                {
                    bcbPause.Value = false;
                    state = optimState.running;
                    scans = new List<baseMMscan>();
                    foreach (baseMMscan mms in _scans)
                        scans.Add(new baseMMscan(mms.getAsString()));
                    optims.Clear(); graphProc.Data[1] = null;
                    Optimize(null, null, null);
                }
                else state = optimState.cancelRequest;
            }                                  
        }
        #endregion Common

        List<Point> raw, proc;
        public List<Point> SingleScan(baseMMscan scan)
        {
            raw = new List<Point>(); 
            OnParamSet(new OptimEventArgs(scan.sParam, scan.sFrom, "scanning..."));
            scanXaxis.Range = new Range<double>(scan.sFrom, scan.sTo);
            
            double r, d = scan.sFrom;
            while ((d < scan.sTo * 1.0001) && !state.Equals(optimState.cancelRequest))
            {
                r = OnTakeAShot(new OptimEventArgs(scan.sParam, d, ""));
                raw.Add(new Point(d, r)); graphScan.Data[0] = raw;
                //double eps = Math.Abs(prm.sTo - prm.sFrom) * 0.0001; bool bb = Utils.InRange(d, prm.sFrom * 0.999, prm.sTo * 1.0001);
                d += scan.sBy;
            }
            OnParamSet(new OptimEventArgs(scan.sParam, scan.sFrom, "scanning..."));
            var pr = SGfilter(raw);
            if (pr.Count.Equals(0)) { log("Err: Savitzky-Gloay filter problem", false); return raw; }
            procXaxis.Range = new Range<double>(pr[0].X, pr[pr.Count-1].X);
            graphProc.Data[1] = pr; Utils.DoEvents();

            return pr;
        }
        private Point maximum(List<Point> ps)
        {
            Point pt = new Point(Double.NaN, -1e6);
            foreach (Point pn in ps)
            {
                if (pn.Y > pt.Y) pt = new Point(pn.X,pn.Y);
            }
            return pt;
        }
        // filters MS-NI
        // https://zone.ni.com/reference/en-XX/help/372636F-01/mstudiowebhelp/html/eedecfb6/
        public List<Point> SGfilter(List<Point> ps)
        {
            List<Point> r = new List<Point>();
            DataStack ds = new DataStack(); ds.AddRange(ps);
            if (numSGdegree.Value > (2 * numSGframe.Value +1)) { log("Err: increase Sidepoints", false); return r; }
            if (ds.Count==0) { log("Err: No data points to S-G filter", false); return r; }
            if (ds.Count < (2 * numSGframe.Value + 1)) { log("Err: Too few data points to S-G filter", false); return r; }
            double[] ra = NationalInstruments.Analysis.Dsp.Filters.SavitzkyGolay.Filter(ds.pointYs(), numSGdegree.Value, numSGframe.Value);
            if (ra.Length != ps.Count) { log("Err: vector sizes mismatch", false); return r; }
            for (int i = 0; i < ps.Count; i++)
                r.Add(new Point(ps[i].X, ra[i]));
            return r;
        }
        private void numSGdegree_ValueChanged(object sender, ValueChangedEventArgs<int> e)
        {
            if (Utils.isNull(bcbPause)) return;
            if (!state.Equals(optimState.paused)) return;
            proc = SGfilter(raw);
            if (proc.Count.Equals(0)) return;
            procXaxis.Range = new Range<double>(proc[0].X, proc[proc.Count-1].X);
            graphProc.Data[1] = proc; Utils.DoEvents();
            Point pt = maximum(proc); crsMaxProc.AxisValue = pt.X;
        }

        private void bcbPause_Click(object sender, RoutedEventArgs e)
        {
            bcbPause.Value = !bcbPause.Value;
            if (!bcbPause.Value && state.Equals(optimState.paused))
            {
                state = optimState.running;
                Point pt = maximum(proc); if (!Double.IsNaN(pt.X)) crsMaxProc.AxisValue = pt.X;
                OnParamSet(new OptimEventArgs(actScan.sParam, pt.X, "optimized"));
                log("param/max cost = " + pt.X.ToString("G5") + " / " + pt.Y.ToString("G5"), false);
                optims.Add(new Tuple<string, double, double>(actScan.sParam, pt.X, pt.Y));

                Optimize(null, null, null);
            }
        }

    }
}
