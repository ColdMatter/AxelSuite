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
using System.Windows.Media.Media3D;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using NationalInstruments.Controls;
using NationalInstruments.Analysis.Dsp.Filters;
using UtilsNS;

namespace Axel_hub.PanelsUC
{
    /// <summary>
    /// Interaction logic for OptimGridScanUC.xaml
    /// </summary>
    public partial class OptimGridScanUC : UserControl, IOptimization
    {
        public OptimGridScanUC()
        {
            InitializeComponent();
        }
        #region Common
        public Dictionary<string, double> opts { get; set; }
        public void Init(Dictionary<string, double> _opts)
        {
            if (Utils.isNull(_opts)) return;
            opts = new Dictionary<string, double>(_opts);
            if (opts.Count.Equals(0)) return;
            if (opts.ContainsKey("numSGdegree")) numSGdegree.Value = Convert.ToInt32(opts["numSGdegree"]);
            if (opts.ContainsKey("numSGframe")) numSGframe.Value = Convert.ToInt32(opts["numSGframe"]);
            if (opts.ContainsKey("numZRmin")) numZRmin.Value = opts["numZRmin"];
            if (opts.ContainsKey("numZRmax")) numZRmax.Value = opts["numZRmax"];
        }
        public void Final()
        {
            if (Utils.isNull(opts)) opts = new Dictionary<string, double>();
            opts["moduleIdx"] = 1;
            opts["numSGdegree"] = numSGdegree.Value;
            opts["numSGframe"] = numSGframe.Value;
            opts["numZRmin"] = numZRmin.Value;
            opts["numZRmax"] = numZRmax.Value;
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
            OnLog(new OptimEventArgs("", detail ? -1 : 1, txt));
        }
        public event EventHandler EndOptimEvent;
        protected virtual void OnEndOptim(OptimEventArgs e)
        {
            EndOptimEvent?.Invoke(this, e);
        }
        #endregion Events
        public optimState state { get; set; }
        public List<baseMMscan> scans { get; set; }
        private void Clear()
        {
            d0 = 0; d1 = 0; xAxis.Label = ""; yAxis.Label = "";
            if (!Utils.isNull(scans)) scans.Clear();
            raw = null; proc = null;
            if (!Utils.isNull(pnts3)) pnts3.Clear();
            theMax = new Point3D(Double.NaN, Double.NaN, Double.NaN);
            crsMax.Visibility = Visibility.Collapsed; 
        }
        public string report(bool lastIter)
        {
            if (Utils.isNull(theMax)) return "";
            OnParamSet(new OptimEventArgs(scans[0].sParam, theMax.Y, "optimized (slow index) - X"));
            OnTakeAShot(new OptimEventArgs(scans[1].sParam, theMax.X, "optimized (fast index) - Y"));
            return scans[0].sParam+" : "+theMax.Y.ToString("G5") + " ;  " + scans[1].sParam + " : " + theMax.X.ToString("G5")
                + " ;  obj.function : " + theMax.Z.ToString("G5");
        }
        public void Optimize(bool? start, List<baseMMscan> _scans, Dictionary<string, double> opts)
        {
            if (!_scans.Count.Equals(2)) { log("Err: the present implementaion of grid optimization accepts only 2 parameters.", false); OnEndOptim(null); return; }
            scans = new List<baseMMscan>(_scans);
            if (Utils.isNull(start)) { log("Err: wrong branch.", false); OnEndOptim(null); return; }
            if ((bool)start)
            {               
                state = optimState.running; Clear();
                scans = new List<baseMMscan>();
                foreach (baseMMscan mms in _scans)
                    scans.Add(new baseMMscan(mms.getAsString()));
                graphIntensity.DataSource = null;
                xAxis.Label = scans[1].sParam; yAxis.Label = scans[0].sParam;
                xAxis.Range = new Range<double>(scans[1].sFrom - scans[1].sBy / 2, scans[1].sTo + scans[1].sBy / 2); // slow idx
                yAxis.Range = new Range<double>(scans[0].sFrom - scans[0].sBy / 2, scans[0].sTo + scans[0].sBy / 2); // fast idx
                d0 = 1 + Convert.ToInt32((scans[1].sTo - scans[1].sFrom) / scans[1].sBy); // slow
                d1 = 1 + Convert.ToInt32((scans[0].sTo - scans[0].sFrom) / scans[0].sBy); // fast
                if (DoubleScan()) UpdateVis();
            }
            else state = optimState.cancelRequest;
            OnEndOptim(new OptimEventArgs("", Double.NaN, report(false)));
        }
        #endregion Common
        int d0, d1; // dimentions (slow - x; fast - y) of the array
        double[,] raw, proc; // [slow, fast]
        List<Point3D> pnts3;
        Point3D theMax;
        private bool DoubleScan()
        {            
            raw = new double[d0, d1]; pnts3 = new List<Point3D>(); 
            for (int i = 0; i < d0; i++) // slow (x)
            {
                double v = scans[1].sFrom + i * scans[1].sBy;
                OnParamSet(new OptimEventArgs(scans[1].sParam, v, "scanning (slow index) - X"));
                for (int j = 0; j < d1; j++) // fast (y)
                {
                    if (state.Equals(optimState.cancelRequest)) return false;
                    double w = scans[0].sFrom + j * scans[0].sBy;
                    raw[i, j] = OnTakeAShot(new OptimEventArgs(scans[0].sParam, w, "scanning (fast index) - Y"));
                    pnts3.Add(new Point3D(v, w, raw[i, j]));
                }
                graphIntensity.DataSource = null; Utils.DoEvents();
                graphIntensity.DataSource = pnts3; Utils.DoEvents();
            }
            return true;
        }
        private List<Point3D> Map3D(double[,] map)
        {
            var r = new List<Point3D>(); double x, y;
            for (int i = 0; i < d0; i++)
            {
                x = scans[1].sFrom + i * scans[1].sBy;
                for (int j = 0; j < d1; j++)
                {                 
                    y = scans[0].sFrom + j * scans[0].sBy;
                    r.Add(new Point3D(x,y, map[i, j]));
                }
            }
            return r;
        }
        #region filter
        protected double[] extractVector(int dm, double[,] mat, int idx)
        {
            if (!Utils.InRange(dm, 0, 1)) { log("Err: wrong dim. "+dm.ToString(), false); return null; }
            int np = mat.GetLength(dm);
            double[] vec = new double[np];
            if (dm.Equals(0))
            {
                if (!Utils.InRange(idx, 0, mat.GetLength(1))) { log("Err: wrong index. " + idx.ToString(), false); return null; }
                for (int i = 0; i < np; i++) vec[i] = mat[i, idx];
            }
            if (dm.Equals(1))
            {
                if (!Utils.InRange(idx, 0, mat.GetLength(0))) { log("Err: wrong index. " + idx.ToString(), false); return null; }
                for (int i = 0; i < np; i++) vec[i] = mat[idx, i];
            }
            return vec;
        }
        protected double[,] replaceVector(int dm, double[,] mat, int idx, double[] vec)
        {
            if (!Utils.InRange(dm, 0, 1)) { log("Err: wrong dim. " + dm.ToString(), false); return null; }
            int np = mat.GetLength(dm);
            if (!vec.Length.Equals(np)) { log("Err: wrong vector size. " + dm.ToString(), false); return null; }
            var prc = (double[,])mat.Clone();

            if (dm.Equals(0))
            {
                if (!Utils.InRange(idx, 0, mat.GetLength(1))) { log("Err: wrong index. " + idx.ToString(), false); return null; }
                for (int i = 0; i < np; i++) prc[i, idx] = vec[i];
            }
            if (dm.Equals(1))
            {
                if (!Utils.InRange(idx, 0, mat.GetLength(0))) { log("Err: wrong index. " + idx.ToString(), false); return null; }
                for (int i = 0; i < np; i++) prc[idx, i] = vec[i];
            }
            return prc;
        }
        public double[] SGfilter1(double[] ds)
        {
            double[] r = new double[ds.Length]; // empty to return in case of error
            if (numSGdegree.Value > (2 * numSGframe.Value + 1)) { log("Err: increase Sidepoints", false); return r; }
            if (ds.Length == 0) { log("Err: No data points to S-G filter", false); return r; }
            if (ds.Length < (2 * numSGframe.Value + 1)) { log("Err: Too few data points to S-G filter", false); return r; }
            double[] ra = SavitzkyGolay.Filter(ds, numSGdegree.Value, numSGframe.Value);
            if (ra.Length != ds.Length) { log("Err: vector sizes mismatch", false); return r; }
            return ra;
        }
        protected double[,] pseudo2dSV(double[,] mat)
        {
            List<double[,]> prc = new List<double[,]>();
            prc.Add((double[,])mat.Clone()); prc.Add((double[,])mat.Clone());
            double[] vec;
            for (int j = 0; j < prc.Count; j++)
            {
                int adm = (j == 0) ? mat.GetLength(1) : mat.GetLength(0);
                for (int i = 0; i < adm; i++)
                {
                    vec = SGfilter1(extractVector(j, mat, i));
                    prc[j] = replaceVector(j, prc[j], i, vec);
                }               
            }
            var r = (double[,])mat.Clone();
            for (int i = 0; i < d0; i++) 
            {
                for (int j = 0; j < d1; j++)
                {
                   r[i,j] = (prc[0][i, j] + prc[1][i, j]) / 2;
                }
            }
            return r;
        }
        private bool Filter(double[,] map = null)
        {
            if (Utils.isNull(map) && Utils.isNull(raw)) return false;
            if (Utils.isNull(map)) map = raw;
            if ((raw.GetLength(0) != d0) || (raw.GetLength(1) != d1)) return false;
            proc = pseudo2dSV(raw);
            return !Utils.isNull(proc);
        }
        #endregion filter
        private void UpdateVis()
        {
            if (Utils.isNull(raw)) { graphIntensity.DataSource = null; return; }
            if (bcbFilter.Value)
                if (!Filter(raw)) return;
            theMax = (bcbFilter.Value) ? FindMax(proc) : FindMax(raw);
            OnParamSet(new OptimEventArgs(scans[0].sParam, theMax.Y, "optimized")); // fast
            OnParamSet(new OptimEventArgs(scans[1].sParam, theMax.X, "optimized")); // slow           

            List<Point3D> map = ((bcbFilter.Value) ? Map3D(proc) : Map3D(raw));
            graphIntensity.DataSource = map;
        }
        private Point3D FindMax(double[,] map)
        {
            var r = new Point3D(); double m = -1e6;
            for (int i = 0; i < d0; i++) // slow x
            {               
                for (int j = 0; j < d1; j++) // fast y
                {
                    if (map[i, j] > m)
                    {
                        m = map[i, j];
                        r = new Point3D(scans[1].sFrom + i * scans[1].sBy,scans[0].sFrom + j * scans[0].sBy, m);
                    }
                }
            }
            List<double> psn = new List<double>(); psn.Add(r.X); psn.Add(r.Y);
            crsMax.SetDataPosition(psn);
            crsMax.Visibility = Visibility.Visible; 
            return r;
        }
        private void numZRmin_ValueChanged(object sender, ValueChangedEventArgs<double> e)
        {
            if (Utils.isNull(numZRmin) || Utils.isNull(numZRmax) || Utils.isNull(intGraphColorScale)) return;
            if (numZRmax.Value <= numZRmin.Value) { log("Err: wrong color limits.", false); return; }
            int nm = intGraphColorScale.Markers.Count;
            for (int i = 0; i < nm; i++)
            {
                double m = numZRmin.Value + i * (numZRmax.Value - numZRmin.Value) / (nm - 1);
                intGraphColorScale.Markers[i]  = new ColorScaleMarker(m, intGraphColorScale.Markers[i].Color);
            }           
        }
        private void numSGdegree_ValueChanged(object sender, NationalInstruments.Controls.ValueChangedEventArgs<int> e)
        {
            if (Utils.isNull(graphIntensity)) return;
            if (bcbFilter.Value) UpdateVis();
        }

        private void bcbFilter_Click(object sender, RoutedEventArgs e)
        {
            bcbFilter.Value = !bcbFilter.Value;
            UpdateVis();
        }
    }
}
