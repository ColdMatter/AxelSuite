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
//using DS345NS;


namespace Axel_hub
{
    public delegate void StartDelegate();
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow: Window
    {
        bool jumboScanFlag = true;
        bool jumboRepeatFlag = true;
        bool jumboADC24Flag = false;

        scanClass ucScan1;
        int nSamples = 1500; 
        private AxelMems axelMems = null;
        Random rnd = new Random();
        DataStack stackN1 = new DataStack(true);
        DataStack stackN2 = new DataStack(true);
        DataStack stackRN1 = new DataStack(true);
        DataStack stackRN2 = new DataStack(true);
        DataStack stackNtot = new DataStack(true);
        private List<Point> _fringePoints = new List<Point>();

        OptionsWindow Options; 

        public MainWindow()
        {
            InitializeComponent();
            tabSecPlots.SelectedIndex = 1;
            ucScan1 = new scanClass();
            gridLeft.Children.Add(ucScan1);
            ucScan1.Height = 266; ucScan1.VerticalAlignment = System.Windows.VerticalAlignment.Top; 

            ucScan1.OnStart += new scanClass.StartHandler(DoStart);
            ucScan1.OnRemote += new scanClass.RemoteHandler(DoRemote);
            ucScan1.OnFileRef += new scanClass.FileRefHandler(DoRefFile);
            ucScan1.OnLog += new scanClass.LogHandler(log);
            
            AxelChart1.Waveform.TimeSeriesMode = false;

            axelMems = new AxelMems();
            axelMems.Acquire += new AxelMems.AcquireHandler(DoAcquire);
            axelMems.RealSampling += new AxelMems.RealSamplingHandler(ucScan1.OnRealSampling);

            iStack = new List<double>(); dStack = new List<double>();
            Options = new OptionsWindow();
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
            mms.sParam = "fringePhase"; // phase is default
            mms.sFrom = numFrom.Value;
            mms.sTo = numTo.Value;
            mms.sBy = numBy.Value;
            return mms;
        }

        private void ADC24(bool down, double period, int sizeLimit)
        {
            if (!down) // user cancel
            {
                AxelChart1.Running = false;
                axelMems.StopAqcuisition();
                AxelChart1.Waveform.logger.Enabled = false;
                log("User ABORT !!!", Brushes.Red.Color);
                return;
            }
            if (AxelChart1.Running) AxelChart1.Running = false;
            AxelChart1.Clear();
            AxelChart1.Waveform.StackMode = true;
            nSamples = sizeLimit;
            AxelChart1.SamplingPeriod = 1/axelMems.RealConvRate(1/period);
            AxelChart1.Running = true;
           
            AxelChart1.remoteArg = "freq: " + (1 / AxelChart1.SamplingPeriod).ToString("G6") + ", aqcPnt: " + nSamples.ToString();
            AxelChart1.Waveform.logger.Enabled = false;

            if (AxelChart1.Waveform.TimeSeriesMode) axelMems.TimingMode = AxelMems.TimingModes.byADCtimer;
            else axelMems.TimingMode = AxelMems.TimingModes.byNone;
            axelMems.StartAqcuisition(nSamples, 1 / AxelChart1.SamplingPeriod); // async acquisition
        }

        private void jumboRepeat(int cycles, double strobe1, double strobe2)
        {
            Clear();
            tabLowPlots.SelectedIndex = 1;
            lastGrpExe.cmd = "repeat";
            lastGrpExe.id = rnd.Next(int.MaxValue);
            lastGrpExe.prms.Clear();
            lastGrpExe.prms["groupID"] = DateTime.Now.ToString("yy-MM-dd_H-mm-ss");
            lastGrpExe.prms["cycles"] = cycles;
            lastGrpExe.prms["strobe1"] = strobe1;
            lastGrpExe.prms["strobe2"] = strobe2;
            if (rbSingle.IsChecked.Value) lastGrpExe.prms["strobes"] = 1;
            else lastGrpExe.prms["strobes"] = 2;

            string jsonR = JsonConvert.SerializeObject(lastGrpExe);
            log("<< " + jsonR, Brushes.Blue.Color);

            ucScan1.remoteMode = RemoteMode.Jumbo_Repeat;
            ucScan1.SendJson(jsonR);
        }
        
        // main ADC call
        public void DoStart(bool jumbo, bool down, double period, int sizeLimit)
        {
            if (jumbo)
            {
                if (!down) // user jumbo cancel
                {
                    AxelChart1.Running = false;
                    axelMems.StopAqcuisition();
                    AxelChart1.Waveform.logger.Enabled = false;
                    log("Jumbo END !", Brushes.Red.Color);
                    return;
                }
                lastGrpExe = new MMexec();
                lastGrpExe.mmexec = "test_drive";
                lastGrpExe.sender = "Axel-hub";

                if (jumboScanFlag)
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
                else btnConfirmStrobes_Click(null, null);
            }
            else
            {
                if(down) Clear(true, false, false);
                ADC24(down, period, sizeLimit);
            }
        }

        private void btnConfirmStrobes_Click(object sender, RoutedEventArgs e)
        {
            int cycles = (int)numCycles.Value;
            if (jumboScanFlag) jumboRepeat(cycles, (double)crsStrobe1.AxisValue, (double)crsStrobe2.AxisValue);
            else
            {   // no scan to pick from
                rbSingle_Checked(null, null);
                if (rbSingle.IsChecked.Value) jumboRepeat(cycles, 7.8, -1);
                else jumboRepeat(cycles, 4.7, 7.8);
            }
      
            if(jumboADC24Flag) ADC24(true, 0.001, 200);

            btnConfirmStrobes.Visibility = System.Windows.Visibility.Hidden;
            log("Jumbo succession is RUNNING !", Brushes.Green.Color);
        }

        public void DoAcquire(List<Point> dt, out bool next)
        {
            next = (ucScan1.EndlessMode() && ucScan1.Running);
            if (axelMems.activeChannel == 2) throw new Exception("not ready for two channels");
            /*{ 
                for (int i = 0; i<dt.Count; i++) 
                {
                    if((i%2) == 0) AxelChart1.Waveform.Add(dt[i]); // channel 0
                    else AxelChart1.Waveform.Add(dt[i]); // channel 1
                }
            }*/
            else AxelChart1.Waveform.AddRange(dt);
            //log(AxelChart1.Waveform.logger.bufferSize.ToString());

            if (!next)
            {
                AxelChart1.Running = false;
                ucScan1.Running = false;
                axelMems.StopAqcuisition(); 
                AxelChart1.Refresh();
            }
        }

        private MMexec lastGrpExe; private MMscan lastScan;
        private double strbLeft = 0, strbRight = 0, signalYmin = 10, signalYmax = 0;
        DataStack srsFringes = null; DataStack srsMotAccel = null; DataStack srsCorr = null; DataStack srsMems = null;
        DataStack signalDataStack = null;  DataStack backgroundDataStack = null;

        // remote MM call
        public void DoRemote(string json) // from TotalCount to 1
        {
            MMexec mme = JsonConvert.DeserializeObject<MMexec>(json);
            if (lastGrpExe == null) lastGrpExe = mme;
            switch(mme.cmd)
            {
                case("shotData"):
                { 
                    bool scanMode = lastGrpExe.cmd.Equals("scan");
                    bool repeatMode = lastGrpExe.cmd.Equals("repeat");
                    bool middleSection = (tabSecPlots.SelectedIndex != 0);
                    if (Convert.ToInt32(mme.prms["runID"]) == 0)
                    {
                        if (Utils.isNull(srsFringes)) srsFringes = new DataStack(true);                        
                        if (Utils.isNull(srsMotAccel)) srsMotAccel = new DataStack(true);
                        if (Utils.isNull(srsCorr)) srsCorr = new DataStack(true);
                        if (Utils.isNull(srsMems)) srsMems = new DataStack(true);
                        Clear(); 
                        if (scanMode) lbInfoFringes.Content = "groupID:" + lastScan.groupID + ";  Scanning: " + lastScan.sParam +
                           ";  From: " + lastScan.sFrom.ToString("G4") + ";  To: " + lastScan.sTo.ToString("G4") + ";  By: " + lastScan.sBy.ToString("G4");
                        if (repeatMode) lbInfoAccelTrend.Content = "groupID:" + lastGrpExe.prms["groupID"] + ";  Repeat: " + lastGrpExe.prms["cycles"] + " cycles";
                    }
                    string s1 = (string)lastGrpExe.prms["groupID"];
                    if (!s1.Equals((string)mme.prms["groupID"])) throw new Exception("Wrong groupID"); 
                    MOTMasterDataConverter.ConvertToDoubleArray(ref mme);

                    string endBit = ""; int runID = 0;
                    runID = Convert.ToInt32(mme.prms["runID"]);
                    if(scanMode) endBit = ";  scanX = " + (lastScan.sFrom+runID*lastScan.sBy).ToString("G4");
                    lbInfoSignal.Content = "cmd: " + lastGrpExe.cmd + ";  grpID: " + lastGrpExe.prms["groupID"] + ";  runID: "+ runID.ToString() + endBit;
                    if (chkVerbatim.IsChecked.Value) log("("+json.Length.ToString()+") "+json);
                    else 
                    {
                        string msg = ">SHOT #"+runID.ToString()+"; grpID: " + mme.prms["groupID"];
                        log(msg, Brushes.DarkGreen.Color);
                        if(scanMode) log(endBit, Brushes.DarkBlue.Color);
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
                    Dictionary<string, double> avgs = MOTMasterDataConverter.AverageShotSegments(mme);
                    if (middleSection)
                    {
                    lboxNB.Items.Clear();
                    foreach (var item in avgs)
                    {
                        lboxNB.Items.Add(string.Format("{0}: {1:F2}",item.Key,item.Value));
                    }
                    }
                    double asymmetry = MOTMasterDataConverter.Asymmetry(avgs, chkBackgroung.IsChecked.Value, chkDarkcurrent.IsChecked.Value);
                    if (Utils.isNull(signalDataStack)) signalDataStack = new DataStack();
                    else signalDataStack.Clear();
                    if (Utils.isNull(backgroundDataStack)) backgroundDataStack = new DataStack();
                    else backgroundDataStack.Clear();

                    int xVal = 0; double N2 = ((double[])mme.prms["N2"]).Average();
                    foreach (double yVal in (double[])mme.prms["N2"])
                    {
                        signalDataStack.Add(new Point(xVal, yVal));
                        xVal++;
                    }
                    double NTot = ((double[])mme.prms["NTot"]).Average();
                    foreach (double yVal in (double[])mme.prms["NTot"])
                    {
                        signalDataStack.Add(new Point(xVal, yVal));
                        xVal++;
                    }

                    xVal = 0; double B2 = ((double[])mme.prms["B2"]).Average();
                    foreach (double yVal in (double[])mme.prms["B2"])
                    {
                        backgroundDataStack.Add(new Point(xVal, yVal));
                        xVal++;
                    }
                    double BTot = ((double[])mme.prms["BTot"]).Average();
                    foreach (double yVal in (double[])mme.prms["BTot"])
                    {
                        backgroundDataStack.Add(new Point(xVal, yVal));
                        xVal++;
                    }
                    if (middleSection)
                    {
                    graphSignal.Data[0] = signalDataStack;
                    graphSignal.Data[1] = backgroundDataStack;
                    }
                    // readjust Y axis
                    if (!chkManualAxis.IsChecked.Value)
                    {
                        double d = Math.Min(signalDataStack.pointYs().Min(), backgroundDataStack.pointYs().Min());
                        d = Math.Floor(10 * d) / 10;
                        signalYmin = Math.Min(d, signalYmin);
                        d = Math.Max(signalDataStack.pointYs().Max(), backgroundDataStack.pointYs().Max());
                        d = Math.Ceiling(10 * d) / 10;
                        signalYmax = Math.Max(d, signalYmax);
                        signalYaxis.Range = new Range<double>(signalYmin - 0.2, signalYmax + 0.2);
                    }

                    double A = 1 - 2 * (N2 - B2) / (NTot - BTot), corr, debalance;
                    // corrected with background
                    double cNtot = NTot - BTot; double cN2 = N2 - B2; double cN1 = cNtot - cN2; 
                    double currX = 1;
                    if (scanMode)
                    {
                        currX = lastScan.sFrom + runID * lastScan.sBy;
                        if (middleSection)
                        { 
                            stackN1.Add(new Point(currX, cN1)); stackN2.Add(new Point(currX, cN2)); stackNtot.Add(new Point(currX, cNtot));
                            stackRN1.Add(new Point(currX, cN1 / cNtot)); stackRN2.Add(new Point(currX, cN2 / cNtot));
                        }
                    }
                    if (middleSection)
                    {
                    if (repeatMode)
                    {
                        stackN1.AddPoint(cN1); stackN2.AddPoint(cN2); stackNtot.AddPoint(cNtot);
                            stackRN1.AddPoint(cN1 / cNtot); stackRN2.AddPoint(cN2 / cNtot);
                    }
                    graphNs.Data[0] = stackN1; graphNs.Data[1] = stackN2; 
                    graphNs.Data[2] = stackRN1; graphNs.Data[3] = stackRN2; graphNs.Data[4] = stackNtot;
                    }
                    if (scanMode) 
                    {
                        srsFringes.Add(new Point(currX, asymmetry));
                        graphFringes.Data[0] = srsFringes;
                    }
                    if (repeatMode) 
                    {
                        if (ucScan1.remoteMode == RemoteMode.Jumbo_Repeat)
                        {
                            if (AxelChart1.Running && jumboADC24Flag)
                            {
                                double start = double.NaN, mn, dsp;
                                if (AxelChart1.Waveform.statsByTime(start, 0.2, out mn, out dsp))
                                {
                                    srsMems.Add(new Point(runID, mn));
                                    graphAccelTrend.Data[2] = srsMems; 
                                }                                   
                            }
                            if (rbSingle.IsChecked.Value)
                            {
                                debalance = asymmetry;
                            }
                            else // double strobe
                            {
                                if ((runID % 2) == 0) strbLeft = asymmetry;
                                else strbRight = asymmetry;
                                debalance = strbRight - strbLeft;
                                log("strbLeft: " + strbLeft.ToString("G3") + "; strbRight: " + strbRight.ToString("G3"));
                            }
                            if ((chkFollowPID.IsChecked.Value) && (ucScan1.remoteMode == RemoteMode.Jumbo_Repeat))
                            {
                                corr = PID(debalance);
                                mme.sender = "Axel-hub";
                                mme.cmd = "phaseAdjust";
                                mme.prms.Clear();
                                mme.prms["runID"] = runID;
                                mme.prms["phaseCorrection"] = corr.ToString("G6");
                                ucScan1.SendJson(JsonConvert.SerializeObject(mme), true); 
                                srsCorr.Add(new Point(runID, corr));
                                graphAccelTrend.Data[1] = srsCorr;
                            }
                       }
                       srsMotAccel.Add(new Point(runID, asymmetry));                       
                       graphAccelTrend.Data[0] = srsMotAccel;
                    }
                    if (scanMode && (ucScan1.remoteMode == RemoteMode.Jumbo_Scan) && jumboRepeatFlag)
                    {
                        if (mme.prms.ContainsKey("last"))
                        {
                            if (Convert.ToInt32(mme.prms["last"]) == 1)
                            {
                                btnConfirmStrobes.Visibility = System.Windows.Visibility.Visible;
                                Utils.TimedMessageBox("Please adjust the strobes and confirm to continue.", "Information", 2500);
                            }
                        }
                    }
                    if ((ucScan1.remoteMode == RemoteMode.Simple_Scan) || (ucScan1.remoteMode == RemoteMode.Simple_Repeat))
                    {
                        if (mme.prms.ContainsKey("last"))
                        {
                            if (Convert.ToInt32(mme.prms["last"]) == 1)
                            {
                                ucScan1.remoteMode = RemoteMode.Free;
                            }
                        }
                    }
                }            
                    break;
                case ("repeat"):
                    {
                        log(json, Brushes.Blue.Color);
                        lastGrpExe = mme.Clone();
                        if (!mme.sender.Equals("Axel-hub")) ucScan1.remoteMode = RemoteMode.Simple_Repeat;
                        tabLowPlots.SelectedIndex = 1;
                        chkN1_Checked(null, null);
                        Clear();
                    }
                    break;
                case ("scan"):
                    {
                        log(json, Brushes.DarkGreen.Color);                       
                        if (!lastScan.FromDictionary(mme.prms))
                        {
                            log("Error in incomming json", Brushes.Red.Color);
                            ucScan1.Abort(true);
                            return;
                        }
                        lastGrpExe = mme.Clone();
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
                        else log("! "+txt, Brushes.DarkOrange.Color);
                    }
                    break;
                case ("abort"):
                    {
                        log(json, Brushes.Red.Color);
                        if (ucScan1.remoteMode != RemoteMode.Free)
                        {
                            ucScan1.Abort(false);
                        }
                    }
                    break;
            }
        }

        List<double> iStack, dStack;
        int iStDepth = 5; int dStDepth = 3;
        public double PID(double curr)
        {
            int hillSide = 1;
            //if (cbHillPos.SelectedIndex == 0) hillSide = -1;
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

            double cr = hillSide * (ndKP.Value * pTerm + ndKI.Value * iTerm + ndKD.Value * dTerm);
            log("PID> " + pTerm.ToString("G3") + "  " + iTerm.ToString("G3") + " " + dTerm.ToString("G3") +
                // PID X correction and Y value after the correction
                " corr " + cr.ToString("G4") + " for " + curr.ToString("G4"), Brushes.Navy.Color);
            return cr;
        }

        // XPS log file reference .....
        public void DoRefFile(string FN, bool statFlag)
        {            
        }

        public void DoCompareChart()
        {
        }
        #region File operation 
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
            log("Open> " + fn);
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
                MessageBox.Show("Error: No data to be saved");
                return;
            }
            //TODO Change this to add mean and standard deviation values
            System.IO.StreamWriter file = new System.IO.StreamWriter(fn);
            if(!Utils.isNull(lastGrpExe)) file.WriteLine("#"+JsonConvert.SerializeObject(lastGrpExe));
            if (!String.IsNullOrEmpty(tbRemSignal.Text)) file.WriteLine("#Rem=" + tbRemSignal.Text);
            file.WriteLine("#XAxis\tN1\tN2\tRN1\tRN2\tNTot");  
            for (int i = 0; i < stackN1.Count; i++)
                file.WriteLine(stackN1[i].X.ToString("G7") + "\t" + stackN1[i].Y.ToString("G7") + "\t" + stackN2[i].Y.ToString("G7") + "\t" + stackRN1[i].Y.ToString("G7") + 
                                              "\t" + stackRN2[i].Y.ToString("G7") + "\t" + stackNtot[i].Y.ToString("G7"));
            file.Close();
            log("Save> " + fn);
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

        public void OpenPair(string fn, ref DataStack ds)
        {
            if (!File.Exists(fn)) throw new Exception("File <" + fn + "> does not exist.");
            if (Utils.isNull(ds)) ds = new DataStack(); ds.Clear();
            int j = 0;
            double X, Y;
            string[] pair;

            foreach (string line in File.ReadLines(fn))
            {
                if (line.Contains("#Rem="))
                {
                    ds.rem = line.Substring(6,255); 
                }
                if (line[0] == '#') continue; //skip comments/service info
                pair = line.Split('\t');
                if (!double.TryParse(pair[0], out X)) throw new Exception("Wrong double at line " + j.ToString());
                if (!double.TryParse(pair[1], out Y)) throw new Exception("Wrong double at line " + j.ToString());
                ds.Add(new Point(X, Y));
                j++;
            }
        }

        public void SavePair(string fn, DataStack ds)
        {
            System.IO.StreamWriter file = new System.IO.StreamWriter(fn);
            if (!ds.rem.Equals("")) file.WriteLine("#rem " + ds.rem);
            for (int i = 0; i < ds.Count; i++)
                file.WriteLine(ds[i].X.ToString("G5") + "\t" + ds[i].Y.ToString("G5"));
            file.Close();
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
            if (result == true) OpenPair(dlg.FileName, ref srsFringes);
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
            if (result == true) SavePair(dlg.FileName, srsFringes);
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
                rowMiddleChart.Height = new GridLength(35);
            }
            else
            {
                if (rowMiddleChart.Height.Value < 38) rowMiddleChart.Height = new GridLength(Math.Min(230,middlePlotHeight), GridUnitType.Star);
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
                stackN1.Clear(); stackN2.Clear(); stackRN1.Clear(); stackRN2.Clear(); stackNtot.Clear();
                if (!Utils.isNull(signalDataStack)) signalDataStack.Clear();
                if (!Utils.isNull(backgroundDataStack)) backgroundDataStack.Clear();
                lboxNB.Items.Clear();
                signalYmin = 10; signalYmax = 0;
            }
            if (Bottom)
            {
                if (!Utils.isNull(srsFringes))
                {
                    srsFringes.Clear();
                    graphFringes.Data[0] = srsFringes;
                }
                    
                if (!Utils.isNull(srsMotAccel)) srsMotAccel.Clear();
                if (!Utils.isNull(srsCorr)) srsCorr.Clear();
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
            if (rowUpperChart.Height.Value < 5) rowUpperChart.Height = new GridLength(Math.Min(230, hiddenTopHeight), GridUnitType.Star);
            else
            {
                hiddenTopHeight = rowUpperChart.Height.Value;
                rowUpperChart.Height = new GridLength(3);
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
        }

        private void imgMenu_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if(Utils.isNull(Options)) Options = new OptionsWindow();
            Options.Show();
        }

        private void frmAxelHub_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Options.genOptions.Save();
            Options.Close();
            e.Cancel = false;
        }
     }
}