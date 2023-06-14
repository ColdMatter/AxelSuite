using DAQ.Environment;
using MOTMaster2.SequenceData;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using System.Threading;
using System.Collections.ObjectModel;
using System.Diagnostics;
using ErrorManager;
using UtilsNS;
using MOTMaster2.ExtDevices;
using System.ComponentModel;
using System.Windows.Input;


namespace MOTMaster2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static Controller controller;
        
        private TCPMessenger messenger;
        DispatcherTimer dispatcherTimer;

        //TODO change this so Controller can access it properly
        RemoteMessaging remoteMsg;
        Modes modes;
        const string RamanPhase = "RamanPhase";
        ExtDeviceDict ExtDevices;
        ExtFactorList ExtFactors;

        public MainWindow()
        {
            controller = new Controller();
            controller.StartApplication();

            Controller.OnRunStatus += new Controller.RunStatusHandler(OnRunStatus);
            //Controller.Onb4Acquire += new Controller.b4AcquireHandler(Onb4Acquire);
            Controller.OnChnChange += new Controller.ChnChangeHandler(OnChnChange);

            InitializeComponent();
            
            ErrorMng.Initialize(ref lbStatus, ref tbLogger, (string)Environs.FileSystem.Paths["configPath"]);

            dispatcherTimer = new DispatcherTimer(DispatcherPriority.Send);
            dispatcherTimer.Tick += dispatcherTimer_Tick;
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);

            this.sequenceControl.ChangedAnalogChannelCell += new SequenceDataGrid.ChangedAnalogChannelCellHandler(this.sequenceData_AnalogValuesChanged);
            Controller.MotMasterDataEvent += OnDataCreated;
            OnStartScan += GaussImage1.StartScanEvent;
            OnNextScanVal += GaussImage1.NextScanValEvent;

            //   ((INotifyPropertyChanged)Controller.sequenceData.Parameters).PropertyChanged += this.InterferometerParams_Changed;
            OpenDefaultModes();
            
            if (false && Utils.TheosComputer()) // Theo's computer
            {
                tiImageProcess.Visibility = System.Windows.Visibility.Visible; tcVisual.SelectedIndex = 2; cbHub.SelectedIndex = 3;
            }
            //Utils.traceDest = tbLogger;
            InitVisuals();
            editMode = false;
            if (Controller.sequenceData.Parameters.ContainsKey("swapAxes"))
            {
                bool sa = Convert.ToDouble(Controller.sequenceData.Parameters["swapAxes"].Value) < 0.5;
                mnXleading.IsChecked = sa; mnYleading.IsChecked = !sa; Controller.ExpData.SwappedAxes = !sa;
            }
            if (!Environs.Hardware.config.DoubleAxes)
            {
                mnXleading.Visibility = Visibility.Collapsed; mnYleading.Visibility = Visibility.Collapsed;
            }           
        }

        private void OnDataCreated(object sender, DataEventArgs e)
        {
            string data = (string)e.Data;
            remoteMsg.sendCommand(data); // to Axel-hub
            if (messenger != null) messenger.Send(data.Replace("\r\n",String.Empty)+"\n"); // mathematica
        }
        public static void DoEvents()
        {
            if (Utils.isNull(Application.Current)) return;
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background,
                                                  new Action(delegate { }));
            //Not needed for repeat run. Might be needed for scan
            // controller.WaitForRunToFinish(); 
        }
        public void InitVisuals()
        {           
            tcMain.SelectedIndex = 0;
            if (!Utils.isNull(Controller.sequenceData))
            {
                SetInterferometerParams(Controller.sequenceData.Parameters);
            }
        }
        private void frmMain_Loaded(object sender, RoutedEventArgs e)
        {
            if (!Environs.Hardware.config.DoubleAxes)
            {
                groupAxes.Visibility = Visibility.Collapsed;
            }
            ExtDevices = new ExtDeviceDict(); ExtFactors = new ExtFactorList();
            if (Environs.Hardware.ExtDevices.ContainsKey("MSquared"))
            {
                if (Environs.Hardware.config.UseMSquared)
                {
                    MSquaredUC ms = new MSquaredUC("MSquared", Brushes.DarkRed);
                    ExtDevices.Add("MSquared", ms); stackExtDevices.Children.Add(ms); ExtFactors.Add(ms.ucExtFactors);
                }
                if (Environs.Hardware.config.UseMuquans)
                {
                    MuQuansUC ms = new MuQuansUC("MuQuans", Brushes.DarkRed);
                    ExtDevices.Add("MuQuans", ms); stackExtDevices.Children.Add(ms); ExtFactors.Add(ms.ucExtFactors);
                }
            }
            if (Environs.Hardware.ExtDevices.ContainsKey("WindFreak"))
            {
                switch (Environs.Hardware.ExtDevices["WindFreak"])
                {
                    case ("1"):
                        WindFreak1UC wf1 = new WindFreak1UC("WindFreak", Brushes.Navy);
                        ExtDevices.Add("WindFreak", wf1); stackExtDevices.Children.Add(wf1); ExtFactors.Add(wf1.ucExtFactors);
                        break;
                    case ("2"):
                        WindFreak2UC wf2 = new WindFreak2UC("WindFreak", Brushes.Navy);
                        ExtDevices.Add("WindFreak", wf2); stackExtDevices.Children.Add(wf2); ExtFactors.Add(wf2.ucExtFactors);
                        break;
                }
            }
            if (Environs.Hardware.ExtDevices.ContainsKey("FlexDDS"))
            {
                int fCount = Convert.ToInt32(Environs.Hardware.ExtDevices["FlexDDS"]);
                for (int i = 0; i < fCount; i++)
                {
                    string edn = "FlexDDS-" + Convert.ToChar(65 + i);
                    FlexDDS_UC fd = new FlexDDS_UC(edn, Brushes.DarkGreen);
                    ExtDevices.Add(edn, fd); stackExtDevices.Children.Add(fd); ExtFactors.Add(fd.ucExtFactors);
                }
            }
            if (Environs.Hardware.ExtDevices.ContainsKey("Magneto"))
            {
                MagnetoUC ms = new MagnetoUC("Magneto", Brushes.DarkOrange);
                ExtDevices.Add("Magneto", ms); stackExtDevices.Children.Add(ms); ExtFactors.Add(ms.ucExtFactors);
            }
            if (Environs.Hardware.ExtDevices.ContainsKey("TiltSynchro"))
            {
                TiltSynchroUC ts = new TiltSynchroUC("TiltSynchro", Brushes.Teal);
                ExtDevices.Add("TiltSynchro", ts); stackExtDevices.Children.Add(ts); ExtFactors.Add(ts.ucExtFactors);
            }


            ExtDevices.Init(ref Controller.sequenceData, ref Controller.genOptions);
            ExtDevices.UpdateFromOptions(ref Controller.genOptions);

            if (File.Exists(historyFile)) history = Utils.readList(historyFile);
            else history = new List<string>();
        }
        private void OpenDefaultModes()
        {
            if (File.Exists(Utils.configPath + "Defaults.cfg"))
            {
                string fileJson = File.ReadAllText(Utils.configPath + "Defaults.cfg");
                modes = JsonConvert.DeserializeObject<Modes>(fileJson);
            }
            else
                modes = new Modes();
            //scan
            MMscan mms = new MMscan();
            mms.AsString = modes.Scan;
            //List<string> 
            cbParamsScan_DropDownOpened(null, null);
            cbParamsScan.Text = mms.sParam;
            tbFromScan.Text = mms.sFrom.ToString();
            tbToScan.Text = mms.sTo.ToString();
            tbByScan.Text = mms.sBy.ToString();
            //multiScan
            lstParams.Items.Clear();
            if (!Utils.isNull(modes.MultiScan)) 
                foreach (string ss in modes.MultiScan) 
                {
                    ListBoxItem lbi = new ListBoxItem();
                    lbi.Content = ss;
                    lstParams.Items.Add(lbi);
                }
        }
        public enum GroupRun { none, repeat, scan, multiScan };
        private GroupRun _groupRun;
        public GroupRun groupRun
        {
            get
            {
                return _groupRun;
            }
            set
            {
                _groupRun = value;
                cbHub.IsEnabled = (value == GroupRun.none);
                tbExperimentRun.IsEnabled = (value == GroupRun.none);
                sequenceControl.IsReadOnly = (value != GroupRun.none);
                if (!Utils.isNull(Controller.ScanParam)) Controller.ScanParam.randomized = chkRandomize.IsChecked.Value;
                if (sequenceControl.IsReadOnly)
                    if (!Controller.genOptions.AIEnabled && remoteMsg.Connected) ErrorMng.warningMsg("Axel-Hub is connected, but analog acquisition is OFF ?!");

                if (value == GroupRun.none) frmMain.ResizeMode = System.Windows.ResizeMode.CanResize;
                else frmMain.ResizeMode = System.Windows.ResizeMode.NoResize;
            }
        }

        //TODO Rename to reflect loop runs
        private bool SingleShot(Dictionary<string, object> paramDict) // true if OK
        {
            try
            {
                controller.BuildMMSequence(paramDict);
            }
            catch (Exception e)
            {
                ErrorMng.errorMsg("Failed to build sequence:" + e.Message +" IN " + e.Source, -1, false);
                return false;
            }
            //if (Math.Abs(Controller.ExpData.axis) == 2) 
                ExtDevices.UpdateDevices(false);
            if (Controller.genOptions.DelayBwnShots > 0) Thread.Sleep(Controller.genOptions.DelayBwnShots);
            
            if (ExtDevices.DetectCancel(ExtDevices.SequenceEvent("start_seq")))
            {
                // stoppting proc
                switch (groupRun)
                {
                    case GroupRun.repeat:
                        if (!btnRun.Content.Equals("R u n")) btnRun_Click(null, null);
                        break;
                    case GroupRun.scan:
                        if (!btnRun.Content.Equals("S c a n")) btnScan_Click(null, null);
                        break;
                }                
            }
            
            controller.RunStart(paramDict);
            //Would like to use RunStart as this Runs in a new thread
            if (controller.IsRunning())
            {
                controller.WaitForRunToFinish();
            }
            return !controller.IsRunning();
        }
        private bool SingleShot() // true if OK
        {
            return SingleShot(null);
        }

        private static string
            scriptCsPath = (string)Environs.FileSystem.Paths["scriptListPath"];
        private static string
            scriptPyPath = (string)Environs.FileSystem.Paths["scriptListPath"];


        #region RUNNING THINGS
        bool wait4adjust = false;
        
        private void realRun(int Iters, string Hub = "none", int cmdId = -1)
        {
            Utils.traceDest = new FileLogger(); //tbLogger;

            controller.AutoLogging = Check4Logging();
            if ((Iters == 0) || (Iters < -1))
            {
                ErrorMng.errorMsg("Invalid <Iteration Number> value.", 2, true);
                if (!btnRun.Content.Equals("R u n")) btnRun_Click(null, null);
                return;
            }
            progBar.Minimum = 0;
            progBar.Maximum = Iters - 1;
            int numInterations = Iters;
            if (Iters == -1)
            {
                numInterations = Int32.MaxValue;
                progBar.Maximum = 100;
            }
            Controller.ScanParam = null;
            Controller.ExpData.ClearData();
            Controller.numInterations = numInterations;
            Controller.ExpData.ExperimentName = tbExperimentRun.Text;
            Controller.StaticSequence = true;
            groupRun = GroupRun.repeat;
            Dictionary<string, object> scanDict = null;
            if (Controller.genOptions.ForceSeqCharge) scanDict = new Dictionary<string, object>();
            if ((Controller.ExpData.ExperimentName.Equals("---") || String.IsNullOrEmpty(Controller.ExpData.ExperimentName)))
            {
                Controller.ExpData.ExperimentName = DateTime.Now.ToString("yy-MM-dd_H-mm-ss");
                tbExperimentRun.Text = Controller.ExpData.ExperimentName;
            }
            StartScanEvent(true, false, null); // GaussImage stuff
            ExtDevices.UpdateDevices(false);
            for (int i = 0; i < numInterations; i++)
            {
                if (groupRun != GroupRun.repeat) break; //False if runThread was stopped elsewhere
                //Console.WriteLine("#: " + i.ToString());
                Controller.BatchNumber = i;
                NextScanValEvent(Convert.ToDouble(i));

                if (!SingleShot(scanDict)) { groupRun = GroupRun.none; }           
                if (Iters == -1) progBar.Value = i % 100;
                else progBar.Value = i;                
                lbCurNumb.Content = i.ToString();
                if (groupRun != GroupRun.repeat) break; 
                DoEvents();
                bool bb = !Utils.isNull(Controller.ExpData.grpMME);
                if (bb) bb &= Controller.ExpData.grpMME.id.Equals(cmdId);
                if (bb) bb &= Controller.ExpData.grpMME.mmexec.Equals("diagnostics");
                wait4adjust = (Controller.ExpData.jumboMode() == ExperimentData.JumboModes.repeat) && !bb;
                int j = 0;
                while ((wait4adjust) && (j < 10))
                {
                    Thread.Sleep(10);
                    DoEvents();
                    j++;
                }
                if (j == 10) ErrorMng.Log("Time-out at wait4adjust loop", Brushes.DarkOrange.Color);
                controller.WaitForRunToFinish();
            }
            StartScanEvent(false, false, null); 
            controller.AutoLogging = false;
        }

        private void btnRun_Click(object sender, RoutedEventArgs e)
        {
            const string run = "R u n";
            if (btnRun.Content.Equals(run))
            {
                Controller.ExpData.SaveRawData = true;
                btnRun.Content = "Stop";
                btnRun.Background = Brushes.Coral;
                Controller.ExpData.grpMME.Clear();
                Controller.SaveTempSequence();
                int Iters = (int)ntbIterNumb.Value;
                
                // Start repeat
                try
                {
                    realRun(Iters);
                }
                catch (Exception ex)
                {
                    ErrorMng.errorMsg(ex.Message, -5);
                }
                if (!btnRun.Content.Equals(run)) btnRun_Click(null, null);
                return;
            }

            if (btnRun.Content.Equals("Stop"))
            {
                tbExperimentRun.Text = "---";
                btnRun.Content = run;
                btnRun.Background = Brushes.LightGreen;
                groupRun = GroupRun.none;
                controller.StopRunning();
                lbCurNumb.Content = "";
                StartScanEvent(false, false, null); 
                // End repeat
                if (!Utils.isNull(sender))
                {
                    MMexec mme = new MMexec("Axel-hub");
                    remoteMsg.sendCommand(mme.Abort("MOTMaster"));
                }
            }

            if (btnRun.Content.Equals("Abort"))
            {
                tbExperimentRun.Text = "---";
                btnRun.Content = run;
                btnRun.Background = Brushes.LightGreen;
                groupRun = GroupRun.none;
                //Send Remote Message to AxelHub
                controller.StopRunning();
                lbCurNumb.Content = "";
                if (!Utils.isNull(sender))
                {
                    MMexec mme = new MMexec("Axel-hub");
                    remoteMsg.sendCommand(mme.Abort("MOTMaster"));
                }
            }
        }

        private bool Check4Logging()
        {
            switch (cbHub.SelectedIndex)
            {
                case 0: return false;               
                case 1: return true;                
                case 2: return Controller.genOptions.AxelHubLogger;
                case 3: return Controller.genOptions.MatematicaLogger;
                default: return false;
            }            
        }

        public delegate void StartScanHandler(bool _start, bool _scanMode, MMscan _mmscan); // when _scanMode == none then cancel
        public event StartScanHandler OnStartScan;

        protected void StartScanEvent(bool _start, bool _scanMode, MMscan _mmscan) // GaussImage
        {
            if (_start) ExtDevices.SequenceEvent("start_proc");

            if (cbHub.SelectedIndex != 3) return;
            if (Utils.isNull(GaussImage1)) return;
            if (!GaussImage1.LineMode) return;
            if (OnStartScan != null) OnStartScan(_start, _scanMode, _mmscan);            
        }
        public delegate void NextScanValHandler(double val);
        public event NextScanValHandler OnNextScanVal;

        protected void NextScanValEvent(double val)
        {
            if (cbHub.SelectedIndex != 3) return;
            if (Utils.isNull(GaussImage1)) return;
            if (!GaussImage1.LineMode) return;
            if (OnNextScanVal != null) OnNextScanVal(val);
        }
        private string SetScanParamExt(string site, string param) // back factor 
        {
            if (!Controller.sequenceData.Parameters.ContainsKey(param))
            {
                Controller.sequenceData.Parameters[param] = new Parameter(param, "", 0, true, false); 
                Utils.TimedMessageBox("Parameter <"+param+"> has been added to the list of parameters.");
                ExtFactors.UpdateFromSequence(ref Controller.sequenceData);
            }
            string[] sa = site.Split(':'); if (sa.Length != 2) return "";
            tcVisual.SelectedIndex = 1;
            ExtFactors.SetFactor(sa[0],sa[1],param);
            return sa[1];
        }
        private void realScan(string site, string prm, string fromScanS, string toScanS, string byScanS, bool randomize = false, string Hub = "none", int cmdId = -1)
        {
            string parameter = prm;
            if (prm.Equals(RamanPhase) && !site.Equals("")) // a special case for ramanPhase
            {
                if (!ExtDevices["MSquared"].CheckEnabled())
                {
                    ErrorMng.errorMsg("ICE block is not available!", 124); return;
                }
                SetScanParamExt(site, prm); // set the device factor
                Parameter paramRP = Controller.sequenceData.Parameters[prm];
                int j = cbParamsScan.Items.IndexOf(RamanPhase);
                if (j>-1) cbParamsScan.SelectedIndex = j;
                MMscan scan = new MMscan(); scan.sParam = parameter;
                scan.sFrom = Convert.ToDouble(fromScanS); scan.sTo = Convert.ToDouble(toScanS); scan.sBy = Convert.ToDouble(byScanS);                
                double wr = scan.sFrom;

                int Iters = (int)Math.Ceiling((scan.sTo - wr) / scan.sBy + 0.001);
                controller.AutoLogging = Check4Logging();
                if ((Iters == 0) || (Iters < 0)) // cancel the scan
                {
                    ErrorMng.errorMsg("Invalid scan values.", 2, true);
                    if (!btnRun.Content.Equals("S c a n")) btnScan_Click(null, null);
                    return;
                }
                progBar.Minimum = 0;
                progBar.Maximum = Iters - 1;
                int numInterations = Iters;
                Controller.ExpData.ClearData();
                Controller.numInterations = numInterations; 
                Controller.ExpData.ExperimentName = tbExperimentRun.Text;
                if ((Controller.ExpData.ExperimentName.Equals("---") || String.IsNullOrEmpty(Controller.ExpData.ExperimentName)))
                {
                    Controller.ExpData.ExperimentName = DateTime.Now.ToString("yy-MM-dd_H-mm-ss");
                    tbExperimentRun.Text = Controller.ExpData.ExperimentName;
                }               
                scan.groupID = Controller.ExpData.ExperimentName;
                Controller.ScanParam = scan.Clone();
                
                Controller.StaticSequence = true;
                if (!ExtFactors.IsScannable(prm))
                {
                    ErrorMng.errorMsg("Param of dvc. <"+prm+"> is not available!", 125); return;
                }
                groupRun = GroupRun.scan;
                StartScanEvent(true, true, scan); 
                for (int i = 0; i < numInterations; i++)
                {
                    if (groupRun == GroupRun.none) break; //False if runThread was stopped elsewhere
                    Controller.BatchNumber = i;
                    NextScanValEvent(Convert.ToDouble(i));

                    //Controller.M2DCS.phaseControl(wr);
                    //Console.WriteLine("#: " + wr.ToString());
                    paramRP.Value = wr;
                    ExtFactors.ScanIter(parameter, i);
                    if (!SingleShot()) { groupRun = GroupRun.none; }
                    progBar.Value = i;

                    lbCurValue.Content = wr.ToString("G5"); DoEvents();
                    wr += scan.sBy;
                    controller.WaitForRunToFinish();
                }
                StartScanEvent(false, true, scan);
                controller.AutoLogging = false; ExtFactors.ScanIter(parameter, -1); // reset factors
                if (!btnScan.Content.Equals("S c a n")) btnScan_Click(null, null);               
                return;
            }

            if (!Controller.sequenceData.Parameters.ContainsKey(prm)) { ErrorMng.errorMsg(string.Format("Parameter {0} not found in sequence", prm), 100, true); return; }
            Parameter param = Controller.sequenceData.Parameters[prm];
            //Sets the sequence to static if we know the scan parameter does not modify the sequence
            Controller.StaticSequence = !param.SequenceVariable;
            Dictionary<string, object> scanDict = new Dictionary<string, object>();
            Controller.ExpData.ClearData();
            Controller.ExpData.SaveRawData = true;
            Controller.ExpData.ExperimentName = tbExperimentRun.Text;
            if (Controller.ExpData.ExperimentName.Equals("---") || String.IsNullOrEmpty(Controller.ExpData.ExperimentName))
            {
                Controller.ExpData.ExperimentName = DateTime.Now.ToString("yy-MM-dd_H-mm-ss");
                tbExperimentRun.Text = Controller.ExpData.ExperimentName;
            }
            controller.AutoLogging = Check4Logging();
            scanDict[parameter] = param.Value;
            object defaultValue = param.Value;
            MMscan scanParam = new MMscan();
            scanParam.sParam = prm;
            scanParam.groupID = Controller.ExpData.ExperimentName;

            int scanLength;
            object[] scanArray;
            if (defaultValue is int && Controller.sequenceData.Parameters.ContainsKey(prm))
            {
                int fromScanI = int.Parse(fromScanS);
                int toScanI = int.Parse(toScanS);
                int byScanI = int.Parse(byScanS);
                scanParam.sFrom = fromScanI;
                scanParam.sTo = toScanI;
                scanParam.sBy = byScanI;
                scanLength = (toScanI - fromScanI) / byScanI + 1;
                if (scanLength < 0)
                {
                    ErrorMng.errorMsg("Incorrect looping parameters. <From> value must be smaller than <To> value if it increases per shot.",3,true);
                    return;
                }
                scanArray = new object[scanLength];
                for (int i = 0; i < scanLength; i++)
                {
                    scanArray[i] = fromScanI;
                    fromScanI += byScanI;
                }
            }
            else
            {
                double fromScanD = double.Parse(fromScanS);
                double toScanD = double.Parse(toScanS);
                double byScanD = double.Parse(byScanS);
                scanParam.sFrom = fromScanD;
                scanParam.sTo = toScanD;
                scanParam.sBy = byScanD;
                scanLength = (int)((toScanD - fromScanD) / byScanD) + 1;
                if (scanLength < 0)
                {
                    ErrorMng.errorMsg("Incorrect looping parameters. <From> value must be smaller than <To> value if it increases per shot.",3,true);
                    return;
                }
                scanArray = new object[scanLength];
           
                for (int i = 0; i < scanLength; i++)
                {
                    scanArray[i] = fromScanD;
                    fromScanD += byScanD;
                }
            }
            bool scanExt = false;
            ExtDevices.UpdateDevices(false);
            if (Controller.sequenceData.Parameters.ContainsKey(prm))
                if (Controller.sequenceData.Parameters[prm].IsLaser)
                    scanExt = ExtFactors.IsScannable(prm); // visual and optim
            Controller.SaveTempSequence(null, scanParam);

            progBar.Minimum = 0;
            progBar.Maximum = scanArray.Length-1;

            int c = 0;
            Controller.ScanParam = scanParam.Clone();
            groupRun = GroupRun.scan;
            if (randomize)
            {
                List<object> ls = scanArray.ToList<object>();
                scanArray = Utils.Randomize<object>(ls).ToArray<object>();
            }
            StartScanEvent(true, true, scanParam); 
            foreach (object scanItem in scanArray)
            {
                Controller.BatchNumber = c; progBar.Value = c;
                param.Value = scanItem;
                NextScanValEvent(Convert.ToDouble(scanItem)); // call gaussImage before the body of the loop
                scanDict[parameter] = scanItem;               
                SetInterferometerParams(scanDict);
                try
                {
                    if (!Utils.isNull(Controller.ScanParam)) Controller.ScanParam.Value = Convert.ToDouble(scanItem);
                    if (scanExt) ExtFactors.ScanIter(parameter, c);
                    if(!SingleShot(scanDict)) groupRun = GroupRun.none;                   
                }
                catch (Exception e)
                {
                    ErrorMng.errorMsg("Error running scan: " + e.Message, -2);
                    break;
                }
                lbCurValue.Content = ((double)scanItem).ToString(Constants.ScanDataFormat);
                DoEvents();
                if (groupRun != GroupRun.scan) break;
                c++;      
            }
            if (!btnScan.Content.Equals("S c a n")) btnScan_Click(null, null);
            StartScanEvent(false, true, scanParam);
            param.Value = defaultValue;
            ExtFactors.ScanIter(parameter, -1); // reset factors           
            lbCurValue.Content = ((double)defaultValue).ToString(Constants.ScanDataFormat);
            controller.AutoLogging = false;
        }

        private void btnScan_Click(object sender, RoutedEventArgs e)
        {
            const string scan = "S c a n";
            var brush = Utils.ToSolidColorBrush("#FFF9E76B"); 
            if (btnScan.Content.Equals(scan))
            {
                btnScan.Content = "Cancel";
                btnScan.Background = Brushes.Coral;
                Controller.ExpData.grpMME.Clear();
                try
                {
                    realScan("",cbParamsScan.Text, tbFromScan.Text, tbToScan.Text, tbByScan.Text, chkRandomize.IsChecked.Value);
                }
                catch (Exception ex)
                {
                    ErrorMng.errorMsg(ex.Message, -5);
                    //btnScan_Click(null, null);
                    return;
                }
                controller.StopRunning();
                StartScanEvent(false, true, null);
                btnScan.Content = scan;
                btnScan.Background = brush;
                groupRun = GroupRun.none;
                return;
            }

            if (btnScan.Content.Equals("Cancel"))
            {
                tbExperimentRun.Text = "---";
                btnScan.Content = scan;
                btnScan.Background = brush;
                groupRun = GroupRun.none;
                controller.StopRunning();
                StartScanEvent(false, true, null);
                if (!Utils.isNull(sender))
                {
                    MMexec mme = new MMexec("Axel-hub");
                    remoteMsg.sendCommand(mme.Abort("MOTMaster"));
                }
            }

            if (btnScan.Content.Equals("Abort"))
            {
                tbExperimentRun.Text = "---";
                btnScan.Content = scan;
                btnScan.Background = brush;
                groupRun = GroupRun.none;
                controller.StopRunning();
                StartScanEvent(false, true, null);
                //Send Remote Message to AxelHub
                //if (!Utils.isNull(sender))
                {
                    MMexec mme = new MMexec("Axel-hub");
                    remoteMsg.sendCommand(mme.Abort("MOTMaster"));
                }
            }
        }
        #endregion
        private bool paramCheck = false; private int _TabItemIndex;
        private void tcMain_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.OriginalSource.GetType() == typeof(TabControl))
            {
                if (paramCheck) return; // avoid recursion
                paramCheck = true;  int gl = 110;
                if (groupRun == GroupRun.none) // before start
                {
                    _TabItemIndex = tcMain.SelectedIndex;
                }
                else // something is running
                {
                    if (tcMain.SelectedIndex != 3) 
                    {
                        if (tcMain.SelectedIndex != _TabItemIndex)
                        {
                            e.Handled = true;
                            tcMain.SelectedIndex = _TabItemIndex;
                            paramCheck = false;
                            return;
                        }
                    } 
                }
                if (tcMain.SelectedIndex == 1) // scan
                {
                    int selIdx = cbParamsScan.SelectedIndex;
                    if (selIdx == -1) selIdx = 0;
                    cbParamsScan.Items.Clear();
                    //cbParamsScan.Items.Add(ramanPhase);
                    foreach (string param in Controller.sequenceData.ScannableParams())
                        if (param != "") cbParamsScan.Items.Add(param);
                    cbParamsScan.SelectedIndex = selIdx;
                }
                if (tcMain.SelectedIndex == 2) // multi-scan
                {
                    cbParamsMScan.Items.Clear();
                    foreach (string param in Controller.sequenceData.ScannableParams())
                        cbParamsMScan.Items.Add(param);
                    if (lstParams.Items.Count > 0)
                    {
                        lstParams.SelectedIndex = 0;
                        lstParams_MouseUp(null, null);
                    }
                    gl = 170;
                }
                if (tcMain.SelectedIndex == 3) // manual
                {
                    int selIdx = cbParamsManual.SelectedIndex;
                    if (selIdx == -1) selIdx = 0;
                    cbParamsManual.Items.Clear();
                    //cbParamsScan.Items.Add(ramanPhase);
                    foreach (string param in Controller.sequenceData.ScannableParams())
                        if (param != "") cbParamsManual.Items.Add(param);
                    //cbParamsManual.Text = ParamsArray[0];
                    cbParamsManual.SelectedIndex = selIdx;
                }
                gridMain.RowDefinitions[2].Height = new GridLength(gl);

                paramCheck = false;
            }
        }

        private void LoadParameters_Click(object sender, RoutedEventArgs e)
        {
            if (Controller.script != null)
            { // Configure open file dialog box
                Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
                dlg.FileName = ""; // Default file name
                dlg.DefaultExt = ".prm"; // Default file extension
                dlg.Filter = "Parameters (.prm)|*.prm"; // Filter files by extension

                // Show open file dialog box
                Nullable<bool> result = dlg.ShowDialog();

                // Process open file dialog box results
                if (result == true)
                {
                    string filename = dlg.FileName;

                    Dictionary<String, Object> LoadedParameters = new Dictionary<string, object>();
                    string json = File.ReadAllText(filename);
                    LoadedParameters = (Dictionary<String, Object>)JsonConvert.DeserializeObject(json, typeof(Dictionary<String, Object>));
                    if (Controller.script != null)
                        foreach (string key in LoadedParameters.Keys)
                            Controller.script.Parameters[key] = LoadedParameters[key];
                    else
                        ErrorMng.warningMsg("You have tried to load parameters without loading a script");
                }
            }
        }

        private void SaveParameters_Click(object sender, RoutedEventArgs e)
        {
            if (Controller.script != null)
            { // Configure open file dialog box
                Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
                dlg.FileName = ""; // Default file name
                dlg.DefaultExt = ".prm"; // Default file extension
                dlg.Filter = "Parameters (.prm)|*.prm"; // Filter files by extension

                // Show open file dialog box
                bool? result = dlg.ShowDialog();

                // Process open file dialog box results
                if (result != true) return;
                string filename = dlg.FileName;
                string json = JsonConvert.SerializeObject(Controller.script.Parameters, Formatting.Indented);
                File.WriteAllText(filename, json);
            }
            else
                ErrorMng.warningMsg("You have tried to save parmaters before loading a script");
        }

        private void SaveSequence_Click(object sender, RoutedEventArgs e)
        {
            if (Controller.sequenceData != null)
            { // Configure open file dialog box
                Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
                dlg.FileName = ""; // Default file name
                dlg.DefaultExt = ".sm2"; // Default file extension
                dlg.Filter = "Sequence (.sm2)|*.sm2"; // Filter files by extension
                //dlg.InitialDirectory = Controller.scriptListPath;

                // Show open file dialog box
                bool? result = dlg.ShowDialog();

                // Process open file dialog box results
                if (result != true) return;
                string filename = dlg.FileName;
                Controller.SaveSequenceToPath(filename);
                Log("Saved Sequence to ..." + filename.Substring(Utils.EnsureRange(filename.Length - 50, 0, 100)));
                if (history.Count > 0)
                {
                    if (!history[0].Equals(filename)) history.Insert(0, filename);
                    if (history.Count > 10) history.RemoveAt(history.Count - 1);
                } 
                else history.Insert(0, filename);
            }
            else
                ErrorMng.warningMsg("You have tried to save a Sequence before loading a script", -1, true);
        }
        private void LoadSequence_Click(object sender, RoutedEventArgs e)
        {
            string filename = (string)((MenuItem)sender).Header;
            if (sender.Equals(mOpenSequence)) 
            {            
                Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
                dlg.FileName = ""; // Default file name
                dlg.DefaultExt = ".sm2"; // Default file extension
                dlg.Filter = "Sequence (.sm2)|*.sm2"; // Filter files by extension

                // Show open file dialog box
                bool? result = dlg.ShowDialog();

                // Process open file dialog box results
                if (result != true) return;
                filename = dlg.FileName;
            }
            if (!File.Exists(filename)) { Log("Error: file not found - " + filename); return; }
            sequenceControl.ClearSteps();
            Controller.LoadSequenceFromPath(filename);
            sequenceControl.UpdateSequenceData();
            ExtFactors.UpdateFromSequence(ref Controller.sequenceData);
            Log("Loaded Sequence from ..." + filename.Substring(filename.Length - 50));
            if (Controller.sequenceData.Parameters.ContainsKey("swapAxes"))
            {
                bool sa = Convert.ToDouble(Controller.sequenceData.Parameters["swapAxes"].Value) < 0.5;
                mnXleading.IsChecked = sa; mnYleading.IsChecked = !sa;
            }                          
        }

       private void SaveEnvironment_Click(object sender, RoutedEventArgs e)
        {
            controller.SaveEnvironment();
        }
        private void LoadEnvironment_Click(object sender, RoutedEventArgs e)
        {
            controller.LoadEnvironment();
        }
        private void EditOptions_Click(object sender, RoutedEventArgs e)
        {
            OptionWindow optionsWindow = new OptionWindow();
            optionsWindow.ShowDialog();
            ExtDevices.UpdateFromOptions(ref Controller.genOptions);
        }
        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("           MOTMaster2 v"+Utils.getAppFileVersion+"\n\n         by Teodor Krastev, et al.\n\n for Imperial College, London, UK", 
                "About" + (Controller.config.Debug ? "   (Debug mode)" : ""));
        }
        private void cbParamsManual_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.OriginalSource.GetType() == typeof(ComboBox) && cbParamsManual.SelectedItem != null)
            {
                string sPrm = cbParamsManual.SelectedItem.ToString();
                if (Controller.sequenceData.Parameters.ContainsKey(sPrm))
                {
                    Parameter Prm = Controller.sequenceData.Parameters[sPrm];
                    tbdValue.Tag = 1;
                    tbdValue.Value = Convert.ToDouble(Prm.Value);
                    tbdValue.Tag = 0;
                    tbdValue_ValueChanged(tbdValue, null);
                }                   
            }                    
        }
        private void cbParamsScan_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.OriginalSource.GetType() == typeof(ComboBox) && cbParamsScan.SelectedItem != null)
            {
                lbCurValue.Content = Controller.sequenceData.Parameters[cbParamsScan.SelectedItem.ToString()].Value.ToString();
            }                
        }
        private void Log(string txt, Color? clr = null)
        {
            if (!chkLog.IsChecked.Value) return;
            ErrorMng.Log(txt, clr);
        }

        #region sequence data manipulation
        bool editMode 
        {
            get { return tcLogProps.SelectedIndex.Equals(1); }
            set 
            { 
                if (value) 
                {
                    if (!editMode) // from OFF to ON
                    {
                        lastCell.SelectedStep = null; lastCell.ChannelName = "";
                        lastCell.sequenceJson = JsonConvert.SerializeObject(Controller.sequenceData, Formatting.Indented, 
                                                                            new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                    }
                    tcLogProps.SelectedIndex = 1; 
                }
                else { tcLogProps.SelectedIndex = 0; lastCell.Clear(); }
                tcMain.IsEnabled = !editMode;
                if (editMode) tiAnalogProps.Visibility = Visibility.Visible;
                else tiAnalogProps.Visibility = Visibility.Collapsed;
            }
        }
        struct lastCellType // the last selected cell when you go into edit mode
        {
            public SequenceStep SelectedStep;
            public DataGridCell SelectedCell;
            public string ChannelName;
            private List<AnalogArgItem> _Data;
            public List<AnalogArgItem> Data
            {
                get { return _Data; }
                set
                {
                    _Data = new List<AnalogArgItem>();
                    foreach (AnalogArgItem ai in value)
                    {
                        _Data.Add(new AnalogArgItem(ai.Name, ai.Value));
                    }
                }
            }
            public AnalogChannelSelector AnalogType;
            public string sequenceJson;
            public void Clear()
            {
                SelectedStep = null; SelectedCell = null; ChannelName = ""; _Data = null;  AnalogType = AnalogChannelSelector.Continue; sequenceJson = "";
            }
        }
        lastCellType lastCell = new lastCellType();
        private void sequenceData_AnalogValuesChanged(object sender, SelectionChangedEventArgs e)
        {   
            if (controller.IsRunning())
            {
                Utils.TimedMessageBox("You cannot edit while the sequence is running", "Warning !", 2500); return;
            }
            editMode = true;
            SequenceStepViewModel model = (SequenceStepViewModel)sequenceControl.sequenceDataGrid.DataContext;
            KeyValuePair<string, AnalogChannelSelector> analogChannel = model.SelectedAnalogChannel;
            SequenceStep step = model.SelectedSequenceStep;
            if (Utils.isNull(lastCell.SelectedStep) && lastCell.ChannelName.Equals("")) // open new edit
            {
                lastCell.SelectedStep = step; lastCell.SelectedCell = sequenceControl.lastSelectedCell; lastCell.ChannelName = analogChannel.Key; lastCell.AnalogType = analogChannel.Value;
                if (lastCell.AnalogType.Equals(AnalogChannelSelector.Continue)) lastCell.Data = new List<AnalogArgItem>();
                else lastCell.Data = step.GetAnalogData(lastCell.ChannelName, lastCell.AnalogType);
            }
            if (Utils.isNull(lastCell.SelectedStep) && lastCell.ChannelName.Equals("") || // open new edit
               (object.ReferenceEquals(step, lastCell.SelectedStep) && (lastCell.ChannelName.Equals(analogChannel.Key)))) // same cell; new type
            {
                analogPropGrid.feedData(step, analogChannel.Key, analogChannel.Value);
            }
            else // move away but already editing 
            {                
                if (!object.ReferenceEquals(step, lastCell.SelectedStep) || !(lastCell.ChannelName.Equals(analogChannel.Key)))
                {
                    Utils.TimedMessageBox("You cannot change another cell before Cancel or Update the selected one.", "Warning !", 2500);
                    if (!Utils.isNull(lastCell.SelectedCell)) lastCell.SelectedCell.Focus();
                    btnCancelProp_Click(null, null);                   
                }
            }
        }
        private void updateProps_Click(object sender, RoutedEventArgs e)
        {
            SequenceParser sqnParser = new SequenceParser();
            bool verified = ParseAnalogItems(analogPropGrid.getData(),sqnParser);
            if (verified)
            {
                lastCell.Data = analogPropGrid.getData();
                SequenceStepViewModel model = (SequenceStepViewModel)sequenceControl.sequenceDataGrid.DataContext;
                model.UpdateChannelValues(lastCell.Data);
            }
            editMode = false;
        }
        private void btnCancelProp_Click(object sender, RoutedEventArgs e)
        {
            //string sequenceJson = JsonConvert.SerializeObject(Controller.sequenceData, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            if (!lastCell.sequenceJson.Equals("") && false)
            {
                Controller.sequenceData = JsonConvert.DeserializeObject<Sequence>(lastCell.sequenceJson);
                sequenceControl.UpdateSequenceData();                
            }
            if (!Utils.isNull(lastCell.Data))
            {
                SequenceStepViewModel model = (SequenceStepViewModel)sequenceControl.sequenceDataGrid.DataContext;
                if (!lastCell.ChannelName.Equals(""))
                    model.SelectedAnalogChannel = new KeyValuePair<string, AnalogChannelSelector>(lastCell.ChannelName, lastCell.AnalogType);
                if (!Utils.isNull(lastCell.SelectedStep))
                    model.SelectedSequenceStep = lastCell.SelectedStep;
                model.UpdateChannelValues(lastCell.Data);
            }
            editMode = false; 
        }

        private bool ParseAnalogItems(List<AnalogArgItem> data, SequenceParser sqnParser)
        {
            if (Controller.sequenceData == null) return false;
            foreach (AnalogArgItem analogItem in data)
            {
                double analogRawValue;
                if (Double.TryParse(analogItem.Value, out analogRawValue)) continue;
                if (Controller.sequenceData.Parameters.ContainsKey(analogItem.Value)) continue;
                switch (analogItem.Name)
                {
                    case "X Values":
                    case "Y Values":
                        string[] sa = analogItem.Value.Split(',');
                        bool bb = true;
                        foreach (string ss in sa)
                            bb &= Double.TryParse(ss, out analogRawValue) || Controller.sequenceData.Parameters.ContainsKey(ss);
                        if (bb) continue;
                        break;
                    case "Interpolation Type":
                        if (analogItem.Value.Equals("Piecewise Linear") || analogItem.Value.Equals("Step")) continue;
                        break;
                    case "Function": //Tries to parse the function string
                        if (sqnParser.CheckFunction(analogItem.Value)) continue;
                        break;
                }
                ErrorMng.errorMsg(string.Format("Incorrect Value given for {0}. Either choose a parameter name or enter a number.", analogItem.Name), 5, true);
                return false;
            }
            return true;
        }

        #endregion sequence data manipulation

        private void frmMain_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ExtDevices.Final();
            if (Controller.genOptions.saveSequence.Equals(GeneralOptions.SaveOption.ask))
            {
                //Save the currently open sequence to a default location
                MessageBoxResult result = MessageBox.Show("MOTMaster is closing. \nDo you want to save the sequence? ...or cancel closing?", "    Save Default Sequence", MessageBoxButton.YesNoCancel);
                if (result == MessageBoxResult.Yes)
                {
                    Controller.SaveSequenceAsDefault();
                    //SaveSequence_Click(sender, null);
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    //List<SequenceStep> steps = sequenceControl.sequenceDataGrid.ItemsSource.Cast<SequenceStep>().ToList();
                    e.Cancel = true;
                }
            }
            if (Controller.genOptions.saveSequence.Equals(GeneralOptions.SaveOption.save)) Controller.SaveSequenceAsDefault();
            Controller.genOptions.Save();
            //modes
            //scan
            MMscan mms = new MMscan();          
            mms.sParam = cbParamsScan.Text;
            mms.sFrom = Convert.ToDouble(tbFromScan.Text);
            mms.sTo = Convert.ToDouble(tbToScan.Text);
            mms.sBy = Convert.ToDouble(tbByScan.Text);
            modes.Scan = mms.AsString;
            //multiScan
            if(Utils.isNull(modes.MultiScan)) modes.MultiScan = new List<string>();
            else modes.MultiScan.Clear();
            foreach (object obj in lstParams.Items)
            {
                if(!Utils.isNull(obj)) modes.MultiScan.Add((string)(obj as ListBoxItem).Content);
            }
            modes.Save();
            if (history.Count > 0) Utils.writeList(historyFile, history);
        }
        private void EditParameters_Click(object sender, RoutedEventArgs e)
        {
            ParametersWindow paramWindow = new ParametersWindow();
            paramWindow.ShowDialog();
            ExtFactors.UpdateFromSequence(ref Controller.sequenceData);            
        }
        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            dispatcherTimer.Stop();
            if (!Utils.isNull(remoteMsg)) 
                if (remoteMsg.lastRcvMsg.Contains("abort"))
                {
                    ErrorMng.Log("Abort Jumbo", Brushes.Red.Color);
                    return;
                }
            if (Controller.ExpData.jumboMode() == ExperimentData.JumboModes.scan)
            {
                ErrorMng.Log("Start Jumbo-Scan", Brushes.Blue.Color);
                MMscan mms = new MMscan();
                mms.FromDictionary(Controller.ExpData.grpMME.prms);
                realScan(mms.sSite,mms.sParam, mms.sFrom.ToString(), mms.sTo.ToString(), mms.sBy.ToString(), false, Controller.ExpData.grpMME.sender, Controller.ExpData.grpMME.id);
            }
            if (Controller.ExpData.jumboMode() == ExperimentData.JumboModes.repeat)
            {
                ErrorMng.Log("Start Jumbo-Repeat", Brushes.Blue.Color);
                string jumboGroupID = (string)Controller.ExpData.grpMME.prms["groupID"];
                int jumboCycles = Convert.ToInt32(Controller.ExpData.grpMME.prms["cycles"]);
                realRun(jumboCycles, Controller.ExpData.grpMME.sender, Controller.ExpData.grpMME.id);
            }
        }
        public bool Interpreter(string json) // deal with incomming commands
        {
            //if (messenger != null) messenger.Send("<" + json + ">");
            //return true;
            //string js = File.ReadAllText(@"e:\VSprojects\set.mme");
            MMexec mme = JsonConvert.DeserializeObject<MMexec>(json);

            if (mme.sender.Equals("")) mme.sender = "none";
            if (mme.id == 0) mme.id = -1;
            switch (mme.cmd)
            {
                case ("phaseAdjust"):                   
                    if (Controller.ExpData.jumboMode() != ExperimentData.JumboModes.repeat) throw new Exception("Not active Jumbo repeat group command!");
                    double corr = Double.NaN;
                    if (mme.prms.ContainsKey("phaseCorrection"))
                    {                           
                        if (Double.TryParse((string)mme.prms["phaseCorrection"], out corr))
                        {
                            Log("<< phaseAdjust to " + corr.ToString("G6"));
                            controller.phaseStrobes.Correction(corr);
                        }
                        else corr = Double.NaN;
                    }                         
                    if (Double.IsNaN(corr)) Log("<< "+Convert.ToInt32(mme.prms["runID"]).ToString()+" shot, same cond.");
                    wait4adjust = false;                           
                    break;
                case ("shoot"): // jumbo-repeat in single shots
                    Controller.genOptions.ForceSeqCharge = true; Dictionary<string, object> scanDict = new Dictionary<string, object>();
                    Controller.ExpData.grpMME = mme.Clone();
                    if (Controller.ExpData.jumboMode() != ExperimentData.JumboModes.single) throw new Exception("Not active Jumbo-single group command!");                    
                    if (mme.prms.Count > 0)
                    {
                        tcMain.SelectedItem = tbSingle;
                        lbSetPrms.Items.Clear();
                        foreach (var pair in mme.prms)
                        {
                            string pn = pair.Key;
                            if (!Controller.sequenceData.Parameters.ContainsKey(pn)) ErrorMng.errorMsg("No such parameter <" + pn + ">", 4578);
                            Controller.sequenceData.Parameters[pn].Value = pair.Value;
                            ListBoxItem lbi = new ListBoxItem();
                            lbi.Content = string.Format(" {0} = {1:G8}", pair.Key, pair.Value);                           
                            lbSetPrms.Items.Add(lbi);
                        }                       
                    }
                    if (!SingleShot(scanDict)) { groupRun = GroupRun.none; }
                    wait4adjust = false;
                    break;
                case ("repeat"):
                    Controller.ExpData.grpMME = mme.Clone();
                    btnRun.Content = "Abort";
                    btnRun.Background = Brushes.LightCoral;
                    tcMain.SelectedIndex = 0; DoEvents();
                    tbExperimentRun.Text = (string)mme.prms["groupID"];
                    int cycles = -1; // default to infinity
                    if (mme.prms.ContainsKey("cycles")) cycles = Convert.ToInt32(mme.prms["cycles"]);
                    if (mme.sender.Equals("Axel-hub"))
                    {
                        if (mme.prms.ContainsKey("strobes")) controller.phaseStrobes.DoubleStrobe = Convert.ToInt32(mme.prms["strobes"]) == 2;
                        if (mme.prms.ContainsKey("strobe1")) controller.phaseStrobes.Strobe1 = Convert.ToDouble(mme.prms["strobe1"]);
                        if (mme.prms.ContainsKey("strobe2") && controller.phaseStrobes.DoubleStrobe) controller.phaseStrobes.Strobe2 = Convert.ToDouble(mme.prms["strobe2"]);
                        controller.phaseStrobes.Count = 0;
                    }
                    else if (mme.sender.Equals("Python"))
                    {
                        realRun(cycles);
                    }
                    ntbIterNumb.Value = cycles;
                    dispatcherTimer.Start();
                    break;
                case ("scan"):
                    Controller.ExpData.grpMME = mme.Clone();
                    btnScan.Content = "Abort";
                    btnScan.Background = Brushes.LightCoral;
                    tcMain.SelectedIndex = 1; DoEvents();
                    tbExperimentRun.Text = (string)mme.prms["groupID"];
                    cbParamsScan.Text = (string)mme.prms["param"];
                    tbFromScan.Text = Convert.ToDouble(mme.prms["from"]).ToString();
                    tbToScan.Text = Convert.ToDouble(mme.prms["to"]).ToString();
                    tbByScan.Text = Convert.ToDouble(mme.prms["by"]).ToString();
                    dispatcherTimer.Start();
                    break;
                case ("get.status"):
                    MMexec mme0 = Controller.InitialCommand(null);  mme0.cmd = "status"; 
                    remoteMsg.sendCommand(JsonConvert.SerializeObject(mme0, Formatting.Indented));
                    break;
                case ("set"):
                    foreach (var prm in mme.prms)
                    {
                        if (Controller.sequenceData.Parameters.ContainsKey(prm.Key)) Controller.sequenceData.Parameters[prm.Key].Value = prm.Value;
                        else ErrorMng.errorMsg("No such parameter <" + prm.Key + ">", 4579);                         
                    }
                    if (tcMain.SelectedItem == tbSingle)
                    { 
                        lbSetPrms.Items.Clear();
                        foreach (var pair in mme.prms)
                        {
                            string pn = pair.Key;                           
                            Controller.sequenceData.Parameters[pn].Value = pair.Value;
                            ListBoxItem lbi = new ListBoxItem();
                            lbi.Content = string.Format(" {0} = {1:G8}", pair.Key, pair.Value);
                            lbSetPrms.Items.Add(lbi);
                        }
                    }
                    break;
                case ("load"):
                    Controller.LoadSequenceFromPath((string)mme.prms["file"]);
                    break;
                case ("save"):
                    Controller.SaveSequenceToPath((string)mme.prms["file"]);
                    break;
                case ("abort"):
                    //Stop running
                    if (Convert.ToString(btnRun.Content) == "Abort") btnRun_Click(null, null);
                    else if (Convert.ToString(btnScan.Content) == "Abort") btnScan_Click(null, null);
                    dispatcherTimer.Start();
                    break;
            }
            return true;
        }
       
        private void cbHub_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            controller.SaveToggle(cbHub.SelectedIndex == 1);
            if (remoteMsg != null)
            {
                remoteMsg.Enabled = (cbHub.SelectedIndex == 2);
                Controller.SendDataRemotely = (cbHub.SelectedIndex == 2 || cbHub.SelectedIndex == 4);
            }
            if (btnRemote == null) return;
            if ((cbHub.SelectedIndex == 2) || (cbHub.SelectedIndex == 4))
            {
                btnRemote.Content = "Connect  ?->"; btnRemote.Background = Brushes.AliceBlue;
            }
            if (cbHub.SelectedIndex == 3) 
            {
                tiImageProcess.Visibility = System.Windows.Visibility.Visible;
                tcVisual.SelectedIndex = 2;
            }
            else
            {
                tiImageProcess.Visibility = System.Windows.Visibility.Hidden;
                tcVisual.SelectedIndex = 0;
            }

            if (cbHub.SelectedIndex == 2 || cbHub.SelectedIndex == 4) 
            {
                btnRemote.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                btnRemote.Visibility = System.Windows.Visibility.Hidden;
            }
        }

        private async void btnRemote_Click(object sender, RoutedEventArgs e)
        {
            if (cbHub.SelectedIndex == 2) // Axel-hub
            {
                remoteMsg.CheckConnection(true); 
                //{ ErrorMng.simpleMsg("Connected to Axel-hub"); }
                //else ErrorMng.errorMsg("Connection to Axel-hub failed !", 666);
            }
            if (cbHub.SelectedIndex == 3) // Mathematica
            {
                if (btnRemote.Content.Equals("Connect"))
                {
                    messenger = new TCPMessenger("127.0.0.1","127.0.0.1");
                    messenger.Remote += Interpreter;
                    Log("Awaiting remote requests");
                    btnRemote.Content = "Disconnect";
                    btnRemote.Background = Brushes.LightGreen;
                    try
                    {
                        await messenger.Run();
                    }
                    catch (Exception ex)
                    {
                        Log("Error with remote command: " + ex.Message);
                    }
                }
                else
                {
                    Log("Closing remote connection");
                    if (messenger != null) messenger.Close();
                    btnRemote.Content = "Connect";
                    btnRemote.Background = Brushes.LightBlue;
                }
            }
        }
        private void OnActiveComm(bool active, bool forced)
        {
            if (active) 
            {
                btnRemote.Content = "Connected  <->"; btnRemote.Background = Utils.ToSolidColorBrush("#FFBEFDD1");
                if (!remoteMsg.partnerPresent) ErrorMng.warningMsg("Conflicting active true status and parner not present");
            }
            else
            {
                btnRemote.Content = "Disconnected -X-"; btnRemote.Background = Brushes.LightYellow;
                if (remoteMsg.partnerPresent) 
                {
                    //if (!remoteMsg.CheckConnection()) 
                    if (cbHub.SelectedIndex == 2)
                        ErrorMng.warningMsg("The Axel-hub is opened, but hasn't been switched to remote");
                }
                else
                {
                    string ahApp = Directory.GetParent(Utils.basePath).Parent.FullName + "\\AxelSuite\\Axel-hub\\Axel-hub\\bin\\Axel-hub.exe";
                    if (!File.Exists(ahApp)) ahApp = File.ReadAllText(Utils.configPath + "axel-hub.bat"); // if the default location is faulty
                    if (!File.Exists(ahApp) || !forced) return;
                    ErrorMng.Status("Status:Axel-hub - not found! ...starting it", Brushes.DarkGreen.Color);
                    System.Diagnostics.Process.Start(ahApp, "-remote:MOTMaster2");
                    Thread.Sleep(1000);
                    if (remoteMsg.CheckConnection())
                        OnActiveComm(remoteMsg.Connected, false);
                    ErrorMng.Reset();
                }
            }                
        }
        private void frmMain_SourceInitialized(object sender, EventArgs e)
        {
            remoteMsg = new RemoteMessaging(); remoteMsg.Connect("Axel Hub");
            remoteMsg.Enabled = false;
            remoteMsg.OnActiveComm += OnActiveComm;
            remoteMsg.OnReceive += Interpreter;

          //  buildBtn_Click(btnBuild, null);
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            tbLogger.Document.Blocks.Clear();  
            //Log(ExtDevices["FlexDDS"].)
        }

        private void chkVerbatim_Checked(object sender, RoutedEventArgs e)
        {
            ErrorMng.Verbatim = chkVerbatim.IsChecked.Value;
        }       
        private void SetInterferometerParams(Dictionary<string, object> scanDict)
        {
            foreach (KeyValuePair<string,object> entry in scanDict)
            {
                NationalInstruments.Controls.NumericTextBoxDouble control = (NationalInstruments.Controls.NumericTextBoxDouble)tiExtDevices.FindName(entry.Key);
                if (control == null) continue;
                else if (Math.Abs(control.Value - (double)entry.Value) > 1e-10)
                { //Only update them if the value has changed.
                    control.Value = (double)entry.Value;                   
                }
                //TODO fix handling of warnings if ICE-BLocs are not connected               
            }
        }

        private void SetInterferometerParams(ObservableDictionary<string, Parameter> observableDictionary)
        {
            foreach (KeyValuePair<string, Parameter> entry in observableDictionary)
            {
                if (entry.Value.SequenceVariable) continue;
                else
                {
                    object control = tiExtDevices.FindName(entry.Key);
                    if (control == null) continue;
                    else
                    {
                        ((NationalInstruments.Controls.NumericTextBoxDouble)control).Value = Convert.ToDouble(entry.Value.Value);
                        //controller.StoreDCSParameter(entry.Key, entry.Value.Value);
                    }
                }
            }
        }

        #region multi-scan
        private void lstParams_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {           
            if (lstParams.SelectedItem == null) return;
            btnMScan.IsEnabled = false;
            MMscan mms = new MMscan();
            string ss = (string)((lstParams.SelectedItem as ListBoxItem).Content);
            mms.AsString = ss;
            cbParamsMScan.Text = mms.sParam;
            tbFromMScan.Text = mms.sFrom.ToString(Constants.ScanDataFormat);
            tbToMScan.Text = mms.sTo.ToString(Constants.ScanDataFormat);
            tbByMScan.Text = mms.sBy.ToString(Constants.ScanDataFormat);
            btnMScan.IsEnabled = true;
        }

        string ParamsMScan = null;
        private void tbFromMScan_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (Utils.isNull(lstParams) || Utils.isNull(btnMScan)) return;
            if (Utils.isNull(lstParams.SelectedItem) || !btnMScan.IsEnabled) return;
            MMscan mms = new MMscan();
            if (Utils.isNull(ParamsMScan)) mms.sParam = cbParamsMScan.Text;
            else mms.sParam = ParamsMScan;
            try
            {
                mms.sFrom = Convert.ToDouble(tbFromMScan.Text);
                mms.sTo = Convert.ToDouble(tbToMScan.Text);
                mms.sBy = Convert.ToDouble(tbByMScan.Text);                
            }
            catch (FormatException)
            {
                ErrorMng.errorMsg("Unable to convert to a Double.",1008);
            }               
            (lstParams.SelectedItem as ListBoxItem).Content = mms.AsString;
        }

        private void cbParamsMScan_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count>0) ParamsMScan = e.AddedItems[0] as string;
            tbFromMScan_TextChanged(null,null);
        }

        private void btnMScan_Click(object sender, RoutedEventArgs e)
        {
            var scanBox = sender as Button;
            switch ((string)scanBox.Content)
            {
                case "Multi Scan":
                    scanBox.Content = "Cancel";
                    scanBox.Background = Brushes.Coral;
                    Controller.ExpData.grpMME.Clear();
                    List<MMscan> mms = new List<MMscan>();
                    foreach (object ms in lstParams.Items)
                    {
                        mms.Add(new MMscan());
                        mms[mms.Count - 1].AsString = (string)(ms as ListBoxItem).Content;
                        if (!mms[mms.Count - 1].Check())
                        {
                            ErrorMng.errorMsg("scan values -> " + (string)(ms as ListBoxItem).Content, 1007); return;
                        }
                    }
                    realMultiScan(ref mms);
                    if(groupRun == GroupRun.multiScan) btnMScan_Click(scanBox, null);
                    break;
                case "Cancel":
                    groupRun = GroupRun.none;
                    controller.StopRunning();
                    controller.AutoLogging = false;
                    scanBox.Content = "Multi Scan";
                    scanBox.Background = Utils.ToSolidColorBrush("#FFF9E76B");
                    break;
                default:
                    break;
            }
        }

        private void realMultiScan(ref List<MMscan> mms)
        {
            if (lstParams.Items.Count == 0) return;
            Controller.ExpData.ExperimentName = tbExperimentRun.Text;
            if ((String.IsNullOrEmpty(Controller.ExpData.ExperimentName) || Controller.ExpData.ExperimentName.Equals("---")))
            {
                Controller.ExpData.ExperimentName = Utils.timeName();
                tbExperimentRun.Text = Controller.ExpData.ExperimentName;
            }
            Dictionary<string, object> scanDict = new Dictionary<string, object>();
            Controller.BatchNumber = 0;
            controller.AutoLogging = Check4Logging();
            Controller.ExpData.grpMME.Clear();
            //int multiCount = mms.Count;
            groupRun = GroupRun.multiScan;
            for (int i = 0; i < mms.Count - 1; i++)
            {
                mms[i].NextInChain = mms[i + 1];
            }
            foreach (MMscan ms in mms)
            {
                ms.Value = ms.sFrom;
            }
            bool saveLog = cbSaveAfterLoop.IsChecked.Value && cbSaveAfterLoop.IsEnabled && (Math.Abs(Controller.ExpData.axis) != 2);
            if (saveLog) Controller.ExpData.CreateMScanLogger(Utils.dataPath + Controller.ExpData.ExperimentName,  Controller.sequenceData, mms);
            bool scanExt = false; string scanPrm = "";
            for (int i = 0; i < mms.Count; i++)
            {
                if (Controller.sequenceData.Parameters.ContainsKey(mms[i].sParam))
                    if (Controller.sequenceData.Parameters[mms[i].sParam].IsLaser)
                        if (ExtFactors.IsScannable(mms[i].sParam))
                        {
                            scanExt = true; scanPrm = mms[i].sParam;
                        }
            }
            do
            {
                Thread.Sleep(10);
                DoEvents();
                lstValue.Items.Clear();
                foreach (MMscan ms in mms)
                {
                    lstValue.Items.Add(ms.Value.ToString("G6"));
                    Controller.SetParameter(ms.sParam, ms.Value);
                    scanDict[ms.sParam] = ms.Value;
                }
                SetInterferometerParams(scanDict);
                if (scanExt) ExtFactors.ScanIter(scanPrm, Controller.BatchNumber);
                if (!SingleShot(scanDict)) groupRun = GroupRun.none;
                controller.WaitForRunToFinish();
                controller.IncrementBatchNumber();

                if (saveLog)
                {
                    Controller.ExpData.LogNextShot(mms);
                    //HARDCODED Sleep to allow for DCS to update !!!!
                    if (mms[mms.Count - 1].isLastValue()) Thread.Sleep(5000);
                }
                if (groupRun != GroupRun.multiScan) break;
            }
            while (mms[0].Next());
            controller.AutoLogging = false;
            if (scanExt) ExtFactors.ScanIter(scanPrm, -1); // reset factors  
            tbExperimentRun.Text = "---";
            if (saveLog) Controller.ExpData.StopMScanLogger();
        }

        private void btnPlusMScan_Click(object sender, RoutedEventArgs e)
        {
            ListBoxItem lbi = new ListBoxItem(); 
            lbi.Content = "prm \t 0..10;0.1";
            lstParams.Items.Add(lbi);
        }

        private void btnMinusMScan_Click(object sender, RoutedEventArgs e)
        {
            if (Utils.isNull(lstParams.SelectedItem)) return;
            lstParams.Items.Remove(lstParams.SelectedItem);
        }

        private void btnUpMScan_Click(object sender, RoutedEventArgs e)
        {
            if (Utils.isNull(lstParams.SelectedItem)) return;
            int si = lstParams.SelectedIndex;
            if(si < 1) return;
            string ss = (string)(lstParams.Items[si-1] as ListBoxItem).Content;
            lstParams.Items.RemoveAt(si - 1);
            ListBoxItem lbi = new ListBoxItem(); lbi.Content = ss;
            lstParams.Items.Insert(si, lbi);
        }

        private void btnDownMScan_Click(object sender, RoutedEventArgs e)
        {
            if (Utils.isNull(lstParams.SelectedItem)) return;
            int si = lstParams.SelectedIndex; int cnt = lstParams.Items.Count;
            if (si > cnt-2) return;
            string ss = (string)(lstParams.Items[si + 1] as ListBoxItem).Content;
            lstParams.Items.RemoveAt(si + 1);
            ListBoxItem lbi = new ListBoxItem(); lbi.Content = ss;
            lstParams.Items.Insert(si, lbi);
        }
        #endregion multi-scan

        private void btnPulseEnable_Click(object sender, RoutedEventArgs e)
        {
            var check = sender as CheckBox;
            string name = check.Name;           
        }
        protected void OnChnChange(int chn)
        {
            switch (chn)
            {
                case 0: rbX.FontWeight = FontWeights.ExtraBold;
                    rbY.FontWeight = FontWeights.Normal;
                    break;
                case 1: rbX.FontWeight = FontWeights.Normal;
                    rbY.FontWeight = FontWeights.ExtraBold;
                    break;
                default: rbX.FontWeight = FontWeights.Bold;
                    rbY.FontWeight = FontWeights.Bold;
                    break;
            }
        }
 
        protected void OnRunStatus(bool running) // example of RunStatus event 
        {            
            //Log("running is " + running.ToString());
        }

        private void lstParams_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            List<MMscan> mms = new List<MMscan>();
            foreach (object ms in lstParams.Items)
            {
                mms.Add(new MMscan());
                mms[mms.Count - 1].AsString = (string)(ms as ListBoxItem).Content;
                if (!mms[mms.Count - 1].Check())
                {
                    ErrorMng.errorMsg("scan values -> " + (string)(ms as ListBoxItem).Content, 1007); return;
                }
            }
        }

        private void frmMain_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            switch (e.Key)
            {
                case (Key.O): 
                    if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                    {
                        LoadSequence_Click(mOpenSequence, e);
                    }
                    break;
                case (Key.S):    
                    if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                    {
                        SaveSequence_Click(sender, e);
                    }
                    break;
                case (Key.F4):
                    EditParameters_Click(sender, e);
                    break;
                case (Key.F1):
                    mWebHelp_Click(sender, e);
                    break;
            }
         }

        private void rbX_Checked(object sender, RoutedEventArgs e)
        {
            if (sender.Equals(rbX)) Controller.ExpData.axis = 0;
            if (sender.Equals(rbY)) Controller.ExpData.axis = 1;
            if (sender.Equals(rbXY)) Controller.ExpData.axis = 2;           
        }

        private void mnXleading_Click(object sender, RoutedEventArgs e)
        {
            if (sender == mnXleading)
            {
                mnXleading.IsChecked = true;
                mnYleading.IsChecked = false;
                if (Controller.sequenceData.Parameters.ContainsKey("swapAxes")) Controller.sequenceData.Parameters["swapAxes"].Value = 0;

            }
            if (sender == mnYleading)
            {
                mnXleading.IsChecked = false;
                mnYleading.IsChecked = true;
                if (Controller.sequenceData.Parameters.ContainsKey("swapAxes")) Controller.sequenceData.Parameters["swapAxes"].Value = 1;
            }
            Controller.ExpData.SwappedAxes = mnYleading.IsChecked;
        }

        private void mWebHelp_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("http://www.axelsuite.com/?pg=motmaster2/user/index.htm");
        }
        readonly string historyFile = Utils.configPath + "history.lst";
        List<string> history;
        private void mOpenRecent_Click(object sender, RoutedEventArgs e)
        {
            /*Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.FileName = ""; // Default file name
            dlg.DefaultExt = ".mme"; // Default file extension
            dlg.Filter = "MotMaster Exec (.mme)|*.mme"; // Filter files by extension

            // Show open file dialog box
            bool? result = dlg.ShowDialog();

            // Process open file dialog box results
            if (result != true) return;
            string inMME = System.IO.File.ReadAllText(dlg.FileName);
            Interpreter(inMME);*/
        }

        private void Image_MouseDown(object sender, MouseButtonEventArgs e)
        {
            About_Click(null, null);
        }

        private void tbdValue_ValueChanged(object sender, NationalInstruments.Controls.ValueChangedEventArgs<double> e)
        {
            if ((cbParamsManual.SelectedItem == null) || Convert.ToInt32(tbdValue.Tag) == 1) return;
            if (sender == tbdValue)
            {
                tbdValue.Tag = 1;
                tbdValue2.Value = tbdValue.Value;
                tbdValue3.Value = tbdValue.Value;
                tbdValue.Tag = 0;
            }
            if (sender == tbdValue2)
            {
                tbdValue.Tag = 1;
                tbdValue.Value = tbdValue2.Value;
                tbdValue3.Value = tbdValue2.Value;
                tbdValue.Tag = 0;
            }
            if (sender == tbdValue3)
            {
                tbdValue.Tag = 1;
                tbdValue.Value = tbdValue3.Value;
                tbdValue2.Value = tbdValue3.Value;
                tbdValue.Tag = 0;
            }

            string sPrm = cbParamsManual.SelectedItem.ToString();
            if (!Controller.sequenceData.Parameters.ContainsKey(sPrm)) return;
            Parameter param = Controller.sequenceData.Parameters[sPrm];
            if (param.Value is int)
            {
                param.Value = (int)tbdValue.Value;
            }
            else if (param.Value is double)
            {
                param.Value = tbdValue.Value;
            }
        }

        private void mOpenRecent_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!mOpenRecent.Visibility.Equals(Visibility.Visible)) return;
            mOpenRecent.Items.Clear();
            foreach (string itm in history)
            {
                MenuItem mi = new MenuItem();
                mi.Header = itm; mi.Click += new System.Windows.RoutedEventHandler(LoadSequence_Click);
                mOpenRecent.Items.Add(mi);
            }
        }

        private void cbParamsScan_DropDownOpened(object sender, EventArgs e)
        {
            string last = cbParamsScan.Text;
            cbParamsScan.Items.Clear();
            //cbParamsScan.Items.Add(ramanPhase);
            foreach (string param in Controller.sequenceData.ScannableParams())
                if (param != "") cbParamsScan.Items.Add(param);
            cbParamsScan.Text = last;
        }
    }
}