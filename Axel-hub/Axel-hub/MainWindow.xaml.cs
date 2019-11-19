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
            ucScan.OnRemoteMode += new scanClass.RemoteModeHandler(RemoteModeEvent);
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
            
            ucScan.OnRemote += new scanClass.RemoteHandler(axes.DoRemote);
            continueJumboRepeat(false); // init (hide) the pointy button
                        
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

        private void frmAxelHub_Loaded(object sender, RoutedEventArgs e)
        {
            ucScan.OnActiveRemote += new scanClass.ActiveRemoteHandler(OnActiveRemote);
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
            Utils.log(tbLog, txt, clr);
        }

        bool DoContinue = false;       
        public bool continueJumboRepeat(bool toggle)
        {
            if (toggle)
            {
                int k = 0;
                rowContinueJumbo.Height = new GridLength(30);
                while (!DoContinue && (k < 3000) && (rowContinueJumbo.Height.Value == 30)) // 5min
                {
                    Thread.Sleep(100); Utils.DoEvents(); k++;
                }
                if (k > 2990) log("Time out (5 min) !!!", Brushes.Red.Color);
                rowContinueJumbo.Height = new GridLength(0);
                DoContinue = false;
                return (k < 2990);
            }
            else
            {
                rowContinueJumbo.Height = new GridLength(0);
                return true;
            }
        }
        private void abtnContinueJumbo_Click(object sender, RoutedEventArgs e)
        {
            DoContinue = true;
        }

        public void RemoteModeEvent(RemoteMode oldMode, RemoteMode newMode)
        {
            if (oldMode.Equals(RemoteMode.Jumbo_Scan) && newMode.Equals(RemoteMode.Ready_To_Remote))
            {
                if (!Options.genOptions.JumboRepeat) return; 
                if (Utils.TheosComputer()) axes.SetChartStrobes(true);

                // wait for user confirmation
                if (!continueJumboRepeat(true)) return; // main call, out - if timeout
                axes.SetChartStrobes(false);
                int nc = (int)axes[0].numCycles.Value;
                axes.jumboRepeat(nc);
            }
        }

        public void DoStart(bool jumbo, bool down, double period, int sizeLimit)
        {
            if (jumbo)
            {
                axes.DoJumboScan(down);                                                             
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
            bool conn = Utils.isNull(ucScan.remote)? false: ucScan.remote.Connected;
            axes.UpdateFromOptions(conn);
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
                else axes.UpdateFromOptions(ucScan.remote.Connected);
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

        private void OnActiveRemote(bool activeComm)
        {
            axes.UpdateFromOptions(activeComm);
        }

        private void frmAxelHub_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {

            axes.Closing(sender,e);
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