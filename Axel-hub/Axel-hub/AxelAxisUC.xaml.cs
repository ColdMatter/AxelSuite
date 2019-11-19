﻿using NationalInstruments.Net;
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
using NationalInstruments.NetworkVariable;
using NationalInstruments.NetworkVariable.WindowsForms;
using NationalInstruments.Tdms;
using NationalInstruments.Controls;
using NationalInstruments.Controls.Rendering;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

using System;
using System.IO;
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
using AxelHMemsNS;
using UtilsNS;
using OptionsNS;

namespace Axel_hub
{
    /// <summary>
    /// Interaction logic for AxelAxisUC.xaml
    /// </summary>
    public partial class AxelAxisClass : UserControl
    {
        private string _prefix;
        public string prefix 
        {
            get { return _prefix; } 
            private set
            {
                _prefix = value;
                if (value.Equals("Y"))
                {
                    gbJumboParams.IsEnabled = false;
                    lbJPrem.Content = "Jumbo Params in X";
                }                   
            } 
        }
        private const int dataLength = 10000;
        private DataStack phiMg = new DataStack(dataLength);
        private DataStack accelMg = new DataStack(dataLength);
        private DispatcherTimer dTimer; 
        private Random random = new Random();
        public List<Point> timeStack = new List<Point>(); // x - time[s]; y - phi[rad]        
        List<Point> quantList = new List<Point>(); List<string> errList = new List<string>();

        public AxelAxisClass()
        {
            InitializeComponent();
        }

        GeneralOptions genOptions = null; ScanModes scanModes = null; Modes modes = null; string modesFile = "";
        public void Init(string _prefix, ref GeneralOptions _genOptions, ref ScanModes _scanModes, ref AxelMems _axelMems) // obligatory 
        {
            prefix = _prefix;
            if (Utils.isNull(_genOptions)) Utils.TimedMessageBox("Non-existant options");
            else genOptions = _genOptions;
            scanModes = _scanModes;
            OpenDefaultModes();
            axelChart.InitOptions(ref genOptions, ref modes, ref _axelMems, prefix);
            strobes.Init(prefix);

            tabSecPlots.SelectedIndex = 1;
        }

        private bool probeMode // simulation of MM2 with AxelProbe
        {
            get
            {
                if (Utils.isNull(lastGrpExe)) return false;
                return lastGrpExe.mmexec.Equals("simulation");
            }
        }

        public bool AxelChartVisible
        {
            get { return axelChart.Visibility == System.Windows.Visibility.Visible; }
            set
            {
                if (value)
                {
                    rowUpperChart.Height = new GridLength((Application.Current.MainWindow.Height - 60)/3);
                    axelChart.Visibility = System.Windows.Visibility.Visible;
                    topSplitter.Visibility = System.Windows.Visibility.Visible;
                }
                else
                {
                    rowUpperChart.Height = new GridLength(1);
                    axelChart.Visibility = System.Windows.Visibility.Collapsed;
                    topSplitter.Visibility = System.Windows.Visibility.Collapsed;
                }
            }
        }

        private void OpenDefaultModes(bool Middle = true, bool Bottom = true)
        {
            modesFile = Utils.configPath + prefix + "_defaults.cfg"; 
            if (File.Exists(modesFile))
            {
                string fileJson = File.ReadAllText(modesFile);
                modes = JsonConvert.DeserializeObject<Modes>(fileJson);
            }
            else
                modes = new Modes();

            //double h = Application.Current.MainWindow.Height - 60;
            bool wannaShowMems = (genOptions.ShowMemsIfRunning && genOptions.MemsInJumbo) || !genOptions.ShowMemsIfRunning;

            if (wannaShowMems)
            {
                if (modes.TopFrame < 2) AxelChartVisible = true;
                else rowUpperChart.Height = new GridLength(modes.TopFrame);
            }
            else AxelChartVisible = false;
            rowMiddleChart.Height = new GridLength(modes.MiddleFrame);

            if (Middle)
            {
                ucSignal.InitOptions(ref genOptions, ref modes, prefix);
                ucSignal.OpenDefaultModes();

                chkBigCalcTblUpdate.IsChecked = modes.RsltTblUpdate;
                chkBigCalcChrtUpdate.IsChecked = modes.RsltChrtUpdate;
                chkJoinLog.IsChecked = modes.JoinLog;
                chkSignalLog.IsChecked = modes.SignalLog;
            }
            if (Bottom)
            {
                numFrom.Value = modes.JumboFrom;
                numTo.Value = modes.JumboTo;
                numBy.Value = modes.JumboBy;
                numCycles.Value = modes.JumboCycles;

                numKcoeff.Value = modes.Kcoeff;
                numPhi0.Value = modes.phi0;
                numScale.Value = modes.scale;
                numOffset.Value = modes.offset;

                chkAutoScaleBottom.IsChecked = modes.AutoScaleBottom;                
            }
        }

        private void UpdateStrobesParams()
        {
            if (Utils.isNull(strobes)) return;            
            strobes.PID_Enabled = genOptions.followPID;
        }

        public delegate void LogHandler(string txt, Color? clr = null);
        public event LogHandler OnLog;

        public void LogEvent(string txt, Color? clr = null)
        {
            if (!Utils.isNull(OnLog)) OnLog(txt, clr);
        }

        public delegate bool SendHandler(string json, bool async = false);
        public event SendHandler OnSend;

        public bool SendEvent(string json, bool async = false)
        {
            if (!Utils.isNull(OnSend)) return OnSend(json, async);
            else return false;
        }

        double hiddenTopHeight = 230.0;
        private void splitterTop_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (rowUpperChart.Height.Value < 5) rowUpperChart.Height = new GridLength(Math.Min(230, hiddenTopHeight), GridUnitType.Pixel);
            else
            {
                hiddenTopHeight = rowUpperChart.Height.Value;
                rowUpperChart.Height = new GridLength(3, GridUnitType.Pixel);
            }
        }

        double middlePlotHeight = 230;
        private void tabSecPlots_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Utils.isNull(sender)) tabSecPlots.SelectedIndex = 0;
            if (tabSecPlots.SelectedIndex == 0)
            {
                middlePlotHeight = rowMiddleChart.Height.Value;
                rowMiddleChart.Height = new GridLength(35, GridUnitType.Pixel);
            }
            else
            {
                if (rowMiddleChart.Height.Value < 38) rowMiddleChart.Height = new GridLength(Math.Min(230, middlePlotHeight), GridUnitType.Pixel);
            }
        }

        ShotList shotList; // arch - on
        private void btnOpenJLog_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.FileName = ""; // Default file name
            dlg.DefaultExt = ".jlg"; // Default file extension
            dlg.Filter = "Join Log File (.jlg)|*.jlg"; // Filter files by extension

            Nullable<bool> result = dlg.ShowDialog();
            if (result == true)
            {
                lbJoinLogInfo.Content = "File: " + dlg.FileName;
                shotList = new ShotList(true, dlg.FileName);
            }
            btnJDlyScan.IsEnabled = File.Exists(shotList.filename) && !shotList.savingMode;
        }

        DataStack srsMdiffQ = new DataStack();
        public DataStack srsFringes = null; DataStack srsMotAccel = null; DataStack srsCorr = null; DataStack srsMems = null; DataStack srsAccel = null;

        private void btnJDlyScan_Click(object sender, RoutedEventArgs e)
        {
            if (Utils.isNull(shotList)) return;
            btnJDlyScan.Value = !btnJDlyScan.Value;
            if (!btnJDlyScan.Value) return;
            if (shotList.savingMode || shotList.filename.Equals("")) throw new Exception("problem with shotList");
            //tabLowPlots.SelectedIndex = 1;
            if (Utils.isNull(srsMotAccel)) srsMotAccel = new DataStack(dataLength);
            if (Utils.isNull(srsCorr)) srsCorr = new DataStack(dataLength);
            if (Utils.isNull(srsMems)) srsMems = new DataStack(dataLength);
            if (Utils.isNull(srsAccel)) srsAccel = new DataStack(dataLength);

            srsMdiffQ.Clear(); double xMin = 1E6, xMax = -1e6;
            double wd = numJFrom.Value / 1000.0;
            SingleShot ss; bool next; int j;
            shotList.resetScan();
            if (!shotList.conditions.Count.Equals(0))
            {
                OnLog(">> processing conditions:", Brushes.DarkSlateGray.Color);
                foreach (KeyValuePair<string, double> pair in shotList.conditions)
                {
                    OnLog(pair.Key + " = " + pair.Value, Brushes.Teal.Color);
                }
            }
            shotList.resetScan(); if (!shotList.archiveMode) OnLog("The file is loaded in memory -> " + shotList.FileCount.ToString() + " shots.", Brushes.DarkSlateGray.Color);
            while ((wd <= (numJTo.Value / 1000.0)) && btnJDlyScan.Value)
            {
                srsMotAccel.Clear(); srsCorr.Clear(); srsMems.Clear(); srsAccel.Clear();
                shotList.resetScan(); j = 0;
                do
                {
                    ss = shotList.archyScan(out next); 
                    srsMotAccel.Add(ss.quant); xMin = Math.Min(xMin, ss.quant.X); xMax = Math.Max(xMax, ss.quant.X);
                    double m = ss.memsWeightAccel(wd, genOptions.Mems2SignLen / 1000.0, true);

                    srsMems.AddPoint(m, ss.quant.X + wd);
                    srsCorr.AddPoint((m - ss.quant.Y) * (m - ss.quant.Y), ss.quant.X);
                    if (chkChartEachIter.IsChecked.Value)
                    {
                        if (!srsMems.Count.Equals(0)) graphAccelTrend.Data[0] = srsMems;
                        if (!srsCorr.Count.Equals(0)) graphAccelTrend.Data[1] = srsCorr;
                        if (!srsMotAccel.Count.Equals(0)) graphAccelTrend.Data[2] = srsMotAccel;
                        if (!srsAccel.Count.Equals(0)) graphAccelTrend.Data[3] = srsAccel;
                    }
                    Utils.DoEvents();
                    j++;
                } while (next && (numJNPnts.Value.Equals(-1) || (j < numJNPnts.Value)) && btnJDlyScan.Value);
                lbInfoAccelTrend.Content = "Info: shot # " + j.ToString();
                if (btnJDlyScan.Value)
                {
                    if (chkChartEachIter.IsChecked.Value)
                    {
                        double d = 0.02 * (xMax - xMin);
                        accelXaxis.Adjuster = RangeAdjuster.None;
                        accelXaxis.Range = new Range<double>(xMin - d, xMax + d);
                    }
                    srsMdiffQ.AddPoint(srsCorr.pointYs().Average(), wd * 1000.0);
                    graphJoinOptim.Data[0] = srsMdiffQ;
                }
                lbJoinLogInfo.Content = "File: " + shotList.filename + " ; Delay = " + (wd * 1000.0).ToString("G4");
                Utils.DoEvents();
                wd += numJBy.Value / 1000.0;
            }
            accelXaxis.Adjuster = RangeAdjuster.FitLoosely;
            btnJDlyScan.Value = false;
            double xm = 0; double ym = 1e6;
            foreach (Point pnt in srsMdiffQ)
            {
                if (pnt.Y < ym)
                {
                    xm = pnt.X; ym = pnt.Y;
                }
            }
            lbJoinLogInfo.Content = "File: " + shotList.filename + " ; Minimum at " + xm.ToString("G5") + " / " + ym.ToString("G5");
            lbInfoAccelTrend.Content = "Info:";
        }

        #region File operation
        private void btnOpenSignal_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.FileName = ""; // Default file name
            dlg.DefaultExt = ".ahs"; // Default file extension
            dlg.Filter = "Axel Hub Signal (.ahs)|*.ahs"; // Filter files by extension

            // Show save file dialog box
            Nullable<bool> result = dlg.ShowDialog();

            // Process save file dialog box results
            string rem = "";
            if (result == true) ucSignal.OpenSignal(dlg.FileName, out rem);
            tbRemSignal.Text = rem;
            OnLog("Opened> " + dlg.FileName);
        }

        private void btnSaveSignalAs_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.FileName = ""; // Default file name
            dlg.DefaultExt = ".ahs"; // Default file extension
            dlg.Filter = "Axel Hub Signal (.ahs)|*.ahs"; // Filter files by extension

            // Show save file dialog box
            Nullable<bool> result = dlg.ShowDialog();
            if (result.Equals(true))
            {
                ucSignal.SaveSignal(dlg.FileName, tbRemSignal.Text);
                OnLog("Saved> " + dlg.FileName);
            }
        }

        private void btnOpenFringes_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.FileName = ""; // Default file name
            dlg.DefaultExt = ".ahf"; // Default file extension
            dlg.Filter = "Axel Hub File (.ahf)|*.ahf"; // Filter files by extension

            // Show save file dialog box
            Nullable<bool> result = dlg.ShowDialog();

            // Process save file dialog box results

            if (result != true) return;
            if (Utils.isNull(srsFringes)) srsFringes = new DataStack(); GroupBox gb = null;
            srsFringes.OpenPair(dlg.FileName, ref gb);
            graphFringes.Data[0] = srsFringes;
            lbInfoFringes.Content = srsFringes.rem;
            tbRemFringes.Text = srsFringes.rem;
        }

        private void btnSaveFringesAs_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.FileName = ""; // Default file name
            dlg.DefaultExt = ".ahf"; // Default file extension
            dlg.Filter = "Axel Hub File (.ahf)|*.ahf"; // Filter files by extension

            // Show save file dialog box
            Nullable<bool> result = dlg.ShowDialog();
            srsFringes.rem = tbRemFringes.Text;
            if (srsFringes.rem.Equals("")) srsFringes.rem = (string)lbInfoFringes.Content;
            if (result == true) srsFringes.SavePair(dlg.FileName, "", "G6"); //genOptions.SaveFilePrec);
        }

        private void btnClearAll_Click(object sender, RoutedEventArgs e)
        {
            if (sender == btnClearSignal)
                Clear(false, true, false);
            if (sender == btnClearAll)
                Clear();
            if (sender == btnClearFringes)
                Clear(false, false, true);
        }
        #endregion

        private double phaseCorr, phaseRad, fringesYmin = 10, fringesYmax = -10, accelYmin = 10, accelYmax = -10;

        public void Clear(bool Top = true, bool Middle = true, bool Bottom = true)
        {
            lastGrpExe = null;
            if (Top)
            {
                axelChart.Clear();
                axelChart.Refresh();
            }
            if (Middle)
            {
                tbRemSignal.Text = "";
                ucSignal.Clear();
            }
            if (Bottom)
            {
                if (!Utils.isNull(srsFringes)) srsFringes.Clear();
                graphFringes.Data[0] = srsFringes;

                if (!Utils.isNull(srsMotAccel)) srsMotAccel.Clear();
                if (!Utils.isNull(srsCorr)) srsCorr.Clear();
                if (!Utils.isNull(srsMems)) srsMems.Clear();
                if (!Utils.isNull(srsAccel)) srsAccel.Clear();
                graphAccelTrend.Data[0] = srsMems; graphAccelTrend.Data[1] = srsCorr;
                graphAccelTrend.Data[2] = srsMotAccel; graphAccelTrend.Data[3] = srsAccel;
                Utils.DoEvents();
                fringesYmin = 10; fringesYmax = -10; accelYmin = 10; accelYmax = -10;
            }
        }

        private void graphNs_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            (sender as Graph).ResetZoomPan();
            if (sender == graphFringes)
            {
                fringesYmin = 10; fringesYmax = -10;
            }
            if (sender == graphAccelTrend)
            {
                accelYmin = 10; accelYmax = -10;
            }
        }

        private void rbSingle_Checked(object sender, RoutedEventArgs e)
        {
            crsDownStrobe.AxisValue = 1.5; crsUpStrobe.AxisValue = 4.6;
        }

        DispatcherTimer ddTimer;
        private void btnJoinLogTest_Click(object sender, RoutedEventArgs e) // internal simulation test
        {
            btnJoinLogTest.Value = !btnJoinLogTest.Value;
            if (!axelChart.Waveform.stopWatch.IsRunning && btnJoinLogTest.Value)
            {
                Utils.TimedMessageBox("Waveform Stopwatch is NOT running.");
                btnJoinLogTest.Value = false;
                return;
            }
            if (btnJoinLogTest.Value)
            {
                resetQuantList();
                if (Utils.isNull(ddTimer))
                {
                    ddTimer = new DispatcherTimer();
                    ddTimer.Tick += new EventHandler(ddTimer_Tick);
                    ddTimer.Interval = new TimeSpan(500 * 10000);
                }
                ddTimer.Start();
            }
            else
            {
                ddTimer.Stop();
                if (errList.Count > 0)
                {
                    Utils.writeList(Utils.dataPath + "errors.jlg", errList);
                    Utils.TimedMessageBox("Some errors, check <errors.jlg> file");
                }
            }
            shotList.enabled = btnJoinLogTest.Value;
        }
 
        private void ddTimer_Tick(object sender, EventArgs e)
        {
            quantList.Add(new Point(axelChart.Waveform.stopWatch.ElapsedMilliseconds / 1000.0, 5)); // 
        }

        private void SaveTrend(string FileName)  // srsMems, srsMotAccel, srsAccel, srsCorr
        {
            if (srsMems.Count == 0 || srsMotAccel.Count == 0)
            {
                Utils.TimedMessageBox("Error: No trend data to be saved !", "ERROR", 2500);
                return;
            }
            DictFileLogger fl = new DictFileLogger(new string[]{"Time","MEMS","MOTaccel","Accel","Corr"}, prefix, FileName);
            fl.defaultExt = ".aht"; fl.setMMexecAsHeader(lastGrpExe);
            fl.Enabled = true;

            Dictionary<string, double> dt = new Dictionary<string, double>();
            for (int i = 0; i < srsMems.Count; i++)
            {
                dt["Time"] = srsMems[i].X; dt["MEMS"] = srsMems[i].Y; dt["MOTaccel"] = srsMotAccel[i].Y; dt["Accel"] = srsAccel[i].Y; 
                if (!Utils.isNull(srsCorr))
                {
                    if (srsCorr.Count > i) dt["Corr"] = srsCorr[i].Y;
                }
                fl.dictLog(dt, genOptions.SaveFilePrec);
            }
            fl.Enabled = false;
            OnLog("Saved> " + FileName);
        }

        private void btnSaveTrendAs_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.FileName = ""; // Default file name
            dlg.DefaultExt = ".aht"; // Default file extension
            dlg.Filter = "Axel Hub Trend (.aht)|*.aht"; // Filter files by extension

            // Show save file dialog box
            Nullable<bool> result = dlg.ShowDialog();
            if (result == true) SaveTrend(dlg.FileName);
        }

        public void resetQuantList()
        {
            quantList.Clear(); // [time,MOTaccel] list of pairs
            errList.Clear(); // accumulates errors
            shotList = new ShotList(chkJoinLog.IsChecked.Value,"",prefix); 
            setConditions(ref shotList.conditions); 
        }

        private void setConditions(ref Dictionary<string, double> dc)
        {
            dc.Clear();
            //dc["sampling"] = Utils.formatDouble(1 / ucScan.GetSamplingPeriod(), "G5");
            dc["K"] = numKcoeff.Value;
            dc["phi0"] = numPhi0.Value;
            dc["scale"] = numScale.Value;
            dc["offset"] = numOffset.Value;
        }

        public void visStrobes(bool enabled)
        {
            if (enabled)
            {
                crsDownStrobe.Visibility = System.Windows.Visibility.Visible;
                crsUpStrobe.Visibility = System.Windows.Visibility.Visible;
                btnSinFit.Visibility = System.Windows.Visibility.Visible;
                if (probeMode) strobes.Reset();
                    
                crsDownStrobe.AxisValue = strobes.Down.X;
                crsUpStrobe.AxisValue = strobes.Up.X;
            }
            else
            {
                crsDownStrobe.Visibility = System.Windows.Visibility.Collapsed;
                crsUpStrobe.Visibility = System.Windows.Visibility.Collapsed;
                btnSinFit.Visibility = System.Windows.Visibility.Collapsed;

                graphFringes.Data[1] = null;
            }            
        }

        private void btnSinFit_Click(object sender, RoutedEventArgs e)
        {
            if (srsFringes.Count == 0)
            {
                Utils.errorMessage("No data points to fit over."); return;
            }
            double[] xs = srsFringes.pointXs(); double[] ys = srsFringes.pointYs();
            double[] coeffs = new double[4];
            // signal(x) = scale[0] * sin(K[1]*x + phi0[2]) + offset[3] -> idx in coeffs
            coeffs[0] = numScale.Value; // scale
            coeffs[1] = numKcoeff.Value; // K
            coeffs[2] = numPhi0.Value; // phi0 
            coeffs[3] = numOffset.Value; // Offset.Y
            double meanSquaredError;
            ModelFunctionCallback callback = new ModelFunctionCallback(ModelFunction);
            double[] fittedData = CurveFit.NonLinearFit(xs, ys, callback, coeffs, out meanSquaredError, 100);
            coeffs[2] = MMDataConverter.Restrict2twoPI(coeffs[2]);
            DataStack fit = new DataStack(DataStack.maxDepth);
            fit.importFromArrays(xs, fittedData);
            graphFringes.Data[1] = fit;

            LogEvent("FIT.meanSqError = " + meanSquaredError.ToString("G5"), Brushes.DarkCyan.Color);
            LogEvent("K = " + coeffs[1].ToString("G5"), Brushes.DarkCyan.Color);
            LogEvent("Phase0 = " + coeffs[2].ToString("G5"), Brushes.DarkCyan.Color);
            LogEvent("Scale = " + coeffs[0].ToString("G5"), Brushes.DarkCyan.Color);
            LogEvent("Offset = " + coeffs[3].ToString("G5"), Brushes.DarkCyan.Color);

            visStrobes(true);

            //numScale.Value = coeffs[0]; numKcoeff.Value = coeffs[1]; numPhi0.Value = coeffs[2];  numOffset.Value = coeffs[3]; 

        }
        // Callback function that implements the fitting model 
        private double ModelFunction(double x, double[] coefficients)
        {
            return (coefficients[0] * Math.Sin(x / coefficients[1] + coefficients[2])) + coefficients[3];
        }

        public bool CombineQuantMems(List<Point> mds, double dur = 0.005, double dly = 0) // mds - last acquired MEMS buffer; dur [s] - quant sample duration: +/- dur (3*dur in total)
        {                                                                                 // dly [s] - time delay of mems start ref to quant,
            if (mds.Count.Equals(0) || quantList.Count.Equals(0) || !shotList.enabled || (dur <= 0)) return false;
            int pCount = shotList.Count;
            List<Point> ds; 
            for (int i = 0; i < quantList.Count; i++)
            {
                if (quantList[i].X < 0) continue; // already processed
                double from = quantList[i].X + dly; // shifted beginning
                double w0 = from - dur; double w1 = from + 2 * dur; // window of interest 
                if (w0 < mds[0].X) // if the window starts before the beginning of the buffer, then go to the Waveform
                {
                    ds = axelChart.Waveform.TimePortion(w0, w1);
                    if (ds.Count > 0)
                        shotList.Add(new SingleShot(quantList[i], ds)); // if empty skip that quantList point
                    else errList.Add("quant.t <limits> 1: " + quantList[i].X.ToString("G5") + " <" + w0.ToString("G5")+", "+w1.ToString("G5")+">");
                    quantList[i] = new Point(-quantList[i].X, quantList[i].Y); // mark as processed (good or bad)
                    continue;
                }
                if ((mds[0].X < w0) && (w1 < mds[mds.Count - 1].X)) // the window of interest is well inside the MEMS buffer
                {
                    SingleShot ss = new SingleShot(quantList[i], mds);
                    ss.mems = ss.memsPortion(new Range<double>(w0, w1));
                    if (ss.mems.Count > 0) shotList.Add(ss);
                    else errList.Add("quant <limits> 2: " + quantList[i].X.ToString("G5") + " <" + w0.ToString("G5") + ", " + w1.ToString("G5") + ">");
                    quantList[i] = new Point(-quantList[i].X, quantList[i].Y); // mark as processed if good
                }
                // in any other case - keep going
            }
            for (int i = quantList.Count - 1; i > 0; i--)
                if (quantList[i].X < 0) quantList.RemoveAt(i); // remove processed points
            // if any points left in quantList they will go for the next turn
            string st = (axelChart.Waveform.stopWatch.IsRunning) ? "; time[s] = "+(axelChart.Waveform.stopWatch.ElapsedMilliseconds / 1000.0).ToString("G5") : "";
            lbInfoAccelTrend.Content = "Info: shots # " + shotList.Count.ToString()+st;
            for (int i = pCount; i < shotList.Count; i++)
            {
                Dictionary<string, double> dt = shotList[i].deconstructAccel(strobes.fringeScale);
                if (dt.Count.Equals(0))
                {
                    LogEvent("Error: empty shot #" + i.ToString(), Brushes.Red.Color);
                    continue;
                }
                showResults(i, dt);
            }
            return true;
        }

        public void SaveDefaultModes(bool Top = true, bool Middle = true, bool Bottom = true)
        {
            double h = Application.Current.MainWindow.Height - 60;
            modes.TopFrame = Utils.EnsureRange(rowUpperChart.Height.Value, 50,600);
            modes.MiddleFrame = Utils.EnsureRange(rowMiddleChart.Height.Value, 50,600);

            if (Top) axelChart.modesFromVisual();

            if (Middle)
            {
                ucSignal.SaveDefaultModes();

                modes.RsltTblUpdate = chkBigCalcTblUpdate.IsChecked.Value;
                modes.RsltChrtUpdate = chkBigCalcChrtUpdate.IsChecked.Value;
                modes.JoinLog = chkJoinLog.IsChecked.Value;
                modes.SignalLog = chkSignalLog.IsChecked.Value;
            }
            if (Bottom)
            {
                modes.JumboFrom = numFrom.Value;
                modes.JumboTo = numTo.Value;
                modes.JumboBy = numBy.Value;
                modes.JumboCycles = (int)numCycles.Value;

                modes.Kcoeff = numKcoeff.Value;
                modes.phi0 = numPhi0.Value;
                modes.scale = numScale.Value;
                modes.offset = numOffset.Value;

                modes.AutoScaleBottom = chkAutoScaleBottom.IsChecked.Value;
                modes.PID_Enabled = genOptions.followPID;

                strobes.SaveConfigFile();
            }
            modes.Save(prefix);
        }

       private void chkMEMS_Checked(object sender, RoutedEventArgs e)
        {
            if (Utils.isNull(plotMems)) return;
            if (chkMEMS.IsChecked.Value) plotMems.Visibility = System.Windows.Visibility.Visible;
            else plotMems.Visibility = System.Windows.Visibility.Hidden;
            if (chkCorr.IsChecked.Value) plotCorr.Visibility = System.Windows.Visibility.Visible;
            else plotCorr.Visibility = System.Windows.Visibility.Hidden;
            if (chkMOT.IsChecked.Value) plotMotAccel.Visibility = System.Windows.Visibility.Visible;
            else plotMotAccel.Visibility = System.Windows.Visibility.Hidden;
            if (chkAccel.IsChecked.Value) plotAccel.Visibility = System.Windows.Visibility.Visible;
            else plotAccel.Visibility = System.Windows.Visibility.Hidden;
        }

        public MMscan jumboScan()
        {
            MMscan mms = new MMscan();
            mms.groupID = DateTime.Now.ToString("yy-MM-dd_H-mm-ss");
            mms.sParam = "ramanPhase"; // phase is default
            mms.sFrom = numFrom.Value;
            mms.sTo = numTo.Value;
            mms.sBy = numBy.Value;
            return mms;
        }

        public void UpdateFromOptions(bool connected)
        {
            if (Utils.isNull(strobes)) return;
            ucSignal.NsCursor.ValuePresenter = new ValueFormatterGroup(" — ", new GeneralValueFormatter("0.00"))
            {
                ValueFormatters = { new GeneralValueFormatter(genOptions.SignalCursorPrec) }
            };
            if (connected) AxelChartVisible = (genOptions.ShowMemsIfRunning && genOptions.MemsInJumbo) || !genOptions.ShowMemsIfRunning;
            else AxelChartVisible = true;
            UpdateStrobesParams();
        }
        
        private int timeStackLimit = 3; // process back 30 time steps

        private Dictionary<string, double> prepNextMeasure(double phi)
        {
            Dictionary<string, double> rslt = new Dictionary<string, double>();

            if (!Double.IsNaN(phi)) rslt["PhiRad"] = phi;

            double tm = axelChart.axelMems.TimeElapsed(); // default reference time stamp to look backwards
            if (Double.IsNaN(tm)) LogEvent("Error: ADC24 stopwatch not running !", Brushes.Red.Color);
            else rslt["time"] = tm;
            
            return rslt;
        } 
        
        // in simple_repeat only Mems; in jumbo_repeat - both
        private Dictionary<string, double> nextMeasure(double phi) // return Mems_V and PhiRad 
        {
            Dictionary<string, double> rslt = prepNextMeasure(phi);
            
        //    timeStack.Add(new Point(tm,phi));
        //    if (tm > timeStack[timeStack.Count - 1].X) timeStackLimit++;
        //    if(timeStack.Count < timeStackLimit) return pnt; // none
        //    else timeStack.RemoveAt(0);
        //    tm = timeStack[0].X;
                           
        //    double strt = tm - (numMemsStart.Value - numMemsLen.Value)/1000.0;
        //    double len = numMemsLen.Value / 1000.0; 
        /*    double mn, dsp;
            //if (AxelChart1.Waveform.statsByTime(strt, len, out mn, out wmn, out dsp)) 

            int j = axelChart.Waveform.Count - 1; // index of the last
            if (j == -1)
            {
                LogEvent("Waiting for incomming MEMS data (AxelChart1.Waveform) !", Brushes.DarkOrange.Color);
                return rslt;
            }
            if (axelChart.Waveform.statsByIdx(j - 100, j, false, out mn, out dsp))
            {
                rslt["MEMS_V"] = mn;
                LogEvent("Mems= " + mn.ToString("G6") + "; Phi= " + phi.ToString("G6"), Brushes.Teal.Color);
            }
            else LogEvent("Error in AxelChart1.Waveform.statsByIdx() !", Brushes.Red.Color);*/
            return rslt;
        }

        public MMscan lastScan = null; private MMexec lastGrpExe = null;
        public void DoPrepare(MMexec mme)
        {
            switch (mme.cmd)
            {
               case ("repeat"):
                    {
                        tabLowPlots.SelectedIndex = 1;
                        ucSignal.chkN1_Checked(null, null); // update state
                        // chkMEMS_Checked(null, null); !!!
                        Clear();
                        lastGrpExe = mme.Clone();
                        if (genOptions.MemsInJumbo)
                        {
                            axelChart.Waveform.TimeSeriesMode = true;
                            plotcursorAccel.Visibility = System.Windows.Visibility.Collapsed;
                        }
                        else
                        {
                            plotcursorAccel.Visibility = System.Windows.Visibility.Visible;                            
                        }
                        string ss = ";  Data Src: Quant-> ";
                        if(probeMode) ss += "Axel-probe";
                        else ss += "MM2";

                        ss += "; MEMS-> ";
                        if (genOptions.MemsInJumbo) ss += "NI-9251";
                        else
                        {
                            if (probeMode) ss += "Axel-probe";
                            else ss += "<NA>";
                        }
                         
                        lbInfoAccelTrend.Content = "grpID: " + mme.prms["groupID"] + ss; //lastGrpExe
                    }
                    break;
                case ("scan"):
                    {
                        lastGrpExe = mme.Clone();
                        lastScan = new MMscan();
                        if (lastScan.FromDictionary(mme.prms)) ucSignal.lastScan = lastScan.Clone();
                        tabLowPlots.SelectedIndex = 0;
                        ucSignal.chkN1_Checked(null, null);
                        Clear();
                        lbInfoFringes.Content = "groupID:" + lastScan.groupID + ";  Scanning: " + lastScan.sParam +
                            ";  From: " + lastScan.sFrom.ToString("G4") + ";  To: " + lastScan.sTo.ToString("G4") + ";  By: " + lastScan.sBy.ToString("G4");
                    }
                    break;
            }
            if (Utils.isNull(srsFringes)) srsFringes = new DataStack(dataLength);
            if (Utils.isNull(srsMotAccel)) srsMotAccel = new DataStack(dataLength);
            if (Utils.isNull(srsCorr)) srsCorr = new DataStack(dataLength);
            if (Utils.isNull(srsMems)) srsMems = new DataStack(dataLength);
            if (Utils.isNull(srsAccel)) srsAccel = new DataStack(dataLength);
            if (Utils.isNull(phiMg)) phiMg = new DataStack(1000);
            if (Utils.isNull(accelMg)) accelMg = new DataStack(1000);

            ucSignal.Init(mme); 
        }

        private void showResults(double xVl, Dictionary<string, double> dr)
        {
            if (chkBigCalcChrtUpdate.IsChecked.Value && !Double.IsNaN(xVl))
            {
                if (dr.ContainsKey("MEMS"))
                {
                    Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,
                        new Action(() =>
                        {
                            srsMems.AddPoint(dr["MEMS"], xVl); graphAccelTrend.Data[0] = srsMems.Portion(genOptions.TrendSignalLen);
                        }));
                }
                if (dr.ContainsKey("PhiCorr"))
                {
                    Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,
                        new Action(() =>
                        {
                            srsCorr.AddPoint(dr["PhiCorr"], xVl); graphAccelTrend.Data[1] = srsCorr.Portion(genOptions.TrendSignalLen);
                        }));
                }
                if (dr.ContainsKey("PhiMg"))
                {
                    Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,
                        new Action(() =>
                        {
                            srsMotAccel.AddPoint(dr["PhiMg"], xVl); graphAccelTrend.Data[2] = srsMotAccel.Portion(genOptions.TrendSignalLen);
                        }));
                }
                if (dr.ContainsKey("Accel"))
                {
                    Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,
                        new Action(() =>
                        {
                            srsAccel.AddPoint(dr["Accel"], xVl); graphAccelTrend.Data[3] = srsAccel.Portion(genOptions.TrendSignalLen);
                        }));
                }
            }
            if ((tabSecPlots.SelectedIndex == 4) && chkBigCalcTblUpdate.IsChecked.Value)
            {
                if (dr.ContainsKey("MEMS"))
                    lbiMEMS.Content = "MEMS[mg] = " + dr["MEMS"].ToString(genOptions.SignalTablePrec);
                if (dr.ContainsKey("Order"))
                {
                    lbiOrder.Content = "Order = " + dr["Order"].ToString(genOptions.SignalTablePrec);
                    lbiOrdRes.Content = "OrdRes[mg] = " + dr["OrdRes"].ToString(genOptions.SignalTablePrec);
                }
                if (dr.ContainsKey("PhiRad"))
                {
                    lbiPhiRad.Content = "Quant[rad]= " + dr["PhiRad"].ToString(genOptions.SignalTablePrec);
                    lbiPhiMg.Content = "Quant[mg]= " + dr["PhiMg"].ToString(genOptions.SignalTablePrec);
                }
                if (dr.ContainsKey("Accel"))
                    lbiAccel.Content = "Accel[mg] = " + dr["Accel"].ToString(genOptions.SignalTablePrec);
            }
        }

        public void DoShot(MMexec mme, MMexec lastGrpExe) // the main call
        {             
            Color clr = Brushes.Black.Color;
            if (prefix.Equals("X")) clr = Brushes.DarkGreen.Color;
            if (prefix.Equals("Y")) clr = Brushes.Navy.Color;

            #region shotData
            bool scanMode = lastGrpExe.cmd.Equals("scan");
            bool repeatMode = lastGrpExe.cmd.Equals("repeat");
            ucSignal.Showing = (tabSecPlots.SelectedIndex == 1); // when the charts are shown
            string s1 = (string)lastGrpExe.prms["groupID"];
            if (!s1.Equals((string)mme.prms["groupID"])) throw new Exception("Wrong groupID");

            MMDataConverter.ConvertToDoubleArray(ref mme);

            // mme info -> label 
            string endBit = ""; 
            int runID = Convert.ToInt32(mme.prms["runID"]); string log_out = "#" + runID.ToString();
            if (scanMode) endBit = ";  scanX = " + (lastScan.sFrom + runID * lastScan.sBy).ToString("G4");
            ucSignal.lbInfoSignal.Content = "cmd: " + lastGrpExe.cmd + ";  grpID: " + lastGrpExe.prms["groupID"] + ";  runID: " + runID.ToString() + endBit;

            double A, currX;
            ucSignal.Update(mme, out currX, out A); // phase (currX) and Asymmetry (quantum Y)
            // currX - scanning param in scan mode and runID in repeat
            //
            // LOWER section
            if (scanMode)
            {
                LogEvent(log_out + ": scan.Ph/Ampl = " + currX.ToString(genOptions.SignalTablePrec) +
                    " / " + A.ToString(genOptions.SignalTablePrec), clr);//asymmetry.ToString
                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,
                    new Action(() =>
                    { 
                        srsFringes.Add(new Point(currX, A)); //asymmetry
                        if (chkAutoScaleBottom.IsChecked.Value) // Fringes
                        {
                            double d;
                            d = Math.Floor(10 * srsFringes.pointYs().Min()) / 10;
                            fringesYmin = Math.Min(d, fringesYmin);
                            d = Math.Ceiling(10 * srsFringes.pointYs().Max()) / 10;
                            fringesYmax = Math.Max(d, fringesYmax);
                            d = (fringesYmax - fringesYmin) * 0.02;
                            fringesYaxis.Range = new Range<double>(fringesYmin - d, fringesYmax + d);
                        }
                        graphFringes.Data[0] = srsFringes;
                    }));
            }
            Dictionary<string, double> statDt = new Dictionary<string, double>();
            if (repeatMode)
            {
                double xVl = runID;
                if (scanModes.remoteMode == RemoteMode.Jumbo_Repeat)
                {
                    if (axelChart.Waveform.stopWatch.IsRunning) xVl = axelChart.Waveform.stopWatch.ElapsedMilliseconds / 1000.0;
                    UpdateStrobesParams(); statDt.Clear();
                    MMexec mmeOut = strobes.backMME(runID, A, mme); // creates strobes.accelSet if in probeMode
                    bool contrastMode = mmeOut.mmexec.Equals("contrastCheck") && mmeOut.prms.ContainsKey("phase." + prefix);
                    if (contrastMode)
                    {
                        statDt["phase.low"] = Convert.ToDouble(mmeOut.prms["phase." + prefix]);
                        LogEvent("phase.low -> " + statDt["phase.low"].ToString(genOptions.SignalTablePrec), Brushes.Coral.Color);
                    }                        
                    SendEvent(JsonConvert.SerializeObject(mmeOut));
                    phaseRad = strobes.zeroFringe();
                    statDt["PhiRad"] = phaseRad - numPhi0.Value;
                    statDt["PhiMg"] = statDt["PhiRad"] * numKcoeff.Value;

                    if (strobes.accelSet.ContainsKey("mems") && probeMode && !genOptions.MemsInJumbo && !contrastMode) // mems feed from probe
                    {
                        statDt["MEMS"] = strobes.accelSet["mems"];
                        statDt["Order"] = strobes.accelSet["order.M"];
                        statDt["OrdRes"] = strobes.accelSet["resid.M"];
                        statDt["Accel"] = strobes.accelSet["accel.M"];
                    }
                    if (mmeOut.prms.ContainsKey("corr." + prefix) && !contrastMode)
                    {
                        phaseCorr = Convert.ToDouble(mmeOut.prms["corr." + prefix]);
                        statDt["PhiCorr"] = phaseCorr;
                    }                                        
                }
                Dictionary<string, double> measr = new Dictionary<string, double>();
                Dictionary<string, double> rslt = new Dictionary<string, double>();
                // the MEMS bit in RemoteMode.xxx_Repeat mode
                if (genOptions.MemsInJumbo)
                {                
                    if (axelChart.Running)
                    {
                        if (scanModes.remoteMode == RemoteMode.Simple_Repeat) rslt = nextMeasure(A);
                        if (scanModes.remoteMode == RemoteMode.Jumbo_Repeat)
                        {
                            if (statDt.ContainsKey("PhiRad")) phaseRad = statDt["PhiRad"];
                            else phaseRad = Double.NaN;

                            measr = nextMeasure(phaseRad);

                            if (mme.prms.ContainsKey("rTime")) // remote time; if not - the default is axelChart.axelMems.TimeElapsed() from nextMeasure method
                            {
                                measr["time"] = Convert.ToDouble(mme.prms["rTime"]);                                 
                            }
                            rslt = Statistics(measr);
                            if (rslt.ContainsKey("PhiMg")) quantList.Add(new Point(rslt["time"], rslt["PhiMg"]));
                        }                       
                        if (rslt.ContainsKey("Accel")) log_out += ": accel = " + rslt["Accel"].ToString(genOptions.SignalTablePrec);
                    }
                    else LogEvent("problem with MEMS !", Brushes.Red.Color);
                }
                else
                {
                    rslt = Statistics(statDt);
                }
                LogEvent(log_out + "   A = "+ A.ToString(genOptions.SignalTablePrec), clr);
                if (repeatMode)
                {
                    if (scanModes.remoteMode == RemoteMode.Simple_Repeat)
                    {
                        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,
                            new Action(() =>
                            {
                                srsMotAccel.AddPoint(A, xVl); graphAccelTrend.Data[2] = srsMotAccel.Portion(genOptions.TrendSignalLen);
                            }));
                    }
                    if ((scanModes.remoteMode == RemoteMode.Jumbo_Repeat) && probeMode && !genOptions.MemsInJumbo) showResults(xVl, rslt);                 
                }
                if (chkAutoScaleBottom.IsChecked.Value && srsMotAccel.Count > 0) // Accel.Trend axis
                {
                    if (scanModes.remoteMode == RemoteMode.Simple_Repeat || scanModes.remoteMode == RemoteMode.Jumbo_Repeat)
                    {
                        accelYmin = Math.Min(Math.Floor(10 * srsMotAccel.pointYs().Min()) / 10, accelYmin);
                        accelYmax = Math.Max(Math.Ceiling(10 * srsMotAccel.pointYs().Max()) / 10, accelYmax);
                        if (rslt.ContainsKey("MEMS"))
                        {
                            accelYmin = Math.Min(Math.Floor(10 * srsMems.pointYs().Min()) / 10, accelYmin);
                            accelYmax = Math.Max(Math.Ceiling(10 * srsMems.pointYs().Max()) / 10, accelYmax);
                        }
                    }
                    if (scanModes.remoteMode == RemoteMode.Jumbo_Repeat)
                    {
                        // !!!
                    }
                    double d = (accelYmax - accelYmin) * 0.02;
                    accelAxis.Range = new Range<double>(accelYmin - d, accelYmax + d);
                }
            }
            if (scanMode && scanModes.remoteMode == RemoteMode.Jumbo_Scan || scanModes.remoteMode == RemoteMode.Simple_Scan)
            {
                if (mme.prms.ContainsKey("last"))
                {
                    if (Convert.ToInt32(mme.prms["last"]) == 1)
                    {
                        if (scanModes.remoteMode == RemoteMode.Jumbo_Scan && genOptions.JumboRepeat) // transition from jumboScan to jumboRepeat
                        {
                            visStrobes(true);
                        }
                    }
                }
            }
            #endregion shotData
        }              

        public Dictionary<string, double> Statistics(Dictionary<string, double> dt) // in MEMS [V]; PhiRad -> out - MEMS [mg], etc.
        {
            Dictionary<string, double> rslt = new Dictionary<string, double>(dt);

            if (!dt.ContainsKey("K")) rslt["K"] = numKcoeff.Value;
            if (!dt.ContainsKey("Phi0")) rslt["Phi0"] = numPhi0.Value;
            if (!dt.ContainsKey("Scale")) rslt["Scale"] = numScale.Value;
            if (!dt.ContainsKey("Offset")) rslt["Offset"] = numOffset.Value;

            if (dt.ContainsKey("PhiRad")) 
            {
                if (!dt.ContainsKey("PhiMg")) rslt["PhiMg"] = (dt["PhiRad"] - rslt["Phi0"]) * rslt["K"];  // convert rad to mg if not there                        
                phiMg.AddPoint(rslt["PhiMg"]);
            }
            if (!dt.ContainsKey("MEMS_V") && !dt.ContainsKey("MEMS")) return rslt; // if neither is present out only Phi
            if (dt.ContainsKey("MEMS_V") && !dt.ContainsKey("MEMS")) rslt["MEMS"] = axelChart.convertV2mg(dt["MEMS_V"]); // convert V to mg

            double resid;
            double ord = calcAccel.accelOrder(rslt["MEMS"] - rslt["PhiMg"], rslt["K"], out resid); 
            rslt["Order"] = Math.Round(ord);
            rslt["OrdRes"] = resid * rslt["K"]; // [mg]

            double orderAccel; double residAccel;
            rslt["Accel"] = calcAccel.resultAccel(rslt["Order"], rslt["PhiRad"], rslt["K"], out orderAccel, out residAccel);                
            accelMg.AddPoint(rslt["Accel"]);

            return rslt;
        }

        private void numScale_ValueChanged(object sender, ValueChangedEventArgs<double> e)
        {
            if (Utils.isNull(_prefix)) return;
            if (!prefix.Equals("")) UpdateFromOptions(false);
        }

        public void Closing(object sender, System.ComponentModel.CancelEventArgs e) // not destroying anything, just preparing
        {

        }
    }
}


    
