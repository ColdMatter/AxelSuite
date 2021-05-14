using System;
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
using System.Diagnostics;
using OptionsNS;
using UtilsNS;
using System.Reflection;

namespace Axel_hub
{    
    /// <summary>
    /// fringes(phi) = cos(period * phi + phase) + offset
    /// </summary>
    public struct FringeParams  
    {
        /// <summary>
        /// in mg per rad
        /// </summary>
        public double period; 
        /// <summary>
        /// the MEMS and the interferometer are not entirely paralel
        /// </summary>
        public double phase;  
        /// <summary>
        /// phase offset [rad]
        /// </summary>
        public double offset; 
    }
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class scanClass : UserControl
    {
        private string ArrangedPartner = ""; //MOTMaster2";Axel Probe"; 

        TimeSpan totalTime, currentTime;
        public DispatcherTimer dTimer; // progress visualization

        /// <summary>
        /// Class constructor - set defaults
        /// </summary>
        public scanClass()
        {
            InitializeComponent();
            
            dTimer = new DispatcherTimer(DispatcherPriority.Send);
            dTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dTimer.Interval = new TimeSpan(0, 0, 1);

            remote = new RemoteMessaging();
            remote.Enabled = false;
            remote.OnReceive += new RemoteMessaging.ReceiveHandler(OnReceive);
            remote.OnActiveComm += new RemoteMessaging.ActiveCommHandler(OnActiveComm);
            remote.OnAsyncSent += new RemoteMessaging.AsyncSentHandler(OnAsyncSend);

            _remoteMode = RemoteMode.Disconnected;
            tabControl.SelectedIndex = 0;
            jumboButton = false;
            //Status("Ready to go ");
        }
        GeneralOptions genOptions = null;
        public ScanModes scanModes = null;

        /// <summary>
        /// Initialize - set genOptions
        /// </summary>
        /// <param name="_genOptions">From options windows</param>
        /// <param name="_scanModes">From saved last used modes</param>
        public void InitOptions(ref GeneralOptions _genOptions, ref ScanModes _scanModes)
        {
            genOptions = _genOptions;
            scanModes = _scanModes;
            SetSamplingRate(scanModes.SamplingFreq);
            if (scanModes.TimeLimitMode) cbTimeEndless.SelectedIndex = 1;
            else cbTimeEndless.SelectedIndex = 0;
            numTimeLimit.Value = scanModes.TimeLimit;           
            numBifferSize.Value = scanModes.SizeLimit;           
        }

        /// <summary>
        /// Set internal from visual modes
        /// </summary>
        public void UpdateModes()
        {
            scanModes.SamplingFreq = (int)GetSamplingFreq(true);
            scanModes.TimeLimitMode = cbTimeEndless.SelectedIndex == 1;
            scanModes.TimeLimit = numTimeLimit.Value;
            scanModes.SizeLimit =numBifferSize.Value;
        }

        /// <summary>
        /// Wrapper of remote.sendCommand
        /// </summary>
        /// <param name="json"></param>
        /// <param name="async"></param>
        /// <returns></returns>
        public bool SendJson(string json, bool async = false)
        {
            int delay = 0;
            if (async) delay = 100;
            return remote.sendCommand(json, delay);
        }
        
        public void SetActivity(string act)
        {
            lbActivity.Content = "Activity: " + act;
        }

        public void SetSamplingRate(int rate) // rate is in Hz
        {
            cbSamplingMode.SelectedIndex = 0;
            numSamplingRate.Value = rate;
        }
        /// <summary>
        /// Show fringes params
        /// </summary>
        /// <param name="fp">fringes params</param>
        public void SetFringeParams(FringeParams fp)
        {
            lbActivity.Content = "Fringe Prm: per= " + fp.period.ToString() + "; phase= " + fp.phase.ToString() + "; off= " + fp.offset.ToString();
        }

        /// <summary>
        /// Shows visual progress of ADC24 acquisition
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            double progress = 0;
            if (totalTime.TotalSeconds > 0) // finite
            {
                if (currentTime.TotalSeconds < totalTime.TotalSeconds) currentTime = currentTime.Add(new TimeSpan(0, 0, 1));   
            }
            else currentTime = currentTime.Add(new TimeSpan(0, 0, 1));          
            lbTimeElapsed.Content = ((int)(currentTime.TotalSeconds)).ToString()+" [s]";
            if ((lbTimeLeft.Visibility == System.Windows.Visibility.Visible) && (totalTime.TotalSeconds > 0))
            {
                lbTimeLeft.Content = (currentTime.TotalSeconds - totalTime.TotalSeconds).ToString() + " [s]";
                progress = 100 * (currentTime.TotalSeconds / totalTime.TotalSeconds);
            }
            progressBar.Value = progress;
        }
        private void Status(string sts)
        {
            if (Utils.isNull(lblStatus)) return;
            lblStatus.Content = ">> " + sts;
        }
        private bool _Running;
        /// <summary>
        /// Some visual adjustments when ADC24 starts/stops
        /// </summary>
        public bool Running
        {
            get { return _Running; }
            set
            {
                bbtnStart.Value = value;               
                if (value)
                {
                    Status("Running....");
                    dTimer.Start();
                    currentTime = new TimeSpan(0, 0, 0);
                }
                else
                {
                    Status("Ready for another go");
                    dTimer.Stop();
                    lbTimeLeft.Visibility = System.Windows.Visibility.Visible;
                    totalTime = new TimeSpan(0, 0, 0);
                    currentTime = new TimeSpan(0, 0, 0);
                    lbTimeElapsed.Content = "...[s]";
                    lbTimeLeft.Content = "...[s]";
                    progressBar.Value = 0;
                }                
                _Running = value;
            }
        }
        /// <summary>
        /// Continuois mode
        /// </summary>
        /// <returns></returns>
        public bool EndlessMode()
        {
            switch (tabControl.SelectedIndex) 
            {
                case 0: return (cbTimeEndless.SelectedIndex == 1);
                case 1: return true;
                default: return false;
            }
        }

        RemoteMode _remoteMode = RemoteMode.Disconnected;
        /// <summary>
        /// Current remode mode - defines the context next group shots
        /// </summary>
        public RemoteMode remoteMode
        {
            get { return _remoteMode; }
            set
            {
                lbMode.Content = "Oper.Mode: " + value.ToString();
                if (value == RemoteMode.Simple_Repeat || value == RemoteMode.Simple_Scan) bbtnStart.Visibility = System.Windows.Visibility.Collapsed;
                else bbtnStart.Visibility = System.Windows.Visibility.Visible;
                RemoteMode tempRemoteMode = _remoteMode; _remoteMode = value; scanModes.remoteMode = value;
                if (!tempRemoteMode.Equals(value)) RemoteModeEvent(tempRemoteMode, value);
                tabControl.IsEnabled = (value == RemoteMode.Ready_To_Remote || value == RemoteMode.Disconnected);               
            }
        }
        public delegate void RemoteModeHandler(RemoteMode oldMode, RemoteMode newMode);
        public event RemoteModeHandler OnRemoteMode;
        protected void RemoteModeEvent(RemoteMode oldMode, RemoteMode newMode)
        {
            if (!Utils.isNull(OnRemoteMode)) OnRemoteMode(oldMode, newMode);
        }

        public RemoteMessaging remote;
        /// <summary>
        /// Incomming from MM2/Axel-probe message
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        private bool OnReceive(string message)
        {
            try
            {
                bool back = true;
                RemoteEvent(message);
                return back;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Start/Stop group operation wity ADC24 params
        /// </summary>
        /// <param name="jumbo"></param>
        /// <param name="down"></param>
        /// <param name="period"></param>
        /// <param name="sizeLimit"></param>
        public delegate void StartHandler(bool jumbo, bool down, double period, int sizeLimit);
        public event StartHandler OnStart;
        protected void StartEvent(bool jumbo, bool down, double period, int sizeLimit)
        {
            if(OnStart != null) OnStart(jumbo, down, period, sizeLimit);
        }

        /// <summary>
        /// Incomming message event thingy
        /// </summary>
        /// <param name="msg"></param>
        public delegate void RemoteHandler(string msg);
        public event RemoteHandler OnRemote;
        protected void RemoteEvent(string msg)
        {
            if (OnRemote != null) OnRemote(msg);
        }

        /// <summary>
        /// Log into left text box
        /// </summary>
        /// <param name="txt"></param>
        /// <param name="clr"></param>
        public delegate void LogHandler(string txt, SolidColorBrush clr = null);
        public event LogHandler OnLog;
        protected void LogEvent(string txt, SolidColorBrush clr = null)
        {
            if (OnLog != null) OnLog(txt, clr);
        }

        /// <summary>
        /// Report sent message in log
        /// </summary>
        /// <param name="OK"></param>
        /// <param name="json2send"></param>
        protected void OnAsyncSend(bool OK, string json2send)
        {
            if (!OK) LogEvent("Error sending -> " + json2send, Brushes.Red);
            //else LogEvent("sending OK -> " + json2send);
        }

        private void Image_MouseDown(object sender, MouseButtonEventArgs e)
        {
            MessageBox.Show("           Axel Hub v" + Utils.getAppFileVersion + "\n         by Teodor Krastev \nfor Imperial College, London, UK\n\n   visit: http://axelsuite.com", "About");
        }

        /// <summary>
        /// Get the sampling period regardless the units
        /// </summary>
        /// <returns>[s]</returns>
        public double GetSamplingFreq(bool actual)
        {
            if (Utils.isNull(numSamplingRate)) return Double.NaN;
            double freq = 1; // in seconds
            switch (cbSamplingMode.SelectedIndex)
            {
                case 0: freq = numSamplingRate.Value; // Hz                       
                        break;
                case 1: freq = 1000 * numSamplingRate.Value; // kHz                       
                        break;
                case 2: freq = 1 / numSamplingRate.Value; // s                        
                        break;
                case 3: freq = 1000 / numSamplingRate.Value;  // ms                       
                        break;
                case 4: throw new Exception("Not implemented yet");                    
            }
            if (actual) freq = MEMSmodel.RealConvRate(freq);
            return Utils.EnsureRange(freq,MEMSmodel.FixConvRate[41], MEMSmodel.FixConvRate[0]);
        }

        /// <summary>
        /// Get the buffer size depending of settings
        /// </summary>
        /// <returns></returns>
        public int GetBufferSize()
        {
            if (Utils.isNull(tcTimeBuffer)) return 1;
            int sizeLimit = 0; // [s]
            switch (tcTimeBuffer.SelectedIndex)
            {
                case 0:                     
                    sizeLimit = (int)(numTimeLimit.Value * GetSamplingFreq(true));
                    break;
                case 1: 
                    sizeLimit = numBifferSize.Value;
                    break;
            }
            return Utils.EnsureRange(sizeLimit, 2, 1000000);
        }

        private bool _jumboButton = true;
        /// <summary>
        /// Set the main scan button to Jumbo mode
        /// </summary>
        private bool jumboButton
        {
            get {return _jumboButton;}
            set
            {
                if (value.Equals(_jumboButton)) return; 
                _jumboButton = value;
                if (value)
                {
                    bbtnStart.TrueContent = "Jumbo Stop";
                    bbtnStart.FalseContent = "Jumbo Run";
                    bbtnStart.FalseBrush = Brushes.LightGreen;
                    bbtnStart.TrueBrush = Brushes.Coral;
                }
                else
                {
                    bbtnStart.TrueContent = "Cancel MEMS";
                    bbtnStart.FalseContent = "Start MEMS";
                    bbtnStart.FalseBrush = Brushes.LightBlue;
                    bbtnStart.TrueBrush = Brushes.Orange;
                }               
                bbtnStart.Value = false; 
            }
        }
        
        /// <summary>
        /// Start something - ADC24 or Jumbo scan/repeat
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void bbtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (OnStart == null) return;
            bbtnStart.Value = !bbtnStart.Value;
            Running = bbtnStart.Value;
            if (!bbtnStart.Value) Abort(true); // send out abort

            double period = 1 / GetSamplingFreq(true); // sampling rate in sec
                        
            if (EndlessMode())
            {
                lbTimeLeft.Visibility = System.Windows.Visibility.Hidden;
                totalTime = new TimeSpan(0, 0, 0);
                currentTime = new TimeSpan(0, 0, 0);
            }
            else
            {
                lbTimeLeft.Visibility = System.Windows.Visibility.Visible;
                totalTime = new TimeSpan(0, 0, (int)(GetBufferSize() * period));
                currentTime = new TimeSpan(0, 0, 0);
            }
            OnStart(jumboButton, Running, period, GetBufferSize()); // the last three are valid only in non-jumbo mode with down = true
        }

        /// <summary>
        /// Abort current operation
        /// </summary>
        /// <param name="local">the origin of Abort is (local) or (remote abort OR end sequence)</param>
        public void Abort(bool local)  
        {
            bool jumbo = (remoteMode == RemoteMode.Jumbo_Scan) || (remoteMode == RemoteMode.Jumbo_Repeat);
            if (remote.Connected) remoteMode = RemoteMode.Ready_To_Remote;
            else remoteMode = RemoteMode.Disconnected;
            jumboButton = (tabControl.SelectedIndex == 1); // remote tab
            Running = false;
            bbtnStart.Value = false;            
            if (local && remote.Connected)
            {
                MMexec mme = new MMexec();
                SendJson(mme.Abort("Axel-hub"));
            }
            else OnStart(jumbo, false, 0, 0);
            theTime.stopTime();
         }
         
        /// <summary>
        /// Turn the tab of the ADC24 setting
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
         private void tabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
         {
             if (remote != null) remote.Enabled = (tabControl.SelectedIndex == 1);
             if (tabControl.SelectedIndex == 1) // remote
             {
                 jumboButton = remote.CheckConnection(true);
             }                     
             else // MEMS
             {
                 bbtnStart.Visibility = System.Windows.Visibility.Visible;
                 Status("Ready to go ");
                 jumboButton = false;
                 _remoteMode = RemoteMode.Disconnected;
                 cbSamplingMode_SelectionChanged(sender, e);
            }
             ActiveRemote(jumboButton);
         }
         
         public delegate void ActiveRemoteHandler(bool active);
         public event ActiveRemoteHandler OnActiveRemote;
         protected void ActiveRemote(bool active)
         {
             if (OnActiveRemote != null) OnActiveRemote(active);
         }

         /// <summary>
         /// Event when the connection goes ON/OFF
         /// </summary>
         /// <param name="active"></param>
         private void OnActiveComm(bool active, bool forced)
         {
             if (tabControl.SelectedIndex != 1) return;
             ledRemote.Value = active;
             if (active)
             {
                 Status("Ready to remote <->");
                 //bbtnStart.Visibility = System.Windows.Visibility.Visible;
                 jumboButton = true; 
                 remoteMode = RemoteMode.Ready_To_Remote;
              }
             else
             {
                 Status("Disconnected -X-");
                 //bbtnStart.Visibility = System.Windows.Visibility.Hidden;
                 remoteMode = RemoteMode.Disconnected;
             }
             ActiveRemote(active);
         }

        private void numTimeLimit_ValueChanged(object sender, NationalInstruments.Controls.ValueChangedEventArgs<double> e)
        {
            groupDigit.Header = "MEMS s.rate (" + GetSamplingFreq(true).ToString() + " [Hz]) / buffer (" + GetBufferSize().ToString() + ")       ";
        }

        private void cbSamplingMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            numTimeLimit_ValueChanged(sender, null);
        }

        private void numBifferSize_ValueChanged(object sender, NationalInstruments.Controls.ValueChangedEventArgs<int> e)
        {
            numTimeLimit_ValueChanged(sender, null);
        }

        /// <summary>
        /// Some secondary to contructor initialilzations
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
         {
             string callingPartner = ""; bool bRemote = false;
             string[] args = Environment.GetCommandLineArgs();
             if(args.Length > 1) Console.Write("Command line arguments: ");
             for (int i = 1; i < args.Length; i++)  
             {
                 string ss = args[i];
                 Console.Write(ss + ",");
                 bRemote = ss.Contains("-remote");
                 if (bRemote)
                 {
                     string[] sa = ss.Split(':');
                     if (sa.Length == 2) callingPartner = sa[1];
                 }
             }
             string computerName = (string)System.Environment.GetEnvironmentVariables()["COMPUTERNAME"];
             string partner = ArrangedPartner; // default 
             if (!callingPartner.Equals("")) partner = callingPartner; //highest priority             
             if (partner == "")
                 switch (computerName) 
                 {
                     case "NAVIGATOR-ANAL":
                     case "DESKTOP-IHEEQUU": partner = "MOTMaster2"; break;
                     case "DESKTOP-U334RMA": partner = "Axel Probe"; break; //"MOTMaster2"
                     default: partner = "Axel Probe"; break;
                 }
             remote.Connect(partner);

             if (bRemote) tabControl.SelectedIndex = 1;
         }
     }
}
