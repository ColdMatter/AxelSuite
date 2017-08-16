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
using UtilsNS;
using RemoteMessagingNS;

namespace scanHub
{    
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class scanClass : UserControl
    {
        public delegate void NewMessageDelegate(string NewMessage);
        private RemoteMessaging mm2;
        private double realSampling;

        private string _tempJson;
        TimeSpan totalTime, currentTime;
        DispatcherTimer dTimer;
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

        public void OnRealSampling(double _realSampling) 
        {
            realSampling = _realSampling;
            groupDigit.Header = " Conversion rate (" + realSampling.ToString() + " [Hz])"; DoEvents();
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
            DoEvents();
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

        private void Status(string sts)
        {
            if (Utils.isNull(lblStatus)) return;
            lblStatus.Content = ">> " + sts;
            DoEvents();
        }

        public bool Running
        {
            get { return (bool)GetValue(RunningProperty); }
            set
            {
                bbtnStart.Value = value;               
                if (value)
                {
                    bbtnStart.Content = "Stop";
                    Status("Running....");
                    dTimer.Start();
                    currentTime = new TimeSpan(0, 0, 0);
                }
                else
                {
                    bbtnStart.Content = "Start";
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

        int TotalCycleCount = 0;
        double TotalCycleTime = 0.0; //[sec]

        private bool MessageHandler(string message)
        {
            try
            {
                bool back = false;
                OnRemote(message);
                return back;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return false;
            }
        } 

        public delegate void StartHandler(bool down, double period, bool TimeMode, bool Endless, double Limit);
        public event StartHandler Start;

        protected void OnStart(bool down, double period, bool TimeMode, bool Endless, double Limit)
        {
            if(Start != null) Start(down, period, TimeMode, Endless, Limit);
        }

        public delegate void RemoteHandler(string msg);
        public event RemoteHandler Remote;

        protected void OnRemote(string msg)
        {
            //TODO Check this doesn't conflict with other parentheses
            if (msg.StartsWith(("{")))
            {
                _tempJson = msg;
            }
            else
            {
                _tempJson = _tempJson + msg;
            }
            if (msg.EndsWith("}"))
            {
                if (Remote != null) Remote(_tempJson);
            }
        }

        public delegate void FileRefHandler(string FN, bool stats);
        public event FileRefHandler FileRef;

        protected void OnFileRef(string FN, bool stats)
        {
            if (FileRef != null) FileRef(FN, stats);
        }

        private void Image_MouseDown(object sender, MouseButtonEventArgs e)
        {
            MessageBox.Show("     Axel Hub v1.0 \n   by Teodor Krastev \nfor Imperial College, London, UK", "About");
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
        
        private void bbtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (Start == null) return;
            Running = !Running; 
            
            bool down = Running;
            bool Endless = true;
            double period = GetSamplingPeriod(); // sampling rate in sec
            int plannedTime = 0; // [s]

            double Limit = 1;
            bool TimeMode = (tabControl.SelectedIndex == 0);
            if (TimeMode)
            {
                Endless = (cbTimeEndless.SelectedIndex == 1);
                if (!double.TryParse(tbTimeLimit.Text, out Limit)) throw new Exception("Not number for Time limit");
                plannedTime = (int)Limit;
            }
            else
            {
                Endless = (cbSizeEndless.SelectedIndex == 1);
                if (!double.TryParse(tbBifferSize.Text, out Limit)) throw new Exception("Not number for Buffer size");
                plannedTime = (int)(Limit * period);
            }
            if (Endless)
            {
                lbTimeLeft.Visibility = System.Windows.Visibility.Hidden;
                totalTime = new TimeSpan(0, 0, 0);
                currentTime = new TimeSpan(0, 0, 0);
            }
            else
            {
                lbTimeLeft.Visibility = System.Windows.Visibility.Visible;
                totalTime = new TimeSpan(0, 0, plannedTime);
                currentTime = new TimeSpan(0, 0, 0);
            }
            DoEvents();
            OnStart(down, period, TimeMode, Endless, Limit);
        }

         private void tabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
         {
             if(mm2 != null) mm2.Enabled = (tabControl.SelectedIndex == 2);
             if (tabControl.SelectedIndex == 2) 
             {
                 if (mm2.CheckConnection()) Status("Ready to remote<->");
                 else Status("Commun. problem");
                 bbtnStart.Visibility = System.Windows.Visibility.Hidden;
             }                     
             else
             {
                bbtnStart.Visibility = System.Windows.Visibility.Visible;
                Status("Ready to go ");
             }            
         }

         private void OnActiveComm(bool active)
         {
             if (active) Status("Ready to remote<->");
             else Status("Commun. problem"); 
         }

        //TODO Add two connections - Axel Probe and MOTMaster2
         private void UserControl_Loaded(object sender, RoutedEventArgs e)
         {
             mm2 = new RemoteMessaging("MOTMaster2");
             mm2.Enabled = false; 
             mm2.Remote += MessageHandler;
             mm2.ActiveComm += OnActiveComm;
         }

         private void btnCheckComm_Click(object sender, RoutedEventArgs e)
         {
             if (mm2.CheckConnection()) Status("Ready to remote<->");
             else Status("Commun. problem");
         }
    }
}
