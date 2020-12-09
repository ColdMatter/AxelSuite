using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using NationalInstruments.Controls.Primitives;
using NationalInstruments.Analysis.Math;
using Axel_hub;
using UtilsNS;

namespace Axel_data
{
    /// <summary>
    /// Interaction logic for QMfitUC.xaml
    /// </summary>
    public partial class QMfitUC : UserControl
    {
        private string doubleFormat = "G5";
        public QMfitUC()
        {
            InitializeComponent();
            Clear();
        }
        public void Clear()
        {
            data = null; curShotList = null; sectSize = -1;
        }
        public delegate void LogHandler(string txt, bool detail = false, Color? clr = null);
        public event LogHandler OnLog;
        protected void LogEvent(string txt, bool detail = false, Color? clr = null)
        {
            if (OnLog != null) OnLog(txt, detail, clr);
        }
        public Dictionary<string, double> GetInitials()
        {
            Dictionary<string, double> r = new Dictionary<string, double>();
            r["period"] = numPeriod.Value; r["phase"] = numPhase.Value; r["ampl"] = numAmpl.Value; r["offset"] = numOffset.Value;
            return r;
        }
        public int curSectIdx 
        { 
            get 
            {
                if (Utils.isNull(curShotList)) return -1;
                if (Utils.isNull(data)) return -1;
                return  (sectSize < 0) ? -1 : Convert.ToInt32(Math.Round((double)(curShotList.lastIdx / sectSize) - 1)); 
            } 
        }
        public ShotList curShotList { get; private set; }
        public int sectSize = -1;
        public List<Point> data { get; private set; }
        public Dictionary<string,double> LoadFromPoints(List<Point> dt, bool andFit = true)
        {
            Dictionary<string, double> rslt = new Dictionary<string, double>(); // andFit false -> empty result
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.DataBind,
                 new Action(() =>
                 {
                     graphMEMSvsQuant.Data[0] = dt;
                     if (!andFit) graphMEMSvsQuant.Data[1] = null;
                     graphMEMSvsQuant.Refresh();
                 }));
            btnJFitSingle.IsEnabled = (dt.Count > 5);
            if (btnJFitSingle.IsEnabled) data = new List<Point>(dt); 
            else data = null;
            if (!btnJFitSingle.IsEnabled) LogEvent("no data in section " + curSectIdx.ToString(), false, Brushes.Red.Color);
            if (!btnJFitSingle.IsEnabled || !andFit) return rslt;
            rslt = BigFIT();
            if (rslt.Count == 0) LogEvent("error at section " + curSectIdx.ToString(), false, Brushes.Red.Color);
            else
            {
                if (rslt.ContainsKey("rmse"))
                {
                    if (Double.IsNaN(rslt["rmse"])) LogEvent("error at section " + curSectIdx.ToString(), false, Brushes.Red.Color);
                }
            }
            return rslt; 
        }
        public Dictionary<string, double> LoadFromListOfShots(List<SingleShot> shotList, bool andFit = true) // plot the portion
        {
            List<Point> dt = new List<Point>();
            foreach (SingleShot ss in shotList)
            {
                // quant vs mems
                dt.Add(new Point(ss.memsWeightAccel(0), ss.quant.Y));
            }
            return LoadFromPoints(dt, andFit);
        }
        public Dictionary<string, double> ScanSection(ref ShotList shotList, int sectLen, out bool next, bool andFit = true)
        {
            curShotList = shotList;
            List<SingleShot> ls = shotList.sectionScan(sectLen, true, out next);
            return LoadFromListOfShots(ls);
        }
         
       // if sectStart > 0 then inefficient use -> must reset and read thru
        public Dictionary<string, double> LoadFromShotList(ref ShotList shotList, int sectLen, int sectStart = -1, bool andFit = true) 
        {
            curShotList = shotList;
            List<Point> dt = new List<Point>(); SingleShot ss; bool next = false;
            if (sectLen < 2) throw new Exception("Too few points ");
            List<SingleShot> ls;
            if (sectStart > -1) // random access, inefficient especially for large files
            {
                shotList.resetScan(); int k = 0;
                do
                {
                    ss = shotList.archiScan(out next);
                    k++;
                } while (next && (k < sectStart));
            }
            if (!next) return null;
            ls = shotList.sectionScan(sectLen, true, out next);  // sequential access                        
            return LoadFromListOfShots(ls, andFit);
        }
        private void reportFit(Dictionary<string, double> fitCoeff)
        {
            tabControl.SelectedIndex = 1;
            liPeriod.Content = "Period  " + fitCoeff["period"].ToString(doubleFormat); 
            liPhase.Content = "Phase  " + fitCoeff["phase"].ToString(doubleFormat);
            liRealPhase.Content = "R.Phase  " + fitCoeff["rphase"].ToString(doubleFormat);
            liAmpl.Content = "Ampl.  " + fitCoeff["ampl"].ToString(doubleFormat); 
            liOffset.Content = "Offset  " + fitCoeff["offset"].ToString(doubleFormat); 
            liRMSE.Content = "RMSE  " + (100*fitCoeff["rmse"]).ToString(doubleFormat)+" [%]";
            if (fitCoeff["rmse"] < 0.01) liRMSE.Foreground = Brushes.ForestGreen;
            if (Utils.InRange(fitCoeff["rmse"],0.01,0.02)) liRMSE.Foreground = Brushes.Coral;
            if (fitCoeff["rmse"] > 0.02) liRMSE.Foreground = Brushes.OrangeRed;
            liCoD.Content = "R^2   " + fitCoeff["r^2"].ToString(doubleFormat);
        }
        private void chartFit(double[] xData, double[] fittedData)
        {
            List<Point> lp = new List<Point>();
            if (xData.Length != fittedData.Length) throw new Exception("Array size problem");
            for (int i = 0; i< xData.Length; i++)
            {
                lp.Add(new Point(xData[i], fittedData[i]));
            }
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.DataBind,
              new Action(() =>
              {
                  graphMEMSvsQuant.Data[1] = lp; 
              }));
        }
        private bool goodFit(Dictionary<string, double> fitRslt)
        {
            if (fitRslt.Count == 0) return false;
            return (fitRslt["ampl"] > 0) && (fitRslt["rmse"] < 0.01);
        }
        public Dictionary<string, double> BigFIT(bool report = true, bool chart = true)
        {
            Dictionary<string, double> rslt = FIT(report, chart); rslt["in_prd"] = numPeriod.Value;
            if (!chkPerturbation.IsChecked.Value || goodFit(rslt)) return rslt;
            List<Dictionary<string, double>> ls = new List<Dictionary<string, double>>();
            double prd0 = numPeriod.Value; double prd = prd0;
            ls.Add(rslt);
            while (!goodFit(rslt) && (ls.Count < 11))
            {
                int cnt = ls.Count;
                if (cnt % 2 == 1) prd = prd0 + (cnt/2 + 1) * 0.05;
                else prd = prd0 - ((cnt - 1)/2 +1) * 0.05;
                numPeriod.Value = prd;
                rslt = FIT(report, chart); rslt["in_prd"] = prd;
                ls.Add(rslt);
            }
            for (int k = ls.Count-1; k > -1; k--)            
            {
                if (ls[k].Count == 0) { ls.RemoveAt(k); continue; }
                if (ls[k]["ampl"] < 0) { ls.RemoveAt(k); continue; }
            }
            int j = 0; double rmse = 1000; int irmse = -1;
            foreach (Dictionary<string, double> rs in ls)
            {  
                if (rs["rmse"] < rmse) { rmse = rs["rmse"]; irmse = j; }
                j++;
            }           
            if (irmse > -1) rslt = ls[irmse];
            numPeriod.Value = rslt["in_prd"]; FIT(report, chart);
            return rslt;
        }
        public Dictionary<string, double> FIT(bool report = true, bool chart = true) 
        {
            Dictionary<string, double> rslt = new Dictionary<string, double>();
            if (Utils.isNull(data)) return rslt;
            int numSamples = data.Count;
            if (numSamples < 5) return rslt;           
            double[] xData = new double[numSamples];
            double[] yData = new double[numSamples];
            for (int i = 0; i< numSamples; i++)
            {
                xData[i] = data[i].X; yData[i] = data[i].Y;
            }
            Array.Sort(xData, yData);
            double[] coefficients = new double[4];
            coefficients[0] = numPeriod.Value; coefficients[1] = numPhase.Value; coefficients[2] = numAmpl.Value; coefficients[3] = numOffset.Value; 
            double meanSquaredError;
            int maximumIterations = 100;
            double[] fittedData = null; 
            ModelFunctionCallback callback = new ModelFunctionCallback(ModelFunction);
            try
            {
                fittedData = CurveFit.NonLinearFit(xData, yData, callback, coefficients, out meanSquaredError, maximumIterations);
            }
            catch (NationalInstruments.Analysis.AnalysisException e)
            {
                LogEvent("============= sect: " + curSectIdx.ToString(), true, Brushes.Blue.Color);
                LogEvent("Error: " + e.Message, false, Brushes.Red.Color);
                return rslt;
            }
            rslt["period"] = coefficients[0]; double phase = coefficients[1];
            rslt["phase"] = phase;
            while (!Utils.InRange(phase, 0, 2 * Math.PI)) // confine in 0 .. 2*Math.PI
            {
                if (phase < 0) phase += 2 * Math.PI;
                else phase -= 2 * Math.PI;
            }
            rslt["rphase"] = phase;
            rslt["ampl"] = coefficients[2]; rslt["offset"] = coefficients[3];
            rslt["rmse"] = Math.Sqrt(meanSquaredError);
            double SSres = 0; double SStot = 0; double yAver = yData.Average();
            for (int i = 0; i < numSamples; i++)
            {
                SSres += Math.Pow(yData[i] - fittedData[i], 2);
                SStot += Math.Pow(yData[i] - yAver, 2);
            }
            rslt["r^2"] = 1 - SSres / SStot;
            if (report) reportFit(rslt);
            if (chart) chartFit(xData, fittedData);
            Utils.DoEvents();
            return rslt;
        }
        
        // Callback function that implements the fitting model
        private double ModelFunction(double x, double[] coefficients)
        {
            return Math.Cos(coefficients[0]* x + coefficients[1]) * coefficients[2] + coefficients[3];
        }

        private void btnJFitSingle_Click(object sender, RoutedEventArgs e)
        {
            if (!Utils.isNull(data)) BigFIT();
        }

        private void btnTest_Click(object sender, RoutedEventArgs e)
        {
            if (Utils.isNull(data)) return;
            int numSamples = data.Count;
            if (numSamples < 5) return;
            double[] coefficients = new double[4];
            coefficients[0] = numPeriod.Value; coefficients[1] = numPhase.Value; coefficients[2] = numAmpl.Value; coefficients[3] = numOffset.Value;
            double[] xData = new double[numSamples];
            double[] yData = new double[numSamples];
            for (int i = 0; i < numSamples; i++)
            {
                xData[i] = data[i].X; yData[i] = ModelFunction(data[i].X, coefficients);
            }
            Array.Sort(xData, yData);
            chartFit(xData, yData);
        }

        private void btnEval_Click(object sender, RoutedEventArgs e)
        {
            if (Utils.isNull(data)) return;
            int numSamples = data.Count;
            if (numSamples < 5) return;
            double[] xData = new double[numSamples];
            double[] yData = new double[numSamples];
            for (int i = 0; i < numSamples; i++)
            {
                xData[i] = data[i].X; yData[i] = data[i].Y;
            }
            numAmpl.Value = (yData.Max() - yData.Min())/2;
            numOffset.Value = yData.Average();
        }
    }
}
