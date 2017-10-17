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
//using RemoteMessagingNS;
using UtilsNS;

namespace scanHub
{    
    public enum RemoteMode
    {
        Jumbo_Scan, // scan as part of Jumbo Run
        Jumbo_Repeat, // repeat as part of Jumbo Run
        Simple_Scan, // scan initiated by MM
        Simple_Repeat, // repeat initiated by MM
        Free // ungrouped data (default state)
    }
    public struct FringeParams  // = cos(period * t + phase) + offset
    {
        public double period;
        public double phase;
        public double offset;
    }
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class scanClass : UserControl
    {
        public delegate void NewMessageDelegate(string NewMessage);
        
        private double realSampling;

        TimeSpan totalTime, currentTime;
        public DispatcherTimer dTimer;
        Stopwatch sw;

        public scanClass()
        {
            InitializeComponent();
            
            dTimer = new DispatcherTimer(DispatcherPriority.Send);
            dTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dTimer.Interval = new TimeSpan(0, 0, 1);

            sw = new Stopwatch();

            tabControl.SelectedIndex = 0;
            //Status("Ready to go ");
        }

        public bool SendJson(string json, bool async = false)
        {
            int delay = 0;
            if (async) delay = 100;
            return remote.sendCommand(json, delay);
        }

        public void SetFringeParams(FringeParams fp)
        {
            lbFringePrm.Content = "Fringe Prm: per= " + fp.period.ToString()+"; phase= " + fp.phase.ToString()+"; off= " + fp.offset.ToString();
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
            if (tabControl.SelectedIndex == 0) return (cbTimeEndless.SelectedIndex == 1);
            else return (cbSizeEndless.SelectedIndex == 1);
        }

        public RemoteMode remoteMode
        {
            get { return (RemoteMode)GetValue(remoteModeProperty); }
            set
            {
                lbMode.Content = "Mode(stage): " + value.ToString();
                SetValue(remoteModeProperty, value);
                tabControl.IsEnabled = (value == RemoteMode.Free);
            }
        }
        public static readonly DependencyProperty remoteModeProperty
            = DependencyProperty.Register(
                  "remoteMode",
                  typeof(RemoteMode),
                  typeof(scanClass),
                  new PropertyMetadata(RemoteMode.Free)
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

        int TotalCycleCount = 0;
        double TotalCycleTime = 0.0; //[sec]

        private bool MessageHandler(string message)
        {
            try
            {
                bool back = true;
                OnRemote(message);
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
            MessageBox.Show("           Axel Hub v1.3 \n         by Teodor Krastev \nfor Imperial College, London, UK", "About");
        }

        private double GetSamplingPeriod()
        {
            double freq = 1; // in seconds
            double vl = 0;
            switch (cbDigitMode.SelectedIndex)
            {
                case 0: if (!double.TryParse(tbDigitValue.Text, out vl)) throw new Exception("Not number for digit. value");  // Hz
                        freq = vl;
                        break;
                case 1: if (!double.TryParse(tbDigitValue.Text, out vl)) throw new Exception("Not number for digit. value");  // kHz
                        freq = 1000 * vl;
                        break;
                case 2: if (!double.TryParse(tbDigitValue.Text, out vl)) throw new Exception("Not number for digit. value");  // s
                        freq = 1 / vl;
                        break;
                case 3: if (!double.TryParse(tbDigitValue.Text, out vl)) throw new Exception("Not number for digit. value");  // ms
                        freq = 1000 / vl;
                        break;
                case 4: throw new Exception("Not implemented yet");                    
            }
            return 1 / freq;
        }
        
        public void bbtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (OnStart == null) return;
            bool jumbo = false;
            switch ((string)bbtnStart.Content)
            {
                case "Start":
                    {
                        bbtnStart.Content = "Cancel";
                        Running = true;                        
                    }
                    break;
                case "Cancel":
                    {
                        bbtnStart.Content = "Start";
                        Running = false;
                        Abort(true);
                        return;
                    }
                    break;
                case "Jumbo Run":
                    {
                        bbtnStart.Content = "Jumbo Stop";
                        jumbo = true;
                        Running = true; 
                    }
                    break;
                case "Jumbo Stop":
                    {
                        bbtnStart.Content = "Jumbo Run";
                        jumbo = false;
                        Running = false;
                        Abort(true);
                        return;
                    }
                    break;
            }
            bool down = Running;
            bbtnStart.Value = down;
            double period = GetSamplingPeriod(); // sampling rate in sec
            int sizeLimit = 0; // [s]

            double Limit = 1;
            if (tabControl.SelectedIndex == 0)
            {
                if (!double.TryParse(tbTimeLimit.Text, out Limit)) throw new Exception("Not number for Time limit");
                sizeLimit = (int)(Limit / period);
            }
            else
            {
                if (!double.TryParse(tbBifferSize.Text, out Limit)) throw new Exception("Not number for Buffer size");
                sizeLimit = (int)Limit;
            }
            if (EndlessMode())
            {
                lbTimeLeft.Visibility = System.Windows.Visibility.Hidden;
                totalTime = new TimeSpan(0, 0, 0);
                currentTime = new TimeSpan(0, 0, 0);
            }
            else
            {
                lbTimeLeft.Visibility = System.Windows.Visibility.Visible;
                totalTime = new TimeSpan(0, 0, (int)(sizeLimit * period));
                currentTime = new TimeSpan(0, 0, 0);
            }
            OnStart(jumbo, down, period, sizeLimit); // the last three are valid only in non-jumbo mode with down = true
         }

        public void Abort(bool local) // the origin of Abort is (local) or (remote / end sequence)
        {
            bool jumbo = (remoteMode == RemoteMode.Jumbo_Scan) || (remoteMode == RemoteMode.Jumbo_Repeat);
            remoteMode = RemoteMode.Free;
            if (tabControl.SelectedIndex == 2) bbtnStart.Content = "Jumbo Run"; // remote tab
            else bbtnStart.Content = "Start";
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
                 remote.CheckConnection();
             }                     
             else
             {
                 bbtnStart.Visibility = System.Windows.Visibility.Visible;
                 Status("Ready to go ");
                 if(bbtnStart.Content.ToString().Equals("Start")) return;
                 if(bbtnStart.Content.ToString().Substring(0,5).Equals("Jumbo")) bbtnStart.Content = "Start"; // TODO ask confirmation                
             }            
         }

         private void OnActiveComm(bool active)
         {
             if (active)
             {
                 Status("Ready to remote<->");
                 bbtnStart.Visibility = System.Windows.Visibility.Visible;
                 if (bbtnStart.Content.ToString().Equals("Start") || bbtnStart.Content.ToString().Equals("Stop"))
                 {
                     bbtnStart.Content = "Jumbo Run";
                     remoteMode = RemoteMode.Free;
                 }  
             }
             else
             {
                 Status("Commun. problem");
                 bbtnStart.Visibility = System.Windows.Visibility.Hidden;
             }
         }

         private void UserControl_Loaded(object sender, RoutedEventArgs e)
         {
             string computerName = (string)System.Environment.GetEnvironmentVariables()["COMPUTERNAME"];
             string partner = "MOTMaster2";
        /*     switch (computerName) 
             {
                 case "NAVIGATOR-ANAL": partner = "MOTMaster2"; break;
                 case "DESKTOP-U334RMA": partner = "Axel Probe"; break;
             }*/
             remote = new RemoteMessagingNS.RemoteMessaging(partner); 
             remote.Enabled = false;
             remote.OnReceive += MessageHandler;
             remote.ActiveComm += OnActiveComm;
             remote.OnAsyncSent += OnAsyncSend;

             string[] args = Environment.GetCommandLineArgs(); 
             if (args.Length > 1) 
             {
                Console.WriteLine("Command line argument: " + args[1]);
                if (args[1].Equals("-remote")) tabControl.SelectedIndex = 2;
             }    
         }

         private void btnCheckComm_Click(object sender, RoutedEventArgs e)
         {
             remote.CheckConnection();
         }
    }
}
