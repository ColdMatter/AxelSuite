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
using PipesServerNS;
using UtilsNS;

namespace scanBoss
{
    
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class scanClass : UserControl
    {
        public delegate void NewMessageDelegate(string NewMessage);
        private PipeServer _pipeServer;
        private string TrackPipeName = "XPStrackPipe";
        private bool Connected = false;
        public readonly double[] FixConvRate = { 102400, 51200, 34133, 25600, 20480, 17067, 14629, 12800, 11378,
            10240, 9309, 8533, 7314, 6400, 5689, 5120, 4655, 4267, 3657, 3200, 2844, 2560, 2327, 2133, 1829,
            1600, 1422, 1280, 1164, 1067, 914, 800, 711, 640, 582, 533, 457, 400, 356, 320, 291, 267}; // [Hz]

        TimeSpan totalTime, currentTime;
        DispatcherTimer dispatcherTimer;

        public double RealConvRate(double wantedCR)
        {
            if (wantedCR > FixConvRate[0]) return FixConvRate[0];
            int len = FixConvRate.Length;
            if (wantedCR <= FixConvRate[len-1]) return FixConvRate[len-1];
            int found = 0;
            for (int i = 0; i < len-1; i++)
            {
                if ((FixConvRate[i] >= wantedCR) && (wantedCR > FixConvRate[i + 1]))
                {
                    found = i; break;
                }
            }
            groupDigit.Header = " Conversion rate (" + FixConvRate[found].ToString() + " [Hz])"; DoEvents();
            return FixConvRate[found];
        }

        private void Connect2Track()
        {
            try
            {
                _pipeServer.Listen(TrackPipeName);
                Connected = true;
            }
            catch (Exception)
            {
                Connected = false;
            }
            if (Connected) Status("Ready to go remote <->");
        }

        public scanClass()
        {
            InitializeComponent();

            _pipeServer = new PipeServer();
            _pipeServer.PipeMessage += new DelegateMessage(PipesMessageHandler);

            dispatcherTimer = new DispatcherTimer(DispatcherPriority.Send);
            dispatcherTimer.Tick += dispatcherTimer_Tick;
            dispatcherTimer.Interval = new TimeSpan(0,0,1);

            //if(!Connected) Connect2Track();

            tabControl.SelectedIndex = 0;
            //Status("Ready to go ");
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
            lblStatus.Content = "Status: " + sts;
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
                    dispatcherTimer.Start();
                    currentTime = new TimeSpan(0, 0, 0);
                }
                else
                {
                    bbtnStart.Content = "Start";
                    Status("Ready for another go");
                    dispatcherTimer.Stop();
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
        double TotalCycleTime = 0; //[sec]
        private void PipesMessageHandler(string message)
        {
            if (!Connected)
            {
                Status("Error: no named pipe to Axel Track");
                return;
            }
            //if (tabControl.SelectedIndex != 2) tabControl.SelectedIndex = 2;  // ???
            try
            {
                if (!CheckAccess()) 
                { 
                    // On a different thread
                    Dispatcher.Invoke(() => PipesMessageHandler(message));
                }
                else
                {
                    if ((!chkRemoteEnabled.IsChecked.Value) || (tabControl.SelectedIndex != 2)) return; 
                    string[] ws = message.Split('>');
                    string ws0 = ws[0].ToUpper();
                    if (ws0 == "ACQ") // acquisition
                    {
                        Status("Axel Track is moving...");
                        double SamplingPeriod = GetSamplingPeriod();
                        string prms = ws[1].Replace("\0", "");
                        string[] wt = prms.Split(';');
                        double CyclePeriod = double.Parse(wt[0]); // one cycle motion only 
                        double Pause = double.Parse(wt[1]); // two time gaps when it changes direction for safety
                        double Distance = double.Parse(wt[2]);
                        double Accel = double.Parse(wt[3]);
                        int CyclesLeft = int.Parse(wt[4]);
                        
                        if (TotalCycleCount == 0)  // first call 
                        {
                            TotalCycleCount = CyclesLeft;
                            TotalCycleTime = TotalCycleCount * (CyclePeriod + 2 * Pause);
                            lbTotalTime.Content = "Time [sec]: " + (int)(TotalCycleTime);
                            lbBufferSize.Content = "Buffer size: " + (int)(TotalCycleTime / SamplingPeriod);

                            lbTimeLeft.Visibility = System.Windows.Visibility.Visible;
                            totalTime = new TimeSpan(0, 0, (int)(TotalCycleTime));
                            Running = true;
                        }                        
                        OnRemote(SamplingPeriod, CyclePeriod, Pause, Distance, Accel, CyclesLeft);
                        if (CyclesLeft == 1)
                        {
                            TotalCycleCount = 0; // last turn
                            Running = false;
                        }                          
                    }
                    if (ws0 == "FRF") // open XPS log file for reference
                    {
                        Status("Axel Track sent ref. file");
                        string fr = ws[1].Replace("\0", "");
                        OnFileRef(fr, chkStatsEnabled.IsChecked.Value);
                        Status("Ready for another go");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        } 

        public delegate void StartHandler(bool down, double period, bool TimeMode, bool Endless, double Limit);
        public event StartHandler Start;

        protected void OnStart(bool down, double period, bool TimeMode, bool Endless, double Limit)
        {
            if(Start != null) Start(down, period, TimeMode, Endless, Limit);
        }

        public delegate void RemoteHandler(double SamplingPeriod, double CyclePeriod, double Pause, double Distance, double Accel, int CyclesLeft);
        public event RemoteHandler Remote;

        protected void OnRemote(double SamplingPeriod, double CyclePeriod, double Pause, double Distance, double Accel, int CyclesLeft)
        {
            if (Remote != null) Remote(SamplingPeriod, CyclePeriod, Pause, Distance, Accel, CyclesLeft);
        }

        public delegate void FileRefHandler(string FN, bool stats);
        public event FileRefHandler FileRef;

        protected void OnFileRef(string FN, bool stats)
        {
            if (FileRef != null) FileRef(FN, stats);
        }

        private void Image_MouseDown(object sender, MouseButtonEventArgs e)
        {
            MessageBox.Show("     Axel Boss v1.3 \n   by Teodor Krastev \nfor Imperial College, London, UK", "About");
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
            freq = RealConvRate(freq);
            return 1 / freq;
        }
        
        private void bbtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (Start == null) return;
            Running = !Running;
            
            bool down = Running;
            bool Endless = true;
            double period = GetSamplingPeriod();
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
             if (tabControl.SelectedIndex == 2) 
             {
                 if (Connected) Status("Ready to go remote <->");
                 else Connect2Track();
                 bbtnStart.Visibility = System.Windows.Visibility.Hidden;
             }                     
             else
             {
                bbtnStart.Visibility = System.Windows.Visibility.Visible;
                Status("Ready to go ");
             }            
         }

         private void cbDigitMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
         {

         }
    }
}
