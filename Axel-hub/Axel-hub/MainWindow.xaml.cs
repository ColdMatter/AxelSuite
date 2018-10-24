using NationalInstruments.Net;
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
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using scanHub;
using AxelHMemsNS;
using AxelChartNS;
using Newtonsoft.Json.Linq;
using UtilsNS;
using RemoteMessagingNS;
using System.Windows.Markup;
using OptionsNS;
using OptionsTypeNS;

namespace Axel_hub
{
    public delegate void StartDelegate();
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow: Window
    {        
        Modes modes;
        scanClass ucScan1;
        private int nSamples = 1500;
        const bool debugMode = true;

        private AxelMems axelMems = null;
        private bool jumboADCFlag = true;
        Random rnd = new Random();
        private const int dataLength = 10000;
        DataStack stackN1 = new DataStack(10000);
        DataStack stackN2 = new DataStack(10000);
        DataStack stackRN1 = new DataStack(10000);
        DataStack stackRN2 = new DataStack(10000);
        DataStack stackNtot = new DataStack(10000);
        DataStack stackNtot_std = new DataStack(10000);
        DataStack stackN2_std = new DataStack(10000);
        DataStack stackN2_int = new DataStack(10000);
        DataStack phiMg = new DataStack(1000);
        DataStack accelMg = new DataStack(1000); 
        private List<Point> _fringePoints = new List<Point>();
        private AutoFileLogger logger = new AutoFileLogger();
        private List<Point> timeStack = new List<Point>(); // x - time[s]; y - phi[rad]

        RemoteMessaging remoteShow;

        OptionsWindow Options; 

        public MainWindow()
        {
            InitializeComponent();
            OpenDefaultModes();

            tabSecPlots.SelectedIndex = 1;
            ucScan1 = new scanClass();
            gridLeft.Children.Add(ucScan1);
            ucScan1.Height = 266;
            ucScan1.VerticalAlignment = System.Windows.VerticalAlignment.Top; ucScan1.HorizontalAlignment = System.Windows.HorizontalAlignment.Left; 

            ucScan1.OnStart += new scanClass.StartHandler(DoStart);
            ucScan1.OnRemote += new scanClass.RemoteHandler(DoRemote);
            ucScan1.OnLog += new scanClass.LogHandler(log);
            
            AxelChart1.Waveform.TimeSeriesMode = false;

            axelMems = new AxelMems();
            axelMems.Acquire += new AxelMems.AcquireHandler(DoAcquire);
            axelMems.RealSampling += new AxelMems.RealSamplingHandler(ucScan1.OnRealSampling);

            iStack = new List<double>(); dStack = new List<double>();
            Options = new OptionsWindow();
            AxelChart1.InitOptions(ref Options.genOptions, ref modes);
            ucScan1.InitOptions(ref Options.genOptions, ref modes);

            showLogger = new AutoFileLogger(); showLogger.defaultExt = ".shw";

            if (false)//(System.Windows.Forms.SystemInformation.MonitorCount > 1) // secondary monitor
            {           
                WindowStartupLocation = WindowStartupLocation.Manual;

                System.Drawing.Rectangle workingArea = System.Windows.Forms.Screen.AllScreens[1].WorkingArea;
                Left = workingArea.Left;
                Top = workingArea.Top;
                Width = workingArea.Width;
                Height = workingArea.Height;
                WindowState = WindowState.Maximized;
                WindowStyle = WindowStyle.None;
                Topmost = true;
                
                Loaded += Window_Loaded;
                Show();
            }
            else // primary monitor
            {
                Left = modes.Left;
                Top = modes.Top;
                Width = modes.Width;
                Height = modes.Height;
            }
            rowUpperChart.Height = new GridLength(modes.TopFrame * modes.Height);
            rowMiddleChart.Height = new GridLength(modes.MiddleFrame * modes.Height);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Maximized;
        }

        private void log(string txt, Color? clr = null)
        {
            if (!chkLog.IsChecked.Value) return;
            string printOut;
            if ((chkVerbatim.IsChecked.Value) || (txt.Length<81)) printOut = txt;
            else printOut = txt.Substring(0,80)+"..."; //
            
            Color ForeColor = clr.GetValueOrDefault(Brushes.Black.Color);
            Application.Current.Dispatcher.BeginInvoke(
              DispatcherPriority.Background,
              new Action(() => 
              {
                TextRange rangeOfText1 = new TextRange(tbLog.Document.ContentStart, tbLog.Document.ContentEnd);
                string tx = rangeOfText1.Text;
                int len = tx.Length; int maxLen = 10000; // the number of chars kept
                if (len > (2 * maxLen)) // when it exceeds twice the maxLen
                {
                    tx = tx.Substring(maxLen);
                    var paragraph = new Paragraph();
                    paragraph.Inlines.Add(new Run(tx));
                    tbLog.Document.Blocks.Clear();
                    tbLog.Document.Blocks.Add(paragraph);
                }
                rangeOfText1 = new TextRange(tbLog.Document.ContentEnd, tbLog.Document.ContentEnd);
                rangeOfText1.Text = Utils.RemoveLineEndings(printOut) + "\r";
                rangeOfText1.ApplyPropertyValue(TextElement.ForegroundProperty, new System.Windows.Media.SolidColorBrush(ForeColor));
                tbLog.ScrollToEnd();
              }));
        }

        public void DoEvents() // use it with caution (or better not), risk to introduce GUI freezing
        {
            DispatcherFrame frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,
                new DispatcherOperationCallback(ExitFrame), frame);
            Dispatcher.PushFrame(frame);
        }

        public object ExitFrame(object f)
        {
            ((DispatcherFrame)f).Continue = false;
            return null;
        }

        private MMscan jumboScan()
        {
            MMscan mms = new MMscan();
            mms.groupID = DateTime.Now.ToString("yy-MM-dd_H-mm-ss");
            mms.sParam = "ramanPhase"; // phase is default
            mms.sFrom = numFrom.Value;
            mms.sTo = numTo.Value;
            mms.sBy = numBy.Value;
            return mms;
        }

        private void startADC24(bool down, double period, int InnerBufferSize)
        {
            if (!down) // user cancel
            {
                AxelChart1.Running = false;
                axelMems.StopAqcuisition();
                AxelChart1.Waveform.logger.Enabled = false;
                log("End of series !!!", Brushes.Red.Color);
                return;
            }
            if (AxelChart1.Running) AxelChart1.Running = false; //
            AxelChart1.Clear();
            AxelChart1.Waveform.StackMode = true; AxelChart1.MEMS2.StackMode = true;          
            nSamples = InnerBufferSize;
            AxelChart1.SamplingPeriod = 1/axelMems.RealConvRate(1/period);
            AxelChart1.Running = true; AxelChart1.MEMS2.Depth = AxelChart1.Waveform.Depth;
           
            AxelChart1.remoteArg = "freq: " + (1 / AxelChart1.SamplingPeriod).ToString("G6") + ", aqcPnt: " + nSamples.ToString();
            AxelChart1.Waveform.logger.Enabled = false;

            if (AxelChart1.Waveform.TimeSeriesMode) axelMems.TimingMode = AxelMems.TimingModes.byStopwatch;
            else axelMems.TimingMode = AxelMems.TimingModes.byNone;
            axelMems.activeChannel = Options.genOptions.MemsChannels;
            axelMems.StartAqcuisition(nSamples, 1 / AxelChart1.SamplingPeriod); // async acquisition 
        }

        private void jumboRepeat(int cycles, double strobe1, double strobe2)
        {
            Clear();
            tabLowPlots.SelectedIndex = 1;
            // set jumbo-repeat conditions & format MMexec
            lastGrpExe.cmd = "repeat";
            lastGrpExe.id = rnd.Next(int.MaxValue);
            lastGrpExe.prms.Clear();
            lastGrpExe.prms["groupID"] = DateTime.Now.ToString("yy-MM-dd_H-mm-ss");
            lastGrpExe.prms["cycles"] = cycles;
            if (chkFollowPID.IsChecked.Value) { lastGrpExe.prms["follow"] = 1; }
            else { lastGrpExe.prms["follow"] = 0; }
            lastGrpExe.prms["strobe1"] = strobe1; 
            lastGrpExe.prms["strobe2"] = strobe2;
            if (rbSingle.IsChecked.Value)
            {
                lastGrpExe.prms["strobes"] = 1;
                strbDownhill.X = strobe1;
            }
            else
            {
                lastGrpExe.prms["strobes"] = 2;
                strbDownhill.X = strobe1; strbUphill.X = strobe2;
            }
            string jsonR = JsonConvert.SerializeObject(lastGrpExe);
            log("<< " + jsonR, Brushes.Blue.Color);           
            ucScan1.remoteMode = RemoteMode.Jumbo_Repeat;
            ucScan1.SendJson(jsonR);
            // set ADC24 and corr. visuals
            if (jumboADCFlag && chkMemsEnabled.IsChecked.Value) 
            {
                if(chkFollowPID.IsChecked.Value) ucScan1.SetActivity("Data acquis. with PID feedback");
                else ucScan1.SetActivity("Data acquis. (no PID feedback)");
                ucScan1.Running = true;
                AxelChart1.Waveform.TimeSeriesMode = true;
                plotcursorAccel.Visibility = System.Windows.Visibility.Collapsed;

                axelMems.Reset(); timeStack.Clear();
                startADC24(true, ucScan1.GetSamplingPeriod(), ucScan1.GetBufferSize());
            }
            if (remoteShow.Connected)
            {
                showLogger.Enabled = debugMode;
                dTimer = new DispatcherTimer(DispatcherPriority.Send);
                dTimer.Tick += dTimer_Tick;
                double sp = ucScan1.GetSamplingPeriod(); double bs = ucScan1.GetBufferSize();
                int dur = (int)(sp*bs*1100.0); // [ms]
                log("set timer to " + dur + " [ms] "+sp.ToString()+"/"+bs.ToString(), Brushes.YellowGreen.Color);
                dTimer.Interval = new TimeSpan(dur*10000);
                dTimer.Start();
            }                
        }
        // the main call in simple mode
        public void DoStart(bool jumbo, bool down, double period, int sizeLimit)
        {
            if (jumbo)
            {
                if (!down) // user jumbo cancel
                {
                    AxelChart1.Running = false;
                    axelMems.StopAqcuisition();
                    AxelChart1.Waveform.logger.Enabled = false;
                    if (!Utils.isNull(dTimer))
                    {
                        dTimer.Stop(); showLogger.Enabled = false;
                    }
                    log("Jumbo END !", Brushes.Red.Color);
                    return;
                }
                lastGrpExe = new MMexec();
                lastGrpExe.mmexec = "test_drive";
                lastGrpExe.sender = "Axel-hub";

                if (Options.genOptions.JumboScan)
                {
                    Clear();
                    tabLowPlots.SelectedIndex = 0;
                    lastGrpExe.cmd = "scan";
                    lastGrpExe.id = rnd.Next(int.MaxValue);
                    lastScan = jumboScan();
                    lastScan.ToDictionary(ref lastGrpExe.prms);

                    string json = JsonConvert.SerializeObject(lastGrpExe);
                    log("<< " + json, Brushes.Green.Color);
                    ucScan1.remoteMode = RemoteMode.Jumbo_Scan;
                    ucScan1.SendJson(json);

                    if (ucScan1.remoteMode == RemoteMode.Free) return;  // end mission
                }
                else 
                {
                    Utils.TimedMessageBox("Open a fringe file, adjust the strobes and confirm.", "Jumbo-Repeat Requirements", 3500);
                    if (Utils.isNull(srsFringes)) srsFringes = new DataStack();
                    else srsFringes.Clear();
                    btnOpenFringes_Click(null, null);
                    if (srsFringes.Count == 0)
                    {
                        Utils.TimedMessageBox("No fringes for Jumbo-repeat", "Error", 5000);
                        ucScan1.Running = false;
                        return;
                    }
                    crsStrobe1.AxisValue = 4.7; crsStrobe2.AxisValue = 7.8;
                    btnConfirmStrobes.Visibility = System.Windows.Visibility.Visible;
                    btnSinFit.Visibility = System.Windows.Visibility.Visible;
                }                                   
            }
            else
            {
                if(down) Clear(true, false, false);
                int buffSize = 200;
                if (sizeLimit > -1) buffSize = sizeLimit;
                startADC24(down, period, buffSize);
            }
        }

        private void btnConfirmStrobes_Click(object sender, RoutedEventArgs e) 
        {
            int cycles = (int)numCycles.Value;
            if (Options.genOptions.JumboScan) jumboRepeat(cycles, (double)crsStrobe1.AxisValue, (double)crsStrobe2.AxisValue);
            else
            {   // no scan to pick from
                rbSingle_Checked(null, null);
                if (rbSingle.IsChecked.Value) jumboRepeat(cycles, 7.8, -1);
                else jumboRepeat(cycles, 4.7, 7.8);
            }
            btnConfirmStrobes.Visibility = System.Windows.Visibility.Hidden;
            btnSinFit.Visibility = System.Windows.Visibility.Hidden;
            log("Jumbo succession is RUNNING !", Brushes.Green.Color);
        }

        public void DoAcquire(List<Point> dt, out bool next)
        {
            next = ucScan1.EndlessMode() && AxelChart1.Running && (ucScan1.Running || (ucScan1.remoteMode == RemoteMode.Simple_Repeat));
            if (axelMems.activeChannel == 2) 
            {            
                for (int i = 0; i<dt.Count; i++) 
                {
                    if((i%2) == 0) AxelChart1.Waveform.Add(dt[i]); // channel 0
                    else AxelChart1.MEMS2.Add(dt[i]); // channel 1
                }
            }
            else AxelChart1.Waveform.AddRange(dt);
            //log("ADC>> "+DateTime.Now.ToString("HH:mm:ss"));

            AxelChart1.Refresh();
            if (!next)
            {
                AxelChart1.Running = false;
                ucScan1.Running = false;
                axelMems.StopAqcuisition(); 
                AxelChart1.Refresh();
            }
        }
        private int timeStackLimit = 3; // process back 30 time steps

        private Dictionary<string, double> prepNextMeasure(double phi)
        {
            double tm = axelMems.TimeElapsed(); // reference time stamp to look backwards
            Dictionary<string, double> rslt = new Dictionary<string, double>();
            if (double.IsNaN(tm))
            {
                log("Error: ADC24 stopwatch not running !", Brushes.Red.Color);
                return rslt;
            }
            else rslt["time"] = tm;
            if (!Double.IsNaN(phi)) rslt["PhiRad"] = phi;
            return rslt;
        }
        // in simple_repeat only Mems; in jumbo_repeat - both
        private Dictionary<string, double> nextMeasure(double phi) // result Mems, Mems2 [V] and phi[rad] 
        {
            Dictionary<string, double> rslt = prepNextMeasure(phi);
            /* timeStack.Add(new Point(tm,phi));
            if (tm > timeStack[timeStack.Count - 1].X) timeStackLimit++;
            if(timeStack.Count < timeStackLimit) return pnt; // none
            else timeStack.RemoveAt(0);
            tm = timeStack[0].X;
                           
            double strt = tm - (numMemsStart.Value - numMemsLen.Value)/1000.0;
            double len = numMemsLen.Value / 1000.0;*/
            double mn, dsp; 
            //if (AxelChart1.Waveform.statsByTime(strt, len, out mn, out wmn, out dsp)) 
            
            int j = AxelChart1.Waveform.Count - 1; // index of the last
            if (j == -1)
            {
                log("Waiting for incomming MEMS data (AxelChart1.Waveform) !", Brushes.DarkOrange.Color);
                return rslt;
            }            
            if (AxelChart1.Waveform.statsByIdx(j - 100, j, false, out mn, out dsp)) 
            {
                rslt["MEMS_V"] = mn; 
                log("Mems= "+mn.ToString("G6")+ "; Phi= "+ phi.ToString("G6"), Brushes.Teal.Color);
            }
            else log("Error in AxelChart1.Waveform.statsByIdx() !", Brushes.Red.Color);
            if (axelMems.activeChannel == 2)
            {
                if (AxelChart1.MEMS2.statsByIdx(j - 100, j, false, out mn, out dsp))
                {
                    rslt["MEMS2_V"] = mn;
                    log("Mems2= " + mn.ToString("G6") + "; Phi= " + phi.ToString("G6"), Brushes.Teal.Color);
                }
                else log("Error in AxelChart1.Waveform.statsByIdx() !", Brushes.Red.Color);   
            }           
            return rslt;
        }

        private MMexec lastGrpExe; private MMscan lastScan; //
        private Point strbDownhill = new Point(); private Point strbUphill = new Point();
        private double phaseCorr, phaseRad, NsYmin = 10, NsYmax = -10, signalYmin = 10, signalYmax = -10,
            fringesYmin = 10, fringesYmax = -10, accelYmin = 10, accelYmax = -10;
        DataStack srsFringes = null; DataStack srsMotAccel = null; DataStack srsCorr = null; DataStack srsMems = null; DataStack srsAccel = null;
        DataStack signalDataStack = null;  DataStack backgroundDataStack = null;

        // remote MM call
        public void DoRemote(string json) // from TotalCount to 1
        {
            MMexec mme = JsonConvert.DeserializeObject<MMexec>(json);
            switch(mme.cmd)
            {
#region shotData
                case("shotData"): // incomming MM2 data
                {
                    if (Utils.isNull(lastGrpExe))
                    {
                        if (MessageBox.Show("Abort sequence?", "Question", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                        {
                            ucScan1.Abort(true);
                        }
                        throw new Exception("Wrong sequence of MMexec's");
                    }
                    bool scanMode = lastGrpExe.cmd.Equals("scan");
                    bool repeatMode = lastGrpExe.cmd.Equals("repeat");
                    bool middleSection = (tabSecPlots.SelectedIndex == 1); // when the charts are shown
                    if (Convert.ToInt32(mme.prms["runID"]) == 0) // init at first shot from series
                    {
                        if (Utils.isNull(srsFringes)) srsFringes = new DataStack(dataLength);
                        if (Utils.isNull(srsMotAccel)) srsMotAccel = new DataStack(dataLength);
                        if (Utils.isNull(srsCorr)) srsCorr = new DataStack(dataLength);
                        if (Utils.isNull(srsMems)) srsMems = new DataStack(dataLength);
                        if (Utils.isNull(srsAccel)) srsAccel = new DataStack(dataLength);
                        if (Utils.isNull(phiMg)) phiMg = new DataStack(1000);
                        if (Utils.isNull(accelMg)) accelMg = new DataStack(1000);

                        Clear(chkMemsEnabled.IsChecked.Value, true, true);
                        logger.Enabled = false; logger.AutoSaveFileName = ""; logger.Enabled = chkLogFile.IsChecked.Value;
                        logger.log("# "+JsonConvert.SerializeObject(lastGrpExe));
                        logger.log("# XAxis\tN1\tN2\tRN1\tRN2\tNTot\tB2\tBtot");
                        if (scanMode) lbInfoFringes.Content = "groupID:" + lastScan.groupID + ";  Scanning: " + lastScan.sParam +
                           ";  From: " + lastScan.sFrom.ToString("G4") + ";  To: " + lastScan.sTo.ToString("G4") + ";  By: " + lastScan.sBy.ToString("G4");
                        if (repeatMode) lbInfoAccelTrend.Content = "groupID:" + lastGrpExe.prms["groupID"] + ";  Repeat: " + lastGrpExe.prms["cycles"] + " cycles";
                    }
                    string s1 = (string)lastGrpExe.prms["groupID"];
                    if (!s1.Equals((string)mme.prms["groupID"])) throw new Exception("Wrong groupID"); 

                    MMDataConverter.ConvertToDoubleArray(ref mme);

                    string endBit = "", log_out = ""; int runID = 0;
                    runID = Convert.ToInt32(mme.prms["runID"]);
                    if(scanMode) endBit = ";  scanX = " + (lastScan.sFrom+runID*lastScan.sBy).ToString("G4");
                    lbInfoSignal.Content = "cmd: " + lastGrpExe.cmd + ";  grpID: " + lastGrpExe.prms["groupID"] + ";  runID: "+ runID.ToString() + endBit;
                    if (chkVerbatim.IsChecked.Value) log("("+json.Length.ToString()+") "+json);
                    else 
                    {
                        log_out = ">SHOT #"+runID.ToString()+"; ";
                        
                        //if(scanMode) log(endBit, Brushes.DarkBlue.Color);
                        if (mme.prms.ContainsKey("last"))
                        {
                            log(">LAST SHOT", Brushes.DarkRed.Color);
                            if (ucScan1.remoteMode == RemoteMode.Jumbo_Repeat)
                            {
                                ucScan1.Abort(false);
                                lastGrpExe.Clear();
                            }
                        }                             
                    }
                    Dictionary<string, double> avgs = MMDataConverter.AverageShotSegments(mme,Options.genOptions.intN2, chkStdDev.IsChecked.Value);
                    if (middleSection)
                    {
                        lboxNB.Items.Clear();
                        foreach (var item in avgs)
                        {
                            ListBoxItem lbi = new ListBoxItem();
                            lbi.Content = string.Format("{0}: {1:" + Options.genOptions.SignalTablePrec + "}", item.Key, item.Value);
                            if (item.Key.IndexOf("_std") > 0) lbi.Foreground = Brushes.Green;
                            else lbi.Foreground = Brushes.Blue;
                            lboxNB.Items.Add(lbi);
                        }
                    }
                    double asymmetry = MMDataConverter.Asymmetry(avgs, chkBackgroung.IsChecked.Value, chkDarkcurrent.IsChecked.Value);
                    //
                    // signal chart (rigth)
                    if (Utils.isNull(signalDataStack)) signalDataStack = new DataStack();
                    else signalDataStack.Clear();
                    if (Utils.isNull(backgroundDataStack)) backgroundDataStack = new DataStack();
                    else backgroundDataStack.Clear();

                    int xVal = 0; double N2 = avgs["N2"];
                    if (middleSection)
                        foreach (double yVal in (double[])mme.prms["N2"])
                        {
                            signalDataStack.Add(new Point(xVal, yVal));
                            xVal++;
                        }
                    double NTot = avgs["NTot"];
                    if (middleSection)
                        foreach (double yVal in (double[])mme.prms["NTot"])
                        {
                            signalDataStack.Add(new Point(xVal, yVal));
                            xVal++;
                        }

                    xVal = 0; double B2 = avgs["B2"];
                    if (middleSection)
                        foreach (double yVal in (double[])mme.prms["B2"])
                        {
                            backgroundDataStack.Add(new Point(xVal, yVal));
                            xVal++;
                        }
                    double BTot = avgs["BTot"];
                    if (middleSection)
                        foreach (double yVal in (double[])mme.prms["BTot"])
                        {
                            backgroundDataStack.Add(new Point(xVal, yVal));
                            xVal++;
                        }
                    if (middleSection) // skip the show
                    {
                        graphSignal.Data[0] = signalDataStack;
                        graphSignal.Data[1] = backgroundDataStack;
                    }
                    // readjust Y axis
                    if (!chkManualAxisMiddle.IsChecked.Value && middleSection) // signal auto-Y-limits
                    {
                        double d = Math.Min(signalDataStack.pointYs().Min(), backgroundDataStack.pointYs().Min());
                        d = Math.Floor(10 * d) / 10;
                        signalYmin = Math.Min(d, signalYmin);
                        d = Math.Max(signalDataStack.pointYs().Max(), backgroundDataStack.pointYs().Max());
                        d = Math.Ceiling(10 * d) / 10;
                        signalYmax = Math.Max(d, signalYmax);
                        d = (signalYmax - signalYmin) * 0.02;
                        signalYaxis.Range = new Range<double>(signalYmin - d, signalYmax + d);
                    }
                    //
                    // Ns chart (left)
                   
                    double corr, disbalance;
                    // corrected with background
                    double cNtot = NTot - BTot; double cN2 = N2 - B2; double cN1 = cNtot - cN2;
                    double A = cN2 / cNtot ;//(N2 - B2) / (NTot - BTot); //double A = 1 - 2 * (N2 - B2) / (NTot - BTot);
                    double currX = 1, cN2_std = 1, cNtot_std = 1;
                    if (chkStdDev.IsChecked.Value)
                    {
                        cN2_std = Math.Sqrt(Math.Pow(avgs["N2_std"],2) + Math.Pow(avgs["B2_std"],2));
                        cNtot_std = Math.Sqrt(Math.Pow(avgs["NTot_std"],2) + Math.Pow(avgs["BTot_std"],2));
                    }
                    double cinitN2 = avgs["initN2"] - B2;
                    if (scanMode)
                    {
                        currX = lastScan.sFrom + runID * lastScan.sBy;
                        if (middleSection)
                        {
                            stackN1.Add(new Point(currX, cN1)); stackN2.Add(new Point(currX, cN2)); stackNtot.Add(new Point(currX, cNtot));
                            stackRN1.Add(new Point(currX, cN1 / cNtot)); stackRN2.Add(new Point(currX, cN2 / cNtot));
                            if (chkStdDev.IsChecked.Value)
                            {
                                stackN2_std.Add(new Point(currX, cN2_std)); stackNtot_std.Add(new Point(currX, cNtot_std));
                            }
                            stackN2_int.Add(new Point(currX, cinitN2));
                        }
                    }
                    else currX = runID;

                    if (middleSection)
                    {
                        if (repeatMode)
                        {
                            stackN1.AddPoint(cN1); stackN2.AddPoint(cN2); stackNtot.AddPoint(cNtot);
                            stackRN1.AddPoint(cN1 / cNtot); stackRN2.AddPoint(cN2 / cNtot);  stackN2_int.AddPoint(cinitN2);
                            if (chkStdDev.IsChecked.Value)
                            {
                                stackN2_std.AddPoint(cN2_std); stackNtot_std.AddPoint(cNtot_std);
                            }
                        }
                        if (!chkManualAxisMiddle.IsChecked.Value) // Ns auto-Y-limits
                        {                      
                            List<double> ld = new List<double>();
                            ld.Add(stackN1.pointYs().Min()); ld.Add(stackN2.pointYs().Min()); ld.Add(stackNtot.pointYs().Min()); 
                            ld.Add(stackRN1.pointYs().Min()); ld.Add(stackRN2.pointYs().Min());
                            double d = ld.Min();
                            d = Math.Floor(10 * d) / 10;
                            NsYmin = Math.Min(d, NsYmin);
                            ld.Clear();
                            ld.Add(stackN1.pointYs().Max()); ld.Add(stackN2.pointYs().Max()); ld.Add(stackNtot.pointYs().Max());
                            ld.Add(stackRN1.pointYs().Max()); ld.Add(stackRN2.pointYs().Max());
                            d = ld.Max();
                            d = Math.Ceiling(10 * d) / 10;
                            NsYmax = Math.Max(d, NsYmax);
                            d = (NsYmax - NsYmin) * 0.02;
                            NsYaxis.Range = new Range<double>(NsYmin - d, NsYmax + d);
                        }
                        graphNs.Data[0] = stackN1.Portion(Options.genOptions.visualDataLength);
                        graphNs.Data[1] = stackN2.Portion(Options.genOptions.visualDataLength);
                        graphNs.Data[2] = stackRN1.Portion(Options.genOptions.visualDataLength);
                        graphNs.Data[3] = stackRN2.Portion(Options.genOptions.visualDataLength);
                        graphNs.Data[4] = stackNtot.Portion(Options.genOptions.visualDataLength);                        
                    }

                    //#XAxis\tN1\tN2\tRN1\tRN2\tNTot\tB2\tBtot
                    if (stackN1.Count > 0)
                    {
                        string ss = currX.ToString(Options.genOptions.SaveFilePrec) + "\t" + stackN1.Last.Y.ToString(Options.genOptions.SaveFilePrec) + 
                            "\t" + stackN2.Last.Y.ToString(Options.genOptions.SaveFilePrec) + "\t" + stackRN1.Last.Y.ToString(Options.genOptions.SaveFilePrec) + 
                            "\t" + stackRN2.Last.Y.ToString(Options.genOptions.SaveFilePrec) + "\t" + stackNtot.Last.Y.ToString(Options.genOptions.SaveFilePrec) +
                            "\t" + B2.ToString(Options.genOptions.SaveFilePrec) + "\t" + BTot.ToString(Options.genOptions.SaveFilePrec);
                        logger.log(ss);
                    }
                    //
                    // LOWER section
                    if (scanMode) 
                    {
                        srsFringes.Add(new Point(currX, A)); //asymmetry
                        if (!chkVerbatim.IsChecked.Value)
                            log(log_out + "scanX/Y= " + currX.ToString(Options.genOptions.SignalTablePrec) +
                            " / " + A.ToString(Options.genOptions.SignalTablePrec), Brushes.DarkGreen.Color);//asymmetry.ToString
                        if (!chkManualAxisBottom.IsChecked.Value) // Fringes
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
                    }
                    Dictionary<string, double> statDt = new Dictionary<string, double>();
                    if (repeatMode) 
                    {
                        if (ucScan1.remoteMode == RemoteMode.Jumbo_Repeat)
                        {    
                            double disbalCorr = 0;
                            if (rbSingle.IsChecked.Value)
                            {
                                disbalance = A;//asymmetry
                            }
                            else // double strobe
                            {
                                if ((runID % 2) == 0) strbDownhill.Y = A;//asymmetry
                                else strbUphill.Y = A;//asymmetry
                                disbalance = strbDownhill.Y - strbUphill.Y;
                                disbalCorr = (disbalance / numScale.Value) / 2 ; // correction for disbalance
                                Color clr = Brushes.Black.Color;
                                if (Math.Abs(disbalCorr) > 0.8) clr = Brushes.Red.Color;
                                log("s.Down/Up: " + strbDownhill.Y.ToString("G4") + "/" + strbUphill.Y.ToString("G4") + "; d.corr: " + disbalCorr.ToString("G4"), clr);
                                
                            }
                            if ((chkFollowPID.IsChecked.Value))
                            {
                                corr = PID(disbalance / numScale.Value);// +disbalCorr; // in rad                               
                                if (rbSingle.IsChecked.Value) // single strobe
                                {
                                    strbDownhill.X = MMDataConverter.Restrict2twoPI(strbDownhill.X + corr); phaseCorr = strbDownhill.X; phaseRad = phaseCorr;
                                }
                                else // double strobe
                                {
                                    if ((runID % 2) == 0) 
                                    {
                                        strbDownhill.X = MMDataConverter.Restrict2twoPI(strbDownhill.X + corr); phaseCorr = strbDownhill.X; 
                                    }
                                    else 
                                    {
                                        strbUphill.X = MMDataConverter.Restrict2twoPI(strbUphill.X + corr); ; phaseCorr = strbUphill.X; 
                                    }
                                    phaseRad = (strbUphill.X + strbDownhill.X) / 2 + disbalCorr; 
                                }
                                MMexec mmeOut = new MMexec("", "Axel-hub","phaseAdjust");
                                mmeOut.prms.Clear();
                                mmeOut.prms["runID"] = runID;
                                mmeOut.prms["phase"] = phaseCorr.ToString("G6");
                                ucScan1.SendJson(JsonConvert.SerializeObject(mmeOut), true); 
                                srsCorr.Add(new Point(runID, corr)); graphAccelTrend.Data[1] = srsCorr.Portion(Options.genOptions.visualDataLength);
                               // statDt["PhiRad"] = phaseRad - numPhi0.Value;
                                statDt["PhiRad"] = phaseRad ;  
                            }
                            else // no PID feedback
                            {
                                phaseRad = //(strbUphill.X + strbDownhill.X) / 2 + 
                                    disbalCorr; 
                                statDt["PhiRad"] = phaseRad - numPhi0.Value; 
                            }
                        }
                        double xVl = runID; Dictionary<string, double> measr = new Dictionary<string, double>(); 
                        Dictionary<string, double> rslt = new Dictionary<string, double>();
                        // the MEMS bit in RemoteMode.xxx_Repeat mode
                        if (AxelChart1.Running && chkMemsEnabled.IsChecked.Value && jumboADCFlag)
                        {
                            if (ucScan1.remoteMode == RemoteMode.Simple_Repeat) rslt = nextMeasure(double.NaN);
                            if (ucScan1.remoteMode == RemoteMode.Jumbo_Repeat)
                            {
                                if (statDt.ContainsKey("PhiRad")) phaseRad = statDt["PhiRad"];
                                else phaseRad = Double.NaN;
                                
                                if (mme.sender.Equals("Axel-probe") && mme.prms.ContainsKey("MEMS"))
                                {
                                    measr = prepNextMeasure(phaseRad);
                                    measr["MEMS"] = Convert.ToDouble(mme.prms["MEMS"]);
                                }
                                else measr = nextMeasure(phaseRad);
                                rslt = Statistics(measr);
                                //if (remoteShow.Connected) export2Show(rslt);
                            }
                            if (rslt.ContainsKey("Accel")) log_out += "accel= " + rslt["Accel"].ToString(Options.genOptions.SignalTablePrec);
                        }                                                 
                        log(log_out, Brushes.DarkGreen.Color);
                        if(repeatMode) 
                        {
                            if (AxelChart1.Waveform.stopWatch.IsRunning) xVl = AxelChart1.Waveform.stopWatch.ElapsedMilliseconds / 1000.0;
                            if (rslt.ContainsKey("MEMS"))
                            {
                                srsMems.AddPoint(rslt["MEMS"], xVl); graphAccelTrend.Data[0] = srsMems.Portion(Options.genOptions.visualDataLength);
                            }
                            if (ucScan1.remoteMode == RemoteMode.Simple_Repeat)
                            {
                                srsMotAccel.Add(new Point(xVl, A)); graphAccelTrend.Data[2] = srsMotAccel.Portion(Options.genOptions.visualDataLength);
                            }                            
                            if (ucScan1.remoteMode == RemoteMode.Jumbo_Repeat)
                            {
                                if (rslt.ContainsKey("PhiMg"))
                                {
                                    srsMotAccel.AddPoint(rslt["PhiMg"], xVl); graphAccelTrend.Data[2] = srsMotAccel.Portion(Options.genOptions.visualDataLength);
                                }
                                if (rslt.ContainsKey("Accel"))
                                {
                                    srsAccel.AddPoint(rslt["Accel"], xVl); graphAccelTrend.Data[3] = srsAccel.Portion(Options.genOptions.visualDataLength);
                                }
                            }
                        }
                        if (!chkManualAxisBottom.IsChecked.Value && srsMotAccel.Count > 0) // Accel.Trend axis
                        {
                            if (ucScan1.remoteMode == RemoteMode.Simple_Repeat || ucScan1.remoteMode == RemoteMode.Jumbo_Repeat) 
                            {
                                accelYmin = Math.Min(Math.Floor(10 * srsMotAccel.pointYs().Min()) / 10, accelYmin);
                                accelYmax = Math.Max(Math.Ceiling(10 * srsMotAccel.pointYs().Max()) / 10, accelYmax);
                                if (rslt.ContainsKey("MEMS"))
                                {
                                    accelYmin = Math.Min(Math.Floor(10 * srsMems.pointYs().Min()) / 10, accelYmin);
                                    accelYmax = Math.Max(Math.Ceiling(10 * srsMems.pointYs().Max()) / 10, accelYmax);
                                }
                            }
                            if (ucScan1.remoteMode == RemoteMode.Jumbo_Repeat)
                            {
                                // !!!
                            }
                            double d = (accelYmax-accelYmin) * 0.02;
                            accelYaxis.Range = new Range<double>(accelYmin - d, accelYmax + d);
                        }
                    }
                    if (scanMode && ucScan1.remoteMode == RemoteMode.Jumbo_Scan || ucScan1.remoteMode == RemoteMode.Simple_Scan)
                    {
                        if (mme.prms.ContainsKey("last"))
                        {
                            if (Convert.ToInt32(mme.prms["last"]) == 1)
                            {
                                if (!Options.genOptions.JumboRepeat)
                                {
                                    ucScan1.Running = false;
                                    startADC24(false, 1.0 / 2133, 200);
                                    ucScan1.remoteMode = RemoteMode.Free;
                                    return;
                                }
                                if (ucScan1.remoteMode == RemoteMode.Jumbo_Scan && Options.genOptions.JumboRepeat) // transition from jumboScan to jumboRepeat
                                {
                                    crsStrobe1.AxisValue = 4.7; crsStrobe2.AxisValue = 7.8;
                                    btnConfirmStrobes.Visibility = System.Windows.Visibility.Visible;
                                    btnSinFit.Visibility = System.Windows.Visibility.Visible;
                                    Utils.TimedMessageBox("Please adjust the strobes and confirm to continue.", "Information", 2500);
                                }
                            }
                        }
                    }
                    if ((ucScan1.remoteMode == RemoteMode.Jumbo_Repeat) || (ucScan1.remoteMode == RemoteMode.Simple_Repeat))
                    {
                        if (mme.prms.ContainsKey("last")) 
                        {
                            if (Convert.ToInt32(mme.prms["last"]) == 1)
                            {
                                ucScan1.Running = false;
                                startADC24(false, 1.0 / 2133, 200);
                                ucScan1.remoteMode = RemoteMode.Free;                               
                            }
                            logger.Enabled = false;
                        }
                    }
                }            
                break;
#endregion shotData
                case ("repeat"):
                    {
                        log(json, Brushes.Blue.Color);
                        lastGrpExe = mme.Clone();
                        if (!mme.sender.Equals("Axel-hub")) ucScan1.remoteMode = RemoteMode.Simple_Repeat;
                        tabLowPlots.SelectedIndex = 1;
                        chkN1_Checked(null, null); // update state
                        chkMEMS_Checked(null, null);
                        Clear();
                        if (jumboADCFlag && chkMemsEnabled.IsChecked.Value) 
                        {
                            ucScan1.SetActivity("Data acquisition");
                            ucScan1.Running = true;
                            AxelChart1.Waveform.TimeSeriesMode = true;
                            plotcursorAccel.Visibility = System.Windows.Visibility.Collapsed;

                            axelMems.Reset(); timeStack.Clear();
                            startADC24(true, ucScan1.GetSamplingPeriod(), ucScan1.GetBufferSize()); 
                        }
                        else
                        {
                            plotcursorAccel.Visibility = System.Windows.Visibility.Visible;
                            ucScan1.SetActivity("");
                        }                           
                    }
                    break;
                case ("scan"):
                    {
                        log(json, Brushes.DarkGreen.Color);                       
                        lastGrpExe = mme.Clone();                       
                        if (Utils.isNull(lastScan)) lastScan = new MMscan();
                        if (!lastScan.FromDictionary(mme.prms))
                        {
                            log("Error in incomming json", Brushes.Red.Color);
                            ucScan1.Abort(true);
                            return;
                        }
                        if (!mme.sender.Equals("Axel-hub")) ucScan1.remoteMode = RemoteMode.Simple_Scan;
                        tabLowPlots.SelectedIndex = 0;
                        chkN1_Checked(null, null);
                        Clear();
                    }
                    break;
                case ("message"):
                    {
                        string txt = (string)mme.prms["text"];
                        int errCode = -1;
                        if (mme.prms.ContainsKey("error")) errCode = Convert.ToInt32(mme.prms["error"]);                        
                        if(txt.Contains("Error") || (errCode > -1))
                        { 
                            log("!!! "+txt, Brushes.Red.Color);
                            if (errCode > -1) log("Error code: "+errCode.ToString(), Brushes.Red.Color);
                        }
                        else log("! "+txt, Brushes.Coral.Color);
                    }
                    break;
                case ("abort"):
                    {
                        log(json, Brushes.Red.Color);
                        if (ucScan1.remoteMode != RemoteMode.Free)
                        {
                            ucScan1.Abort(false);
                        }
                        logger.Enabled = false;
                    }
                    break;
            }
        }

        public Dictionary<string, double> Statistics(Dictionary<string, double> dt) // in MEMS [V]; PhiRad -> out - MEMS [mg], etc.
        {   
            Dictionary<string, double> rslt = new Dictionary<string,double>(dt);
            if (!(dt.ContainsKey("MEMS_V") || dt.ContainsKey("MEMS")) || (!dt.ContainsKey("PhiRad"))) return rslt; // works only if both are present
            
            if (!dt.ContainsKey("K")) rslt["K"] = numKcoeff.Value;
            if (!dt.ContainsKey("Phi0")) rslt["Phi0"] = numPhi0.Value;
            if (!dt.ContainsKey("Scale")) rslt["Scale"] = numScale.Value;
            
            if(dt.ContainsKey("MEMS_V")) rslt["MEMS"] = AxelChart1.convertV2mg(dt["MEMS_V"]); // convert V to mg
            if(dt.ContainsKey("MEMS2_V")) rslt["MEMS2"] = AxelChart1.convertV2mg(dt["MEMS2_V"],true);  

            rslt["PhiMg"] = (dt["PhiRad"] - rslt["Phi0"]) * rslt["K"];  // convert rad to mg
          //  rslt["PhiMg"] = (dt["PhiRad"] ) * rslt["K"];  // convert rad to mg
            phiMg.AddPoint(rslt["PhiMg"]);
            
            double ord = (rslt["MEMS"] - rslt["PhiMg"]) / (2 * Math.PI * numKcoeff.Value);
            rslt["Order"] = Math.Round(ord);
            rslt["OrdRes"] = ord - Math.Round(ord);
            rslt["Accel"] = 2 * Math.PI * numKcoeff.Value * rslt["Order"] + rslt["PhiMg"];
            accelMg.AddPoint(rslt["Accel"]);
           
            string ss = "";
            if ((tabSecPlots.SelectedIndex == 4) && chkBigCalcUpdate.IsChecked.Value)
            {
                if (rslt.ContainsKey("MEMS2")) ss = " / "+rslt["MEMS2"].ToString(Options.genOptions.SignalTablePrec);
                lbiMEMS.Content = "MEMS[mg] = " + rslt["MEMS"].ToString(Options.genOptions.SignalTablePrec) + ss;

                lbiPhiRad.Content = "Phi[rad] = " + rslt["PhiRad"].ToString(Options.genOptions.SignalTablePrec);
                lbiPhiMg.Content = "Phi[mg] = " + rslt["PhiMg"].ToString(Options.genOptions.SignalTablePrec);

                lbiOrder.Content = "Order = " + rslt["Order"].ToString(Options.genOptions.SignalTablePrec);
                lbiOrdRes.Content = "OrdRes[mg] = " + rslt["OrdRes"].ToString(Options.genOptions.SignalTablePrec);
                lbiAccel.Content = "Accel[mg] = " + rslt["Accel"].ToString(Options.genOptions.SignalTablePrec);
            }
            return rslt;
        }

        private void export2Show(Dictionary<string, double> dt)
        {
            if (dt.Count == 0) return;
            Dictionary<string, double> ds = new Dictionary<string, double>();
            ds["sender"] = remoteShow.keyID;
            if (dt.ContainsKey("MEMS")) ds["MEMS"] = dt["MEMS"];
            if (dt.ContainsKey("MEMS2")) ds["MEMS2"] = dt["MEMS2"];
            if(dt.ContainsKey("PhiMg")) ds["PhiMg"] = dt["PhiMg"];
            if(dt.ContainsKey("Accel")) ds["Accel"] = dt["Accel"];
            string json = JsonConvert.SerializeObject(ds);
            showLogger.log(json);
            remoteShow.sendCommand(json);
        }

        List<double> iStack, dStack;
        int iStDepth = 5; int dStDepth = 3;
        public double PID(double curr)
        {
            double pTerm = curr;
            iStack.Add(curr); while (iStack.Count > iStDepth) iStack.RemoveAt(0);
            double iTerm = iStack.Average();
            dStack.Add(curr); while (dStack.Count > dStDepth) dStack.RemoveAt(0);
            double dTerm = 0;
            for (int i = 0; i < dStack.Count - 1; i++)
            {
                dTerm += dStack[i + 1] - dStack[i];
            }
            dTerm /= Math.Max(dStack.Count - 1, 1);

            double cr = ndKP.Value * pTerm + ndKI.Value * iTerm + ndKD.Value * dTerm;
            log("PID> " + pTerm.ToString("G3") + "  " + iTerm.ToString("G3") + " " + dTerm.ToString("G3") +
                // PID X correction and Y value after the correction
                " corr " + cr.ToString("G4") + " for " + curr.ToString("G4"), Brushes.Navy.Color);
            return cr;
        }

        #region File operation 
        //TODO Change this to add new columns to corresponding datastacks (N2_std, Ntot_std etc) - maybe using a list?
        public bool OpenSignal(string fn)
        {
            if (!File.Exists(fn)) throw new Exception("File <" + fn + "> does not exist.");

            Clear(false, true, false);
            string[] ns; int j = 0; double x,d; 
            foreach (string line in File.ReadLines(fn))
            {
                if(line.Contains("#{"))
                {
                    lastGrpExe = JsonConvert.DeserializeObject<MMexec>(line.Substring(1));
                }
                if (line.Contains("#Rem="))
                {
                    tbRemSignal.Text = line.Substring(5); lbInfoSignal.Content = tbRemSignal.Text;
                }
                if (line[0] == '#') continue; //skip comments/service info
                ns = line.Split('\t');
                if (ns.Length < 6) continue;                
                if (!double.TryParse(ns[0], out x)) throw new Exception("Wrong double at line " + j.ToString());
                if (!double.TryParse(ns[1], out d)) throw new Exception("Wrong double at line " + j.ToString());
                stackN1.Add(new Point(x,d));
                if (!double.TryParse(ns[2], out d)) throw new Exception("Wrong double at line " + j.ToString());
                stackN2.Add(new Point(x, d));
                if (!double.TryParse(ns[3], out d)) throw new Exception("Wrong double at line " + j.ToString());
                stackRN1.Add(new Point(x, d));
                if (!double.TryParse(ns[4], out d)) throw new Exception("Wrong double at line " + j.ToString());
                stackRN2.Add(new Point(x, d));
                if (!double.TryParse(ns[5], out d)) throw new Exception("Wrong double at line " + j.ToString());
                stackNtot.Add(new Point(x, d));                
                j++;
            }
            graphNs.Data[0] = stackN1; graphNs.Data[1] = stackN2;
            graphNs.Data[2] = stackRN1; graphNs.Data[3] = stackRN2; graphNs.Data[4] = stackNtot;
            log("Opened> " + fn);
            return true;
        }

        private void btnOpenSignal_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.FileName = ""; // Default file name
            dlg.DefaultExt = ".ahs"; // Default file extension
            dlg.Filter = "Axel Hub Signal (.ahs)|*.ahs"; // Filter files by extension

            // Show save file dialog box
            Nullable<bool> result = dlg.ShowDialog();

            // Process save file dialog box results
            if (result == true) OpenSignal(dlg.FileName);
        }

        public void SaveSignal(string fn) 
        {
            if (stackN1.Count == 0)
            {
                MessageBox.Show("Error: No signal data to be saved");
                return;
            }
            System.IO.StreamWriter file = new System.IO.StreamWriter(fn);
            if(!Utils.isNull(lastGrpExe)) file.WriteLine("#"+JsonConvert.SerializeObject(lastGrpExe));
            if (!String.IsNullOrEmpty(tbRemSignal.Text)) file.WriteLine("#Rem=" + tbRemSignal.Text);
            file.WriteLine("#XAxis\tN1\tN2\tRN1\tRN2\tNTot\tN2_std\tNtot_std\tN2int");  
            for (int i = 0; i < stackN1.Count; i++)
            {
                string ss = stackN1[i].X.ToString(Options.genOptions.SaveFilePrec) + "\t" + stackN1[i].Y.ToString(Options.genOptions.SaveFilePrec) + "\t" +
                    stackN2[i].Y.ToString(Options.genOptions.SaveFilePrec) + "\t" + stackRN1[i].Y.ToString(Options.genOptions.SaveFilePrec) + "\t" +
                    stackRN2[i].Y.ToString(Options.genOptions.SaveFilePrec) + "\t" + stackNtot[i].Y.ToString(Options.genOptions.SaveFilePrec) + "\t" +
                    stackN2_int[i].Y.ToString(Options.genOptions.SaveFilePrec);
                if (stackN2_std.Count > i) ss += "\t" + stackN2_std[i].Y.ToString(Options.genOptions.SaveFilePrec) + "\t" + stackNtot_std[i].Y.ToString(Options.genOptions.SaveFilePrec);
                file.WriteLine(ss);
            }
            file.Close();
            log("Saved> " + fn);
        }

        private void btnSaveSignalAs_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.FileName = ""; // Default file name
            dlg.DefaultExt = ".ahs"; // Default file extension
            dlg.Filter = "Axel Hub Signal (.ahs)|*.ahs"; // Filter files by extension

            // Show save file dialog box
            Nullable<bool> result = dlg.ShowDialog();
            if (result == true) SaveSignal(dlg.FileName);
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
            graphFringes.DataSource = srsFringes;
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
            if (result == true) srsFringes.SavePair(dlg.FileName, "", Options.genOptions.SaveFilePrec);
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

        private void splitDown_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            frmAxelHub.Top = 0;
            frmAxelHub.Height = SystemParameters.WorkArea.Height;  
            frmAxelHub.Left = SystemParameters.WorkArea.Width * 0.3;
            frmAxelHub.Width = SystemParameters.WorkArea.Width * 0.7;
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
                if (rowMiddleChart.Height.Value < 38) rowMiddleChart.Height = new GridLength(Math.Min(230,middlePlotHeight), GridUnitType.Pixel);
            }
        }

        private void btnLogClear_Click(object sender, RoutedEventArgs e)
        {
           tbLog.Document.Blocks.Clear();
        }

        private void ndKP_KeyDown(object sender, KeyEventArgs e)
        {
            if ((e.Key == Key.L) && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
            {
                log("Coeff.PID= " + ndKP.Value.ToString("G3") + " / " + ndKI.Value.ToString("G3") + " / " + ndKD.Value.ToString("G3") + " ;");
            }     
        }

        private void Clear(bool Top = true, bool Middle = true, bool Bottom = true)
        {
            if (Top)
            {
                AxelChart1.Clear();                
                AxelChart1.Refresh(); 
            }
            if (Middle)
            {
                tbRemSignal.Text = "";
                if (!Utils.isNull(stackN1)) stackN1.Clear();
                if (!Utils.isNull(stackN2)) stackN2.Clear();
                if (!Utils.isNull(stackRN1)) stackRN1.Clear();
                if (!Utils.isNull(stackRN2)) stackRN2.Clear();
                if (!Utils.isNull(stackNtot)) stackNtot.Clear();
                for (int i = 0; i < graphNs.Data.Count; i++ ) graphNs.Data[i] = null;
                NsYmin = 10; NsYmax = -10;

                if (!Utils.isNull(signalDataStack)) signalDataStack.Clear();
                if (!Utils.isNull(backgroundDataStack)) backgroundDataStack.Clear();
                for (int i = 0; i < graphSignal.Data.Count; i++) graphSignal.Data[i] = null;
                signalYmin = 10; signalYmax = -10;
                lboxNB.Items.Clear();
            }
            if (Bottom)
            {
                if (!Utils.isNull(srsFringes)) srsFringes.Clear();
                for (int i = 0; i < graphFringes.Data.Count; i++) graphFringes.Data[i] = null;
 
                if (!Utils.isNull(srsMotAccel)) srsMotAccel.Clear();
                if (!Utils.isNull(srsCorr)) srsCorr.Clear();
                if (!Utils.isNull(srsMems)) srsMems.Clear();
                if (!Utils.isNull(srsAccel)) srsAccel.Clear();
                for (int i = 0; i < graphAccelTrend.Data.Count; i++) graphAccelTrend.Data[i] = null;
                graphAccelTrend.Data.Clear();
                fringesYmin = 10; fringesYmax = -10; accelYmin = 10; accelYmax = -10;
            }
        }

        private void chkN1_Checked(object sender, RoutedEventArgs e)
        {
            if (plotN1 != null)
            {
                if (chkN1.IsChecked.Value) plotN1.Visibility = System.Windows.Visibility.Visible;
                else plotN1.Visibility = System.Windows.Visibility.Hidden;
            }
            if (plotN2 != null)
            {
                if (chkN2.IsChecked.Value) plotN2.Visibility = System.Windows.Visibility.Visible;
                else plotN2.Visibility = System.Windows.Visibility.Hidden;
            }
            if (plotRN1 != null)
            {
                if (chkRN1.IsChecked.Value) plotRN1.Visibility = System.Windows.Visibility.Visible;
                else plotRN1.Visibility = System.Windows.Visibility.Hidden;
            }
            if (plotRN2 != null)
            {
                if (chkRN2.IsChecked.Value) plotRN2.Visibility = System.Windows.Visibility.Visible;
                else plotRN2.Visibility = System.Windows.Visibility.Hidden;
            }
            if (plotNtot != null)
            {
                if (chkNtot.IsChecked.Value) plotNtot.Visibility = System.Windows.Visibility.Visible;
                else plotNtot.Visibility = System.Windows.Visibility.Hidden;
            }
        }

        private void rbSingle_Checked(object sender, RoutedEventArgs e)
        {
            crsStrobe1.AxisValue = 2;
            if (rbSingle.IsChecked.Value) crsStrobe2.Visibility = System.Windows.Visibility.Hidden;
            else
            {
                crsStrobe2.Visibility = System.Windows.Visibility.Visible;
                crsStrobe2.AxisValue = 5;
            }
        }

        double hiddenTopHeight = 230;
        private void splitterTop_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (rowUpperChart.Height.Value < 5) rowUpperChart.Height = new GridLength(Math.Min(230, hiddenTopHeight), GridUnitType.Pixel);
            else
            {
                hiddenTopHeight = rowUpperChart.Height.Value;
                rowUpperChart.Height = new GridLength(3, GridUnitType.Pixel);
            }
        }

        private void graphNs_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                if (e.Key == Key.C)
                { AxelChart1.btnCpyPic_Click(sender, null); }
            }
        }

        private void graphNs_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            (sender as Graph).ResetZoomPan();
            if (sender == graphNs)
            {
                NsYmin = 10; NsYmax = -10;
            }
            if (sender == graphSignal)
            {
                signalYmin = 10; signalYmax = -10;
            }
            if (sender == graphFringes)
            {
                fringesYmin = 10; fringesYmax = -10;
            }
            if (sender == graphAccelTrend)
            {
                accelYmin = 10; accelYmax = -10;
            }
        }

        private void imgMenu_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if(Utils.isNull(Options)) Options = new OptionsWindow();
            if(!Utils.isNull(sender)) Options.ShowDialog();
            NsCursor.ValuePresenter = new ValueFormatterGroup(" — ", new GeneralValueFormatter("0.00"))
            {
                ValueFormatters = { new GeneralValueFormatter(Options.genOptions.SignalCursorPrec) }
            };
        }

        #region close and modes
        private void OpenDefaultModes(bool Middle = true, bool Bottom = true)
        {
            if (File.Exists(Utils.configPath + "Defaults.cfg"))
            {
                string fileJson = File.ReadAllText(Utils.configPath + "Defaults.cfg");
                modes = JsonConvert.DeserializeObject<Modes>(fileJson);
            }
            else
                modes = new Modes();
            if (Middle)
            {
                chkManualAxisMiddle.IsChecked = modes.ManualYAxisMiddle;
                chkBackgroung.IsChecked = modes.Background;
                chkDarkcurrent.IsChecked = modes.DarkCurrent;
                chkStdDev.IsChecked = modes.StdDev;
                chkN1.IsChecked = modes.N1;
                chkN2.IsChecked= modes.N2;
                chkRN1.IsChecked = modes.RN1;
                chkRN2.IsChecked = modes.RN2;
                chkNtot.IsChecked = modes.Ntot;

                chkBigCalcUpdate.IsChecked = modes.RsltUpdating;
                chkDetail.IsChecked = modes.RsltDetails;
                chkLogFile.IsChecked = modes.SignalLogFile;
            }
            if (Bottom)
            {
                numFrom.Value = modes.JumboFrom;
                numTo.Value = modes.JumboTo;
                numBy.Value = modes.JumboBy;
                numCycles.Value = modes.JumboCycles;

                chkMemsEnabled.IsChecked = modes.MemsEnabled;
                numMemsStart.Value = modes.MemsStart;
                numMemsLen.Value = modes.MemsLen;

                numKcoeff.Value = modes.Kcoeff;
                numPhi0.Value = modes.phi0;
                numScale.Value = modes.scale;

                chkManualAxisBottom.IsChecked = modes.ManualYAxisBottom;
                ndKP.Value = modes.kP;
                ndKI.Value = modes.kI;
                ndKD.Value = modes.kD;
                chkFollowPID.IsChecked = modes.PID_Enabled;
                if(modes.DoubleStrobe) rbDouble.IsChecked = true;
                else rbSingle.IsChecked = true;
            }
        }

        private void SaveDefaultModes(bool Middle = true, bool Bottom = true)
        {
            if (Middle)
            {
                modes.ManualYAxisMiddle = chkManualAxisMiddle.IsChecked.Value;
                modes.Background = chkBackgroung.IsChecked.Value;
                modes.DarkCurrent = chkDarkcurrent.IsChecked.Value;
                modes.StdDev = chkStdDev.IsChecked.Value;
                modes.N1 = chkN1.IsChecked.Value;
                modes.N2 = chkN2.IsChecked.Value;
                modes.RN1 = chkRN1.IsChecked.Value;
                modes.RN2 = chkRN2.IsChecked.Value;
                modes.Ntot = chkNtot.IsChecked.Value;

                modes.RsltUpdating = chkBigCalcUpdate.IsChecked.Value;
                modes.RsltDetails = chkDetail.IsChecked.Value;
                modes.SignalLogFile = chkLogFile.IsChecked.Value; 
            }
            if (Bottom)
            {
                modes.JumboFrom = numFrom.Value;
                modes.JumboTo = numTo.Value;
                modes.JumboBy = numBy.Value;
                modes.JumboCycles = (int)numCycles.Value;

                modes.MemsEnabled = chkMemsEnabled.IsChecked.Value;
                modes.MemsStart = numMemsStart.Value;
                modes.MemsLen = numMemsLen.Value;

                modes.Kcoeff = numKcoeff.Value;
                modes.phi0 = numPhi0.Value;
                modes.scale = numScale.Value;

                modes.ManualYAxisBottom = chkManualAxisBottom.IsChecked.Value;
                modes.kP = ndKP.Value;
                modes.kI = ndKI.Value;
                modes.kD = ndKD.Value;
                modes.PID_Enabled = chkFollowPID.IsChecked.Value;
                modes.DoubleStrobe = rbDouble.IsChecked .Value;
            }
            modes.Save();
        }

        private void frmAxelHub_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            logger.Enabled = false;
            ucScan1.UpdateModes();
            //visuals
            if (Options.genOptions.saveVisuals)
            {
                modes.Left = Left;
                modes.Top = Top;
                modes.Width = Width;
                modes.Height = Height;
                modes.TopFrame = rowUpperChart.Height.Value / Height;
                modes.MiddleFrame = rowMiddleChart.Height.Value / Height;
            }
 
            if (Options.genOptions.saveModes.Equals(GeneralOptions.SaveModes.ask))
            {
                //Save the currently open sequence to a default location
                MessageBoxResult result = MessageBox.Show("Axel-hub is closing. \nDo you want to save the modes? ...or cancel closing?", "    Save Defaults", MessageBoxButton.YesNoCancel);
                if (result == MessageBoxResult.Yes)
                {
                    SaveDefaultModes();
                    //SaveSequence_Click(sender, null);
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    //List<SequenceStep> steps = sequenceControl.sequenceDataGrid.ItemsSource.Cast<SequenceStep>().ToList();
                    e.Cancel = true;
                }
            }
            if (Options.genOptions.saveModes.Equals(GeneralOptions.SaveModes.save)) SaveDefaultModes();
            Options.genOptions.Save();
            Options.Close();
        }
        #endregion 

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

        private void btnSinFit_Click(object sender, RoutedEventArgs e)
        {
            if(srsFringes.Count == 0) 
            {
                Utils.errorMessage("No data points to fit over."); return;
            }
            double[] xs = srsFringes.pointXs(); double[] ys = srsFringes.pointYs(); 
            double[] coeffs = new double[4];
            // signal(x) = scale[0] * sin(per[1]*x + phi0[2]) + offset[3] -> idx in coeffs
            coeffs[0] = (ys.Max() - ys.Min()) / 2; // scale
            coeffs[1] = 1; // period
            coeffs[2] = numPhi0.Value; // phi0 
            coeffs[3] = ys.Average(); // Offset.Y
            double meanSquaredError;
            ModelFunctionCallback callback = new ModelFunctionCallback(ModelFunction);
            double[] fittedData = CurveFit.NonLinearFit(xs, ys, callback, coeffs, out meanSquaredError, 100);
            coeffs[2] = MMDataConverter.Restrict2twoPI(coeffs[2]);
            DataStack fit = new DataStack(DataStack.maxDepth);
            fit.importFromArrays(xs, fittedData);
            graphFringes.Data[1] = fit;

            log("FIT.meanSqError = " + meanSquaredError.ToString("G5"), Brushes.DarkCyan.Color);
            log("Phase0 = " + coeffs[2].ToString("G5"), Brushes.DarkCyan.Color);
            log("Scale = "+coeffs[0].ToString("G5"), Brushes.DarkCyan.Color);
            log("Period = " + coeffs[1].ToString("G5"), Brushes.DarkCyan.Color);
            log("Offset = " + coeffs[3].ToString("G5"), Brushes.DarkCyan.Color);

            crsStrobe2.AxisValue = coeffs[2];
            crsStrobe1.AxisValue = MMDataConverter.Restrict2twoPI(coeffs[2]+Math.PI);
            //numScale.Value = coeffs[0]; numPhi0.Value = coeffs[2]; 
        }
        // Callback function that implements the fitting model 
        private double ModelFunction(double x, double[] coefficients)
        {
            return (coefficients[0] * Math.Sin(x / coefficients[1] + coefficients[2])) + coefficients[3];
        }

        private bool OnShowReceive(string message)
        {
            try
            {
                bool back = true;
                //RemoteEvent(message);
                return back;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return false;
            }
        }

        private void OnShowActiveComm(bool active, bool forced)
        {
            ledAxelShow.Value = active && remoteShow.Enabled;
        }

        private void chkAxelShow_Checked(object sender, RoutedEventArgs e)
        {
            remoteShow.Enabled = chkAxelShow.IsChecked.Value;
            remoteShow.CheckConnection(true);         
        }
        protected void OnShowAsyncSend(bool OK, string json2send)
        {
            //if (!OK) LogEvent("Error sending -> " + json2send, Brushes.Red.Color);
            //else LogEvent("sending OK -> " + json2send);
        }

        private void frmAxelHub_Loaded(object sender, RoutedEventArgs e)
        {
            remoteShow = new RemoteMessaging("Axel Show", 667);
            remoteShow.Enabled = false;
            remoteShow.OnReceive += new RemoteMessaging.ReceiveHandler(OnShowReceive);
            remoteShow.OnActiveComm += new RemoteMessaging.ActiveCommHandler(OnShowActiveComm);
            remoteShow.OnAsyncSent += new RemoteMessaging.AsyncSentHandler(OnShowAsyncSend);
        }

        DispatcherTimer dTimer; Timer timer; Random random = new Random(); AutoFileLogger showLogger;
        private void btnTestAxelShow_Click(object sender, RoutedEventArgs e)
        {
            btnTestAxelShow.Value = !btnTestAxelShow.Value;
            showLogger.Enabled = debugMode && btnTestAxelShow.Value;

            if (btnTestAxelShow.Value) timer = new Timer(Timer_Tick, sender, 1, 2000);           
            else timer = null;           
        }

        private void Timer_Tick(object sender)
        {
            Dictionary<string, double> dt = new Dictionary<string, double>();
            dt["MEMS"] = 0.1 + 0.1*random.NextDouble();
            dt["MEMS2"] = 0.3 + 0.1 * random.NextDouble();
            dt["PhiMg"] = 0.5 + 0.1 * random.NextDouble();
            dt["Accel"] = 0.7 + 0.1 * random.NextDouble();
            export2Show(dt);
            // Forcing the CommandManager to raise the RequerySuggested event
            CommandManager.InvalidateRequerySuggested();
        }
        private void dTimer_Tick(object sender, EventArgs e)
        {
            if (!remoteShow.Connected) return;
            Dictionary<string, double> dt = new Dictionary<string, double>();
            int bs = ucScan1.GetBufferSize(); int len = AxelChart1.Waveform.Count;
            double mn, sd;
            if (AxelChart1.Waveform.statsByIdx(len - bs, len - 1, false, out mn, out sd))
                dt["MEMS"] = AxelChart1.convertV2mg(mn);
            if (!Utils.isNull(AxelChart1.MEMS2))
            {
                len = AxelChart1.MEMS2.Count;
                if (AxelChart1.MEMS2.statsByIdx(len - bs, len - 1, false, out mn, out sd))
                    dt["MEMS2"] = AxelChart1.convertV2mg(mn,true);
            }
            if(phiMg.Count > 0) 
            {
                dt["PhiMg"] = phiMg.pointYs().Average();
                phiMg.Clear();
            }
            if (accelMg.Count > 0)
            {
                dt["Accel"] = accelMg.pointYs().Average();
                accelMg.Clear();
            }                 
            export2Show(dt);
            // Forcing the CommandManager to raise the RequerySuggested event
            CommandManager.InvalidateRequerySuggested();
        }

        private void SaveTrend(string FileName)  // srsMems, srsMotAccel, srsAccel, srsCorr
        {
            if (srsMems.Count == 0 || srsMotAccel.Count == 0)
            {
                Utils.TimedMessageBox("Error: No trend data to be saved !", "ERROR", 2500);
                return;
            }
            System.IO.StreamWriter file = new System.IO.StreamWriter(FileName);
            file.WriteLine("#Time\tMEMS\tMOTaccel\tAccel\tCorr"); string ss;
            for (int i = 0; i < srsMems.Count; i++)
            {
                ss = srsMems[i].X.ToString(Options.genOptions.SaveFilePrec) + "\t" + srsMems[i].Y.ToString(Options.genOptions.SaveFilePrec) + "\t" +
                    srsMotAccel[i].Y.ToString(Options.genOptions.SaveFilePrec) + "\t" + srsAccel[i].Y.ToString(Options.genOptions.SaveFilePrec);
                if (!Utils.isNull(srsCorr))
                {
                    if (srsCorr.Count > i) ss += "\t" + srsCorr[i].Y.ToString(Options.genOptions.SaveFilePrec);
                    else ss += "\t0.0";
                }
                file.WriteLine(ss);
            }
            file.Close();
            log("Saved> " + FileName);
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
     } 
}