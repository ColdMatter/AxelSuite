using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
using NationalInstruments.Analysis.Math;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using AxelChartNS;
using RemoteMessagingNS;
using UtilsNS;

namespace Axel_show
{
    public struct gMap
    {
        public string html;
        public int zoom;
        public Point center, initPos, size;
        public List<Point> loc; 
        public List<Point> start;
        public int numbPlane;
        public string googleKey;
        public double fact;

        public gMap(bool dummy) 
        {
            // map itself
            html = "";
            googleKey = File.ReadAllText(Utils.configPath+"google.key"); 
            size = new Point(500, 500); // width, height
            zoom = 13;
            fact = -1;
            center = new Point(49.910, -6.431); //  latitude(south-north), longitude(east-west)
            // the planes            
            initPos = new Point(49.912, -6.333);
            numbPlane = 4;
            loc = new List<Point>(); start = new List<Point>();
            for (int i = 0; i < numbPlane; i++) loc.Add(new Point());
            for (int i = 0; i < 3; i++) start.Add(new Point()); 
        }

        public Point pos(double dist, int idx, double bearing = 270)
        {
            const double R = 6371e3;
            double phi = (initPos.X + idx * 0.006) * Math.PI / 180; // latitude(south-north) 
            double lamda = initPos.Y * Math.PI / 180; // longitude(east-west)
            double brng = bearing * Math.PI / 180; // all in rad from north

            double phi2 = Math.Asin(Math.Sin(phi) * Math.Cos(dist / R) + Math.Cos(phi) * Math.Sin(dist / R) * Math.Cos(brng)); // [rad]
            double lamda2 = lamda + Math.Atan2(Math.Sin(brng) * Math.Sin(dist / R) * Math.Cos(phi),  // [rad]
                                       Math.Cos(dist / R) - Math.Sin(phi) * Math.Sin(phi2));
            return new Point(phi2 * 180 / Math.PI, lamda2 * 180 / Math.PI); //  latitude(south-north), longitude(east-west)
        }

        public Point middle() // of loc
        {
            Point md = new Point();
            for (int i = 0; i < numbPlane; i++)
            {
                md.X = md.X + loc[i].X;
                md.Y = md.Y + loc[i].Y;
            }
            md.X = md.X / numbPlane; md.Y = md.Y / numbPlane;  
            return md;
        }
    } 

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        const bool DebugMode = false;
        const int dsCount = 5;
        private int refIdx = 0;
        int depth = 10000;
        List<DataStack> dsAcc, dsTr;
        List<string> archive;
        Random random;
        Stopwatch sw = new Stopwatch();
        DispatcherTimer dTimer, mapTimer;
        StringBuilder htm = null;
        gMap map = new gMap(true);

        RemoteMessaging remoteHub, remoteTilt;
        private AutoFileLogger logger, traceLog;
        
        public MainWindow()
        {
            InitializeComponent();
            dsAcc = new List<DataStack>(); // MEMS, MEMS2, MOTaccel, Accel, Tilt
            dsTr = new List<DataStack>();
            for (int i = 0; i < dsCount; i++)
            {
                dsAcc.Add(new DataStack(depth)); dsTr.Add(new DataStack(depth)); 
            }
            random = new Random((int)(DateTime.Now.Ticks & 0xFFFFFFFF));

            dTimer = new System.Windows.Threading.DispatcherTimer();
            dTimer.Tick += new EventHandler(dTimer_Tick);
            dTimer.Interval = new TimeSpan(0, 0, 2);

            mapTimer = new System.Windows.Threading.DispatcherTimer();
            mapTimer.Tick += new EventHandler(mapTimer_Tick);
            mapTimer.Interval = new TimeSpan(0, 0, 6);
        }

        private void newShot(double time, double[] acc) // for archive and random modes
        {
            if (acc.Length != dsCount) throw new Exception("Wrong array length");
            for (int i = 0; i < dsCount; i++)
            {
                if (Double.IsNaN(time)) dsAcc[i].AddPoint(acc[i]);
                else dsAcc[i].AddPoint(acc[i], time);
            }
            int cnt = dsAcc[0].Count;
            double[] db = calcTrajAtIdx(cnt - 1);
            for (int j = 0; j < dsCount; j++)
            {
                double last = 0; if (dsTr[j].Count > 0 && !request2Reset) last = dsTr[j].Last.Y;
                dsTr[j].AddPoint(last + db[j], dsAcc[0].Last.X);
            }
            request2Reset = false;
        }

        private void dTimer_Tick(object sender, EventArgs e)
        {
            if (!btnGo.Value) return;

            if (rbDataFlow.IsChecked.Value)
            {
                if (dsAcc[0].Count == 0) return;
                Refresh();
            }
            if (rbArchive.IsChecked.Value && !Utils.isNull(archive))
            {               
                string[] sa = archive[dsAcc[0].Count].Split('\t');
                double tm = Convert.ToDouble(sa[0]); double[] acc = new double[dsCount];
                for (int i = 0; i < dsCount; i++) 
                    acc[i] = Convert.ToDouble(sa[i+1]);
                newShot(tm, acc);
                Refresh();
            }
            if (rbRandom.IsChecked.Value)
            {
                double[] acc = new double[dsCount];
                for (int i = 0; i < dsCount; i++) 
                    acc[i] = i + 0.5 * random.NextDouble();
                newShot(Double.NaN, acc);
                Refresh();
            }
        }

        private void mapTimer_Tick(object sender, EventArgs e)
        {
            if (!btnGo.Value || !chkMap.IsChecked.Value) return;
            if (!Utils.isNull(htm))
            {
                int j = 0;
                while (map.fact > 0 || j > 10000) { DoEvents(); j++;  }
                webBrowser.NavigateToString(htm.ToString()); DoEvents();
            }
        }

        private void Axel_show_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if(rowTools.Height.Value == 0) rowTools.Height = new GridLength(60, GridUnitType.Pixel);
            else rowTools.Height = new GridLength(0, GridUnitType.Pixel);
        }
        public void DoEvents()
        {
             if (closingFlag) return; Thread.Sleep(1);
             System.Windows.Application.Current.Dispatcher.Invoke(
                System.Windows.Threading.DispatcherPriority.Background,
                new System.Action(delegate { }));
        }
/*
        public void DoEvents()
        {
            System.Windows.Application.Current.Dispatcher.Invoke(
                System.Windows.Threading.DispatcherPriority.Background,
                new System.Threading.ThreadStart(() => { }));
        public void DoEvents()
        {
            //if (closingFlag) return; Thread.Sleep(1);
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
*/
        private void Clear(bool acc = true, bool tr = true)
        {
            for (int i = 0; i < dsCount; i++)
            {
                if (acc) dsAcc[i].Clear(); 
                if (tr) dsTr[i].Clear();
            }
        }

        private double convertMg2mms2(double mg)
        {
            return 9.8 * mg;
        }
        // 0 - MEMS; 1 - MEMS2; 2 - PhiMg; 3 - Accel; 4 - Tilt
        private void Refresh(int backFrom = 0, bool adjustAxes = true)
        {
            if (!btnGo.Value || dsAcc[0].Count == 0 || closingFlag) return; //|| dsTr[0].Count == 0
            int len = (int)numNP.Value; double[] acc = new double[dsCount]; double[] tr = new double[dsCount];
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, // graphs
                new Action(() => 
                {
                    DataStack ds = dsAcc[2].Portion(len, backFrom); acc[2] = ds.Last.Y;
                    graphAccel.Data[0] = ds;
                    ds = dsAcc[1].Portion(len, backFrom); acc[1] = ds.Last.Y;
                    graphAccel.Data[1] = ds;
                    ds = dsAcc[refIdx].Portion(len, backFrom); acc[refIdx] = ds.Last.Y;
                    graphAccel.Data[2] = ds;

                    DataStack dt = dsTr[2].Portion(len, backFrom); tr[2] = dt.Last.Y;
                    graphTraj.Data[0] = dt;
                    dt = dsTr[1].Portion(len, backFrom); tr[1] = dt.Last.Y;
                    graphTraj.Data[1] = dt;
                    dt = dsTr[refIdx].Portion(len, backFrom); tr[refIdx] = dt.Last.Y;
                    graphTraj.Data[2] = dt;      
              
                    if (adjustAxes)
                    {
                        DataStack wAcc = dsAcc[0].Portion(len,backFrom);
                        if (wAcc.Count > 2)
                        {
                            if (wAcc.First.X < wAcc.Last.X) // horizontal
                            {
                                double last = wAcc.First.X + (wAcc[1].X - wAcc[0].X) * numNP.Value;
                                last = Math.Max(0.9*last, wAcc.Last.X);
                                accelXaxis.Range = new Range<double>(wAcc.First.X, last);
                                trajXaxis.Range = new Range<double>(wAcc.First.X, last);
                            }
                        }
                    }
                }));
            DoEvents(); if (closingFlag) return;

            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, // labels
                new Action(() => 
                {
                    lbMEMS.Content = convertMg2mms2(acc[1]).ToString(tbPrec.Text); // MEMS2
                    lbMOTaccel.Content = convertMg2mms2(acc[2]).ToString(tbPrec.Text); // MOTaccel
                    lbRefAccel.Content = convertMg2mms2(acc[refIdx]).ToString(tbPrec.Text); // ref

                    string ss = (sw.ElapsedMilliseconds / 1000.0).ToString(tbPrec.Text) + "\t";
                    for (int i = 0; i < dsCount-1; i++) ss += acc[i].ToString(tbPrec.Text) + "\t";
                    if (chkLog.IsChecked.Value) logger.log(ss);
                }));
            DoEvents(); if (closingFlag) return;
            
            if (!chkMap.IsChecked.Value) return;  // map
            htm = new StringBuilder(map.html);
            map.fact = 1;
            htm.Replace("#googleKey#", map.googleKey);
            htm.Replace("#zoom#", map.zoom.ToString());
            htm.Replace("#center#", map.center.X.ToString("G5") + "," + map.center.Y.ToString("G5"));
            htm.Replace("#height#", (map.size.Y/0.61).ToString("F0")); htm.Replace("#width#", (map.size.X/0.58).ToString("F0"));
            map.start[0] = map.pos(map.initPos.Y, 2); htm.Replace("#StartUp#", map.start[0].X.ToString("G7") + "," + (map.start[0].Y).ToString("G7"));
            map.start[1] = map.pos(map.initPos.Y, -2); htm.Replace("#StartDown#", map.start[1].X.ToString("G7") + "," + (map.start[1].Y).ToString("G7"));

            map.loc[0] = map.pos(tr[1] * map.fact, 1); htm.Replace("#MEMS#", map.loc[0].X.ToString("G7") + "," + (map.loc[0].Y).ToString("G7"));
            map.loc[1] = map.pos(tr[2] * map.fact, 0); htm.Replace("#MOT#", map.loc[1].X.ToString("G7") + "," + (map.loc[1].Y).ToString("G7"));
            map.loc[2] = map.pos(tr[refIdx] * map.fact, -1); htm.Replace("#REF#", map.loc[2].X.ToString("G7") + "," + (map.loc[2].Y).ToString("G7"));
            if(DebugMode) File.WriteAllText(Utils.configPath + "temp.html", htm.ToString());
            map.fact = -1;
            DoEvents(); 
        }

        private double calcSingleInteg(int idx, DataStack ds) // looks two point back to integrate
        {
            if (idx == 0 || idx == 1) return 0.0;
            if(!Utils.InRange(idx,0,ds.Count-1) || Utils.isNull(ds)) return Double.NaN;
            double dt = (ds[idx].X - ds[idx - 2].X)/2;
            return ((ds[idx - 2].Y + 2*ds[idx - 1].Y + ds[idx].Y) / 4) * dt * dt;
        }

        private double[] calcTrajAtIdx(int idx)
        {           
            // ALL have the same X axis           
            double[] d = new double[dsCount];
            for (int i = 0; i < dsCount; i++)
            {
                if (!Utils.InRange(idx, 0, dsAcc[i].Count - 1)) throw new Exception("Wrong index (calcTrajAtIdx/high): " + idx.ToString());
                if (idx == 0 || idx == 1) d[i] = 0.0;
                else d[i] = calcSingleInteg(idx, dsAcc[i]);
                if (Double.IsNaN(d[i])) throw new Exception("Wrong index (calcTrajAtIdx/low): " + idx.ToString()); 
            }               
            return d;
        }
                
        private void dataFlowGo()
        {
            if (!remoteHub.Connected) Utils.TimedMessageBox("No connection to Axel-hub.");
            else
            {
                if (btnGo.Value)
                {
                    Utils.TimedMessageBox("Ready to roll. Start acquisition!");
                    Clear();
                }
                else Utils.TimedMessageBox("Acquisition has been canceled.");
                bool bb = btnGo.Value && chkLog.IsChecked.Value;
                if (bb) { logger.Enabled = false; logger.AutoSaveFileName = ""; } logger.Enabled = bb;
                logger.log("#Time\tMEMS\tMEMS2\tMOTAccel\tAccel\tTilt");
                traceLog.Enabled = btnGo.Value && DebugMode;
                sw.Restart(); stage = 0;
            }                
            //if (!remoteTilt.Connected) Utils.TimedMessageBox("No connection to Axel-tilt.");
        }

        private void archiveGo() // load archive file
        {
            if (btnGo.Value) Clear();
            else return;
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.FileName = ""; // Default file name
            dlg.InitialDirectory = Utils.dataPath;
            dlg.Filter = "Axel Log File (.log)|*.log"; // Filter files by extension
            Nullable<bool> result = dlg.ShowDialog();
            if (result == false) return;

            archive = Utils.readList(dlg.FileName);
            for (int i = 0; i < dsCount; i++)
            {
                dsAcc[i].Depth = Math.Min(archive.Count, 10000);
            }
        }

        private void randomGo() // prepare for random mode
        {
            if (btnGo.Value) Clear();
            else return;
            int totNP = (int)(numNP.Value*10.0);
            for (int i = 0; i < dsCount; i++)
            {                        
                dsAcc[i].Depth = totNP;                                 
            }
        }
       
        private void btnGo_Click(object sender, RoutedEventArgs e)
        {
            btnGo.Value = !btnGo.Value;
            if (btnGo.Value)
            {
                // prepare
                if (rbDataFlow.IsChecked.Value) dataFlowGo();
                if (rbArchive.IsChecked.Value) archiveGo();
                if (rbRandom.IsChecked.Value) randomGo();
                // run
                dTimer.Start(); mapTimer.Start(); 
            }
            else
            {
                 dTimer.Stop(); mapTimer.Stop();
            }
            grpDataSource.IsEnabled = !btnGo.Value;
        }

        private void graphAccel_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ((Graph)sender).ResetZoomPan();
        }

        private void chkManualYaxis_Checked(object sender, RoutedEventArgs e)
        {
            if (chkManualYaxis.IsChecked.Value)
            {
                accelYaxis.Adjuster = RangeAdjuster.None; graphAccel.HorizontalScrollBarVisibility = GraphScrollBarVisibility.Auto;
                trajYaxis.Adjuster = RangeAdjuster.None; graphTraj.HorizontalScrollBarVisibility = GraphScrollBarVisibility.Auto;
            }
            else
            {
                accelYaxis.Adjuster = RangeAdjuster.FitLoosely; graphAccel.HorizontalScrollBarVisibility = GraphScrollBarVisibility.Hidden;
                trajYaxis.Adjuster = RangeAdjuster.FitLoosely; graphTraj.HorizontalScrollBarVisibility = GraphScrollBarVisibility.Hidden;
            }
        }

        private void numNP_ValueChanging(object sender, ValueChangingEventArgs<double> e)
        {
            if(btnGo.Value) btnGo_Click(null,null);
        }

        int stage = 0; double none = 0.0;
        private bool OnHubReceive(string message) // get data for Axel-hub
        {
            traceLog.log("in> " + message);
	        if (!btnGo.Value) return false;
            if (stage != 0) traceLog.log(sw.ElapsedMilliseconds.ToString() + " > wait for st:0");
            while (stage != 0) { DoEvents(); }
            if (stage != 0) Utils.TimedMessageBox("Wrong stage in Axel-show (0 expected, "+stage.ToString()+" observed)");
            stage = 1; traceLog.log(sw.ElapsedMilliseconds.ToString() + " > st:1");
            if(!rbDataFlow.IsChecked.Value || !btnGo.Value) return false;
            Dictionary<string, double> dt = new Dictionary<string, double>();
            try
            {
                bool back = true;
                dt = JsonConvert.DeserializeObject<Dictionary<string, double>>(message);
                if((int)dt["sender"] != 667) return false;
                if (dt.Count == 1) return true;
                double tm = sw.ElapsedMilliseconds / 1000.0;

                if (dt.ContainsKey("MEMS")) dsAcc[0].AddPoint(dt["MEMS"], tm);
                else dsAcc[0].AddPoint(none, tm);
                if (dt.ContainsKey("MEMS2")) dsAcc[1].AddPoint(dt["MEMS2"], tm);
                else dsAcc[1].AddPoint(none, tm);
                if (dt.ContainsKey("PhiMg")) dsAcc[2].AddPoint(dt["PhiMg"], tm);
                else dsAcc[2].AddPoint(none, tm);
                if (dt.ContainsKey("Accel")) dsAcc[3].AddPoint(dt["Accel"], tm);
                else dsAcc[3].AddPoint(none, tm);
                stage = 2; traceLog.log(sw.ElapsedMilliseconds.ToString() + " > st:2");
                // ask Tilt for its position
                if (remoteTilt.Connected)
                {
                    remoteTilt.sendCommand("query.tilt", 5);                   
                    stage = 3; traceLog.log(sw.ElapsedMilliseconds.ToString() + " > st:3"); 
                    int j = 0;
                    while (stage == 3 && j < 5000) { j++; DoEvents(); }
                    if (j > 4900) Utils.TimedMessageBox("Tilt reply - time out!");
                }
                else
                {
                    if (Utils.TheosComputer()) OnTiltReceive("tilt=" + (0.9 + 0.1 * random.NextDouble()).ToString("G6")); // simulation random
                    else OnTiltReceive("tilt=0.0");
                }                
                return back;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return false;
            }
        }

        private bool OnTiltReceive(string message)
        {
            try
            {
                bool back = true;
                string[] sa = message.Split('=');
                if (sa[0].Equals("tilt")) dsAcc[dsCount-1].AddPoint(Convert.ToDouble(sa[1]), sw.ElapsedMilliseconds / 1000.0);
                else { throw new Exception("Wrong <query.tilt> reply"); }
                int len = dsAcc[0].Count; traceLog.log(sw.ElapsedMilliseconds.ToString() + " > tilt");
                if (dsAcc[1].Count != len || dsAcc[2].Count != len || dsAcc[3].Count != len || dsAcc[4].Count != len) { Utils.TimedMessageBox("dsAcc size problem","Warning",500); }
                if (len < 3)
                {
                    stage = 0; traceLog.log(sw.ElapsedMilliseconds.ToString() + " >> st:0"); return true;
                }
                if (stage == 2 || stage == 3)
                {
                    double[] db = calcTrajAtIdx(len - 2); double prev = 0;  traceLog.log(sw.ElapsedMilliseconds.ToString() + " > calcTrajAtIdx");
                    for (int j = 0; j < dsCount; j++)
                    {
                        if (dsTr[j].Count == 0 || request2Reset) prev = 0;
                        else prev = dsTr[j].Last.Y;
                        dsTr[j].AddPoint(prev + db[j], dsAcc[0].Last.X);
                    }
                    request2Reset = false;
                    stage = 0; traceLog.log(sw.ElapsedMilliseconds.ToString() + " >>> st:0"); 
                }
                else { Utils.TimedMessageBox("Wrong stage in Axel-show (2,3 expected, " + stage.ToString() + " observed)"); ; }
                return back;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return false;
            }
        }

        private void OnHubActiveComm(bool active, bool forced)
        {
            ledAxelHub.Value = active && remoteHub.Enabled;
        }

        private void OnTiltActiveComm(bool active, bool forced)
        {
            ledAxelTilt.Value = active && remoteTilt.Enabled;
        }

        private void chkAxelHub_Checked(object sender, RoutedEventArgs e)
        {
            if (Utils.isNull(remoteHub)) return;
            remoteHub.Enabled = chkAxelHub.IsChecked.Value;
            remoteHub.CheckConnection(true);
        }
        private void chkAxelTilt_Checked(object sender, RoutedEventArgs e)
        {
            if (Utils.isNull(remoteTilt)) return;
            remoteTilt.Enabled = chkAxelTilt.IsChecked.Value;
            remoteTilt.CheckConnection(true);
        }

        private void Axel_show_Loaded(object sender, RoutedEventArgs e)
        {
            remoteHub = new RemoteMessaging("Axel Hub", 667);
            remoteHub.Enabled = false;
            remoteHub.OnReceive += new RemoteMessaging.ReceiveHandler(OnHubReceive);
            remoteHub.OnActiveComm += new RemoteMessaging.ActiveCommHandler(OnHubActiveComm);

            remoteTilt = new RemoteMessaging("Axel Tilt", 668);
            remoteTilt.Enabled = false;
            remoteTilt.OnReceive += new RemoteMessaging.ReceiveHandler(OnTiltReceive);
            remoteTilt.OnActiveComm += new RemoteMessaging.ActiveCommHandler(OnTiltActiveComm);

            map.html = File.ReadAllText(Utils.configPath + "Trajectories.html");
            string path = Utils.configPath.Replace("\\","/"); 
            map.html = map.html.Replace("#path#", path);

            depth = (int)numNP.Range.Maximum;
            logger = new AutoFileLogger(); logger.defaultExt = ".log"; logger.Enabled = false;
            traceLog = new AutoFileLogger(); traceLog.defaultExt = ".trc"; traceLog.Enabled = false;

            numRate_ValueChanged(null, null);
            chkAxelHub_Checked(null, null); chkAxelTilt_Checked(null, null);
        }

        private void rbDataFlow_Checked(object sender, RoutedEventArgs e)
        {
            if (Utils.isNull(chkLog)) return;
            if (rbDataFlow.IsChecked.Value) chkLog.Visibility = System.Windows.Visibility.Visible;
            else chkLog.Visibility = System.Windows.Visibility.Collapsed;
            chkLog.IsChecked = false;
        }
        bool request2Reset = false;
        private void btnReset_Click(object sender, RoutedEventArgs e)
        {
            request2Reset = true;
        }

        private void webBrowser_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            map.size.Y = webBrowser.ActualHeight - 1; map.size.X = webBrowser.ActualWidth - 1;
            //mapSize.Y = gridMain.RowDefinitions[3].ActualHeight - 1; mapSize.X = gridMain.ActualWidth - 1; 
        }

        private bool closingFlag = false;
        private void Axel_show_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            closingFlag = true;
            if(btnGo.Value) btnGo_Click(null, null); 
        }

        private void numRate_ValueChanged(object sender, ValueChangedEventArgs<double> e)
        {
            if (Utils.isNull(dTimer) || Utils.isNull(numRate)) return;
            int tick_time = (int)(numRate.Value * 1000.0 * 10000.0); // in ticks
            bool running = dTimer.IsEnabled;
            if(running) dTimer.Stop();
            dTimer.Interval = new TimeSpan(tick_time);
            if (running) dTimer.Start();
            running = mapTimer.IsEnabled;
            if (running) mapTimer.Stop();
            mapTimer.Interval = new TimeSpan((int)(tick_time*Math.PI));
            if (running) mapTimer.Start();
        }

        private void imgPeacock_MouseDown(object sender, MouseButtonEventArgs e)
        {
            MessageBox.Show("           Axel Show v1.4 \n         by Teodor Krastev \nfor Imperial College, London, UK\n\n   visit: http://axelsuite.com", "About");
        }

        private void cbRef_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch (cbRef.SelectedIndex)
            {
                case 0: refIdx = 0;
                    break;
                case 1: refIdx = 4;
                    break;
            }
            if (btnGo.Value) btnGo_Click(null, null);    
        }
     }
}
