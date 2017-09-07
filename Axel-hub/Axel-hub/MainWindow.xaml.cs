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
        bool jumboRepeatFlag = true;
        bool jumboADC24Flag = false;

        scanClass ucScan1;
        int nSamples = 1500; 
        private AxelMems axelMems = null;
        Random rnd = new Random();
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

        private void ADC24(bool down, double period, bool TimeMode, double Limit)
        {
            if (!down) // user cancel
            {
                AxelChart1.Running = false;
                axelMems.StopAqcuisition();
                AxelChart1.Waveform.logger.Enabled = false;
                log("User ABORT !!!", Brushes.Red.Color);
                return;
            }

            AxelChart1.Waveform.TimeMode = TimeMode;
            if (TimeMode)
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
                    tabLowPlots.SelectedIndex = 1;
                    lastGrpExe.cmd = "repeat";
                    lastGrpExe.id = rnd.Next(int.MaxValue);
                    lastGrpExe.prms.Clear();
                    lastGrpExe.prms["groupID"] = DateTime.Now.ToString("yy-MM-dd_H-mm-ss");
                    lastGrpExe.prms["cycles"] = 100;
                    if (rbSingle.IsChecked.Value) lastGrpExe.prms["strobes"] = 1;
                    else lastGrpExe.prms["strobes"] = 2;
                
                    string jsonR = JsonConvert.SerializeObject(lastGrpExe);
                    log("<< " + jsonR, Brushes.Blue.Color);

                    ucScan1.remoteMode = RemoteMode.Jumbo_Repeat;
                    ucScan1.SendJson(jsonR);
                }
                if(jumboADC24Flag) ADC24(down, 0.001, false, 200);

                ucScan1.Abort(false); // reset
            }
            else
            {
                ADC24(down, period, TimeMode, Limit);
            }
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
        private double strbLeft = 0, strbRight = 0;
        ChartCollection<Point> srsFringes = null; ChartCollection<Point> srsMotAccel = null; ChartCollection<Point> srsCorr = null;
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
                        if (Utils.isNull(srsFringes)) srsFringes = new ChartCollection<Point>();
                        else srsFringes.Clear();
                        if (Utils.isNull(srsMotAccel)) srsMotAccel = new ChartCollection<Point>();
                        else srsMotAccel.Clear();
                        if (Utils.isNull(srsCorr)) srsCorr = new ChartCollection<Point>();
                        else srsCorr.Clear();

                        if (lastGrpExe.cmd.Equals("scan")) lbInfoFrng.Content = "groupID:" + lastScan.groupID + ";  Scanning: " + lastScan.sParam +
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
                    DataStack signalDataStack = new DataStack();
                    DataStack backgroundDataStack = new DataStack();

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
                    Point[] pA, pB;
                    pA = signalDataStack.ToArray();
                    pB = backgroundDataStack.ToArray();
                    graphSignal.DataSource = new List<Point[]>() {pA, pB};
                    double A = 1 - 2 * (N2 - B2) / (NTot - BTot), corr, debalance;
                    if (lastGrpExe.cmd.Equals("scan")) // title command
                    {
                        srsFringes.Append(new Point((lastScan.sFrom + runID * lastScan.sBy), asymmetry));
                        graphFringes.DataSource = srsFringes;
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

                        srsMotAccel.Append(new Point(runID, debalance));
                        srsCorr.Append(new Point(runID, corr));
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
        public bool Open(string fn)
        {
            if (!File.Exists(fn)) throw new Exception("File <" + fn + "> does not exist.");
            AxelChart1.Open(fn);
            AxelChart1.Refresh();

            int ext = 0; tbRem.Text = "";
            foreach (string line in File.ReadLines(fn))
            {
                if (line.Contains("#Rem="))
                {
                    tbRem.Text = line.Substring(5);                    
                }
            }
            if (ext < 1) MessageBox.Show("Some internal extensions are missing in <" + fn + ">.");

            log("Open> " + fn);
            return (ext == 1);
        }

        private void btnOpen_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.FileName = ""; // Default file name
            dlg.DefaultExt = ".abf"; // Default file extension
            dlg.Filter = "Axel Boss File (.abf)|*.abf"; // Filter files by extension

            // Show save file dialog box
            Nullable<bool> result = dlg.ShowDialog();

            // Process save file dialog box results
            if (result == true) Open(dlg.FileName);
        }

        public void Save(string fn) 
        {
            System.IO.StreamWriter file = new System.IO.StreamWriter(fn);
            if (AxelChart1.remoteArg == string.Empty) throw new Exception("No remote arguments in upper chart");
            file.WriteLine("#" + AxelChart1.remoteArg);
            
            if (!String.IsNullOrEmpty(tbRem.Text)) file.WriteLine("#Rem=" + tbRem.Text);
            for (int i = 0; i < AxelChart1.Waveform.Count; i++)
                file.WriteLine(AxelChart1.Waveform[i].X.ToString() + "\t" + AxelChart1.Waveform[i].Y.ToString());
            file.Close();
            log("Save> " + fn);
        }

        private void btnSaveAs_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.FileName = ""; // Default file name
            dlg.DefaultExt = ".abf"; // Default file extension
            dlg.Filter = "Axel Boss File (.abf)|*.abf"; // Filter files by extension

            // Show save file dialog box
            Nullable<bool> result = dlg.ShowDialog();
            if (result == true) Save(dlg.FileName);
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            tbRem.Text = "";
            AxelChart1.Clear();
            AxelChart1.Refresh();
        }
        #endregion

        private void splitDown_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            frmAxelHub.Top = 0;
            frmAxelHub.Height = SystemParameters.WorkArea.Height;  
            frmAxelHub.Left = SystemParameters.WorkArea.Width * 0.3;
            frmAxelHub.Width = SystemParameters.WorkArea.Width * 0.7;
            tabSecPlots.SelectedIndex = 0;
        }

        private void tabSecPlots_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            double d = ActualHeight / 2 - 6; 
            if (d < 25) return;
            if ((tabSecPlots.SelectedIndex == 0) || (Utils.isNull(sender)))
            {
                rowUpperChart.Height = new GridLength(d, GridUnitType.Star);
                rowMiddleChart.Height = new GridLength(30, GridUnitType.Star);
                rowLowerChart.Height = new GridLength(d, GridUnitType.Star);
            }
            else
            {
                int mh = 150;
                rowUpperChart.Height = new GridLength(d-mh/2, GridUnitType.Star);
                rowMiddleChart.Height = new GridLength(mh, GridUnitType.Star);
                rowLowerChart.Height = new GridLength(d-mh/2, GridUnitType.Star);                
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

        private void rbSingle_Checked(object sender, RoutedEventArgs e)
        {
          /*  if ((crsFringes2 == null) || (crsFringes1 == null)) return;
            if (rbSingle.IsChecked.Value)
            {
                crsFringes2.Visibility = System.Windows.Visibility.Hidden;
                crsFringes1.AxisValue = 7.85;
            }
            else
            {
                crsFringes2.Visibility = System.Windows.Visibility.Visible;
                crsFringes1.AxisValue = 4.71;
                crsFringes1.AxisValue = 7.85;
                leftLvl = double.NaN; rightLvl = double.NaN;
            }*/
        }
    }
}