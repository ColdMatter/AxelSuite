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
    public struct FringeParams  // fringes(phi) = cos(period * phi + phase) + offset
    {
        public double period; // in mg per rad
        public double phase;  // the MEMS and the interferometer are not entirely paralel
        public double offset; // phase offset [rad]
    }
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class scanClass : UserControl
    {
        private double realSampling;
        private string ArrangedPartner = ""; //MOTMaster2";Axel Probe"; 

        TimeSpan totalTime, currentTime;
        public DispatcherTimer dTimer;

        public scanClass()
        {
            InitializeComponent();
            
            dTimer = new DispatcherTimer(DispatcherPriority.Send);
            dTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dTimer.Interval = new TimeSpan(0, 0, 1);

            _remoteMode = RemoteMode.Disconnected;
            tabControl.SelectedIndex = 0;
            jumboButton = false;
            //Status("Ready to go ");
        }
        GeneralOptions genOptions = null;
        public ScanModes scanModes = null;
        public void InitOptions(ref GeneralOptions _genOptions, ref ScanModes _scanModes)
        {
            genOptions = _genOptions;
            scanModes = _scanModes;
            SetSamplingRate(scanModes.SamplingFreq);
            if (scanModes.TimeLimitMode) cbTimeEndless.SelectedIndex = 1;
            else cbTimeEndless.SelectedIndex = 0;
            tbTimeLimit.Text = scanModes.TimeLimit.ToString();
            if (scanModes.SizeLimitMode) cbSizeEndless.SelectedIndex = 1;
            else cbSizeEndless.SelectedIndex = 0;
            tbBifferSize.Text = scanModes.SizeLimit.ToString();           
        }

        public void UpdateModes()
        {
            scanModes.SamplingFreq = (int)Math.Round(1 / GetSamplingPeriod());
            scanModes.TimeLimitMode = cbTimeEndless.SelectedIndex == 1;
            scanModes.TimeLimit = Convert.ToInt32(tbTimeLimit.Text);
            scanModes.SizeLimitMode = cbSizeEndless.SelectedIndex == 1;
            scanModes.SizeLimit = Convert.ToInt32(tbBifferSize.Text);
        }

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
            tbSamplingRate.Text = rate.ToString();
        }

        public void SetFringeParams(FringeParams fp)
        {
            lbActivity.Content = "Fringe Prm: per= " + fp.period.ToString() + "; phase= " + fp.phase.ToString() + "; off= " + fp.offset.ToString();
        }

        public void OnRealSampling(double _realSampling) 
        {
            realSampling = _realSampling;
            groupDigit.Header = " Conversion rate (" + realSampling.ToString() + " [Hz])"; 
        }  

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

        public bool EndlessMode()
        {
            switch (tabControl.SelectedIndex) 
            {
                case 0: return (cbTimeEndless.SelectedIndex == 1);
                case 1: return (cbSizeEndless.SelectedIndex == 1);
                case 2: return true;
                default: return false;
            }
        }

        RemoteMode _remoteMode = RemoteMode.Disconnected;
        public RemoteMode remoteMode
        {
            get { return _remoteMode; }
            set
            {
                lbMode.Content = "Oper.Mode: " + value.ToString();
                if (value == RemoteMode.Simple_Repeat || value == RemoteMode.Simple_Scan || value == RemoteMode.Disconnected) bbtnStart.Visibility = System.Windows.Visibility.Collapsed;
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

        public RemoteMessaging remote { get; set; }

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

        public delegate void StartHandler(bool jumbo, bool down, double period, int sizeLimit);
        public event StartHandler OnStart;

        protected void StartEvent(bool jumbo, bool down, double period, int sizeLimit)
        {
            if(OnStart != null) OnStart(jumbo, down, period, sizeLimit);
        }

        public delegate void RemoteHandler(string msg);
        public event RemoteHandler OnRemote;

        protected void RemoteEvent(string msg)
        {
            if (OnRemote != null) OnRemote(msg);
        }

        public delegate void FileRefHandler(string FN, bool stats);
        public event FileRefHandler OnFileRef;

        protected void FileRefEvent(string FN, bool stats)
        {
            if (OnFileRef != null) OnFileRef(FN, stats);
        }

        public delegate void LogHandler(string txt, Color? clr = null);
        public event LogHandler OnLog;

        protected void LogEvent(string txt, Color? clr = null)
        {
            if (OnLog != null) OnLog(txt, clr);
        }

        protected void OnAsyncSend(bool OK, string json2send)
        {
            if (!OK) LogEvent("Error sending -> " + json2send, Brushes.Red.Color);
            //else LogEvent("sending OK -> " + json2send);
        }

        private void Image_MouseDown(object sender, MouseButtonEventArgs e)
        {
            string ver = System.Diagnostics.FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion.ToString();
            MessageBox.Show("           Axel Hub v" + ver + "\n         by Teodor Krastev \nfor Imperial College, London, UK\n\n   visit: http://axelsuite.com", "About");
        }

        public double GetSamplingPeriod()
        {
            double freq = 1; // in seconds
            double vl = 0;
            switch (cbSamplingMode.SelectedIndex)
            {
                case 0: if (!double.TryParse(tbSamplingRate.Text, out vl)) throw new Exception("Not number for digit. value");  // Hz
                        freq = vl;
                        break;
                case 1: if (!double.TryParse(tbSamplingRate.Text, out vl)) throw new Exception("Not number for digit. value");  // kHz
                        freq = 1000 * vl;
                        break;
                case 2: if (!double.TryParse(tbSamplingRate.Text, out vl)) throw new Exception("Not number for digit. value");  // s
                        freq = 1 / vl;
                        break;
                case 3: if (!double.TryParse(tbSamplingRate.Text, out vl)) throw new Exception("Not number for digit. value");  // ms
                        freq = 1000 / vl;
                        break;
                case 4: throw new Exception("Not implemented yet");                    
            }
            return 1 / Utils.EnsureRange(freq, 0.001, 1e6);
        }

        public int GetBufferSize()
        {
            int sizeLimit = 0; // [s]
            double Limit = 1;
            switch (tabControl.SelectedIndex)
            {
                case 0: case 2:
                    if (!double.TryParse(tbTimeLimit.Text, out Limit)) throw new Exception("Not number for Time limit");
                    sizeLimit = (int)(Limit / GetSamplingPeriod());
                    break;
                case 1: 
                    if (!double.TryParse(tbBifferSize.Text, out Limit)) throw new Exception("Not number for Buffer size");
                    sizeLimit = (int)Limit;
                    break;
            }
            return Utils.EnsureRange(sizeLimit, 2, 1000000);
        }

        private bool _jumboButton = true;
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
                    bbtnStart.TrueContent = "Cancel";
                    bbtnStart.FalseContent = "Start";
                    bbtnStart.FalseBrush = Brushes.LightBlue;
                    bbtnStart.TrueBrush = Brushes.Orange;
                }               
                bbtnStart.Value = false; 
            }
        }
        
        public void bbtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (OnStart == null) return;
            bbtnStart.Value = !bbtnStart.Value;
            Running = bbtnStart.Value;
            if (!bbtnStart.Value) Abort(true);

            double period = GetSamplingPeriod(); // sampling rate in sec
                        
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

        public void Abort(bool local) // the origin of Abort is (local) or (remote abort OR end sequence)
        {
            bool jumbo = (remoteMode == RemoteMode.Jumbo_Scan) || (remoteMode == RemoteMode.Jumbo_Repeat);
            remoteMode = RemoteMode.Ready_To_Remote;
            jumboButton = (tabControl.SelectedIndex == 2); // remote tab
            Running = false;
            bbtnStart.Value = false;            
            if (local)
            {
                MMexec mme = new MMexec();
                SendJson(mme.Abort("Axel-hub"));
            }
            else OnStart(jumbo, false, 0, 0);
        }

         private void tabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
         {
             if (remote != null) remote.Enabled = (tabControl.SelectedIndex == 2);
             if (tabControl.SelectedIndex == 2) 
             {
                 jumboButton = remote.CheckConnection(true);
             }                     
             else
             {
                 bbtnStart.Visibility = System.Windows.Visibility.Visible;
                 Status("Ready to go ");
                 jumboButton = false;
             }
             ActiveRemote(jumboButton);
         }

         public delegate void ActiveRemoteHandler(bool active);
         public event ActiveRemoteHandler OnActiveRemote;
         protected void ActiveRemote(bool active)
         {
             if (OnActiveRemote != null) OnActiveRemote(active);
         }

         private void OnActiveComm(bool active, bool forced)
         {
             if (tabControl.SelectedIndex != 2) return;
             ledRemote.Value = active;
             if (active)
             {
                 Status("Ready to remote <->");
                 bbtnStart.Visibility = System.Windows.Visibility.Visible;
                 jumboButton = true; 
                 remoteMode = RemoteMode.Ready_To_Remote;
              }
             else
             {
                 Status("Disconnected -X-");
                 bbtnStart.Visibility = System.Windows.Visibility.Hidden;
                 remoteMode = RemoteMode.Disconnected;
             }
             ActiveRemote(active);
         }

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
             if(!callingPartner.Equals("")) partner = callingPartner; //highest priority             
             if(partner == "")
                 switch (computerName) 
                 {
                     case "NAVIGATOR-ANAL": partner = "MOTMaster2"; break;
                     case "DESKTOP-U334RMA": partner = "Axel Probe"; break; //"MOTMaster2"
                 }
             remote = new RemoteMessaging(partner); 
             remote.Enabled = false;
             remote.OnReceive += new RemoteMessaging.ReceiveHandler(OnReceive);
             remote.OnActiveComm += new RemoteMessaging.ActiveCommHandler(OnActiveComm);
             remote.OnAsyncSent += new RemoteMessaging.AsyncSentHandler(OnAsyncSend);

             if(bRemote) tabControl.SelectedIndex = 2;
         }

         private void UserControl_Initialized(object sender, EventArgs e)
         {
         }
     }
}
