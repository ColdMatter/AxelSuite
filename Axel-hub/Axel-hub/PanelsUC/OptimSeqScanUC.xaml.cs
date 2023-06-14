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
using NationalInstruments.Analysis.Dsp.Filters;
using UtilsNS;
using System.IO;
using Newtonsoft.Json;

namespace Axel_hub.PanelsUC
{
    
    /// <summary>
    /// Interaction logic for OptimSeqScan.xaml
    /// </summary>
    public partial class OptimSeqScanUC : UserControl, IOptimization
    {
        public OptimSeqScanUC()
        {
            InitializeComponent();
            state = optimState.idle;
        }
        public string objFunc { get; set; }
        public JsonSeqReport jsr;
        #region Common
        public Dictionary<string, double> opts { get; set; }        
               
        public void Init(Dictionary<string, double> _opts, bool report_enabled)
        {                       
            if (Utils.isNull(_opts)) return;
            opts = new Dictionary<string, double>(_opts);
            if (opts.Count.Equals(0)) return;
            if (opts.ContainsKey("numSGdegree")) numSGdegree.Value = Convert.ToInt32(opts["numSGdegree"]);
            if (opts.ContainsKey("numSGframe"))  numSGframe.Value =  Convert.ToInt32(opts["numSGframe"]);
            if (opts.ContainsKey("numIters"))    numIters.Value =    Convert.ToInt32(opts["numIters"]);
            //IEnumerable<RadixNumericTextBoxInt32> collection = mainGrid.Children.OfType<RadixNumericTextBoxInt32>();            
            jsr = new JsonSeqReport();
            jsr.enabled = report_enabled; 
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
            if (jsr.enabled)
            {               
                jsr.objFunc = objFunc;
                string json = JsonConvert.SerializeObject(jsr);
                System.IO.File.WriteAllText(jsr.folder+"data.json", "dt =" + Environment.NewLine + json);
                File.Copy(Utils.configPath + "seq_template.RPR\\" + "seq_optim_report.htm", jsr.folder + "seq_optim_report.htm");
                Utils.TimedMessageBox("Report saved in  " + jsr.folder);
            }
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
            int idx = 0; int stIdx = 0; double d = -1e6;
            if (lastIter) stIdx = ni - np;
            for (int i = stIdx; i < ni; i++)
            {
                if (optims[i].Item3 > d) { d = optims[i].Item3; idx = i; }
            }
            int iw = idx / np; // iteration winner
            int iws = iw * np; 
            if (lastIter) ss = "> iteration #"+(ni/np).ToString() + " with max obj.function = "+d.ToString("G5");
            else
            {
                for (int i = iws; i < (iws + np); i++)
                {
                    ss += optims[i].Item1.ToString()+" = " +optims[i].Item2.ToString() + "; "; 
                }
                ss = ss.Substring(0, ss.Length - 1);
            }
            return ss;
        }
        List<Tuple<string,double,double>> optims = new List<Tuple<string, double, double>>(); // log of results

        public void Optimize(bool? start, List<baseMMscan> _scans, Dictionary<string, double> opts)      
        {
            if (Utils.isNull(start)) // SingleScan
            {
                if (state.Equals(optimState.cancelRequest)) return;
                if (state.Equals(optimState.paused)) { log("Err: Wrong state order.", false); return; }
                int ni = optims.Count(); // if (ni.Equals(0)) { log("Err: no optims.", false); return; }
                int np = scans.Count(); if (np.Equals(0)) { log("Err: no scans.", false); return; }
                int ip = ni % np; // index of param inside ii iteration
                int ii = Convert.ToInt32(Math.Floor((double)(ni / np))); // iteration index
                if (ip.Equals(0) && (optims.Count() != 0)) log(report(true), false);
                if (ni.Equals(np*numIters.Value)) 
                { 
                    state = optimState.idle;
                    jsr.optim_final = report(false);
                    OnEndOptim(new OptimEventArgs("", Double.NaN, jsr.optim_final)); return; 
                }
                actScan = scans[ip]; state = optimState.running;
                jsr.iters[ii, ip] = new IterSeqReport(); jsr.iters[ii, ip].iter_N = ii;
                proc = SingleScan(actScan, ref jsr.iters[ii,ip]);
                if (state.Equals(optimState.cancelRequest)) { state = optimState.idle; return; }
                Point pt = maximum(proc); if (!Double.IsNaN(pt.X)) crsMaxProc.AxisValue = pt.X;
                if (bcbPause.Value) { state = optimState.paused; return; }
                OnParamSet(new OptimEventArgs(actScan.sParam, pt.X, "optimized"));
                //OnTakeAShot(new OptimEventArgs(actScan.sParam, pt.X, ""));
                log("param<"+ actScan.sParam +">/ max obj.function = " + pt.X.ToString("G5") + " / " + pt.Y.ToString("G5"), false);
                optims.Add(new Tuple<string, double, double>(actScan.sParam, pt.X, pt.Y));
                if (jsr.enabled)
                {
                    jsr.iters[ii, ip].raw_pic = Utils.timeName() + "_raw.png"; jsr.iters[ii, ip].SG_pic = Utils.timeName() + "_SG.png";
                    ImgUtl.VisualCompToPng(graphScan, jsr.folder + jsr.iters[ii, ip].raw_pic);
                    ImgUtl.VisualCompToPng(graphProc, jsr.folder + jsr.iters[ii, ip].SG_pic);
                    jsr.iters[ii, ip].optim_prm_value = pt.X; jsr.iters[ii, ip].optim_objectFunc = pt.Y;
                }                
                Optimize(null, null, null); // recursive till state == optimState.cancelRequest
            }
            else
            {
                if ((bool)start) // optimization begin
                {
                    bcbPause.Value = false;
                    state = optimState.running;
                    scans = new List<baseMMscan>();
                    foreach (baseMMscan mms in _scans)
                        scans.Add(new baseMMscan(mms.getAsString()));
                    optims.Clear(); graphProc.Data[1] = null;
                    // reporting
                    if (jsr.enabled)
                    {
                        jsr.dateTime = DateTime.Now.ToString("yy-MM-dd_H-mm-ss");
                        jsr.folder = Utils.dataPath + jsr.dateTime + ".SEQ\\";
                        if (!Directory.CreateDirectory(jsr.folder).Exists)
                            throw new Exception("Cannot create report directory: " + Utils.dataPath + jsr.dateTime);                           
                        jsr.SG_degree = numSGdegree.Value;
                        jsr.SG_sidepoints = numSGframe.Value;
                        jsr.nIters = numIters.Value;
                        jsr.prms = new string[scans.Count];
                    }
                    for (int i=0; i<scans.Count; i++)
                    {
                        jsr.prms[i] = scans[i].sParam;
                    }
                    jsr.objFunc = objFunc;
                    jsr.iters = new IterSeqReport[jsr.nIters, scans.Count];  
                    Optimize(null, null, null); 
                }
                else state = optimState.cancelRequest;
            }                                  
        }
        #endregion Common

        List<Point> raw, proc;
        public List<Point> SingleScan(baseMMscan scan, ref IterSeqReport isr)
        {           
            isr.param = scan.sParam;
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
            if (pr.Count.Equals(0)) { log("Err: Savitzky-Golay filter problem", false); return raw; }
            procXaxis.Range = new Range<double>(pr[0].X, pr[pr.Count-1].X);
            graphProc.Data[1] = pr; Utils.DoEvents(); Thread.Sleep(200);
            isr.raw_data = raw.ToArray(); isr.SG_data = pr.ToArray();
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
            double[] ra = SavitzkyGolay.Filter(ds.pointYs(), numSGdegree.Value, numSGframe.Value);
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
                log("param/max obj.function = " + pt.X.ToString("G5") + " / " + pt.Y.ToString("G5"), false);
                optims.Add(new Tuple<string, double, double>(actScan.sParam, pt.X, pt.Y));

                Optimize(null, null, null);
            }
        }

    }
    public class IterSeqReport
    {
        public int iter_N { get; set; } // first index of iters[,]
        public string param { get; set; } // corresponds to second index of iters[,]
        public string raw_pic { get; set; } // only the filename
        public Point[] raw_data { get; set; }
        public string SG_pic { get; set; } // only the filename
        public Point[] SG_data { get; set; }

        public double optim_prm_value { get; set; }
        public double optim_objectFunc { get; set; }
    }
    public class JsonSeqReport
    {
        [JsonIgnore]
        public bool enabled { get; set; }
        [JsonIgnore]
        public string folder { get; set; }
        public string dateTime { get; set; }
        public string[] prms { get; set; }
        public string objFunc { get; set; }
        public int nIters { get; set; }

        public int SG_degree { get; set; }
        public int SG_sidepoints { get; set; }

        public IterSeqReport[,] iters { get; set; } // size is nIters x prms.Length 

        public string optim_final { get; set; }
    }
}
