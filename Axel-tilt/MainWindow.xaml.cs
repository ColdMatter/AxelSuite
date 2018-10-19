using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
using NationalInstruments.Controls;
using RemoteMessagingNS;
using Newtonsoft.Json;
using UtilsNS;
using AxelChartNS;

namespace Axel_tilt
{
    
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool DebugMode = true;
        public double period = 100.0;
        private string doublePrec = "G6";
        Tilt tilt;
        // first time -> 0; last time -> 100
        public static readonly int maxTextLen = 10000;
        public static readonly double[,] trapezePtrn = { {0 , 0 }, { 33 , 100 }, { 66 , 100 } , { 100, 0} }; // {time [0..100], ampl [-100..100]}
        public static readonly double[,] trianglePtrn = { { 0, 0 }, { 25, 100 }, { 50, 0 }, { 75, -100 }, { 100, 0 } };
        public static readonly double[,] stairsPtrn = { { 0, 0 }, { 5, 25 }, { 25, 25 }, { 30, 50 }, { 50, 50 }, { 55, 75 }, { 75, 75 }, { 80, 100 }, { 100, 100 }}; 

        public MainWindow()
        {
            InitializeComponent();
            random = new Random();
            ds = new DataStack();
            tilt = new Tilt();
            tilt.OnEnd += new Tilt.EndHandler(DoEnd);
            tilt.OnLog += new Tilt.LogHandler(inLog);
            tilt.OnMove += new Tilt.MoveHandler(DoMove);          
            ndMoveA.Value = tilt.horizontal.posA; ndMoveB.Value = tilt.horizontal.posB; ndSpeed.Value = Utils.formatDouble(tilt.horizontal.speed,"G3");
            tilt.SetSpeed();
        }
        private void myLog(string txt)
        {
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,
                new Action(() =>
                {
                    if (!chkLog.IsChecked.Value) return;
                    tbLog.AppendText(txt + "\r\n");
                    string text = tbLog.Text;
            
                    if (text.Length > 2 * maxTextLen) tbLog.Text = text.Substring(maxTextLen);
                    tbLog.Focus();
                    tbLog.CaretIndex = tbLog.Text.Length;
                    tbLog.ScrollToEnd();
                }));
        }

        private void log(string txt, bool debug = false)
        {
            if (DebugMode) // in DebugMode show everything
            {
                myLog(txt);
            }
            else // in not DebugMode only regular logs
            {
                if (!debug) myLog(txt);
            }
        }

        private void inLog(string txt)
        {
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,
                new Action(() =>
                {
                    if (tabCtrlMain.SelectedIndex != 1) return;
                    tbInnerLog.AppendText(txt + "\r\n");
                    string text = tbInnerLog.Text;
                    if (text.Length > 2 * maxTextLen) tbInnerLog.Text = text.Substring(maxTextLen);
                    tbInnerLog.Focus();
                    tbInnerLog.CaretIndex = tbInnerLog.Text.Length;
                    tbInnerLog.ScrollToEnd();
                }));
        }
        private void inLog(List<string> ls)
        {
            foreach (string ss in ls) inLog(ss);
        }

        double lastAccel = 0;
        private void ShowAccel(double accel)
        {
            gbDirectControl.Header = "Direct control:  " + accel.ToString(doublePrec) + " [mg]";
            lastAccel = accel;
        }

        RemoteMessaging remoteShow;
        Random random;
        DataStack ds;

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

        private bool OnShowReceive(string message)
        {
            try
            {
                bool back = true;
                if (!message.Equals("query.tilt")) Utils.TimedMessageBox("Unknown command: " + message);
                double tlt = tilt.GetAccel(); 
                remoteShow.sendCommand("tilt="+tlt.ToString("G6"), 10);
                return back;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return false;
            }
        }

        private void OnShowActiveComm(bool active, bool forced)
        {
            ledAxelShow.Value = active;
        }
        private void Axel_tilt_Loaded(object sender, RoutedEventArgs e)
        {
            remoteShow = new RemoteMessaging("Axel Show", 668);
            remoteShow.Enabled = false;
            remoteShow.OnReceive += new RemoteMessaging.ReceiveHandler(OnShowReceive);
            remoteShow.OnActiveComm += new RemoteMessaging.ActiveCommHandler(OnShowActiveComm);

            tilt.MemsCorr = chkMemsCorr.IsChecked.Value;
        }

        private void chkAxelShow_Checked(object sender, RoutedEventArgs e)
        {
            remoteShow.Enabled = chkAxelShow.IsChecked.Value;
            remoteShow.CheckConnection(true);
        }

        private void imgAbout_MouseDown(object sender, MouseButtonEventArgs e)
        {
            MessageBox.Show("           Axel Tilt v1.1 \n         by Teodor Krastev \nfor Imperial College, London, UK\n\n   visit: http://axelsuite.com", "About");
        }

        private void btnInitaiteA_Click(object sender, RoutedEventArgs e)
        {
            if (Utils.isNull(tilt)) return;
            Motor m;
            if (sender.Equals(btnInitaiteA)) m = tilt.mA;
            else m = tilt.mB;
            if (m.Home()) inLog(m.letter() + "> going home...");
            if (m.Zero()) inLog(m.letter() + "> set to zero.");
        }

        private void btnMoveA_Click(object sender, RoutedEventArgs e)
        {
            if (Utils.isNull(tilt)) return;
            Motor m; double dist; 
            if (sender.Equals(btnMoveA)) { m = tilt.mA; dist = ndMoveA.Value; }
            else { m = tilt.mB; dist = ndMoveB.Value; }
            inLog(m.letter() + "> is moving to " + dist.ToString(doublePrec) + " [mm]...");
            m.MoveD(dist);
            inLog(m.letter() + "> has arrived.");
        }

        private void btnStatusA_Click(object sender, RoutedEventArgs e)
        {
            if (Utils.isNull(tilt)) return;
            Motor m;
            if (sender.Equals(btnStatusA)) m = tilt.mA;
            else m = tilt.mB;
            inLog(m.letter() + "> status ----");
            inLog(m.ListStatus());
            inLog("---------------");
        }

        private void btnInitiateTilt_Click(object sender, RoutedEventArgs e)
        {
            inLog("Tilt> is going home..."); DoEvents();
            tilt.HomeAndZero();
            inLog("Tilt> is home.");
            lbState.Content = "Cur.State: Tilt is initialized.";
            btnMoveToPreset.IsEnabled = true;
        }

        private void btnMoveToPreset_Click(object sender, RoutedEventArgs e)
        {
            inLog("Tilt> is going horizontal");
            inLog("Tilt> at A/B: " + ndMoveA.Value.ToString(doublePrec) + " / " + ndMoveB.Value.ToString(doublePrec) + " ..."); DoEvents();
            tilt.SetHorizontal(ndMoveA.Value, ndMoveB.Value);
            inLog("Tilt> is horizontal.");
            lbState.Content = "Cur.State: Tilt is ready to use.";
            ndMoveMm.Value = 0; ndMoveMrad.Value = 0; ndGotoPos.Value = 0;
            btnGoTo_Click(null, null);
        }

        private void btnMoveMm_Click(object sender, RoutedEventArgs e)
        {
            double dist = ndMoveMm.Value; SetBacklash(true);
            inLog("Tilt> is going to " + dist.ToString(doublePrec) + " [mm]..."); DoEvents();
            tilt.MoveDist(dist, false);
            tilt.Wait4Stop();
            inLog("Tilt> has arrived.");
            ndMoveMrad.Value = Utils.formatDouble(tilt.dist2tilt(dist), doublePrec);
            ndGotoPos.Value = Utils.formatDouble(tilt.tilt2accel(ndMoveMrad.Value), doublePrec);
            ShowAccel(ndGotoPos.Value);
        }

        private void btnMoveMrad_Click(object sender, RoutedEventArgs e)
        {
            double mrad = ndMoveMrad.Value; SetBacklash(true);
            inLog("Tilt> is going to " + mrad.ToString(doublePrec) + " [mrad]..."); DoEvents();
            ndMoveMm.Value = Utils.formatDouble(tilt.tilt2dist(mrad), doublePrec);
            tilt.MoveDist(ndMoveMm.Value, false);
            tilt.Wait4Stop();
            inLog("Tilt> has arrived.");
            ndGotoPos.Value = Utils.formatDouble(tilt.tilt2accel(mrad),doublePrec);
            ShowAccel(ndGotoPos.Value);
        }

        private void btnGoTo_Click(object sender, RoutedEventArgs e)
        {
            double accel = ndGotoPos.Value; SetBacklash(true); ShowAccel(accel);
            inLog("Tilt> is going to " + accel.ToString(doublePrec) + " [mg]..."); DoEvents();
            ndMoveMrad.Value = Utils.formatDouble(tilt.accel2tilt(accel), doublePrec);            
            ndMoveMm.Value = Utils.formatDouble(tilt.tilt2dist(ndMoveMrad.Value), doublePrec);
            tilt.MoveDist(ndMoveMm.Value);
            tilt.Wait4Stop();
            inLog("Tilt> has arrived.");
            //gotoPos(ndGotoPos.Value);
        }

        private void btnSpeed_Click(object sender, RoutedEventArgs e)
        {
            if (Utils.isNull(tilt)) return;
            if (tilt.SetSpeed(ndSpeed.Value)) inLog("set speed to: " + ndSpeed.Value.ToString("G3")+" [mm/s]");
            else inLog("Problem setting the speed");
        }

        private void SetBacklash(bool bl)
        {
            string ss = (bl) ? " (on)" : " (off)";
            gbBacklash.Header = (string)"Backlash comp." + ss;
            if(bl) 
            {
                if(rbBacklashON.IsChecked.Value || rbBacklashAuto.IsChecked.Value) tilt.SetBacklash(bl);
            }
            else 
            {
                if(rbBacklashOFF.IsChecked.Value || rbBacklashAuto.IsChecked.Value) tilt.SetBacklash(bl);
            }
        }

        private void rbBacklashON_Checked(object sender, RoutedEventArgs e) // only checked event
        {
            if (Utils.isNull(tilt)) return;
            tilt.SetBacklash(sender.Equals(rbBacklashON));
            if (sender.Equals(rbBacklashON)) inLog("Backlash mode is ON");
            else inLog("Backlash mode is OFF");
            tilt.AutoBacklash = false;
        }

        private void rbBacklashAuto_Checked(object sender, RoutedEventArgs e) // only checked event
        {
            if (Utils.isNull(tilt)) return;
            tilt.AutoBacklash = true;
        }

        private void btnDown_Click(object sender, RoutedEventArgs e)
        {
            SetBacklash(false);
            lastAccel -= ndStepMg.Value;
            tilt.MoveAccel(lastAccel);
            ShowAccel(lastAccel);
        }
        private void btnUp_Click(object sender, RoutedEventArgs e)
        {
            SetBacklash(false);
            lastAccel += ndStepMg.Value;
            tilt.MoveAccel(lastAccel);
            ShowAccel(lastAccel);
        }

        private void Axel_tilt_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            btnRun.Value = false; tilt.request2Stop = true;
            tilt.SetSpeed(); tilt.MoveAccel(0,false);
            tilt.horizontal.posA = ndMoveA.Value; tilt.horizontal.posB = ndMoveB.Value; 
            string fileJson = JsonConvert.SerializeObject(tilt.horizontal);
            File.WriteAllText(Utils.configPath + "horizontal.cfg",fileJson);
        }

        private void DoMove(Point target)
        {
             Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, 
                new Action(() =>
                    {
                        Point pnt = new Point(target.X + ndPeriodDur.Value*loopCount, target.Y);
                        dq.Add(pnt);
                        graphTilt.Data[1] = dq;
                        log("Move to " + pnt.Y.ToString("G5") + " at "+  pnt.X.ToString("G5"), true);
                    })); 
        }
        private void dTimer_Tick(object sender, EventArgs e)
        {
             if (!btnRun.Value || tilt.request2Stop) return;
             double accel = tilt.GetAccel(); ShowAccel(accel);
             dt.AddPoint(tilt.GetAccel(), sw.ElapsedMilliseconds / 1000.0);
             Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, 
                new Action(() =>
                    {
                        if(dt.Count>1) graphTilt.Data[0] = dt;
                    }));
        }

        private void SinglePeriod()
        {
            switch (cbDriftType.SelectedIndex)
            {
                case 0: tilt.MoveInPattern(trianglePtrn, ndPeriodDur.Value, ndAmplitude.Value, ndOffset.Value);
                    break;
                case 1: tilt.MoveInPattern(trapezePtrn, ndPeriodDur.Value, ndAmplitude.Value, ndOffset.Value);
                    break;
                case 2: tilt.MoveInPattern(stairsPtrn, ndPeriodDur.Value, ndAmplitude.Value, ndOffset.Value);
                    break;
            }
        }

        private void DoEnd(bool userCancel)
        {
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, 
                new Action(() =>
                    {
                        if (userCancel)
                        {
                            btnRun.Value = false; tilt.Stop(); tilt.SetSpeed(); tilt.MoveAccel(0); ShowAccel(tilt.GetAccel());
                            if (!Utils.isNull(dTimer)) dTimer.Stop(); 
                            log("user canceled the scan.");
                        }
                        else
                        {
                            switch (cbFinite.SelectedIndex) 
                            {
                                case 0: // Finite mode
                                    btnRun.Value = false; tilt.SetSpeed(); tilt.MoveAccel(0); 
                                    double accel = tilt.GetAccel(); ShowAccel(accel);
                                    dt.AddPoint(tilt.GetAccel(), sw.ElapsedMilliseconds / 1000.0);
                                    if (!Utils.isNull(dTimer)) dTimer.Stop();
                                    log("finished the scan.");
                                    break;
                                case 1: // Continious mode
                                    loopCount += 1;
                                    SinglePeriod();
                                    break;
                            }
                        }
                    }));
        }

        DispatcherTimer dTimer;
        DataStack dt, dq;
        Stopwatch sw = new Stopwatch();
        int loopCount = 0;

        private void btnRun_Click(object sender, RoutedEventArgs e) // main scan
        {
            btnRun.Value = !btnRun.Value;
            if (btnRun.Value)
            {
                tilt.request2Stop = false; loopCount = 0;
                log("Start scanning...");
                tilt.SetSpeed(); tilt.MoveAccel(0); ShowAccel(tilt.GetAccel());                 
                SetBacklash(false); DoEvents();
            }
            else 
            { 
                tilt.request2Stop = true;  DoEnd(true);              
                DoEvents(); return; 
            }
            dTimer = new DispatcherTimer(DispatcherPriority.Send);
            dTimer.Tick += dTimer_Tick;
            int dur = (int)(ndPeriodDur.Value * (ndStepPer.Value / 100.0) * 1000.0); // ms
            dTimer.Interval = new TimeSpan(dur * 10000);
            dTimer.Start();

            dt = new DataStack(); dq = new DataStack(); graphTilt.Data[0] = null; graphTilt.Data[1] = null; sw.Restart(); 
            axisHoriz.Range = new Range<double>(0, ndShownPer.Value * ndPeriodDur.Value * 1.08);
            switch (cbDriftType.SelectedIndex)
            {
                case 1:
                case 2: axisVert.Range = new Range<double>(0, ndAmplitude.Value * 1.08);
                    break;
                case 0: 
                case 3: axisVert.Range = new Range<double>(-ndAmplitude.Value * 1.08, ndAmplitude.Value * 1.08);
                    break;
            }           
            SinglePeriod();          
        }

        private void btnHomeAndHoriz_Click(object sender, RoutedEventArgs e)
        {
            log("Initializing tilt platform..."); DoEvents();
            btnInitiateTilt_Click(null, null); btnMoveToPreset_Click(null, null);
            log("Done!");
        }

        private void btnMM_A_Click(object sender, RoutedEventArgs e)
        {
            if (Utils.isNull(tilt)) return;
            Motor m;
            if (sender.Equals(btnMM_A)) m = tilt.mA;
            else m = tilt.mB;
            inLog(m.letter() + "> is at "+m.GetPosition().ToString(doublePrec)+" [mm]");
        }

        private void btnMM_tilt_Click(object sender, RoutedEventArgs e)
        {
            if (Utils.isNull(tilt)) return;
            if (sender.Equals(btnMM_tilt)) inLog("tilt> is at " + tilt.GetPosition().ToString(doublePrec) + " [mm]");
            else inLog("tilt> is at " + tilt.GetAccel().ToString(doublePrec) + " [mg]");
        }

        private void tbInnerLog_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete) tbInnerLog.Text = "";
        }

        private void chkMemsCorr_Checked(object sender, RoutedEventArgs e)
        {
            if(!Utils.isNull(tilt)) tilt.MemsCorr = chkMemsCorr.IsChecked.Value; 
        }

        private void cbFinite_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch (cbFinite.SelectedIndex)
            {
                case 0: ndShownPer.Value = 1;
                    break;
                case 1: ndShownPer.Value = 3;
                    break;
            }
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            tilt.Stop();
        }

        private void ndGotoPos_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return || e.Key == Key.Enter) btnGoTo_Click(null, null);
        }
    }
}

