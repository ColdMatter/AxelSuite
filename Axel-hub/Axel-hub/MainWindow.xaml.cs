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
        bool jumboRepeatFlag = false;
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
        public MainWindow()
        {
            InitializeComponent();
            tabSecPlots.SelectedIndex = 1;
            ucScan1 = new scanClass();
            gridLeft.Children.Add(ucScan1);
            ucScan1.Height = 266; ucScan1.VerticalAlignment = System.Windows.VerticalAlignment.Top; 

            ucScan1.Start += new scanClass.StartHandler(DoStart);
            ucScan1.Remote += new scanClass.RemoteHandler(DoRemote);
            ucScan1.FileRef += new scanClass.FileRefHandler(DoRefFile);
            AxelChart1.Waveform.TimeSeriesMode = false;

            axelMems = new AxelMems();
            axelMems.Acquire += new AxelMems.AcquireHandler(DoAcquire);
            axelMems.RealSampling += new AxelMems.RealSamplingHandler(ucScan1.OnRealSampling);

            iStack = new List<double>(); dStack = new List<double>();
        }

        private void log(string txt, Color? clr = null)
        {
            if (!chkLog.IsChecked.Value) return;
            string printOut;
            if ((chkVerbatim.IsChecked.Value) || (txt.Length<81)) printOut = txt;
            else printOut = txt.Substring(0,80)+"..."; //
            
            Color ForeColor = clr.GetValueOrDefault(Brushes.Black.Color);
            TextRange rangeOfText1 = new TextRange(tbLog.Document.ContentEnd, tbLog.Document.ContentEnd);
            rangeOfText1.Text = printOut + "\n";
            rangeOfText1.ApplyPropertyValue(TextElement.ForegroundProperty, new System.Windows.Media.SolidColorBrush(ForeColor));
            tbLog.ScrollToEnd();

        }

        public void DoEvents()
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

        private void ADC24(bool down, double period, bool TimeLimitMode, double Limit)
        {
            if (!down) // user cancel
            {
                AxelChart1.Running = false;
                axelMems.StopAqcuisition();
                AxelChart1.Waveform.logger.Enabled = false;
                log("User ABORT !!!", Brushes.Red.Color);
                return;
            }

            AxelChart1.Waveform.TimeLimitMode = TimeLimitMode;
            if (TimeLimitMode)
            {
                AxelChart1.Waveform.TimeLimit = Limit;
                nSamples = (int)(Limit / period);
            }
            else
            {
                AxelChart1.Waveform.SizeLimit = (int)Limit;
                //AxelChart1.Waveform.StackMode = true;
                nSamples = AxelChart1.Waveform.SizeLimit;
            }
            AxelChart1.SamplingPeriod = period;
            AxelChart1.Running = true;
            AxelChart1.Clear();
            AxelChart1.remoteArg = "freq: " + (1 / period).ToString("G6") + ", aqcPnt: " + nSamples.ToString();
            AxelChart1.Waveform.SizeLimit = nSamples;
            AxelChart1.Waveform.logger.Enabled = false;
            
            axelMems.StartAqcuisition(nSamples, 1 / period); // sync acquisition
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
        public void DoStart(bool jumbo, bool down, double period, bool TimeMode, double Limit)
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
               
                if(jumboScanFlag) 
                {
                    Clear();
                    tabLowPlots.SelectedIndex = 0;
                    lastGrpExe.cmd = "scan";
                    lastGrpExe.id = rnd.Next(int.MaxValue);
                    lastScan = jumboScan();
                    lastScan.ToDictionary(ref lastGrpExe.prms);

                    string json = JsonConvert.SerializeObject(lastGrpExe);
                    log("<< "+json, Brushes.Green.Color);
                    ucScan1.remoteMode = RemoteMode.Jumbo_Scan;
                    ucScan1.SendJson(json);
                    if (ucScan1.remoteMode == RemoteMode.Free) return;  // abort mission
                }
                if(jumboRepeatFlag) 
                {
                    btnConfirmStrobes.Visibility = System.Windows.Visibility.Visible;
                    Utils.TimedMessageBox("Please adjust the strobes and confirm to continue.", "Information", 2500);
                }
            }
            else
            {
                Clear(true, false, false);
                ADC24(down, period, TimeMode, Limit);
            }
        }

        private void btnConfirmStrobes_Click(object sender, RoutedEventArgs e)
        {
            int cycles = 100;
            if (jumboScanFlag) jumboRepeat(cycles, (double)crsStrobe1.AxisValue, (double)crsStrobe2.AxisValue);
            else
            {
                rbSingle_Checked(null, null);
                if (rbSingle.IsChecked.Value) jumboRepeat(cycles, 7.8, -1);
                else jumboRepeat(cycles, 4.7, 7.8);
            }
      
            if(jumboADC24Flag) ADC24(true, 0.001, false, 200);

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

            DoEvents();
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
        DataStack srsFringes = null; DataStack srsMotAccel = null; DataStack srsCorr = null;
        DataStack signalDataStack = null;  DataStack backgroundDataStack = null;

        // remote MM call
        public void DoRemote(string json) // from TotalCount to 1
        {
            MMexec mme = JsonConvert.DeserializeObject<MMexec>(json);
            switch(mme.cmd)
            {
                case("shotData"):
                { 
                    log(json);
                    if (Convert.ToInt32(mme.prms["runID"]) == 0)
                    {
                        if (Utils.isNull(srsFringes)) srsFringes = new DataStack(true);                        
                        if (Utils.isNull(srsMotAccel)) srsMotAccel = new DataStack(true);
                        if (Utils.isNull(srsCorr)) srsCorr = new DataStack(true);
                        Clear();

                        if (lastGrpExe.cmd.Equals("scan")) lbInfoFringes.Content = "groupID:" + lastScan.groupID + ";  Scanning: " + lastScan.sParam +
                           ";  From: " + lastScan.sFrom.ToString("G4") + ";  To: " + lastScan.sTo.ToString("G4") + ";  By: " + lastScan.sBy.ToString("G4");
                        if (lastGrpExe.cmd.Equals("repeat")) lbInfoAccelTrend.Content = "groupID:" + lastGrpExe.prms["groupID"] + ";  Repeat: " + lastGrpExe.prms["cycles"] + " cycles";

                    }
                    string s1 = (string)lastGrpExe.prms["groupID"];
                    if (!s1.Equals((string)mme.prms["groupID"])) throw new Exception("Wrong groupID"); 
                    MOTMasterDataConverter.ConvertToDoubleArray(ref mme);

                    string endBit = ""; int runID = 0;
                    runID = Convert.ToInt32(mme.prms["runID"]);
                    if(lastGrpExe.cmd.Equals("scan")) endBit = ";  cur.value: "+(lastScan.sFrom+runID*lastScan.sBy).ToString("G4");

                    if (lastGrpExe.cmd.Equals("repeat")) endBit = ";  runID: " + runID.ToString();
                  
                    lbInfo.Content = "last group cmd: " + lastGrpExe.cmd + ";  groupID: " + lastGrpExe.prms["groupID"] +
                                     ";  runID: "+ mme.prms["runID"]+ endBit;
                    Dictionary<string, double> avgs = MOTMasterDataConverter.AverageShotSegments(mme);
                    lboxNB.Items.Clear();
                    foreach (var item in avgs)
                    {
                        lboxNB.Items.Add(string.Format("{0}: {1:F2}",item.Key,item.Value));
                    }
                    double asymmetry = MOTMasterDataConverter.Asymmetry(avgs);
                    signalDataStack = new DataStack();
                    backgroundDataStack = new DataStack();

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
                    graphSignal.Data[0] = signalDataStack;
                    graphSignal.Data[1] = backgroundDataStack;
                    // readjust Y axis
                    double mn = Math.Min(signalDataStack.pointYs().Min(), backgroundDataStack.pointYs().Min());
                    signalYmin = Math.Min(mn, signalYmin);
                    double mx = Math.Max(signalDataStack.pointYs().Max(), backgroundDataStack.pointYs().Max());
                    signalYmax = Math.Max(mx, signalYmax);
                    signalYaxis.Range = new Range<double>(signalYmin - 0.2, signalYmax + 0.2);

                    double A = 1 - 2 * (N2 - B2) / (NTot - BTot), corr, debalance;
                    // corrected with background
                    double cNtot = NTot - BTot; double cN2 = N2 - B2; double cN1 = cNtot - cN2;
                    stackN1.AddPoint(cN1); stackN2.AddPoint(cN2); stackNtot.AddPoint(cNtot);
                    stackRN1.AddPoint(cN1/cNtot); stackRN2.AddPoint(cN2/cNtot); 
                    graphNs.Data[0] = stackN1; graphNs.Data[1] = stackN2; 
                    graphNs.Data[2] = stackRN1; graphNs.Data[3] = stackRN2; graphNs.Data[4] = stackNtot;

                    if (lastGrpExe.cmd.Equals("scan")) // title command
                    {
                        srsFringes.Add(new Point((lastScan.sFrom + runID * lastScan.sBy), asymmetry));
                        graphFringes.Data[0] = srsFringes;
                    }
                    if (lastGrpExe.cmd.Equals("repeat")) // title command
                    {
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
                        corr = PID(debalance);
                        if (ucScan1.remoteMode == RemoteMode.Jumbo_Repeat)
                        {
                            mme.sender = "Axel-hub";
                            mme.cmd = "phaseConvert";
                            mme.prms.Clear();
                            mme.prms["runID"] = runID;
                            mme.prms["accelVoltage"] = corr.ToString("G6");

                            if(!ucScan1.SendJson(JsonConvert.SerializeObject(mme))) log("Error sending phaseConvert !!!", Brushes.Red.Color);   
                        }
                        srsMotAccel.Add(new Point(runID, debalance));
                        srsCorr.Add(new Point(runID, corr));
                        graphAccelTrend.Data[0] = srsMotAccel;
                        graphAccelTrend.Data[1] = srsCorr; //   new List<ChartCollection<Point>>() {, };
                    }
                    DoEvents();
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
            string[] ns; int i, j = 0; double d; 
            foreach (string line in File.ReadLines(fn))
            {
                if (line.Contains("#Rem="))
                {
                    tbRemSignal.Text = line.Substring(5); lbInfoSignal.Content = tbRemSignal.Text;
                }
                if (line[0] == '#') continue; //skip comments/service info
                ns = line.Split('\t');
                if (!int.TryParse(ns[0], out i)) throw new Exception("Wrong double at line " + j.ToString());
                if (!double.TryParse(ns[1], out d)) throw new Exception("Wrong double at line " + j.ToString());
                stackN1.Add(new Point(i,d));
                if (!double.TryParse(ns[2], out d)) throw new Exception("Wrong double at line " + j.ToString());
                stackN2.Add(new Point(i, d));
                if (!double.TryParse(ns[3], out d)) throw new Exception("Wrong double at line " + j.ToString());
                stackRN1.Add(new Point(i, d));
                if (!double.TryParse(ns[4], out d)) throw new Exception("Wrong double at line " + j.ToString());
                stackRN2.Add(new Point(i, d));
                if (!double.TryParse(ns[5], out d)) throw new Exception("Wrong double at line " + j.ToString());
                stackNtot.Add(new Point(i, d));                
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
            System.IO.StreamWriter file = new System.IO.StreamWriter(fn);
            if (!String.IsNullOrEmpty(tbRemSignal.Text)) file.WriteLine("#Rem=" + tbRemSignal.Text);
            file.WriteLine("index\tN1\tN2\tRN1\tRN2\tNTot\tXAxis"); 
            for (int i = 0; i < stackN1.Count; i++)
                file.WriteLine(i.ToString() + "\t" + stackN1[i].Y.ToString("G7") + "\t" + stackN2[i].Y.ToString("G7")
                    + "\t" + stackRN1[i].Y.ToString("G7") + "\t" + stackRN2[i].Y.ToString("G7") + "\t" + stackNtot[i].Y.ToString("G7") + "\t" + srsFringes[i].X.ToString("G7"));
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

        
        private void graphSignal_SourceUpdated(object sender, DataTransferEventArgs e)
        {
        }

        private void graphSignal_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
        }

        private void graphSignal_TargetUpdated(object sender, DataTransferEventArgs e)
        {
        }
    }
}