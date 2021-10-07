using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using NationalInstruments.Controls;
using UtilsNS;


namespace Axel_hub.Showcase
{
    /// <summary>
    /// Interaction logic for ShowcaseUC.xaml
    /// </summary>
    public partial class ShowcaseClass : UserControl
    {
        public ShowcaseClass()
        {
            InitializeComponent();
        }
        const double gMems = 9.81792; // m/s^2
        const bool simulation = false;
        private DispatcherTimer dTimer;
        public ShowcaseClassWindow Showcase;
        public bool IsShowcaseShowing
        {
            get
            {
                if (Utils.isNull(Showcase)) return false;
                return Showcase.IsVisible;
            }
        }
        public event ShowcaseClassWindow.ShowcaseEventHandler OnShowcaseUCEvent;
        private void btnShowcaseDemo_Click(object sender, RoutedEventArgs e)
        {
            if (!Utils.isNull(Showcase))
            {
                if (!Showcase.IsVisible)
                {
                    Showcase.Show();
                }

                if (Showcase.WindowState == WindowState.Minimized)
                {
                    Showcase.WindowState = WindowState.Normal;
                }

                Showcase.Activate();
                Showcase.Topmost = true;  // important
                Showcase.Topmost = false; // important
                Showcase.Focus();         // important
                return;
            }
            Showcase = new ShowcaseClassWindow();
            Showcase.OnShowcaseEvent += new ShowcaseClassWindow.ShowcaseEventHandler(ShowcaseAction);
            Showcase.Show();
 
            dTimer = new DispatcherTimer(DispatcherPriority.Send);
            dTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dTimer.Interval = new TimeSpan(0, 0, 1);

            showcaseState = ShowcaseStates.idle;
        }
        public enum ShowcaseStates { idle, scan, run };
        private ShowcaseStates _showcaseState;
        public ShowcaseStates showcaseState
        {
            get { return _showcaseState; }
            set
            {
                switch (value)
                {
                    case (ShowcaseStates.idle):
                        Showcase.btnScan.IsEnabled = true;
                        if (Utils.isNull(Showcase.scanSrs)) Showcase.btnRun.IsEnabled = false;
                        else Showcase.btnRun.IsEnabled = (Showcase.scanSrs.Count > 0);
                        Showcase.btnStop.IsEnabled = Showcase.btnRun.IsEnabled;
                        break;
                    case (ShowcaseStates.scan):
                        Showcase.btnRun.IsEnabled = false;
                        break;
                    case (ShowcaseStates.run):
                        Showcase.btnScan.IsEnabled = false;
                        Showcase.btnRun.IsEnabled = false;
                        break;
                }
                _showcaseState = value;
            }
        }
        public bool ShowcaseAction(string msg) // coming from ShowcaseClassWindow
        {
            switch (msg)
            {
                case "Scan":
                    ShowcaseAction("Stop");                    
                    showcaseState = ShowcaseStates.scan;                     
                    if (simulation)
                    {
                        baseMMscan scan = new baseMMscan();
                        scan.sFrom = 0; scan.sTo = 6.4; scan.sBy = 0.2; 
                        Showcase.InitScan(scan);
                        dTimer.Start(); scCount = 0;
                    }
                    break;
                case "Run":
                    ShowcaseAction("Stop");                     
                    showcaseState = ShowcaseStates.run;                     
                    if (simulation)
                    {
                        Showcase.InitRun();
                        dTimer.Start(); scCount = 0;
                    }
                     break;
                case "Stop":                    
                    showcaseState = ShowcaseStates.idle;
                    if (simulation) dTimer.Stop();
                    break;
            }
            if ((OnShowcaseUCEvent != null) && !simulation) return OnShowcaseUCEvent(msg);
            else return false;
        }
        public void InitRun(int depth)
        {
            if (!IsShowcaseShowing) return;
            Showcase.axisYrun.Label = ""; Showcase.axisDrun.Label = "";
            if (rbMQdiff.IsChecked.Value)
            {
                if (rbMS2.IsChecked.Value) Showcase.axisDrun.Label = "acceleration [m/s^2]";
                if (rbMg.IsChecked.Value) Showcase.axisDrun.Label = "acceleration [mg]";
                Showcase.axisDrun.Visibility = Visibility.Visible;
                Showcase.plotDiff.Visibility = Visibility.Visible;
            }
            else
            {
                if (rbMS2.IsChecked.Value) Showcase.axisYrun.Label = "acceleration [m/s^2]";
                if (rbMg.IsChecked.Value) Showcase.axisYrun.Label = "acceleration [mg]";
                Showcase.axisDrun.Visibility = Visibility.Collapsed;
                Showcase.plotDiff.Visibility = Visibility.Collapsed;
            }
            Showcase.InitRun(depth);
        }
        public void ShowcaseNextScanPoint(double xVl, double A)
        {
            if (!IsShowcaseShowing) return;
            if (showcaseState.Equals(ShowcaseStates.scan)) Showcase.nextScanPoint(xVl, A);
        }
        public void ShowcaseNextRunPoint(double xVl, Dictionary<string, double> dp)
        {
            if (!IsShowcaseShowing) return;            
            double mg2ms2(double mg) // 1000 mg = 1 gMems
            {
                return gMems * (mg / 1000);
            }            
            if (showcaseState.Equals(ShowcaseStates.run))
            {
                if (rbMS2.IsChecked.Value)
                {                                   
                    if (dp.ContainsKey("PhiMg"))                    
                        dp["PhiMg"] = mg2ms2(dp["PhiMg"]);                    
                    
                    if (dp.ContainsKey("MEMS"))                    
                        dp["MEMS"] = mg2ms2(dp["MEMS"]);
                    
                    if (dp.ContainsKey("Accel"))                    
                        dp["Accel"] = mg2ms2(dp["Accel"]);                     
                }
                if (rbMQdiff.IsChecked.Value && dp.ContainsKey("MEMS") && dp.ContainsKey("PhiMg")) 
                    dp["Diff"] = dp["MEMS"] - dp["PhiMg"];
                Showcase.nextRunPoint(xVl, dp);
            }
        }
        private int scCount;
        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            if (!IsShowcaseShowing) return;
            switch (showcaseState)
            {
                case ShowcaseStates.idle:
                    dTimer.Stop();
                    break;
                case ShowcaseStates.scan:
                    ShowcaseNextScanPoint(scCount, Math.Sin(0.1 * scCount));
                    scCount++;
                    break;
                case ShowcaseStates.run:
                    Dictionary<string, double> dp = new Dictionary<string, double>();
                    dp["PhiMg"] = Math.Sin(0.1 * scCount);
                    dp["MEMS"] = Math.Sin(0.1 * scCount) + 0.4;
                    dp["Accel"] = Math.Sin(0.1 * scCount) - 0.2;
                    ShowcaseNextRunPoint(scCount, dp);
                    scCount++;
                    break;
            }
        }
        public void Final()
        {
            if (!Utils.isNull(Showcase)) Showcase.Close();
        }
    }
}
