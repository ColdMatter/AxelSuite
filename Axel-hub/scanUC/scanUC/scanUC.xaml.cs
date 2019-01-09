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
using RemoteMessagingNS;
using OptionsTypeNS;
using UtilsNS;

namespace scanHub
{    
    public enum RemoteMode
    {
        Jumbo_Scan, // scan as part of Jumbo Run
        Jumbo_Repeat, // repeat as part of Jumbo Run
        Simple_Scan, // scan initiated by MM
        Simple_Repeat, // repeat initiated by MM
        Ready_To_Remote,
        Disconnected
    }
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

            tabControl.SelectedIndex = 0;
            jumboButton = false;
            //Status("Ready to go ");
        }
        GeneralOptions genOptions = null;
        Modes genModes = null;
        public void InitOptions(ref GeneralOptions _genOptions, ref Modes _genModes)
        {
            genOptions = _genOptions;

            SetSamplingRate(_genModes.SamplingFreq);
            if (_genModes.TimeLimitMode) cbTimeEndless.SelectedIndex = 1;
            else cbTimeEndless.SelectedIndex = 0;
            tbTimeLimit.Text = _genModes.TimeLimit.ToString();
            if (_genModes.SizeLimitMode) cbSizeEndless.SelectedIndex = 1;
            else cbSizeEndless.SelectedIndex = 0;
            tbBifferSize.Text = _genModes.SizeLimit.ToString();
            genModes = _genModes;
        }

        public void UpdateModes()
        {
            genModes.SamplingFreq = (int)Math.Round(1 / GetSamplingPeriod());
            genModes.TimeLimitMode = cbTimeEndless.SelectedIndex == 1;
            genModes.TimeLimit = Convert.ToInt32(tbTimeLimit.Text);
            genModes.SizeLimitMode = cbSizeEndless.SelectedIndex == 1;
            genModes.SizeLimit = Convert.ToInt32(tbBifferSize.Text);
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

        public bool Running
        {
            get { return (bool)GetValue(RunningProperty); }
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
                SetValue(RunningProperty, value);
            }
        }

        // Using a DependencyProperty as the backing store for Running.  
        public static readonly DependencyProperty RunningProperty
            = DependencyProperty.Register(
                  "Running",
                  typeof(bool),
                  typeof(scanClass),
                  new PropertyMetadata(false)
              );

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

        public RemoteMode remoteMode
        {
            get { return (RemoteMode)GetValue(remoteModeProperty); }
            set
            {
                lbMode.Content = "Oper.Mode: " + value.ToString();
                if (value == RemoteMode.Simple_Repeat || value == RemoteMode.Simple_Scan || value == RemoteMode.Disconnected) bbtnStart.Visibility = System.Windows.Visibility.Collapsed;
                else bbtnStart.Visibility = System.Windows.Visibility.Visible;
                SetValue(remoteModeProperty, value);
                tabControl.IsEnabled = (value == RemoteMode.Ready_To_Remote || value == RemoteMode.Disconnected);
            }
        }
        public static readonly DependencyProperty remoteModeProperty
            = DependencyProperty.Register(
                  "remoteMode",
                  typeof(RemoteMode),
                  typeof(scanClass),
                  new PropertyMetadata(RemoteMode.Disconnected)
              );

        public RemoteMessagingNS.RemoteMessaging remote
        {
            get { return (RemoteMessagingNS.RemoteMessaging)GetValue(remoteProperty); }
            set { SetValue(remoteProperty, value); }
        }
        public static readonly DependencyProperty remoteProperty
            = DependencyProperty.Register(
                  "remote",
                  typeof(RemoteMessagingNS.RemoteMessaging),
                  typeof(scanClass),
                  new PropertyMetadata(null)
              );

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
            MessageBox.Show("           Axel Hub v1.4 \n         by Teodor Krastev \nfor Imperial College, London, UK\n\n   visit: http://axelsuite.com", "About");
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
            return 1 / freq;
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
            return sizeLimit;

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
                    bbtnStart.FalseBrush = Brushes.LightCyan;
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
            OnStart(jumbo, false, 0, 0);
            if (local)
            {
                RemoteMessagingNS.MMexec mme = new RemoteMessagingNS.MMexec();
                SendJson(mme.Abort("Axel-hub"));
            }
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
         }

         private void UserControl_Loaded(object sender, RoutedEventArgs e)
         {
             string callingPartner = ""; bool bRemote = false;
             string[] args = Environment.GetCommandLineArgs();
             if (args.Length > 1)
             {
                 string ss = args[1];
                 Console.WriteLine("Command line argument: " + ss);
                 bRemote = ss.Contains("-remote");
                 if (bRemote)
                 {
                     string[] sa = ss.Split(':');
                     if (sa.Length == 2) callingPartner = sa[1];
                 }
             }
             string computerName = (string)System.Environment.GetEnvironmentVariables()["COMPUTERNAME"];
             string partner = ArrangedPartner;
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
     }
}
