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

//using NationalInstruments.Net;
using NationalInstruments.Analysis;
using NationalInstruments.Analysis.Conversion;
using NationalInstruments.Analysis.Dsp;
using NationalInstruments.Analysis.Dsp.Filters;
using NationalInstruments.Analysis.Math;
using NationalInstruments.Analysis.Monitoring;
using NationalInstruments.Analysis.SignalGeneration;
using NationalInstruments.Analysis.SpectralMeasurements;
using NationalInstruments;
using NationalInstruments.DAQmx;
//using NationalInstruments.NetworkVariable;
//using NationalInstruments.NetworkVariable.WindowsForms;
//using NationalInstruments.Tdms;
using NationalInstruments.Controls;
using NationalInstruments.Controls.Rendering;

using AxelChartNS;
using UtilsNS;

namespace AxelProcUC
{
    struct movePrmStr
    {
        public double TimeElapsed;
        public double CurrentPosition;
        public double CalcVelocity;
        public double CalcAcceleration;
        public double CurrentVelocity;
        public double CurrentAcceleration;
        public double SetpointPosition;
        public double SetpointVelocity;
        public double SetpointAcceleration;
        public double FollowingError;
    }

    public struct SingleStatStr
    {
        public void clear()
        {
            level = 0;
            sd = 0;
        }
        public double level;
        public double sd;
    }

    public struct StatsStr
    {
        public bool ok;
        public void clear()
        {
            upper.clear();
            lower.clear();
        }
        public SingleStatStr upper;
        public SingleStatStr lower;
        public SingleStatStr mean()
        {
            SingleStatStr rslt;
            rslt.level = (Math.Abs(upper.level) + Math.Abs(lower.level)) / 2;
            rslt.sd = (upper.sd + lower.sd) / 2;
            return rslt;
        }
    } 


    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class UserControl1 : UserControl
    {
        public UserControl1()
        {
            InitializeComponent();
        }
        #region Statistical routines

        private DataStack CalcFirstDerv(DataStack orig, int deg = 2) // 2-4
        {
            DataStack rslt = new DataStack();
            deg = Utils.EnsureRange(deg, 2, 4);
            double y;
            switch (deg)
            {
                case 2:
                    for (int i = 2; i < orig.Count - 2; i++)
                    {
                        y = -2 * orig[i - 2].Y - orig[i - 1].Y + orig[i + 1].Y + 2 * orig[i + 2].Y;
                        rslt.AddPoint(y / 10, orig[i].X);
                    }
                    break;
                case 3:
                    for (int i = 3; i < orig.Count - 3; i++)
                    {
                        y = -3 * orig[i - 3].Y - 2 * orig[i - 2].Y - orig[i - 1].Y +
                            orig[i + 1].Y + 2 * orig[i + 2].Y + 3 * orig[i + 3].Y;
                        rslt.AddPoint(y / 28, orig[i].X);
                    }
                    break;
                case 4:
                    for (int i = 4; i < orig.Count - 4; i++)
                    {
                        y = -4 * orig[i - 4].Y - 3 * orig[i - 3].Y - 2 * orig[i - 2].Y - orig[i - 1].Y +
                            orig[i + 1].Y + 2 * orig[i + 2].Y + 3 * orig[i + 3].Y + 4 * orig[i + 4].Y;
                        rslt.AddPoint(y / 60, orig[i].X);
                    }
                    break;
            }
            return rslt;
        }

        private DataStack statsByHistoExt(DataStack src, double thresholdAmpl, double thresholdHisto, bool centered, bool upper, out SingleStatStr stats)
        // threshold is the level of distrimination as % of the max
        {
            Func<string, bool> log = new Func<string, bool>((txt) =>
            {
                //Console.WriteLine(txt); 
                tbLog.AppendText(txt + "\n");
                return true;
            });
            DataStack rslt = new DataStack();
            stats.level = 0; stats.sd = 0;
            try
            {
                if (src.Count == 0) throw new Exception("No data to be processed");
                if (upper) log(">-- calculating upper part --<");
                else log(">-- calculating lower part --<");
                DataStack offmd;
                if (centered)
                {
                    double offsetY = src.pointYs().Sum() / src.Count;
                    log("offset by Y= " + offsetY.ToString("G3"));
                    offmd = src.Clone(0, -offsetY);
                }
                else offmd = src.Clone();

                double minY = src.pointYs().Min(); double maxY = src.pointYs().Max();
                DataStack md = new DataStack();
                for (int i = 0; i < src.Count; i++)
                {
                    if (upper)
                    {
                        if (offmd[i].Y < (thresholdAmpl / 100) * maxY) continue;
                        md.AddPoint(offmd[i].Y, offmd[i].X);
                    }
                    else
                    {
                        if (offmd[i].Y > (thresholdAmpl / 100) * minY) continue;
                        md.AddPoint(-offmd[i].Y, offmd[i].X);
                    }
                }

                DataStack hst = AxelChart1.Histogram(md);
                // peak detection
                double[] amplitudes, locations, secondDerivatives;
                double initialThreshold;
                int initialWidth;
                bool endOfData;

                PeakDetector peakDetector = new PeakDetector();
                // Set initial state of peakDetector
                initialThreshold = 0.0;
                initialWidth = 20;
                peakDetector.Reset(initialThreshold, initialWidth, PeakPolarity.Peaks);
                endOfData = true;

                // Find location of amplitude, locations and second derivates of peaks in signalIn array
                peakDetector.Detect(hst.pointYs(), endOfData, out amplitudes, out locations, out secondDerivatives);
                int pkIdx = 0; double mx = 0;
                if (amplitudes.Length == 0) throw new Exception("No peaks found in histogram");
                if (locations.Length > 1) log(locations.Length.ToString() + " peaks found, searching for the highest one");
                for (int i = 0; i < amplitudes.Length; i++)
                {
                    if (mx > amplitudes[i]) continue;
                    mx = amplitudes[i];
                    pkIdx = i;
                }
                //log("peakPosIdx in histo= " + locations[pkIdx].ToString("G3"));
                // fit Gauss
                double ampl, res;
                double iAmpl = hst.pointYs().Max();
                int cIdx = (int)Math.Round(locations[pkIdx]); double iCentre = hst.pointXs()[cIdx];
                double iSDev = md.pointSDevY();
                double[] fitted = CurveFit.GaussianFit(hst.pointXs(), hst.pointYs(), FitMethod.Bisquare, iAmpl, iCentre, iSDev,
                                                       out ampl, out stats.level, out stats.sd, out res);
                cIdx = hst.indexByX(stats.level);
                if (cIdx == -1) throw new Exception("No level index found");
                log("ampl= " + ampl.ToString("G4") + "; level= " + stats.level.ToString("G4"));
                log("SDev= " + stats.sd.ToString("G4") + "; res= " + res.ToString("G4")); //+ "; cIdx= " + cIdx.ToString());
                int li = 0; int ri = hst.Count - 1;
                double lvl = (thresholdHisto / 100) * ampl;
                for (int i = cIdx; i < hst.Count; i++)
                {
                    if (hst[i].Y < lvl)
                    {
                        ri = i; break;
                    }
                }
                for (int i = cIdx; i > 0; i--)
                {
                    if (hst[i].Y < lvl)
                    {
                        li = i; break;
                    }
                }
                log("left Idx= " + li.ToString() + "; level= " + hst[li].X.ToString("G4"));
                log("right Idx= " + ri.ToString() + "; level= " + hst[ri].X.ToString("G4"));

                for (int i = 0; i < md.Count; i++)
                {
                    if (Utils.InRange(md[i].Y, hst[li].X, hst[ri].X)) rslt.AddPoint(md[i].Y, md[i].X);
                }
                log("sp.mean= " + rslt.pointYs().Average().ToString("G3") + "; sp.SDev= " + rslt.pointSDevY(false).ToString("G3")); //+"%"
                log("src.Cnt= " + src.Count.ToString() + "; rslt.Cnt= " + rslt.Count.ToString());

                return rslt;
            }
            catch (Exception e)
            {
                rslt.Clear();
                log("Error: " + e.Message);
                return rslt;
            }
        }

        // same as above but params from GUI
        public DataStack statsByHistoExt(DataStack src, bool upper, out SingleStatStr stats)
        {
            return statsByHistoExt(src, ntbAmplThre.Value, ntbHistoThre.Value, chkCentralize.IsChecked.Value, upper, out stats);
        }

        public List<DataStack> SplitCycles(DataStack src) // split upper or lower result of statsByHisto
        {
            double intervalThre = 0.2; int minCount = 10;
            List<DataStack> rslt = new List<DataStack>();
            rslt.Add(new DataStack());
            for (int i = 0; i < src.Count - 1; i++)
            {
                if ((src[i + 1].X - src[i].X) < intervalThre)
                {
                    rslt.Last().Add(src[i]);
                }
                else
                {
                    if (rslt.Last().Count < minCount) rslt.Last().Clear();
                    else rslt.Add(new DataStack());
                }
            }
            return rslt;
        }

        public StatsStr statsByHisto()
        {
            StatsStr rslt;
            //if (Utils.isNull(OriginalWaveform)) 
            OriginalWaveform = AxelChart1.Waveform.Clone();
            DataStack wu = statsByHistoExt(OriginalWaveform, true, out rslt.upper);
            DataStack wl = statsByHistoExt(OriginalWaveform, false, out rslt.lower);
            rslt.ok = (wu.Count > 0) && (wl.Count > 0);
            return rslt;
        }

        private double myResidue(DataStack src, double slope, double intercept)
        {
            double kai = 0;
            for (int i = 0; i < src.Count; i++)
            {
                kai += Math.Pow(src.pointYs()[i] - (src.pointXs()[i] * slope + intercept), 2);
            }
            return Math.Sqrt(kai / src.Count);
        }

        private StatsStr statsByLinRegr(DataStack src)
        {
            StatsStr mr, rslt;
            double slope, intercept, residue;
            DataStack wu = statsByHistoExt(src, true, out mr.upper);
            CurveFit.LinearFit(wu.pointXs(), wu.pointYs(), FitMethod.LeastSquare, out slope, out intercept, out residue);
            rslt.upper.level = intercept; rslt.upper.sd = residue; ; // myResidue(src, slope, intercept);

            DataStack wl = statsByHistoExt(src, false, out mr.lower);
            CurveFit.LinearFit(wl.pointXs(), wl.pointYs(), FitMethod.LeastSquare, out slope, out intercept, out residue);
            rslt.lower.level = intercept; rslt.lower.sd = residue;
            rslt.ok = (wu.Count > 0) && (wl.Count > 0);
            return rslt;
        }
        #endregion

        public void reportRslt(StatsStr stats)
        {
            if (!stats.ok)
            {
                lbResult.Content = "Problem...!";
                return;
            }
            lbResult.Content = "Aver.SDev = " + (100 * stats.mean().sd / stats.mean().level).ToString("G3") + "%";
            lbLvlP.Content = "level+ = " + stats.upper.level.ToString("G4");
            lvDspP.Content = "SDev+ = " + stats.upper.sd.ToString("G4");
            lbLvlM.Content = "level- = " + stats.lower.level.ToString("G4");
            lvDspM.Content = "SDev- = " + stats.lower.sd.ToString("G4");
            AxelChart1.Refresh();
        }

        private void btnShowDerv_Click(object sender, RoutedEventArgs e)
        {
            OriginalWaveform = AxelChart1.Waveform.Clone();
            AxelChart1.Waveform = CalcFirstDerv(AxelChart1.Waveform);
            AxelChart1.Refresh();
        }

        private void btnOriginal_Click(object sender, RoutedEventArgs e)
        {
            AxelChart1.Waveform = OriginalWaveform.Clone();
            AxelChart1.Refresh();
        }

        private void btnShowCleared_Click(object sender, RoutedEventArgs e)
        {
            if (Utils.isNull(OriginalWaveform)) OriginalWaveform = AxelChart1.Waveform.Clone();
            StatsStr stats;
            AxelChart1.Waveform = statsByHistoExt(OriginalWaveform, true, out stats.upper);
            DataStack wl = statsByHistoExt(OriginalWaveform, false, out stats.lower);
            stats.ok = (AxelChart1.Waveform.Count > 0) && (wl.Count > 0);
            reportRslt(stats);
        }

        private void btnSimul_Click(object sender, RoutedEventArgs e)
        {
            AxelChart1.Waveform.Clear(); AxelChart1.SamplingPeriod = 0.001; double d, w;
            for (int i = 0; i < 20000; i++)
            {
                d = AxelChart1.SamplingPeriod * i;
                /*w =  Math.Cos(6.28 * 10 *d) + Math.Cos(6.28 * 50 *d);
                for (int j = 1; j < 5; j++)
                {
                    w += Math.Cos(6.28 * 100 * j *d);
                }*/
                //Waveform.AddPoint(5*(0.5 * Math.Cos(6.28 * 30 * d) + Math.Cos(6.28 * 2 * d)), d);

                AxelChart1.Waveform.AddPoint(Math.Exp(d), d);
            }
            AxelChart1.Refresh();
        }


        private void btnLinRegr_Click(object sender, RoutedEventArgs e)
        {
            if (Utils.isNull(OriginalWaveform)) OriginalWaveform = AxelChart1.Waveform.Clone();
            /*  StatsStr stats = statsByLinRegr(OriginalWaveform);
              reportRslt(stats);*/
            /*    double v, va,vb, axel;
               Waveform.Clear();
               for (int i = 1; i < OriginalWaveform.Count - 1; i++)
               {

                   v = (OriginalWaveform[i + 1].Y - OriginalWaveform[i - 1].Y) / (2 * SamplingPeriod);
                   va = (OriginalWaveform[i].Y - OriginalWaveform[i - 1].Y) / SamplingPeriod;
                   vb = (OriginalWaveform[i + 1].Y - OriginalWaveform[i].Y) / SamplingPeriod;
                   axel = (vb - va) / SamplingPeriod;
         
                   AddPoint(axel, OriginalWaveform[i].X);
               } 
              feList.Clear(); afeList.Clear(); 
               List<double> posList = new List<double>();
               double[] iniCond = { 0.0, 0.0 };
               double[] velArr = SignalProcessing.Differentiate(OriginalWaveform.pointYs(), SamplingPeriod, iniCond, iniCond, DifferentiationMethod.Forward);
               double[] accelArr = SignalProcessing.Differentiate(velArr, SamplingPeriod, iniCond, iniCond, DifferentiationMethod.Forward);
               for (int i = 4; i < velArr.Length - 4; i++)
               {
                   AddPoint(accelArr[i], SamplingPeriod*i);             
               } 
            double Xmin = Math.Max(((AxisDouble)graphScroll.Axes[0]).Range.Minimum, Waveform[0].X);
            double Xmax = Math.Min(((AxisDouble)graphScroll.Axes[0]).Range.Maximum, Waveform[Waveform.Count - 2].X);

            int X0 = AxelChart1.Waveform.indexByX(Xmin);
            int X1 = AxelChart1.Waveform.indexByX(Xmax);

            if ((X0 == -1) || (X1 == -1)) throw new Exception("Problem with X range of scroll graph");
            Console.WriteLine(X0.ToString() + " / " + X1.ToString());
            DataStack partial = new DataStack();

            for (int i = X0; i < X1; i++)
            {
                partial.Add(AxelChart1.Waveform[i]);
            }
            tbLog.AppendText("top part: M= " + partial.pointYs().Average().ToString("G3") + "; SD= " + partial.pointSDevY(false).ToString("G3") +
                                " (" + partial.pointSDevY(true).ToString("G3") + "%)\n");*/
        }

        public void OpenFileRef(string fn)
        {
            //Group1.Pos.CurrentPosition	Group1.Pos.CurrentVelocity	Group1.Pos.CurrentAcceleration	
            //Group1.Pos.SetpointPosition	Group1.Pos.SetpointVelocity	Group1.Pos.SetpointAcceleration	Group1.Pos.FollowingError
            int j = -1;
            movePrmStr mp;
            string[] prms;
            double timeInterval = 1;
            List<movePrmStr> mList = new List<movePrmStr>();
            foreach (string line in File.ReadLines(fn))
            {
                j++;
                if (j == 0) // first row
                {
                    prms = line.Split('\t');
                    double.TryParse(prms[0], out timeInterval);
                }
                if (j < 2) continue; // skip first two rows
                prms = line.Split('\t');
                if (prms.Length != 8) break; // end of list or broken (incomplete) row 
                mp.TimeElapsed = j * timeInterval;
                if (!double.TryParse(prms[0], out mp.CurrentPosition)) throw new Exception("Wrong double at line " + j.ToString());
                if (!double.TryParse(prms[1], out mp.CurrentVelocity)) throw new Exception("Wrong double at line " + j.ToString());
                if (!double.TryParse(prms[2], out mp.CurrentAcceleration)) throw new Exception("Wrong double at line " + j.ToString());
                //if (prms[3].Equals("0") || prms[4].Equals("0") || prms[5].Equals("0")) continue; // ??? clean or raw
                if (!double.TryParse(prms[3], out mp.SetpointPosition)) throw new Exception("Wrong double at line " + j.ToString());
                if (!double.TryParse(prms[4], out mp.SetpointVelocity)) throw new Exception("Wrong double at line " + j.ToString());
                if (!double.TryParse(prms[5], out mp.SetpointAcceleration)) throw new Exception("Wrong double at line " + j.ToString());
                if (!double.TryParse(prms[6], out mp.FollowingError)) throw new Exception("Wrong double at line " + j.ToString());
                mp.CalcVelocity = 0; mp.CalcAcceleration = 0;
                mList.Add(mp);
            }
            j = 0;
            Clear();
            TimeMode = false;
            SizeLimit = mList.Count;
            List<double> feList = new List<double>(); List<double> afeList = new List<double>();
            List<double> posList = new List<double>();
            for (int i = 0; i < mList.Count; i++)
            {
                posList.Add(mList[i].CurrentPosition);
            }
            double[] iniCond = { 0.0, 0.0 };
            double[] velArr = SignalProcessing.Differentiate(posList.ToArray(), timeInterval, iniCond, iniCond, DifferentiationMethod.Forward);
            double[] accelArr = SignalProcessing.Differentiate(velArr, timeInterval, iniCond, iniCond, DifferentiationMethod.Forward);
            for (int i = 4; i < mList.Count - 4; i++)
            {
                mp = mList[i];
                //mp.CalcVelocity = velArr[i];
                mp.CalcAcceleration = accelArr[i];
                feList.Add(mp.FollowingError); afeList.Add(Math.Abs(mp.FollowingError));
                Add(new Point(mp.TimeElapsed, mp.CalcAcceleration));
                mList[i] = mp;
            }
            Double average = feList.Average(); RefFileStats["Mean_FE"] = average;
            double sumOfSquaresOfDifferences = feList.Select(val => (val - average) * (val - average)).Sum();
            RefFileStats["SDev_FE"] = Math.Sqrt(sumOfSquaresOfDifferences / feList.Count);
            RefFileStats["Min_FE"] = feList.Min(); RefFileStats["Max_FE"] = feList.Max();
            RefFileStats["Mean_abs_FE"] = afeList.Average();
        }
    }
}
