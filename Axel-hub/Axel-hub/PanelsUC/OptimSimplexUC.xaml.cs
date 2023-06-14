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
using Cureos.Numerics.Optimizers;
using NationalInstruments.Controls;
using UtilsNS;
using System.IO;
using Newtonsoft.Json;
using Path = System.IO.Path;

namespace Axel_hub.PanelsUC
{
     /// <summary>
    /// Interaction logic for OptimSimplexUC.xaml
    /// </summary>
    public partial class OptimSimplexUC : UserControl, IOptimization
    {
        private bool simulation = Utils.TheosComputer(); // local
        enum OptimMethod { NedlerMead, Powell} 
        OptimMethod optimMethod
        {
            get
            {
                switch (cbOptimMethod.SelectedIndex)
                {
                    case 1:  return OptimMethod.Powell;
                    default: return OptimMethod.NedlerMead;
                }
            }
        } 
        public optimState state { get; set; }
        public List<baseMMscan> scans { get; set; }
        public string objFunc { get; set; }
        public JsonSimplexReport jsr;

        SimplexConstant[] initVals;
        ObjectiveFunctionDelegate NMobjFunction;
        BobyqaObjectiveFunctionDelegate PowellObjFunction;
        RegressionResult NMresult;
        OptimizationSummary powellResult;

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
        private string lastParams;
        private double _NMobjFunction1(double[] prms)
        {
            if (prms.Length.Equals(0)) throw new Exception("No parameters to optimize");
            if (!prms.Length.Equals(scans.Count)) { log("Error: parameters mismatch", false); return 0; }
            double[] ps = new double[prms.Length];
            
            for (int i = 0; i < prms.Length; i++)
                ps[i] = Utils.EnsureRange(prms[i], scans[i].sFrom, scans[i].sTo);

            OptimEventArgs ex; string ss = "iters: " + (iters.Count+1).ToString() + "; " + scans[0].sParam+"= " + ps[0].ToString("G6") + "; ";
            lastParams = scans[0].sParam + "= " + ps[0].ToString("G6") + "; ";
            for (int i = 1; i < scans.Count; i++)
            {
                ex = new OptimEventArgs(scans[i].sParam, ps[i], "scanning"); scans[i].Value = ps[i];
                ss += scans[i].sParam + "= " + ps[i].ToString("G6") + "; ";
                lastParams += scans[i].sParam + "= " + ps[i].ToString("G6") + "; ";
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
        private double _PowellObjectiveFunction(int n, double[] x) // n is ignored
        {
            return _NMobjFunction1(x);
        }
        public OptimSimplexUC()
        {
            InitializeComponent();
            state = optimState.idle;
            srsPrms = new List<List<Point>>() ;
        }
        #region Common        
        public Dictionary<string, double> opts { get; set; }
        public void Init(Dictionary<string, double> _opts, bool report_enabled)
        {
            if (Utils.isNull(_opts)) return;
            opts = new Dictionary<string, double>(_opts);
            if (opts.Count.Equals(0)) return;
            NMobjFunction = new ObjectiveFunctionDelegate(_NMobjFunction1);
            PowellObjFunction = new BobyqaObjectiveFunctionDelegate(_PowellObjectiveFunction);
            if (opts.ContainsKey("optimMethod")) cbOptimMethod.SelectedIndex = Convert.ToInt32(opts["optimMethod"]);
            if (opts.ContainsKey("numIters")) numIters.Value = Convert.ToInt32(opts["numIters"]);
            iters = new List<Point>(); meta = new List<Point>();
            NMresult = null; powellResult = null;
            jsr = new JsonSimplexReport();
            jsr.enabled = report_enabled;
        }
        public void Final()
        {
            if (Utils.isNull(opts)) opts = new Dictionary<string, double>();
            opts["optimMethod"] = cbOptimMethod.SelectedIndex;
            opts["numIters"] = numIters.Value;
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
            if (jsr.enabled)
            {
                ImgUtl.VisualCompToPng(graphScan, jsr.folder + "obj_func.png");
                ImgUtl.VisualCompToPng(graphProc, jsr.folder + "prms_trend.png");
                string json = JsonConvert.SerializeObject(jsr);
                System.IO.File.WriteAllText(jsr.folder + "data.json", "dt =" + Environment.NewLine + json);
                File.Copy(Path.Combine(Utils.configPath, "simplex_template.RPR", "simplex_optim_report.htm"), Path.Combine(jsr.folder, "simplex_optim_report.htm"));                
                Utils.TimedMessageBox("Report saved in  " + jsr.folder);
            }
        }
            #endregion Events
        public string report(bool lastIter)
        {
            string ss = "No results";
            switch (optimMethod)
            {
                case OptimMethod.NedlerMead:
                    if (Utils.isNull(NMresult)) return ss;
                    if (state == optimState.cancelRequest)
                        ss = "termination: UserCancelation; iters: " + iters.Count.ToString() + "; obj.value: " + (-NMresult.ErrorValue).ToString("G6");
                    else
                        ss = "termination: " + NMresult.TerminationReason.ToString() + "; iters: " + NMresult.EvaluationCount.ToString() + "; obj.value: " + (-NMresult.ErrorValue).ToString("G6");
                    break;
                case OptimMethod.Powell:
                    if (Utils.isNull(powellResult)) return ss;
                    if (state == optimState.cancelRequest)
                        ss = "termination: UserCancelation; iters: " + iters.Count.ToString() + "; obj.value: " + (-powellResult.F).ToString("G6");
                    else 
                        ss = "termination: " + powellResult.Status.ToString() + "; iters: " + powellResult.Evals.ToString() + "; obj.value: " + (-powellResult.F).ToString("G6");
                    break;
            }
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
                for (int i = 0; i < scans.Count; i++)
                {
                    srsPrms.Add(new List<Point>()); graphProc.Plots.Add(new Plot(scans[i].sParam));
                }
                if (simulation) // for simulation -> equal int conditions
                {
                    scans[0].Value = 1; scans[1].Value = -1; scans[2].Value = 1; 
                }
                
                int sCount = scans.Count;
                double cp = _opts.ContainsKey("ConvPrec") ? _opts["ConvPrec"] : 0.1; opts["ConvPrec"] = cp;
                opts["numIters"] = numIters.Value;           
                switch (optimMethod)
                {
                    case OptimMethod.NedlerMead:
                        initVals = new SimplexConstant[sCount];
                        for (int i = 0; i < sCount; i++)
                            initVals[i] = new SimplexConstant(scans[i].Value, 0.1 + scale*scans[i].Value);           
                        NMresult = NelderMeadSimplex.Regress(initVals, cp, numIters.Value, NMobjFunction);
                        break;
                    case OptimMethod.Powell:
                        double[] xl = new double[sCount]; double[] xu = new double[sCount]; double[] x0 = new double[sCount];
                        for (int i = 0; i < sCount; i++)
                        {
                            xl[i] = scans[i].sFrom; xu[i] = scans[i].sTo; x0[i] = scans[i].Value;
                        }
                        var optimizer = new Bobyqa(sCount, PowellObjFunction, xl, xu);
                        optimizer.MaximumFunctionCalls = numIters.Value;
                        optimizer.TrustRegionRadiusStart = cp*1000; //optimizer.Logger = Console.Out;
                        optimizer.TrustRegionRadiusEnd = cp; //*1e-5                       
                        powellResult = optimizer.FindMinimum(x0);
                        break;
                }
                for (int i = 0; i<scans.Count; i++)
                    OnParamSet(new OptimEventArgs(scans[i].sParam, Double.NaN, "optimized"));
                if (jsr.enabled)
                {
                    jsr.dateTime = DateTime.Now.ToString("yy-MM-dd_H-mm-ss");
                    jsr.folder = Utils.dataPath + jsr.dateTime + ".SIM\\";
                    if (!Directory.CreateDirectory(jsr.folder).Exists)
                        throw new Exception("Cannot create report directory: " + Utils.dataPath + jsr.dateTime);                
                    jsr.prms = new string[scans.Count];
                    jsr.convPrec = cp;
                    for (int i = 0; i < scans.Count; i++)
                    {
                        jsr.prms[i] = scans[i].sParam;
                    }
                    jsr.objFunc = objFunc;
                    jsr.method = cbOptimMethod.Text;
                }
                jsr.optim_final = lastParams + report(false);
                OnEndOptim(new OptimEventArgs("", Double.NaN, report(false)));
            }
            else state = optimState.cancelRequest;
        }
        #endregion Common

        private void numIters_ValueChanged(object sender, NationalInstruments.Controls.ValueChangedEventArgs<int> e)
        {
            if (Utils.isNull(opts)) return;
            opts["numIters"] = numIters.Value;
        }

    }
    public class JsonSimplexReport
    {
        [JsonIgnore]
        public bool enabled { get; set; }
        [JsonIgnore]
        public string folder { get; set; }
        public string dateTime { get; set; }
        public string[] prms { get; set; }
        public double convPrec { get; set; }
        public string objFunc { get; set; }
        public string method { get; set; }

        public string optim_final { get; set; }
    }

}
