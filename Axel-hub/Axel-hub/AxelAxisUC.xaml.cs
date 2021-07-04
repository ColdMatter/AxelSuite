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
using System.Threading;
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
using System.Windows.Media.Media3D;
using UtilsNS;
using OptionsNS;

namespace Axel_hub
{
    /// <summary>
    /// Interaction logic for AxelAxisUC.xaml
    /// AxelAxisClass repressents a single axis of acceleration 
    /// encapsulated and accesable in AxelAxes list of AxelAxisClass
    /// Future intermediator abstract movement (linear or rotation) 
    /// component will be implemented. 
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
        public MMexec mm2status = null;

        private const int dataLength = 10000; // default length of data kept in
        private DataStack phiMg = new DataStack(dataLength);
        private DataStack accelMg = new DataStack(dataLength);
        private Random random = new Random();
        public List<Point> timeStack = new List<Point>(); // x - time[s]; y - phi[rad]        
        private List<Point3D> quantList = new List<Point3D>();

        /// <summary>
        /// Class constructor 
        /// </summary>
        public AxelAxisClass()
        {
            InitializeComponent();
        }

        GeneralOptions genOptions = null; ScanModes scanModes = null; Modes modes = null; string modesFile = "";
        /// <summary>
        /// Late initialiazation after the dust from loading of main form and ucScan has settle
        /// </summary>
        /// <param name="_prefix">ID</param>
        /// <param name="_genOptions">general options</param>
        /// <param name="_scanModes">ucScan options</param>
        /// <param name="_axelMems">ADC24 abstraction</param>
        public void Init(string _prefix, ref GeneralOptions _genOptions, ref ScanModes _scanModes, ref AxelMems _axelMems) // obligatory 
        {
            prefix = _prefix;
            if (Utils.isNull(_genOptions)) Utils.TimedMessageBox("Non-existant options");
            else genOptions = _genOptions;
            scanModes = _scanModes;
            OpenDefaultModes();
            axelChart.InitOptions(ref genOptions, ref modes, ref _axelMems, prefix);
            strobes.Init(prefix, ref genOptions);

            tabSecPlots.SelectedIndex = 1;
            if (Utils.TheosComputer()) tiOptimization.Visibility = Visibility.Visible;
            else tiOptimization.Visibility = Visibility.Collapsed;

        }

        /// <summary>
        /// Simulation of MM2 with AxelProbe
        /// It should be in very limited (ideally none) use
        /// </summary>
        private bool probeMode 
        {
            get
            {
                if (Utils.isNull(lastGrpExe)) return false;
                return lastGrpExe.mmexec.Equals("simulation");
            }
        }

        /// <summary>
        /// Visual optimization hiding/showing AxelChart under some conditions
        /// </summary>
        public bool AxelChartVisible
        {
            get { return axelChart.Visibility == System.Windows.Visibility.Visible; }
            set
            {
                if (value)
                {
                    if (!Utils.isNull(Application.Current.MainWindow))
                    {
                        double h = (Application.Current.MainWindow.Height - 60) / 3;
                        if (rowUpperChart.Height.Value > h) rowUpperChart.Height = new GridLength(h);
                    }                       
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

        /// <summary>
        /// Load and set visual options & modes
        /// </summary>
        /// <param name="Middle">selective to middle</param>
        /// <param name="Bottom">selective to bottom</param>
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
            bool wannaShowMems = (genOptions.ShowMemsIfRunning && !genOptions.memsInJumbo.Equals(GeneralOptions.MemsInJumbo.None)) || !genOptions.ShowMemsIfRunning;

            if (wannaShowMems)
            {
                if (modes.TopFrame < 2) AxelChartVisible = true;
                else rowUpperChart.Height = new GridLength(modes.TopFrame);
            }
            else AxelChartVisible = false;
            rowMiddleChart.Height = new GridLength(modes.MiddleFrame);

            if (Middle)
            {
                ucSignal.Init(ref genOptions, ref modes, ref axelChart.axelMems, prefix);
                ucSignal.OpenDefaultModes();

                chkBigCalcTblUpdate.IsChecked = modes.RsltTblUpdate;
                chkBigCalcChrtUpdate.IsChecked = modes.RsltChrtUpdate;
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

        /// <summary>
        /// propagate followPID from options to strobe user control
        /// </summary>
        private void UpdateStrobesParams()
        {
            if (Utils.isNull(strobes)) return;            
            strobes.SetPID_Enabled(genOptions.followPID && !genOptions.Diagnostics);
        }

        /// <summary>
        /// Log event managment
        /// </summary>
        /// <param name="txt"></param>
        /// <param name="clr"></param>
        public delegate void LogHandler(string txt, SolidColorBrush clr = null);
        public event LogHandler OnLog;
        public void LogEvent(string txt, SolidColorBrush clr = null)
        {
            if (!Utils.isNull(OnLog)) OnLog(txt, clr);
        }

        /// <summary>
        /// Send event managment
        /// </summary>
        /// <param name="json"></param>
        /// <param name="async"></param>
        /// <returns></returns>
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
        /// <summary>
        /// Visual optimization
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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
        ShotList shotListRaw;
        FileLogger errLog;
 
        #region File operation
        /// <summary>
        /// Open signal dialog box
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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

        /// <summary>
        /// Save signal dialog box
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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

        /// <summary>
        /// Open fringes scan file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnOpenFringes_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.FileName = ""; // Default file name
            dlg.DefaultExt = ".sdt"; // Default file extension
            dlg.Filter = "Scan Data file (.sdt)|*.sdt"; // Filter files by extension

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

        /// <summary>
        /// Save fringes scan file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSaveFringesAs_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.FileName = ""; // Default file name
            dlg.DefaultExt = ".sdt"; // Default file extension
            dlg.Filter = "Scan Data file (.sdt)|*.sdt"; // Filter files by extension

            // Show save file dialog box
            Nullable<bool> result = dlg.ShowDialog();
            srsFringes.rem = tbRemFringes.Text;
            if (srsFringes.rem.Equals("")) srsFringes.rem = (string)lbInfoFringes.Content;
            if (result == true) srsFringes.SavePair(dlg.FileName, "", "G6"); //genOptions.SaveFilePrec);
        }

        /// <summary>
        /// Clear and initialte all the visuals
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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

        DataStack srsMdiffQ = new DataStack(); 
        public DataStack srsFringes = null; DataStack srsMotAccel = null; DataStack srsCorr = null; DataStack srsMems = null; DataStack srsAccel = null;

        private double phaseCorr, phaseRad, fringesYmin = 10, fringesYmax = -10, accelYmin = 10, accelYmax = -10;
        /// <summary>
        /// Panel-selective clear command
        /// </summary>
        /// <param name="Top"></param>
        /// <param name="Middle"></param>
        /// <param name="Bottom"></param>
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
                fringesYmin = 10; fringesYmax = -10; accelYmin = 10; accelYmax = -10;
            }
        }

        /// <summary>
        /// Chart zoom reset
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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
        /// <summary>
        /// Initiate strobes positions
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void rbSingle_Checked(object sender, RoutedEventArgs e)
        {
            crsDownStrobe.AxisValue = 1.5; crsUpStrobe.AxisValue = 4.6;
        }

        DispatcherTimer ddTimer;
        /// <summary>
        /// internal simulation test of join data 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnJoinLogTest_Click(object sender, RoutedEventArgs e) // 
        {
            btnJoinLogTest.Value = !btnJoinLogTest.Value;
            if (!theTime.isTimeRunning && btnJoinLogTest.Value)
            {
                Utils.TimedMessageBox("Waveform Stopwatch is NOT running.");
                btnJoinLogTest.Value = false;
                return;
            }
            if (btnJoinLogTest.Value)
            {
                resetQuantList("");
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
            }
            shotList.enabled = btnJoinLogTest.Value;
        }
 
        private void ddTimer_Tick(object sender, EventArgs e)
        {
            if (theTime.isTimeRunning)
                quantList.Add(new Point3D(theTime.elapsedTime, 3.0, 5.0)); 
        }

        /// <summary>
        /// Save trend as time & srsMems, srsMotAccel, srsAccel, srsCorr series table
        /// </summary>
        /// <param name="FileName"></param>
        private void SaveTrend(string FileName)  // 
        {
            if (srsMems.Count == 0 || srsMotAccel.Count == 0)
            {
                Utils.TimedMessageBox("Error: No trend data to be saved !", "ERROR", 2500);
                return;
            }
            DictFileLogger fl = new DictFileLogger(new string[]{"Time","MEMS","MOTaccel","Accel","Corr"}, prefix, FileName);
            fl.defaultExt = ".aht"; fl.setMMexecAsHeader(lastGrpExe.Clone());
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

        /// <summary>
        /// Save trend dialog box
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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

        /// <summary>
        /// Reinitialize quant list 
        /// </summary>
        public void resetQuantList(string filename)
        {
            quantList.Clear(); // [time,MOTaccel] list of pairs

            string fn = Utils.dataPath + "errorLog." + prefix;
            bool enb = File.Exists(fn);
            errLog = new FileLogger("", fn); 
            errLog.Enabled = enb; //Utils.designTime;

            shotList = new ShotList(true, filename, prefix, false);
            shotList.enabled = !genOptions.Diagnostics;
            setConditions(ref shotList.conditions);           
            
            shotListRaw = new ShotList(true, filename, prefix, true);
            shotListRaw.enabled = genOptions.logRawJdt;
            setConditions(ref shotListRaw.conditions);
            accelCalibr mems = (prefix == "X") ? axelChart.axelMems.memsX : axelChart.axelMems.memsY;
            shotListRaw.streamWriter.subheaders.Add(mems.IdString());           
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

        /// <summary>
        /// Switching the visual strobe cursors ON/OFF 
        /// </summary>
        /// <param name="enabled">New state</param>
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

        /// <summary>
        /// Fit a sin (ModelFunction) function
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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

            LogEvent("FIT.meanSqError = " + meanSquaredError.ToString("G5"), Brushes.DarkCyan);
            LogEvent("K = " + coeffs[1].ToString("G5"), Brushes.DarkCyan);
            LogEvent("Phase0 = " + coeffs[2].ToString("G5"), Brushes.DarkCyan);
            LogEvent("Scale = " + coeffs[0].ToString("G5"), Brushes.DarkCyan);
            LogEvent("Offset = " + coeffs[3].ToString("G5"), Brushes.DarkCyan);

            visStrobes(true);
            //numScale.Value = coeffs[0]; numKcoeff.Value = coeffs[1]; numPhi0.Value = coeffs[2];  numOffset.Value = coeffs[3]; 
        }
        
        /// <summary>
        /// Callback function that implements the fitting model 
        /// </summary>
        /// <param name="x"></param>
        /// <param name="coefficients"></param>
        /// <returns></returns>
        private double ModelFunction(double x, double[] coefficients)
        {
            return (coefficients[0] * Math.Sin(x / coefficients[1] + coefficients[2])) + coefficients[3];
        }

        public bool RecordQuantMems(double[] md, ref Dictionary<string,double> tm, double A)
        {
            bool rslt = true;
            double iTime = tm["iTime"]; // actual interferometer start [s]
            double tTime = tm["tTime"];  // actual interferometer duration [s]
            double step = 1.0 / tm["samplingRate"];
            double cap =  genOptions.Mems2ExtInfCap / 1000.0 ; // all in [s]            
            double bTime = tm["bTime"] * step;        double aTime = tm["aTime"] * step;
            double bTimeCap = Math.Min(bTime, cap);   double aTimeCap = Math.Min(aTime, cap);

            tm["eiTime"] = iTime - bTime; // md start iTime [s]
            tm["etTime"] = tTime + (bTime + aTime); // md duration tTime [s]
            DataStack adm = new DataStack(md.Length+100);
            double time = tm["eiTime"]; 
            for (int i = 0; i < md.Length; i++)
            {
                adm.AddPoint(md[i], time);
                time += step;
            }
            DataStack adm2 = new DataStack(md.Length+100);
            for (int i = 0; i < adm.Count; i++)
                if ((adm[i].X < (iTime - bTimeCap)) || (adm[i].X > (iTime + tTime + aTimeCap))) continue;
                else  adm2.AddPoint(adm[i].Y, adm[i].X);
            DataStack adm3 = adm2.Compress(genOptions.MemsAverOver);
            axelChart.Waveform.AddRange(adm3);
            axelChart.Refresh();
            if (shotListRaw.enabled)
            {
                double temper = genOptions.TemperatureEnabled ? axelChart.memsTemperature : Double.NaN;
                SingleShot ss = new SingleShot(new Point3D(iTime, A, tTime), adm3, temper, "r"+ genOptions.MemsAverOver.ToString());                
                shotListRaw.Add(ss);
            }
            return rslt;
        }

        bool combLock = false;
        /// <summary>
        /// Create SingleShot and add it to shotList for further processing
        /// </summary>
        /// <param name="mds">last acquired MEMS buffer</param>
        /// <param name="dur">[s] - quant sample duration: +/- dur (3*dur in total)</param>
        /// <param name="dly">[s] - time delay of mems start ref to quant</param>
        /// <returns></returns>
        public bool CombineQuantMems(List<Point> mds, double dur = 0.005, double dly = 0) 
        {
            if (combLock || mds.Count.Equals(0) || quantList.Count.Equals(0) || (dur <= 0))
            {
                if (combLock) errLog.log("Comb lock !");
                if (mds.Count.Equals(0)) errLog.log("mds.Count.Equals(0) !");
                if (quantList.Count.Equals(0)) errLog.log("quantList.Count.Equals(0) !");
                if (dur <= 0) errLog.log("dur <= 0 !");
                return false;
            }               
            combLock = true;
            try
            {   
                int pCount = shotList.Count; 
                double temper = genOptions.TemperatureEnabled ? axelChart.memsTemperature : Double.NaN;

                DataStack ds;
                for (int i = 0; i < quantList.Count; i++)
                {
                    if (quantList[i].X < 0) continue; // already processed
                    double from = quantList[i].X + dly; // shifted aqc. start
                    double w0 = from - genOptions.Mems2SignLenMult * dur; double w1 = from + 2 * genOptions.Mems2SignLenMult * dur; // the window of interest - defined by quant time (X) and dur/dly
                    if (w0 < 0) continue;

                    if (w0 < mds[0].X) // if the window of interest starts before the beginning of the buffer, then go to the Waveform for history data
                    {
                        if ((axelChart.Waveform.First.X > w0) || (w1 > axelChart.Waveform.Last.X)) continue;
                        if (shotListRaw.enabled)
                        {
                            SingleShot ss = new SingleShot(new Point3D(quantList[i].X, quantList[i].Y, dur), axelChart.Waveform, temper, "m1");
                            ss.cutMems(w0, w1); // cut mems to size
                            shotListRaw.Add(ss);
                            if (ss.mems.Count.Equals(0)) errLog.log("raw> quant.t <limits> 1: " + quantList[i].X.ToString("G5") + " <" + w0.ToString("G5") + ", " + w1.ToString("G5") + ">");
                        }
                        if (shotList.enabled)
                        {
                            SingleShot ss = new SingleShot(new Point3D(quantList[i].X, quantList[i].Y, dur), axelChart.TimePortionMg(w0, w1), temper, "m1"); // ds in mg
                            shotListRaw.Add(ss);
                            if (ss.mems.Count.Equals(0)) errLog.log("quant.t <range> 1: " + quantList[i].X.ToString("G5") + " <" + w0.ToString("G5") + ", " + w1.ToString("G5") + ">");
                        }
                        quantList[i] = new Point3D(-quantList[i].X, quantList[i].Y, quantList[i].Z); // mark as processed (good or bad)
                        continue;
                    }
                    if ((mds[0].X < w0) && (w1 < mds[mds.Count - 1].X)) // the window of interest is well inside the MEMS buffer
                    {
                        SingleShot ss = new SingleShot(new Point3D(quantList[i].X, quantList[i].Y, dur), mds, temper, "m2");
                        ss.cutMems(w0, w1); // cut mems to size
                        if (ss.mems.Count > 0) // there is something there
                        {
                            if (shotListRaw.enabled) shotListRaw.Add(ss);

                            if (shotList.enabled)
                            {
                                ds = new DataStack(axelChart.Waveform.Depth, prefix);  ds.AddRange(ss.mems);
                                SingleShot ss2 = new SingleShot(new Point3D(quantList[i].X, quantList[i].Z, dur), axelChart.convertV2mg(ds));
                                shotList.Add(ss2);
                            }
                               
                        }
                        else errLog.log("quant <range> 2: " + quantList[i].X.ToString("G5") + " <" + w0.ToString("G5") + ", " + w1.ToString("G5") + ">");
                            
                        quantList[i] = new Point3D(-quantList[i].X, quantList[i].Y, quantList[i].Z); // mark as processed if good
                        continue;
                    }
                    errLog.log("quant <range> 3: " + quantList[i].X.ToString("G5") + " <" + w0.ToString("G5") + ", " + w1.ToString("G5") + ">  "+
                               "inBuff ["+ mds[0].X.ToString("G5") + ", " + mds[mds.Count - 1].X.ToString("G5")+"]");
                    // in any other case - keep going
                }
                for (int i = quantList.Count - 1; i > -1; i--)
                    if (quantList[i].X < 0) quantList.RemoveAt(i); // remove processed points
                                                                   // if any points left in quantList they will go for the next turn
                string st = (theTime.isTimeRunning) ? "; time[s] = " + theTime.elapsedTime.ToString("G5") : "; no time";
                lbInfoAccelTrend.Content = "Info: shots # " + shotList.Count.ToString() + st;
                if (shotList.enabled)
                {
                    for (int i = pCount; i < shotList.Count; i++)
                    {
                        Dictionary<string, double> dt = shotList[i].deconstructAccel(strobes.fringeScale);
                        if (dt.Count.Equals(0))
                        {
                            LogEvent("Error: empty shot #" + i.ToString(), Brushes.Red);
                            continue;
                        }
                        showResults(i, dt);
                    }
                }
            }
            finally
            {
                combLock = false;
            }
            return true;
        }

        /// <summary>
        /// Selective mode saving
        /// </summary>
        /// <param name="Top"></param>
        /// <param name="Middle"></param>
        /// <param name="Bottom"></param>
        public void SaveDefaultModes(bool Top = true, bool Middle = true, bool Bottom = true)
        {
            double h = Application.Current.MainWindow.Height - 60;
            modes.TopFrame = Utils.EnsureRange(rowUpperChart.Height.Value, 10,600);
            modes.MiddleFrame = Utils.EnsureRange(rowMiddleChart.Height.Value, 50,600);

            if (Top) axelChart.modesFromVisual();

            if (Middle)
            {
                ucSignal.SaveDefaultModes();

                modes.RsltTblUpdate = chkBigCalcTblUpdate.IsChecked.Value;
                modes.RsltChrtUpdate = chkBigCalcChrtUpdate.IsChecked.Value;
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

                strobes.SaveConfigFile();
            }
            modes.Save(prefix);
        }

        /// <summary>
        /// Control of visibility of the accel. trend graph series 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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
        
        /// <summary>
        /// Creates jumboScan component to be sent to MM2
        /// </summary>
        /// <returns></returns>
        public MMscan jumboScan()
        {
            MMscan mms = new MMscan();
            mms.groupID = Utils.timeName();
            mms.sSite = "MSquared:Raman Phase [rad]";
            mms.sParam = "RamanPhase"; // phase is default
            mms.sFrom = numFrom.Value;
            mms.sTo = numTo.Value;
            mms.sBy = numBy.Value;
            return mms;
        }

        /// <summary>
        /// Update some visual options
        /// </summary>
        /// <param name="connected"></param>
        public void UpdateFromOptions(bool connected)
        {
            if (Utils.isNull(strobes)) return;
            ucSignal.NsCursor.ValuePresenter = new ValueFormatterGroup(" — ", new GeneralValueFormatter("0.00"))
            {
                ValueFormatters = { new GeneralValueFormatter(genOptions.SignalCursorPrec) }
            };
            if (connected) AxelChartVisible = (genOptions.ShowMemsIfRunning && !genOptions.memsInJumbo.Equals(GeneralOptions.MemsInJumbo.None)) || !genOptions.ShowMemsIfRunning;
            else AxelChartVisible = true;
            UpdateStrobesParams();
        }
        
        /// <summary>
        /// Prepare for the next measureme with specific phase
        /// </summary>
        /// <param name="phi">Phase</param>
        /// <returns></returns>
        private Dictionary<string, double> prepNextMeasure(double phi)
        {
            Dictionary<string, double> rslt = new Dictionary<string, double>();

            if (!Double.IsNaN(phi)) rslt["PhiRad"] = phi;
            if (theTime.isTimeRunning) rslt["time"] = theTime.elapsedTime;

            return rslt;
        } 
        
        // in simple_repeat only Mems; in jumbo_repeat - both

        /// <summary>
        /// UNDER DEVELOPMENT
        /// Incomming measurement with specific phase
        /// </summary>
        /// <param name="phi">Phase</param>
        /// <returns></returns>
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
        /// <summary>
        /// Prepare for particular proc mode
        /// </summary>
        /// <param name="mme"></param>
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
                        if (genOptions.memsInJumbo.Equals(GeneralOptions.MemsInJumbo.None))
                        {
                            plotcursorAccel.Visibility = System.Windows.Visibility.Visible;
                        }
                        else
                        {
                            axelChart.Waveform.TimeSeriesMode = true;
                            plotcursorAccel.Visibility = System.Windows.Visibility.Collapsed;                 
                        }
                        string ss = ";  Data Src: Quant-> MM2";
                        ss += "; MEMS-> ";
                        switch (genOptions.memsInJumbo)
                        {
                            case GeneralOptions.MemsInJumbo.USB9251:
                                ss += "USB-9251";
                                break;
                            case GeneralOptions.MemsInJumbo.PXI4462:
                                ss += "PXI-4462";
                                break;
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

            ucSignal.Init(mme, mm2status); 
        }

        /// <summary>
        /// Add a measurement to graph series (point) and table of results (update)
        /// </summary>
        /// <param name="xVl">Horiz. value</param>
        /// <param name="dr">Dictionary of results</param>
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

        private void chkSignalLog_Checked(object sender, RoutedEventArgs e)
        {
            if (!Utils.isNull(modes)) SaveDefaultModes(false,true,false);
        }

        public delegate void SendMMexecHandler(MMexec mme);
        public event SendMMexecHandler SendMMexecEvent;
        protected virtual void OnSendMMexec(MMexec mme)
        {
            SendMMexecEvent?.Invoke(mme);
        }
        private void tabLowPlots_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (tabLowPlots.SelectedIndex == 3) columnPID.Width = new GridLength(0);
            else { columnPID.Width = new GridLength(145); return; }
            // Optim init
            if (!Utils.TheosComputer())
            /*{
                Dictionary<string, object> dct = new Dictionary<string, object>();
                dct.Add("Param1", 1.11);
                dct.Add("Param2", 2.22);
                dct.Add("Param3", 3.33);
                OptimUC1.Init(dct);
            }
            else*/
            {
                mm2status = null;
                OnSendMMexec(new MMexec("", "Axel-hub", "status"));
                int i = 0;
                while (Utils.isNull(mm2status) && (i < 100))
                {
                    //Utils.DoEvents();
                    Thread.Sleep(40);
                    i++;
                }
                OptimUC1.Init(mm2status?.prms);               
            }
        }

        /// <summary>
        /// Process an incomming shot 
        /// </summary>
        /// <param name="mme">The actual shot</param>
        /// <param name="lastGrpExe">The context (scanning mode) of the shot</param>
        public void DoShot(MMexec mme, MMexec lastGrpExe) // the main call
        {
            SolidColorBrush clr = Brushes.Black;
            if (prefix.Equals("X")) clr = Brushes.DarkGreen;
            if (prefix.Equals("Y")) clr = Brushes.Navy;

            #region shotData
            bool scanMode = lastGrpExe.cmd.Equals("scan");
            bool repeatMode = lastGrpExe.cmd.Equals("repeat");
            ucSignal.Showing = (tabSecPlots.SelectedIndex == 1); // when the charts are shown
            string s1 = (string)lastGrpExe.prms["groupID"];
            if (!s1.Equals((string)mme.prms["groupID"])) LogEvent("Ussue with groupID > " + s1 + " : " + (string)mme.prms["groupID"], Brushes.Coral);

            MMDataConverter.ConvertToDoubleArray(ref mme);

            // mme info -> label 
            string endBit = ""; 
            int runID = Convert.ToInt32(mme.prms["runID"]); string log_out = "#" + runID.ToString();
            if (scanMode) endBit = ";  scanX = " + (lastScan.sFrom + runID * lastScan.sBy).ToString("G4");
            ucSignal.lbInfoSignal.Content = "cmd: " + lastGrpExe.cmd + ";  grpID: " + lastGrpExe.prms["groupID"] + ";  runID: " + runID.ToString() + endBit;

            double A; double currX = Double.NaN;
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
                /*if (theTime.isTimeRunning) xVl = theTime.elapsedTime;
                else
                    if (runID == 0) theTime.startTime();*/
                if (scanModes.remoteMode == RemoteMode.Jumbo_Repeat) // react back to MM2
                {   
                    if (genOptions.Diagnostics) 
                    {
                        MMexec mmeOut = strobes.backMME(runID, A, mme);
                        SendEvent(JsonConvert.SerializeObject(mmeOut));
                    }
                    else // acceleration
                    {
                        UpdateStrobesParams(); statDt.Clear();
                        MMexec mmeOut = strobes.backMME(runID, A, mme); // creates strobes.accelSet if in probeMode
                        bool contrastMode = mmeOut.mmexec.Equals("contrastCheck") && mmeOut.prms.ContainsKey("phase." + prefix);
                        if (contrastMode)
                        {
                            statDt["phase.low"] = Convert.ToDouble(mmeOut.prms["phase." + prefix]);
                            LogEvent("phase.low -> " + statDt["phase.low"].ToString(genOptions.SignalTablePrec), Brushes.Coral);
                        }                        
                        SendEvent(JsonConvert.SerializeObject(mmeOut));
                        phaseRad = strobes.zeroFringe();
                        statDt["PhiRad"] = phaseRad - numPhi0.Value;
                        statDt["PhiMg"] = statDt["PhiRad"] * numKcoeff.Value;

                        if (strobes.accelSet.ContainsKey("mems") && probeMode && genOptions.memsInJumbo.Equals(GeneralOptions.MemsInJumbo.None) && !contrastMode) // mems feed from probe
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
                }
                Dictionary<string, double> measr = new Dictionary<string, double>();
                Dictionary<string, double> rslt = new Dictionary<string, double>();
                // the MEMS bit in RemoteMode.xxx_Repeat mode
                if (!genOptions.memsInJumbo.Equals(GeneralOptions.MemsInJumbo.None))
                {                
                    if (axelChart.Active)
                    {
                        if (scanModes.remoteMode == RemoteMode.Simple_Repeat) rslt = nextMeasure(A); //???
                        if (scanModes.remoteMode == RemoteMode.Jumbo_Repeat)
                        {
                            // Timing section
                            measr["bTime"] = (mme.prms.ContainsKey("bTime")) ? Convert.ToInt32(mme.prms["bTime"]) : 0; // before
                            measr["aTime"] = (mme.prms.ContainsKey("aTime")) ? Convert.ToInt32(mme.prms["aTime"]) : 0; // after
                            measr["samplingRate"] = (mme.prms.ContainsKey("samplingRate")) ? Convert.ToInt32(mme.prms["samplingRate"]) : 200000;
                            
                            if (mme.prms.ContainsKey("iTime")) // remote acquis. start time; if not - out
                            {
                                measr["iTime"] = theTime.relativeTime((long)Convert.ToInt64(mme.prms["iTime"])) + measr["bTime"] / measr["samplingRate"];  // now in sec
                                xVl = Double.IsNaN(currX) ? measr["iTime"] : currX;
                            }
                            else throw new Exception("No interfermeter start time specified at MM2.");
                            if (mme.prms.ContainsKey("tTime")) // inteferometer steps duration, if not - the default is genOptions.Mems2SignLen
                            {
                                long tTimeTicks = Convert.ToInt64(mme.prms["tTime"]);
                                if (tTimeTicks > 0)
                                {
                                    measr["tTime"] = Utils.tick2sec(tTimeTicks) - (measr["bTime"] + measr["aTime"] )/ measr["samplingRate"]; // now in sec
                                    genOptions.Mems2SignLen = measr["tTime"] * 1000.0;
                                }
                                else throw new Exception("Wrong interferometer section duration specified at MM2.");
                            }
                            else throw new Exception("No interferometer section duration specified at MM2.");

                            switch (genOptions.memsInJumbo)
                            {
                                case GeneralOptions.MemsInJumbo.USB9251:
                                    if (genOptions.Diagnostics)
                                    {
                                        quantList.Add(new Point3D(xVl, A, -1));
                                        log_out += " (" + xVl.ToString("F1") + ")";
                                    }
                                    else
                                    {
                                        if (statDt.ContainsKey("PhiRad")) phaseRad = statDt["PhiRad"];
                                        else phaseRad = Double.NaN;

                                        measr = nextMeasure(phaseRad); // dealing with phase
                                        rslt = Statistics(measr);

                                        if (rslt.ContainsKey("PhiMg") && (rslt.ContainsKey("PhiRad"))) quantList.Add(new Point3D(rslt["time"], rslt["PhiRad"], rslt["PhiMg"]));
                                    }
                                    break;
                                case GeneralOptions.MemsInJumbo.PXI4462:
                                    if (genOptions.Diagnostics)
                                    {
                                        if (mme.prms.ContainsKey("Interferometer"))
                                        {
                                            RecordQuantMems((double[])mme.prms["Interferometer"], ref measr, A);
                                        }
                                        else throw new Exception("No interferometer data comming from MM2.");
                                        log_out += " (" + xVl.ToString("F1") + ")";
                                    }
                                    else
                                    {
                                        /*if (statDt.ContainsKey("PhiRad")) phaseRad = statDt["PhiRad"];
                                        else phaseRad = Double.NaN;

                                        measr = nextMeasure(phaseRad); // dealing with phase
                                        rslt = Statistics(measr);

                                        if (rslt.ContainsKey("PhiMg") && (rslt.ContainsKey("PhiRad"))) quantList.Add(new Point3D(rslt["time"], rslt["PhiRad"], rslt["PhiMg"]));*/
                                    }
                                    break;
                            }
                            if (rslt.ContainsKey("Accel")) log_out += ": accel = " + rslt["Accel"].ToString(genOptions.SignalTablePrec);
                        }
                    }
                    else
                        if (scanModes.remoteMode == RemoteMode.Jumbo_Repeat)    
                            LogEvent("problem with MEMS !", Brushes.Red);
                }
                else
                {
                    rslt = Statistics(statDt);
                }
                LogEvent(log_out + "   A = "+ A.ToString(genOptions.SignalTablePrec), clr);
                if (repeatMode) // visuals
                {
                    if (scanModes.remoteMode == RemoteMode.Simple_Repeat)
                    {
                        if (!Double.IsNaN(A))
                            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,
                                new Action(() =>
                                {
                                    srsMotAccel.AddPoint(A, xVl); graphAccelTrend.Data[2] = srsMotAccel.Portion(genOptions.TrendSignalLen);
                                }));
                    }
                    if (scanModes.remoteMode == RemoteMode.Jumbo_Repeat)
                    {
                        if (genOptions.Diagnostics)
                        {
                            srsMotAccel.AddPoint(A, xVl); 
                            graphAccelTrend.Data[2] = srsMotAccel.Portion(genOptions.TrendSignalLen);
                            double[] da = (double[])mme.prms["Interferometer"];
                            srsMems.AddPoint(da.Average(), xVl);
                            graphAccelTrend.Data[0] = srsMems.Portion(genOptions.TrendSignalLen);
                        }
                        else
                            if (probeMode) showResults(xVl, rslt);
                    }               
                }
                if (chkAutoScaleBottom.IsChecked.Value && srsMotAccel.Count > 0) // Accel.Trend axis
                {
                    if (scanModes.remoteMode == RemoteMode.Simple_Repeat || scanModes.remoteMode == RemoteMode.Jumbo_Repeat)
                    {
                        if (srsMotAccel.Count > 0)
                        {
                            accelYmin = Math.Min(Math.Floor(10 * srsMotAccel.pointYs().Min()) / 10, accelYmin);
                            accelYmax = Math.Max(Math.Ceiling(10 * srsMotAccel.pointYs().Max()) / 10, accelYmax);
                        }
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
        /// <summary>
        /// Extract acceleration params/statistics from result dict
        /// </summary>
        /// <param name="dt">Result dicionary</param>
        /// <returns></returns>
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

        /// <summary>
        /// Propagate options if numScale changes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void numScale_ValueChanged(object sender, ValueChangedEventArgs<double> e)
        {
            if (Utils.isNull(_prefix)) return;
            if (!prefix.Equals("")) UpdateFromOptions(false);
        }

        /// <summary>
        /// If any additional actions needed (empty so far)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void Closing(object sender, System.ComponentModel.CancelEventArgs e) // not destroying anything, just preparing
        {
            OptimUC1.Final();
        }

        private void btnExtractHeader_Click(object sender, RoutedEventArgs e)
        {
            using (var fbd = new System.Windows.Forms.FolderBrowserDialog())
            {
                System.Windows.Forms.DialogResult result = fbd.ShowDialog();

                if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    string[] fls = Directory.GetFiles(fbd.SelectedPath, "*.ahs");
                    LogEvent("Extract headers from *.ahs files in folder:", Brushes.DarkBlue);
                    LogEvent(fbd.SelectedPath, Brushes.DarkBlue);
                    foreach (string fl in fls)
                    {
                        LogEvent("> " + fl, Brushes.Blue);
                        List<string> ls = Utils.readList(fl, false);
                        List<string> lt = new List<string>();
                        foreach (string ln in ls)
                        {
                            if (ln.Length.Equals(0)) continue;
                            if (ln[0].Equals('#'))
                                lt.Add(ln.Remove(0, 1));
                        }
                        Utils.writeList(System.IO.Path.ChangeExtension(fl, ".ahh"), lt);
                    }
                    LogEvent("<><><><><><><><><><><><><><>", Brushes.Navy);
                }
            }
        }
    }
}


    

