﻿//using NationalInstruments.Net;
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

// for simulation with Axel Probe put in command line  -remote:"Axel Probe"

namespace Axel_hub
{
    public delegate void StartDelegate();
    /// <summary>
    /// Interaction logic for MainWindow.xaml -> test 2
    /// command line arguments (space separated): -remote:<c>partner</c>  -hw:<c>config.file</c>
    /// where <c>partner</c> is remote partner name <c>title</c>; hw<c>hardware</c>, <c>config.file.hw</c> is in Config folder
    /// </summary>
    public partial class MainWindow : Window
    {
        scanClass ucScan;
        AxelAxesClass axes;
        const bool debugMode = true;       
        
        List<Point> quantList = new List<Point>();
        List<string> errList = new List<string>();

        Random rnd = new Random();
        private const int dataLength = 10000;
        private List<Point> _fringePoints = new List<Point>();

        OptionsWindow Options; ScanModes scanModes = null;
        /// <summary>
        /// Class constructor
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            Utils.extendedDataPath = true; // AH monthly data
            if (!Directory.Exists(Utils.dataPath)) Directory.CreateDirectory(Utils.dataPath);
            ucScan = new scanClass();
            gridLeft.Children.Add(ucScan);
            ucScan.Height = 285;
            ucScan.VerticalAlignment = System.Windows.VerticalAlignment.Top; ucScan.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
            ucScan.OnStart += new scanClass.StartHandler(DoStart);
            ucScan.OnRemoteMode += new scanClass.RemoteModeHandler(RemoteModeEvent);
            ucScan.OnLog += new scanClass.LogHandler(log);            

            Options = new OptionsWindow();  
            OpenDefaultModes();
            ucScan.InitOptions(ref Options.genOptions, ref scanModes);
            axes = new AxelAxesClass(ref Options.genOptions, ref ucScan);
            axes.OnLog += new AxelAxesClass.LogHandler(log);
            axes.AddAxis(ref X_AxelAxis,"X"); axes.AddAxis(ref Y_AxelAxis,"Y");
            
            ucScan.OnRemote += new scanClass.RemoteHandler(axes.DoRemote);
            continueJumboRepeat(false); // init (hide) the pointy button
                        
            if (false)//(System.Windows.Forms.SystemInformation.MonitorCount > 1) // secondary monitor (temp off)
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
        /// <summary>
        /// Hook up the main incomming data flow
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void frmAxelHub_Loaded(object sender, RoutedEventArgs e)
        {
            ucScan.OnActiveRemote += new scanClass.ActiveRemoteHandler(OnActiveRemote);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Maximized;
        }

        /// <summary>
        /// Log text in the textbox on the left
        /// </summary>
        /// <param name="txt">text to log</param>
        /// <param name="clr"></param>
        private void log(string txt, SolidColorBrush clr = null)
        {
            if (!chkLog.IsChecked.Value) return;
            string printOut;
            if ((chkVerbatim.IsChecked.Value) || (txt.Length < 81)) printOut = txt;
            else printOut = txt.Substring(0, 80) + "..."; //           
            Utils.log(tbLog, txt, clr);
        }
             
        /// <summary>
        /// Shows/Hide continue arrow button and wait 5 min for a click
        /// </summary>
        /// <param name="toggle">show/hide state</param>
        /// <returns>ok -> true; timeout -> false</returns>
        public bool continueJumboRepeat(bool toggle)
        {
            if (toggle) //axes[0].ShowcaseUC1.IsShowcaseShowing
            {               
                axes.SetChartStrobes(true);
                if (axes[0].ShowcaseUC1.IsShowcaseShowing) axes.ShowcaseReaction("Scan:end");
                int k = 0;
                rowContinueJumbo.Height = new GridLength(30);
                while (!axes.DoContinue && (k < 3000) && (rowContinueJumbo.Height.Value == 30) && !axes.closeRequest) // 5min
                {
                    Thread.Sleep(100); Utils.DoEvents(); k++;
                }
                if (k > 2990) log("Time out (5 min) !!!", Brushes.Red);
                rowContinueJumbo.Height = new GridLength(0);
                axes.DoContinue = false;
                return (k < 2990);
            }
            else
            {
                axes.SetChartStrobes(false);
                rowContinueJumbo.Height = new GridLength(0);
                return true;
            }
        }
        /// <summary>
        /// Make the method above to conclude
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void abtnContinueJumbo_Click(object sender, RoutedEventArgs e)
        {
            axes.DoContinue = true;
        }
        /// <summary>
        /// Some action when remode mode of ucScan has been changed
        /// </summary>
        /// <param name="oldMode"></param>
        /// <param name="newMode"></param>
        public void RemoteModeEvent(RemoteMode oldMode, RemoteMode newMode)
        {
            if (oldMode.Equals(RemoteMode.Jumbo_Scan) && newMode.Equals(RemoteMode.Ready_To_Remote))
            {
                if (!Options.genOptions.JumboRepeat) return; 
                //if (axes.probeMode) axes.SetChartStrobes(true); //Utils.TheosComputer()

                // wait for user confirmation
                if (!Options.genOptions.Diagnostics)
                {                    
                    if (!continueJumboRepeat(true)) return; // main call, out - if timeout
                    axes.SetChartStrobes(false);
                }
                axes.DoJumboRepeat(ucScan.bbtnStart.Value, axes[0].numCycles.Value);
                if (axes[0].ShowcaseUC1.IsShowcaseShowing)
                    axes[0].ShowcaseUC1.showcaseState = Showcase.ShowcaseClass.ShowcaseStates.idle;
            }
            if (oldMode.Equals(RemoteMode.Jumbo_Repeat) && newMode.Equals(RemoteMode.Ready_To_Remote))
            {
                if (axes[0].ShowcaseUC1.IsShowcaseShowing)
                    axes[0].ShowcaseUC1.showcaseState = Showcase.ShowcaseClass.ShowcaseStates.idle;
            }
        }

        /// <summary>
        /// Star/Stop jumbo or Mems only
        /// </summary>
        /// <param name="jumbo"></param>
        /// <param name="down">Star/Stop</param>
        /// <param name="period">sampling rate related</param>
        /// <param name="sizeLimit">data buffer length</param>
        public void DoStart(bool jumbo, bool down, double period, int sizeLimit)
        {
            if (jumbo)               
            {
                if (down) axes.SendMMexec(new MMexec("", "Axel-hub", "status"));
                axes.DoJumboScan(down); // get back fringes no matter scan or not
                if (Options.genOptions.JumboRepeat && !Options.genOptions.JumboScan)
                {
                    if (!Options.genOptions.Diagnostics && down)
                    {

                        if (!continueJumboRepeat(true)) return; // main call, out - if timeout
                        axes.SetChartStrobes(false);
                    }
                    axes.DoJumboRepeat(down, axes[0].numCycles.Value);
                }                                  
            }
            else
            {
                if (down) axes.Clear(true, false, false); // reset before start
                int buffSize = 200;
                if (sizeLimit > -1) buffSize = sizeLimit;
                axes.startADC(down, period, buffSize);
            } 
            if (!down) log("End of series!", Brushes.Red);
        } 
        private void splitDown_MouseDoubleClick(object sender, MouseButtonEventArgs e) // !!! to AA
        {
            frmAxelHub.Top = 0;
            frmAxelHub.Height = SystemParameters.WorkArea.Height;
            frmAxelHub.Left = SystemParameters.WorkArea.Width * 0.3;
            frmAxelHub.Width = SystemParameters.WorkArea.Width * 0.7;
        }
        private void btnLogClear_Click(object sender, RoutedEventArgs e)
        {
            tbLog.Document.Blocks.Clear();           
        }
        /// <summary>
        /// set the axes visual layout
        /// </summary>
        /// <param name="mc">which axis or both</param>
        private void setAxesLayout(int mc)
        {
            tcAxes.SelectedIndex = 0; 
            switch (mc)
            {
                case 0: // X 
                    if (Utils.isSingleChannelMachine) tiXAxis.Visibility = Visibility.Collapsed;
                    else tiXAxis.Visibility = Visibility.Visible;
                    tiYAxis.Visibility = Visibility.Collapsed;
                    colRight.Width = new GridLength(0, GridUnitType.Pixel);
                    splitPanels.Visibility = Visibility.Collapsed;
                    break;
                case 1: // Y 
                    tiYAxis.Visibility = Visibility.Visible;
                    colRight.Width = new GridLength(0, GridUnitType.Pixel);
                    splitPanels.Visibility = Visibility.Collapsed;
                    if (gridPrimary.Children.Contains(Y_AxelAxis)) break;
                    gridSecondary.Children.Remove(Y_AxelAxis);
                    gridPrimary.Children.Add(Y_AxelAxis);
                    break;
                case 2: // X/Y
                    tiYAxis.Visibility = Visibility.Collapsed;                    
                    colLeft.Width = new GridLength(300, GridUnitType.Star);
                    colRight.Width = new GridLength(300, GridUnitType.Star);
                    splitPanels.Visibility = Visibility.Visible;
                    if (gridSecondary.Children.Contains(Y_AxelAxis)) break;
                    gridPrimary.Children.Remove(Y_AxelAxis);
                    gridSecondary.Children.Add(Y_AxelAxis);
                    break;
            }
            axes.OnOptionsChange(Options.genOptions);
        }
         /// <summary>
         /// Open options dialog window
         /// </summary>
         /// <param name="sender"></param>
         /// <param name="e"></param>
        private void imgMenu_MouseUp(object sender, MouseButtonEventArgs e)
        {
            //if (Utils.isNull(Options)) Options = new OptionsWindow();
            if (ucScan.Running){
                Utils.TimedMessageBox("Options are disabled while a measurement is running"); return;
            }
            if (!Utils.isNull(sender))
            {
                int mc = Options.genOptions.AxesChannels;
                Options.ShowDialog();
                if (!Options.genOptions.AxesChannels.Equals(mc))
                {
                    setAxesLayout(Options.genOptions.AxesChannels);
                }
                else axes.OnOptionsChange(Options.genOptions);
            }
        }

        #region close and modes
        /// <summary>
        /// Configure scan from modes file (scanDefaults.cfg)
        /// or create scanMode from scratch
        /// </summary>
        private void OpenDefaultModes()
        {
            if (File.Exists(Utils.configPath + "scanDefaults.cfg"))
            {
                string fileJson = File.ReadAllText(Utils.configPath + "scanDefaults.cfg");
                scanModes = JsonConvert.DeserializeObject<ScanModes>(fileJson);
            }
            else
                scanModes = new ScanModes();
        }

        /// <summary>
        /// When something happens with the remote comm.
        /// </summary>
        /// <param name="activeComm"></param>
        private void OnActiveRemote(bool activeComm)
        {
            axes.OnOptionsChange(Options.genOptions);
        }

        /// <summary>
        /// closing the shop, with some optional savings
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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
            if (!Utils.isNull(Options))
            {
                Options.keepOpen = false; Options.Close();
            }
        }
        #endregion

        private void frmAxelHub_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key.Equals(Key.F1)) System.Diagnostics.Process.Start("http://www.axelsuite.com");
        }
    }
}