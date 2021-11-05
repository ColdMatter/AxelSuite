using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
using NumUtils.NelderMeadSimplex;
using NationalInstruments.Controls;
using UtilsNS;

namespace Axel_hub.PanelsUC
{
     /// <summary>
    /// Interaction logic for OptimSimplexUC.xaml
    /// </summary>
    public partial class OptimSimplexUC : UserControl, IOptimization
    {
        public optimState state { get; set; }
        public List<baseMMscan> scans { get; set; }
        
        SimplexConstant[] initVals;
        ObjectiveFunctionDelegate objFunction;
        RegressionResult regResult;

        List<Point> iters, meta;
        List<List<Point>> srsPrms;
        /// <summary>
        /// set parameters from scns limited to scns sFrom and sTo
        /// and takes a shot; return signal
        /// </summary>
        /// <param name="scns"></param>
        /// <returns></returns>
        private double setPrmsAndShot(List<baseMMscan> scns) 
        {
            double[] vals = new double[scns.Count];
            for (int i = 0; i < scns.Count; i++)
                vals[i] = Utils.EnsureRange(scns[i].Value, scans[i].sFrom, scans[i].sTo);

            OptimEventArgs ex;
            for (int i = 1; i < scans.Count; i++)
            {                
                ex = new OptimEventArgs(scns[i].sParam, vals[i], "scanning");
                OnParamSet(ex);
            }
            ex = new OptimEventArgs(scans[0].sParam, vals[0], "scanning");
            return OnTakeAShot(ex);
        }

        private double _objFunction1(double[] prms)
        {
            if (prms.Length.Equals(0)) throw new Exception("No parameters to optimize");
            if (!prms.Length.Equals(scans.Count)) { log("Error: parameters mismatch", false); return 0; }
            double[] ps = new double[prms.Length];
            
            for (int i = 0; i < prms.Length; i++)
                ps[i] = Utils.EnsureRange(prms[i], scans[i].sFrom, scans[i].sTo);

            OptimEventArgs ex; string ss = "iters: " + iters.Count.ToString() + "; " + scans[0].sParam+"= " + ps[0].ToString("G6") + "; ";
            for (int i = 1; i < scans.Count; i++)
            {
                ex = new OptimEventArgs(scans[i].sParam, ps[i], "scanning"); scans[i].Value = ps[i];
                ss += scans[i].sParam + "= " + ps[i].ToString("G6") + "; ";
                OnParamSet(ex);
            }
            ex = new OptimEventArgs(scans[0].sParam, ps[0], "scanning"); scans[0].Value = ps[0];
            double rslt = OnTakeAShot(ex); // setPrmsAndShot(scans);
            log(ss + "  Obj.Value = "+rslt.ToString("G6"), true); 
            iters.Add(new Point(iters.Count, rslt)); graphScan.Data[0] = iters;
            if (prms.Length.Equals(scans.Count))
                for (int i = 0; i < prms.Length; i++)
                {
                    srsPrms[i].Add(new Point(iters.Count, ps[i]));
                    graphProc.Data[i] = srsPrms[i];
                }
            return -rslt; // reverse for minimum value
        }
        public OptimSimplexUC()
        {
            InitializeComponent();
            state = optimState.idle;
            srsPrms = new List<List<Point>>() ;
        }
        #region Common        
        public Dictionary<string, double> opts { get; set; }
        public void Init(Dictionary<string, double> _opts)
        {
            if (Utils.isNull(_opts)) return;
            opts = new Dictionary<string, double>(_opts);
            if (opts.Count.Equals(0)) return;
            objFunction = new ObjectiveFunctionDelegate(_objFunction1);
            if (opts.ContainsKey("numIters")) numIters.Value = Convert.ToInt32(opts["numIters"]);
            iters = new List<Point>(); meta = new List<Point>();
            regResult = null;
        }
        public void Final()
        {
            if (Utils.isNull(opts)) opts = new Dictionary<string, double>();
            opts["moduleIdx"] = 2;
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
            OnLog(new OptimEventArgs("", detail ? 1 : -1, txt));
        }
        public event EventHandler EndOptimEvent;
        protected virtual void OnEndOptim(OptimEventArgs e)
        {
            EndOptimEvent?.Invoke(this, e);
        }
        #endregion Events
        public string report(bool lastIter)
        {
            if (Utils.isNull(regResult)) return "No result yet";
            string ss = "termination: " + regResult.TerminationReason.ToString() + "; iters: " + iters.Count.ToString() +
                "; obj.value: " + (-regResult.ErrorValue).ToString("G6");
            return ss;
        }

        private void Clear()
        {
            iters.Clear(); srsPrms.Clear(); graphProc.Plots.Clear();
        }
        double scale = 5;
        public void Optimize(bool? start, List<baseMMscan> _scans, Dictionary<string, double> _opts) 
        {
            scans = new List<baseMMscan>();
            foreach (baseMMscan mms in _scans)
                scans.Add(new baseMMscan(mms.getAsString()));
            if ((bool)start)
            {
                state = optimState.running; Clear();
                initVals = new SimplexConstant[scans.Count];
                for (int i = 0; i < scans.Count; i++)
                {
                    srsPrms.Add(new List<Point>()); graphProc.Plots.Add(new Plot(scans[i].sParam));
                }
                    
                //scans[0].Value = 1; scans[1].Value = -1; scans[2].Value = 1; // for simulation

                for (int i = 0; i < scans.Count; i++)
                    initVals[i] = new SimplexConstant(scans[i].Value, 0.1 + scale*scans[i].Value);           
                double cp = _opts.ContainsKey("ConvPrec") ? _opts["ConvPrec"] : 0.1; opts["ConvPrec"] = cp;
                opts["numIters"] = numIters.Value;           
                regResult = NelderMeadSimplex.Regress(initVals, cp, numIters.Value, objFunction);
                for (int i = 0; i<scans.Count; i++)
                    OnParamSet(new OptimEventArgs(scans[i].sParam, Double.NaN, "optimized"));
            }
            else state = optimState.cancelRequest;

            OnEndOptim(new OptimEventArgs("", Double.NaN, report(false)));
        }
        #endregion Common

        private void numIters_ValueChanged(object sender, NationalInstruments.Controls.ValueChangedEventArgs<int> e)
        {
            if (Utils.isNull(opts)) return;
            opts["numIters"] = numIters.Value;
        }

    }
}
