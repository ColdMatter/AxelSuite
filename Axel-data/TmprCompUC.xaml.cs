using System;
using System.IO;
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

using NationalInstruments.Analysis.Math;
using NationalInstruments.Controls;

using Axel_hub;
using UtilsNS;

namespace Axel_data
{
    /// <summary>
    /// Interaction logic for TmprCompUC.xaml
    /// </summary>
    public partial class TmprCompClass : UserControl
    {
        private static readonly Range<double> RelativeRange = new Range<double>(0.2, 0.8);

        DataStack srsMems, srsTmpr, srsMems2, srsTmpr2, srsRes, srsResHisto;
        public TmprCompClass()
        {
            InitializeComponent();
            srsMems = new DataStack(DataStack.maxDepth);
            srsTmpr = new DataStack(DataStack.maxDepth);
            srsMems2 = new DataStack(DataStack.maxDepth);
            srsTmpr2 = new DataStack(DataStack.maxDepth);
            srsRes = new DataStack(DataStack.maxDepth);
            srsResHisto = new DataStack(1000);
        }
        public void Initialize()
        {

        }

        public delegate void LogHandler(string txt, bool detail = false, SolidColorBrush clr = null);
        public event LogHandler OnLog;

        protected void LogEvent(string txt, bool detail = false, SolidColorBrush clr = null)
        {
            if (OnLog != null) OnLog(txt, detail, clr);
        }

        private void btnOpenLog_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.FileName = ""; // Default file name
            dlg.DefaultExt = ".ahf"; // Default file extension
            dlg.Filter = "Axel Hub File (.ahf)|*.ahf|" + "All files (*.*)|*.*";  // Filter files by extension

            // Show save file dialog box
            Nullable<bool> result = dlg.ShowDialog();

            // Process save file dialog box results
            if (result != true) return;

            btnClear_Click(null, null); int k = 0;  
            foreach (string line in File.ReadLines(dlg.FileName))
            {
                if (line.Equals("")) continue;
                if (line[0].Equals('#')) continue;
                string[] sa = line.Split('\t');
                double tm = Convert.ToDouble(sa[0]);
                srsMems.AddPoint(Convert.ToDouble(sa[1]), tm); srsTmpr.AddPoint(Convert.ToDouble(sa[2]), tm);
                k++;
            }
            graphRaw.Data[0] = srsMems; graphRaw.Data[1] = srsTmpr;
            OnLog("Opened: " + dlg.FileName + " (" + k.ToString() + " pnts)", false, Brushes.Maroon);
            btnFIT.IsEnabled = true;
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            srsMems.Clear(); srsTmpr.Clear(); srsMems2.Clear(); srsTmpr2.Clear(); srsRes.Clear(); srsResHisto.Clear();
            graphRaw.Data[0] = null; graphRaw.Data[1] = null;
            richResults.Document.Blocks.Clear();
            btnFIT.IsEnabled = false;
            Utils.DoEvents();
        }

        private double ModelFunction(double x, double[] a)
        {
            int idx = Convert.ToInt32(x);
            int cn = sePoliDeg.Value + 1;
            double rslt = 0;
            for (int i = 0; i < cn; i++)
            {
                rslt += a[i] * Math.Pow(srsTmpr2[idx].Y,i);
            }
            return rslt; 
        }

        private void btnFIT_Click(object sender, RoutedEventArgs e)
        {
            double upperLmt = 0, lowerLmt = 0; srsMems2.Clear(); srsTmpr2.Clear();
            if (chkPartialProc.IsChecked.Value) 
            {
                var horizontalRange = rangeCursor.ActualHorizontalRange;
                upperLmt = (double)horizontalRange.GetMaximum();
                lowerLmt = (double)horizontalRange.GetMinimum(); 
                for (int i = 0; i < srsMems.Count; i++)
                {
                    if (!Utils.InRange(srsMems[i].X, lowerLmt, upperLmt))
                      continue;
                  srsMems2.Add(srsMems[i]); srsTmpr2.Add(srsTmpr[i]); 
                }                
            }
            else 
            {
                srsMems2.AddRange(srsMems); srsTmpr2.AddRange(srsTmpr);
            }
                
            int np = srsMems2.Count;
            if (!srsMems2.Count.Equals(srsTmpr2.Count)) throw new Exception("uneven indexes");
            double[] inputXData = new double[np];
            double[] inputYData = new double[np];
            int cn = sePoliDeg.Value + 1;
            double[] coefficients = new double[cn];
            coefficients[0] = 2571; coefficients[1] = 0.05;
            double meanSquaredError = 0; int maximumIterations = 10000;
            for (int i = 0; i < np; i++) 
            {                           
                inputXData[i] = i; inputYData[i] = srsMems2[i].Y;
            }
            double mDev = Statistics.StandardDeviation(inputYData);
            ModelFunctionCallback callback = new ModelFunctionCallback(ModelFunction);
            try
            {
                double[] fittedData = CurveFit.NonLinearFit(inputXData, inputYData, callback, coefficients, out meanSquaredError, maximumIterations);    
                Histogram(Residuals(fittedData), 50);

                Utils.log(richResults, "Raw signal stDev = " + mDev.ToString("G5"), Brushes.Navy);
                double rDev = Statistics.StandardDeviation(srsRes.pointYs());
                Utils.log(richResults, "Resid. stDev = " + rDev.ToString("G5"), Brushes.Navy);
                Utils.log(richResults, "Improvement = " + (mDev / rDev).ToString("G5") + " times", Brushes.Blue);
            }
            catch (Exception exp)
            {
                MessageBox.Show(exp.Message);
            }
            for (int i = 0; i < cn; i++) 
            {
                string txt = '['+i.ToString()+"] "+coefficients[i].ToString("G6");
                Utils.log(richResults, txt);
            }
            Utils.log(richResults, "MeanSqError= " + meanSquaredError.ToString("G5"), Brushes.Maroon);           
        }

        private DataStack Residuals(double[] fittedData)
        {
            srsRes.Clear(); graphResid.Data[0] = null;
            for (int i = 0; i < srsMems2.Count; i++)
            {
                srsRes.AddPoint(srsMems2[i].Y - fittedData[i], srsMems2[i].X);
            }
            graphResid.Data[0] = srsRes;
            return srsRes;
        }
        
        private DataStack Histogram(DataStack src, int numBins = 300)
        {
            graphResidHisto.Data[0] = null;
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
            graphResidHisto.Data[0] = rslt;
            return rslt;
        }

        private void chkPartialProc_Checked(object sender, RoutedEventArgs e)
        {
            rangeCursor.Visibility = System.Windows.Visibility.Visible;
            rangeCursor.SetRelativeHorizontalRange(RelativeRange);
        }

        private void chkPartialProc_Unchecked(object sender, RoutedEventArgs e)
        {
            rangeCursor.Visibility = System.Windows.Visibility.Collapsed;
        }

    }
}
