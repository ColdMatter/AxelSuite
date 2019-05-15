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
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

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
using System.Windows.Markup;

using UtilsNS;
using RemoteMessagingNS;
using OptionsNS;
using AxelHMemsNS;

//
// command line arguments (space separated): -remote:<partner> -hw:<config.file>
// where <partner> is remote partner name (title); hw(hardware), <confog.file>.hw is in Config folder   
//

namespace Axel_hub
{
    public delegate void StartDelegate();
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Modes modes;
        scanClass ucScan;
        AxelAxesClass axes;
        private int nSamples = 1500;
        const bool debugMode = true;
        
        
        List<Point> quantList = new List<Point>();
        ShotList shotList; // arch - on
        List<string> errList = new List<string>();

        Random rnd = new Random();
        private const int dataLength = 10000;
        private List<Point> _fringePoints = new List<Point>();

        RemoteMessaging remoteShow;

        OptionsWindow Options; ScanModes scanModes = null;

        public MainWindow()
        {
            InitializeComponent();
            
            ucScan = new scanClass();
            gridLeft.Children.Add(ucScan);
            ucScan.Height = 266;
            ucScan.VerticalAlignment = System.Windows.VerticalAlignment.Top; ucScan.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
            ucScan.OnStart += new scanClass.StartHandler(DoStart);            
            ucScan.OnLog += new scanClass.LogHandler(log);            
           //if (axelMems.hw.ContainsKey("filename")) AxelChart1.SetHWfile(axelMems.hw["filename"]);
            //else AxelChart1.SetHWfile("");

            //iStack = new List<double>(); dStack = new List<double>();
            Options = new OptionsWindow();  
            OpenDefaultModes();
            ucScan.InitOptions(ref Options.genOptions, ref scanModes);
            axes = new AxelAxesClass(ref Options.genOptions, ref ucScan);
            axes.OnLog += new AxelAxesClass.LogHandler(log);
            axes.AddAxis(ref X_AxelAxis,"X"); axes.AddAxis(ref Y_AxelAxis,"Y");
            axes[0].OnLog += new AxelAxisClass.LogHandler(log); axes[1].OnLog += new AxelAxisClass.LogHandler(log);
            ucScan.OnRemote += new scanClass.RemoteHandler(axes.DoRemote);            
                        
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
                Left = scanModes.Left;
                Top = scanModes.Top;
                Width = scanModes.Width;
                Height = scanModes.Height;
            }
            setAxesLayout(Options.genOptions.AxesChannels);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Maximized;
        }

        private void log(string txt, Color? clr = null)
        {
            if (!chkLog.IsChecked.Value) return;
            string printOut;
            if ((chkVerbatim.IsChecked.Value) || (txt.Length < 81)) printOut = txt;
            else printOut = txt.Substring(0, 80) + "..."; //

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

      /*   private void jumboRepeat(int cycles, double downStrobe, double upStrobe)
        {
            Clear(); // visual stuff
            quantList.Clear(); // [time,MOTaccel] list of pairs
            errList.Clear(); // accumulates errors
            shotList = new ShotList(chkJoinLog.IsChecked.Value); 
            //setConditions(ref shotList.conditions); !!!

            tabLowPlots.SelectedIndex = 1;
            // set jumbo-repeat conditions & format MMexec
            lastGrpExe.cmd = "repeat";
            lastGrpExe.id = rnd.Next(int.MaxValue);
            lastGrpExe.prms.Clear();
            lastGrpExe.prms["groupID"] = DateTime.Now.ToString("yy-MM-dd_H-mm-ss");
            lastGrpExe.prms["cycles"] = cycles;
            if (chkFollowPID.IsChecked.Value) { lastGrpExe.prms["follow"] = 1; }
            else { lastGrpExe.prms["follow"] = 0; }
            lastGrpExe.prms["downStrobe"] = downStrobe;            
            lastGrpExe.prms["upStrobe"] = upStrobe;
            if (rbSingle.IsChecked.Value)
            {
                lastGrpExe.prms["strobes"] = 1;
                strbDownhill.X = downStrobe;
            }
            else
            {
                lastGrpExe.prms["strobes"] = 2;
                strbDownhill.X = downStrobe; strbUphill.X = upStrobe;
            }
            string jsonR = JsonConvert.SerializeObject(lastGrpExe);
            log("<< " + jsonR, Brushes.Blue.Color);
            ucScan.remoteMode = RemoteMode.Jumbo_Repeat;            
            // set ADC24 and corr. visuals
            if (jumboADCFlag && chkMemsEnabled.IsChecked.Value)
            {
                if (chkFollowPID.IsChecked.Value) ucScan.SetActivity("Data acquis. with PID feedback");
                else ucScan.SetActivity("Data acquis. (no PID feedback)");
                ucScan.Running = true;
                AxelChart1.Waveform.TimeSeriesMode = true;
                plotcursorAccel.Visibility = System.Windows.Visibility.Collapsed;

                axelMems.Reset(); timeStack.Clear();
                if (probeMode) AxelChart1.SetInfo("Axel-Probe feeds the MEMS");
                else startADC24(true, ucScan.GetSamplingPeriod(), ucScan.GetBufferSize());
                Thread.Sleep(1000); Utils.DoEvents();
            }
            ucScan.SendJson(jsonR);
        }*/
        // the main call in simple mode
        public void DoStart(bool jumbo, bool down, double period, int sizeLimit)
        {
            if (jumbo)
            {
              /*  if (!down) // user jumbo cancel
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
                    ucScan.remoteMode = RemoteMode.Jumbo_Scan;
                    ucScan.SendJson(json);

                    if (ucScan.remoteMode == RemoteMode.Ready_To_Remote) return; // end mission
                }
                else
                {
                    Utils.TimedMessageBox("Open a fringe file, adjust the strobes and confirm.", "Jumbo-Repeat Requirements", 3500);
                    if (Utils.isNull(srsFringes)) srsFringes = new DataStack();
                    else srsFringes.Clear();
                    if (probeMode)
                    {
                        GroupBox gb = null; tabLowPlots.SelectedIndex = 0;
                        srsFringes.OpenPair(Utils.configPath+"fringes.ahf", ref gb);
                        graphFringes.DataSource = srsFringes;
                        lbInfoFringes.Content = srsFringes.rem;
                        tbRemFringes.Text = srsFringes.rem;
                        crsDownStrobe.AxisValue = 1.6; crsUpStrobe.AxisValue = 4.8;
                    }
                    // else btnOpenFringes_Click(null, null); MOVE to ucScan
                    if (srsFringes.Count == 0)
                    {
                        Utils.TimedMessageBox("No fringes for Jumbo-repeat", "Error", 5000);
                        ucScan.Running = false;
                        return;
                    }
                    btnConfirmStrobes.Visibility = System.Windows.Visibility.Visible;
                    btnSinFit.Visibility = System.Windows.Visibility.Visible;
                } */
            }
            else
            {
                if (down) axes.Clear(true, false, false);
                int buffSize = 200;
                if (sizeLimit > -1) buffSize = sizeLimit;
                axes.startADC(down, period, buffSize);
            } 
        }

  /*    */

        private MMexec lastGrpExe; //
        private Point strbDownhill = new Point(); private Point strbUphill = new Point();
        private double phaseCorr, phaseRad, fringesYmin = 10, fringesYmax = -10, accelYmin = 10, accelYmax = -10;
        DataStack srsFringes = null; DataStack srsMotAccel = null; DataStack srsCorr = null; DataStack srsMems = null; DataStack srsAccel = null;
        

        // remote MM call
/*
        public Dictionary<string, double> Statistics(Dictionary<string, double> dt) // in MEMS [V]; PhiRad -> out - MEMS [mg], etc.
        {
            Dictionary<string, double> rslt = new Dictionary<string, double>(dt);
            if (!(dt.ContainsKey("MEMS_V") || dt.ContainsKey("MEMS")) || (!dt.ContainsKey("PhiRad"))) return rslt; // works only if both are present

            if (!dt.ContainsKey("K")) rslt["K"] = numKcoeff.Value;
            if (!dt.ContainsKey("Phi0")) rslt["Phi0"] = numPhi0.Value;
            if (!dt.ContainsKey("Scale")) rslt["Scale"] = numScale.Value;

            if (dt.ContainsKey("MEMS_V")) rslt["MEMS"] = AxelChart1.convertV2mg(dt["MEMS_V"]); // convert V to mg
            if (dt.ContainsKey("MEMS2_V")) rslt["MEMS2"] = AxelChart1.convertV2mg(dt["MEMS2_V"], true);

            rslt["PhiMg"] = (dt["PhiRad"] - rslt["Phi0"]) * rslt["K"];  // convert rad to mg
            //  rslt["PhiMg"] = (dt["PhiRad"] ) * rslt["K"];  // convert rad to mg
            phiMg.AddPoint(rslt["PhiMg"]);

            double ord = (rslt["MEMS"] - rslt["PhiMg"]) / (2 * Math.PI * numKcoeff.Value);
            rslt["Order"] = Math.Round(ord);
            rslt["OrdRes"] = ord - Math.Round(ord);
            rslt["Accel"] = 2 * Math.PI * numKcoeff.Value * rslt["Order"] + rslt["PhiMg"];
            accelMg.AddPoint(rslt["Accel"]);

            string ss = "";
            if ((tabSecPlots.SelectedIndex == 4) && chkBigCalcTblUpdate.IsChecked.Value)
            {
                if (rslt.ContainsKey("MEMS2")) ss = " / " + rslt["MEMS2"].ToString(Options.genOptions.SignalTablePrec);
                lbiMEMS.Content = "MEMS[mg] = " + rslt["MEMS"].ToString(Options.genOptions.SignalTablePrec) + ss;

                lbiPhiRad.Content = "Phi[rad] = " + rslt["PhiRad"].ToString(Options.genOptions.SignalTablePrec);
                lbiPhiMg.Content = "Phi[mg] = " + rslt["PhiMg"].ToString(Options.genOptions.SignalTablePrec);

                lbiOrder.Content = "Order = " + rslt["Order"].ToString(Options.genOptions.SignalTablePrec);
                lbiOrdRes.Content = "OrdRes[mg] = " + rslt["OrdRes"].ToString(Options.genOptions.SignalTablePrec);
                lbiAccel.Content = "Accel[mg] = " + rslt["Accel"].ToString(Options.genOptions.SignalTablePrec);
            }
            return rslt;
        }

        List<double> iStack, dStack;
        int iStDepth = 5; int dStDepth = 3;
        public double PID(double disbalance)
        {
            double pTerm = disbalance;
            iStack.Add(disbalance); while (iStack.Count > iStDepth) iStack.RemoveAt(0);
            double iTerm = iStack.Average();
            dStack.Add(disbalance); while (dStack.Count > dStDepth) dStack.RemoveAt(0);
            double dTerm = 0;
            for (int i = 0; i < dStack.Count - 1; i++)
            {
                dTerm += dStack[i + 1] - dStack[i];
            }
            dTerm /= Math.Max(dStack.Count - 1, 1);

            double cr = ndKP.Value * pTerm + ndKI.Value * iTerm + ndKD.Value * dTerm;
            log("PID> " + pTerm.ToString("G3") + "  " + iTerm.ToString("G3") + " " + dTerm.ToString("G3") +
                // PID X correction and Y value after the correction
                " corr " + cr.ToString("G4") + " for " + disbalance.ToString("G4"), Brushes.Navy.Color);
            return cr;
        }

        private void splitDown_MouseDoubleClick(object sender, MouseButtonEventArgs e) // !!! to AA
        {
            frmAxelHub.Top = 0;
            frmAxelHub.Height = SystemParameters.WorkArea.Height;
            frmAxelHub.Left = SystemParameters.WorkArea.Width * 0.3;
            frmAxelHub.Width = SystemParameters.WorkArea.Width * 0.7;
        }*/

        private void btnLogClear_Click(object sender, RoutedEventArgs e)
        {
            tbLog.Document.Blocks.Clear();
        }

        private void setAxesLayout(int mc)
        {
            tcAxes.SelectedIndex = 0; 
            switch (mc)
            {
                case 0: tiYAxis.Visibility = Visibility.Collapsed;
                    colRight.Width = new GridLength(0, GridUnitType.Pixel);
                    splitPanels.Visibility = Visibility.Collapsed;
                    break;
                case 1: tiYAxis.Visibility = Visibility.Visible;
                    colRight.Width = new GridLength(0, GridUnitType.Pixel);
                    splitPanels.Visibility = Visibility.Collapsed;
                    if (gridPrimary.Children.Contains(Y_AxelAxis)) break;
                    gridSecondary.Children.Remove(Y_AxelAxis);
                    gridPrimary.Children.Add(Y_AxelAxis);
                    break;
                case 2: tiYAxis.Visibility = Visibility.Collapsed;                    
                    colLeft.Width = new GridLength(300, GridUnitType.Star);
                    colRight.Width = new GridLength(300, GridUnitType.Star);
                    splitPanels.Visibility = Visibility.Visible;
                    if (gridSecondary.Children.Contains(Y_AxelAxis)) break;
                    gridPrimary.Children.Remove(Y_AxelAxis);
                    gridSecondary.Children.Add(Y_AxelAxis);
                    break;
            }
            axes.UpdateFromOptions();
        }
 
        private void imgMenu_MouseUp(object sender, MouseButtonEventArgs e)
        {
            //if (Utils.isNull(Options)) Options = new OptionsWindow();
            if (!Utils.isNull(sender))
            {
                int mc = Options.genOptions.AxesChannels;
                Options.ShowDialog();
                if (!Options.genOptions.AxesChannels.Equals(mc))
                {
                    setAxesLayout(Options.genOptions.AxesChannels);
                }
                else axes.UpdateFromOptions();
            }
        }

        #region close and modes

        private void OpenDefaultModes(bool Middle = true, bool Bottom = true)
        {
            if (File.Exists(Utils.configPath + "scanDefaults.cfg"))
            {
                string fileJson = File.ReadAllText(Utils.configPath + "scanDefaults.cfg");
                scanModes = JsonConvert.DeserializeObject<ScanModes>(fileJson);
            }
            else
                scanModes = new ScanModes();
        }

        private void frmAxelHub_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //logger.Enabled = false;
            axes.axelMems.StopAqcuisition(); Thread.Sleep(200);
            ucScan.UpdateModes();
            //visuals
            if (Options.genOptions.saveVisuals)
            {
                scanModes.Left = Left;
                scanModes.Top = Top;
                scanModes.Width = Width;
                scanModes.Height = Height;
            }

            if (Options.genOptions.saveModes.Equals(GeneralOptions.SaveModes.ask))
            {
                //Save the currently open sequence to a default location
                MessageBoxResult result = MessageBox.Show("Axel-hub is closing. \nDo you want to save the modes? ...or cancel closing?", "    Save Defaults", MessageBoxButton.YesNoCancel);
                if (result == MessageBoxResult.Yes)
                {
                    scanModes.Save(); axes.SaveDefaultModes();
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                }
            }
            if (Options.genOptions.saveModes.Equals(GeneralOptions.SaveModes.save))
            {
                scanModes.Save(); axes.SaveDefaultModes();
            }
            if (!Utils.isNull(Options)) Options.Close();
        }
        #endregion
    }
}