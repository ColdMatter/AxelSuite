using System;
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
using System.IO;
using NationalInstruments.Controls;
using NationalInstruments.Analysis.Math;
using RemoteMessagingNS;
using Newtonsoft.Json;
using UtilsNS;

namespace Axel_probe
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {   
        ChartCollection<Point> fringes, ramp, corr;
        ChartCollection<double> signalN, signalB;
        List<double> iStack, dStack, corrList;
        bool cancelRequest = false;
        bool pauseSingle = false;
        DispatcherTimer dispatcherTimer;
        double driftRange, driftStep; 
        const double driftPeriod = 100;
        RemoteMessaging remote;
        string remoteDoubleFormat = "G6";
        Random rnd = new Random();
        AutoFileLogger logger;
        Stopwatch sw;

        public MainWindow()
        {
            InitializeComponent();

            driftRange = ndAmplitude.Value; driftStep = ndStep.Value;

            dispatcherTimer = new DispatcherTimer(DispatcherPriority.Send);
            dispatcherTimer.Tick += dispatcherTimer_Tick;
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);

            fringes = new ChartCollection<Point>();
            grFringes.DataSource = fringes;

            signalN = new ChartCollection<double>();
            graphNs.Data[0] = signalN;
            signalB = new ChartCollection<double>();
            graphNs.Data[1] = signalB;

            ramp = new ChartCollection<Point>();
            grRslt.Data[0] = ramp;
            corr = new ChartCollection<Point>();
            grRslt.Data[1] = corr;

            iStack = new List<double>(); dStack = new List<double>(); corrList = new List<double>();
            logger = new AutoFileLogger();
            sw = new Stopwatch();
            Running = false;
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

        private int getIfringe(double x) // get index from phase in fringes
        {
            for (int i = 0; i < fringes.Count-1; i++)
            {
                if (Utils.InRange(x,fringes[i].X,fringes[i+1].X)) return i;
            }
            return -1;
        } 

        private void log(string txt)
        {
            if (!chkLog.IsChecked.Value) return;
            tbLog.AppendText(txt + "\r\n");
            string text = tbLog.Text;
            int maxLen = 10000;
            if (text.Length > 2 * maxLen) tbLog.Text = text.Substring(maxLen);
            tbLog.Focus();
            tbLog.CaretIndex = tbLog.Text.Length;
            tbLog.ScrollToEnd();
        }
        
        private bool debugMode = true;
        private void dlog(string txt)
        {
            if(debugMode) log(txt);
        }

        Random rand = new Random(); 
        public Point Gauss()
        {
            Point g = new Point(0, 0);
            if (chkAddGaussX.IsChecked.Value)
            {
                g.X = Gauss01() * ndGaussNoiseX.Value;
            }
            if (chkAddGaussY.IsChecked.Value)
            {
                g.Y = Gauss01() * ndGaussNoiseY.Value / 100;
            }
            return g;
        }

        public double Gauss01()  //random normal mean:0 stDev:1
        {
            double u1 = 1.0 - rand.NextDouble(); //uniform(0,1] random doubles
            double u2 = 1.0 - rand.NextDouble();
            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) *
                         Math.Sin(2.0 * Math.PI * u2); 
            return randStdNormal;
        }

        public double Breathing(double x)
        {
            if (!chkVaryAmpl.IsChecked.Value) return 0;
            double per = Math.Sin(Math.PI * x / ndBreathePeriod.Value);
            double norm = ndBreatheAmpl.Value / 100;
            return per * norm;
        }

        private double accelDrift(double pos) // return drift [mg] at pos for selected accel. trend
        {
            double halfPrd = 0.5*driftPeriod;
            double rng = ndAmplitude.Value;
            double slope = rng / halfPrd;
            double drift = 0;
            switch (cbDriftType.SelectedIndex)
            {
                case 0: drift = rng;
                    break;
                case 1:// trapeze 1323
                    if (pos < (0.8 * halfPrd))
                    {
                        drift = slope * pos;
                    };
                    if ((pos >= (0.8 * halfPrd)) && (pos <= (1.2 * halfPrd)))
                    {
                        drift = 0.8 * rng;
                    }
                    if (pos > (1.2 * halfPrd))
                    {
                        drift = rng - slope * (pos - halfPrd);
                    }
                    drift = drift / 0.8;
                    break;
                case 2: // sine
                    drift = rng * Math.Sin(2 * Math.PI * pos );
                    break;
            }
            return drift;
        }

        public Dictionary<string, double> decomposeAccel(double accel, double mems, double fringeTop, double factor)
        {
            // accel[mg] - target(real); mems[mg] - measured (real + noise); fringeTop[mg] - from PID follow; factor[mg per 2pi]
            Dictionary<string, double> da = new Dictionary<string, double>();
            da["accel.R"] = accel;
            double orderR = Math.Floor(accel / factor);
            da["order.R"] = orderR;
            da["phase.R"] = accel - orderR * factor;

            da["mems"] = mems;
            double ft = fringeTop/factor; // phase normalized to [mg/2pi] units
            ft = ft - (int)ft; // res [0,1]
            ft = ft * factor; // now in mg 
            da["order.M"] = Math.Round((mems - ft) / factor); // using the phase (interfer.) from PID
            da["phase.M"] = ft;
            da["accel.M"] = da["order.M"] * factor + ft;

            da["resid"] = accel - da["accel.M"];
            if (!Utils.isNull(ress)) ress.Add(da["resid"]);
            lbDecomposeAccel.Items.Clear();
            foreach (var pair in da)
            {
                ListBoxItem lbi = new ListBoxItem();
                lbi.Content = pair.Key +": "+ pair.Value.ToString("G5");
                int c = lbDecomposeAccel.Items.Count;
                if (Utils.InRange(c, 0, 2)) lbi.Foreground = Brushes.Green;
                if (Utils.InRange(c, 3, 6)) lbi.Foreground = Brushes.Blue;
                lbDecomposeAccel.Items.Add(lbi);
            }
            double tm = sw.ElapsedMilliseconds/1000.0;
            logger.log(tm.ToString("G5")+"\t"+ mems.ToString("G6") + "\t" + ft.ToString("G6") + "\t" + da["accel.M"].ToString("G6"));
            return da;
        }

        private double fringesGenerator(double driftPos, double driftAcc) // return 
        {
            double sp = 0; double noise = Gauss().X;
            fringes.Clear(); double orderFactor = ndOrderFactor.Value; 
            while ((sp < 4 * Math.PI) && !stopRequest)
            {
                fringes.Add(new Point(sp, Breathing(driftPos) + Math.Cos(sp - driftAcc / orderFactor + noise) + Gauss().Y));
                sp += Math.PI / 200;
                //if ((k % 10) == 0) { DoEvents(); k++; }
            }
            grFringes.DataSource = fringes;
            return driftAcc + noise;
        }

        Dictionary<string, double> lastDecomposedAccel = new Dictionary<string, double>();
        private double calcAtPos(bool jumbo, double driftPos, bool odd) // driftPos - [0..driftPeriod]; odd - odd/even number for 2 strobe mode
            // returns drift [mg]
        {
            double err;
            double orderFactor = ndOrderFactor.Value;
            double driftAcc = accelDrift(driftPos); // noiseless accel. [mg] from driftPos
            if (jumbo) ramp.Add(new Point(driftPos, driftAcc));

            double accel = fringesGenerator(driftPos, driftAcc); // accel. [mg] with noise
            log("pos.X: "+driftPos.ToString("G5")+"; acc: " + driftAcc.ToString("G5") + "; +ns: " + accel.ToString("G5"));
            DoEvents();
            double midFringe = -1; //double fringeTopRad = -1;
            if (chkFollowPID.IsChecked.Value)
            {
                if (rbSingle.IsChecked.Value)
                {
                    err = SingleAdjust(driftAcc);
                    if (!double.IsNaN(err) && jumbo)
                    {
                        corr.Add(new Point(driftPos, err)); corrList.Add(err);
                    }                    
                    midFringe = ((double)crsDownStrobe.AxisValue - (Math.PI/2));
                }
                if (rbDouble.IsChecked.Value)
                {
                    err = DoubleAdjust(!odd, driftAcc);
                    if (!double.IsNaN(err) && jumbo)
                    {
                        corr.Add(new Point(driftPos, err)); corrList.Add(err);
                    }
                    midFringe = (((double)crsUpStrobe.AxisValue + (double)crsDownStrobe.AxisValue) / 2);
                }
            }
            double midFringeMg = fringeRad2mg(midFringe); // from rad to mg
            lastDecomposedAccel = decomposeAccel(driftAcc, accel, midFringeMg, 2*Math.PI * orderFactor);
            DoEvents(); devList.Add(Math.Abs(midFringe - fringeMg2rad(accel)));
            log("fringe drift[mg]/[rad]= " + midFringeMg.ToString("G5") + " / " + midFringe.ToString("G5"));
            log("-----------------------------------");
            return driftAcc;
        }
        public bool Running 
        {
            get { return btnRun.Content.Equals("S t o p"); }
            set 
            {
                if (value) { btnRun.Content = "S t o p"; btnRun.Foreground = Brushes.DarkRed; }
                else { btnRun.Content = "In  R U N"; btnRun.Foreground = Brushes.DarkGreen; }
            }
        }
        private void btnReset_Click(object sender, RoutedEventArgs e)
        {
            stopRequest = true;
            double rng = ndAmplitude.Value;
            double step = ndStep.Value;
            downhillLvl = double.NaN; uphillLvl = double.NaN;
            ((AxisDouble)grRslt.Axes[0]).Range = new Range<double>(0, driftPeriod);
            ((AxisDouble)grRslt.Axes[1]).Range = new Range<double>(-rng * 1.05, rng * 1.05);
            ((AxisDouble)grRslt.Axes[2]).Range = new Range<double>(-0.1, 0.1);

            ramp.Clear(); corr.Clear(); corrList.Clear(); devList.Clear();
            fringesGenerator(0, 0);
        }

        bool stopRequest = false; List<double> devList = new List<double>();
        private void btnRun_Click(object sender, RoutedEventArgs e)
        {
            DoEvents();
            Running = !Running;
            if (Running)
            {
                logger.AutoSaveFileName = ""; logger.Enabled = true; sw.Start();
            }
            else
            {
                logger.Enabled = false;
            }
            stopRequest = !Running;
            DoEvents();
            if (stopRequest) return;

            double rng = ndAmplitude.Value;
            double step = ndStep.Value;
            downhillLvl = double.NaN; uphillLvl = double.NaN;
            ((AxisDouble)grRslt.Axes[0]).Range = new Range<double>(0, driftPeriod);
            ((AxisDouble)grRslt.Axes[1]).Range = new Range<double>(-rng*1.05, rng*1.05);
            ((AxisDouble)grRslt.Axes[2]).Range = new Range<double>(-0.1, 0.1);

            double drift, pos; int j; pauseSingle = false;
             //tbLog.Text = ""; 
            log("RUN -> " + cbDriftType.Text);
            do
            {
                ramp.Clear(); corr.Clear(); corrList.Clear(); devList.Clear();
                pos = 0; j = 0;  
                while ((pos < driftPeriod + 0.0001) && !stopRequest)
                {
                    drift = calcAtPos(true, pos, (j % 2) == 1);
                    pos += step; j++;
                    do
                    {
                        DoEvents(); 
                    } while (chkPause.IsChecked.Value && !pauseSingle);
                    pauseSingle = false;
                    System.Threading.Thread.Sleep((int)ndDelay.Value);
                }                
                if (corrList.Count > 0)
                    log("Aver.dev.=" + devList.ToArray().Average().ToString("G4") + "\n====================");
            } while ((cbFinite.SelectedIndex == 1) && !stopRequest);
            Running = false; logger.Enabled = false; sw.Stop();
        }

        private void frmMain_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            stopRequest = true;
        }

        private void grFringes_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ((Graph)sender).ResetZoomPan();
        }

        int iStDepth = 5; int dStDepth = 3; // history depths 
        public double PID(double diffY) // returns correction
        {
            double pTerm = diffY; // proportional
            iStack.Add(diffY); while (iStack.Count > iStDepth) iStack.RemoveAt(0);
            double iTerm = iStack.Average(); // integral
            dStack.Add(diffY); while (dStack.Count > dStDepth) dStack.RemoveAt(0);
            double dTerm = 0; // differential
            for (int i = 0; i < dStack.Count - 1; i++)
            {
                dTerm += dStack[i + 1] - dStack[i];
            }
            dTerm /= Math.Max(dStack.Count - 1, 1);    
       
            double cr = ndKP.Value * pTerm + ndKI.Value * iTerm + ndKD.Value * dTerm;

            if (rbDouble.IsChecked.Value) cr /= 2;
            int curIdx1 = getIfringe((double)crsDownStrobe.AxisValue+cr);
            if (curIdx1 == -1) log("invalid index curIdx1");
            else log("PID> " + pTerm.ToString("G3") + "; " + iTerm.ToString("G3") + "; " + dTerm.ToString("G3") + " ("+cr.ToString("G3") +")"+
                     // PID X correction and Y value after the correction
                     "| new:" + fringes[curIdx1].X.ToString("G3") + "/" + fringes[curIdx1].Y.ToString("G3"));  // new pos X,Y
            return cr;
        }
        public double fringeRad2mg(double rad)
        {
            return (rad - Math.PI)  * ndOrderFactor.Value;
        }

        public double fringeMg2rad(double mg)
        {
            return (mg / ndOrderFactor.Value) + Math.PI;
        }

        private double SingleAdjust(double drift) // return error in fringe position/phase
        {
            double curX = (double)crsDownStrobe.AxisValue;
            int curIdx = getIfringe(curX);
            if (curIdx == -1)
            {
                log("Error: index cannot be found"); return double.NaN;
            }          
            if (!remote.Enabled)
            {
                double corr = PID(fringes[curIdx].Y);
                crsDownStrobe.AxisValue = curX + corr;
            }
            curIdx = getIfringe((double)crsDownStrobe.AxisValue);
            if (curIdx > -1) return drift - (2.5 * Math.PI - fringes[curIdx].X); 
            else return double.NaN;
        }

        double downhillLvl = double.NaN, uphillLvl = double.NaN;
        private double DoubleAdjust(bool even, double accel) // return error in fringe position/phase
        {
            // accel [mg] - set in fringes 

            // simulation of measurement at cursor position
            int curIdx1, curIdx2; double cx, curX1, curX2;
            if (even) // even
            {
                curX1 = (double)crsDownStrobe.AxisValue;
                curIdx1 = getIfringe(curX1);
                if (curIdx1 == -1)
                {
                    log("Error: index cannot be found"); return double.NaN;
                }
                downhillLvl = fringes[curIdx1].Y;
            }
            else // odd
            {            
                curX2 = (double)crsUpStrobe.AxisValue;
                curIdx2 = getIfringe(curX2);
                if (curIdx2 == -1)
                {
                    log("Error: index cannot be found"); return double.NaN;
                }
                uphillLvl = fringes[curIdx2].Y;
            }
            if (!remote.Connected) // local run
            {
                if (double.IsNaN(uphillLvl) || double.IsNaN(downhillLvl)) return double.NaN;
                double corr = PID(downhillLvl - uphillLvl);
                crsDownStrobe.AxisValue = (double)crsDownStrobe.AxisValue + corr; // adjust down-hill
                crsUpStrobe.AxisValue = (double)crsUpStrobe.AxisValue + corr; // adjust up-hill
            }
            cx = ((double)crsUpStrobe.AxisValue + (double)crsDownStrobe.AxisValue) / 2;
            return accel - fringeRad2mg(cx);
        }

        private void ndKP_KeyDown(object sender, KeyEventArgs e)
        {
           if ((e.Key == Key.L) && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
           {
               log("Coeff.PID= " + ndKP.Value.ToString("G3") + " / " + ndKI.Value.ToString("G3") + " / " + ndKD.Value.ToString("G3") + " ;");
           }              
        }

        private void ndRange_KeyDown(object sender, KeyEventArgs e)
        {
            if ((e.Key == Key.L) && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
            {
                log("Accel= " + ndAmplitude.Value.ToString("G3") + " / " + driftPeriod.ToString("G3") + " / " + ndStep.Value.ToString("G3") + " ;");
            }
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            tbLog.Text = "";
        }

        private void rbSingle_Checked(object sender, RoutedEventArgs e)
        {
            if ((crsUpStrobe == null) || (crsDownStrobe == null)) return;
            if (rbSingle.IsChecked.Value)
            {
                crsUpStrobe.Visibility = System.Windows.Visibility.Hidden;
                crsDownStrobe.AxisValue = 7.85;
            }
            else
            {
                crsUpStrobe.Visibility = System.Windows.Visibility.Visible;
                crsDownStrobe.AxisValue = 4.71;
                crsDownStrobe.AxisValue = 7.85;
                downhillLvl = double.NaN; uphillLvl = double.NaN;
            }
        }

        private void frmMain_Loaded(object sender, RoutedEventArgs e)
        {
            remote = new RemoteMessaging("Axel Hub");
            remote.OnActiveComm += new RemoteMessaging.ActiveCommHandler(OnActiveComm);
            remote.OnReceive += new RemoteMessaging.ReceiveHandler(OnReceive);
            remote.Enabled = chkRemoteEnabled.IsChecked.Value;
        }

        private MMexec lastGrpExe;
        private void OnActiveComm(bool active, bool forced)
        {
            ledComm.Value = active;
            if (active)
            {
                grpRemote.Foreground = Brushes.Green;
                grpRemote.Header = "Remote - is ready <->";
            }
            else
            {
                grpRemote.Foreground = Brushes.Red;
                grpRemote.Header = "Remote - problem -X-";
            }
        }

        private void btnCommCheck_Click(object sender, RoutedEventArgs e)        
        {
            chkRemoteEnabled.SetCurrentValue(CheckBox.IsCheckedProperty, true);
            if (remote.CheckConnection(true)) grpRemote.Header = "Remote - is ready <->";
            else {
                grpRemote.Header = "Remote - not found! ...starting it";
                System.Diagnostics.Process.Start(File.ReadAllText(Utils.configPath + "axel-hub.bat"), "-remote");
                Thread.Sleep(1000);
                remote.CheckConnection(true);
           }
        }

        private bool OnReceive(string json)
        {
            //if (string.IsNullOrEmpty(json) == true) // throw new Exception("Nothing to receive");
            if(Utils.isNull(json) || json == "") return false;
            MMexec mme = JsonConvert.DeserializeObject<MMexec>(json);
            if (Utils.isNull(mme)) return false;
            switch (mme.cmd)
            {
                case ("scan"):
                    {
                        MMscan mms = new MMscan();
                        lastGrpExe = mme.Clone();
                        if (mms.FromDictionary(mme.prms)) dispatcherTimer.Start();
                        else
                        {
                            MMexec mmj = new MMexec();
                            remote.sendCommand(mmj.Abort("Axel-probe"));
                            log("Error in incomming json !");
                        }                            
                    }
                    break;
                case ("repeat"):
                    {
                        if ((crsDownStrobe == null) || (crsUpStrobe == null)) return false;
                        if (mme.prms.ContainsKey("follow"))
                        {
                            chkFollowPID.IsChecked = (Convert.ToInt32(mme.prms["follow"]) == 1);
                        }
                        if (mme.prms.ContainsKey("strobes"))
                        {
                            if (Convert.ToInt32(mme.prms["strobes"]) == 1) rbSingle.IsChecked = true;
                            else rbDouble.IsChecked = true;
                        }
                            
                        if (rbSingle.IsChecked.Value)
                        {
                            crsUpStrobe.Visibility = System.Windows.Visibility.Hidden;
                            crsDownStrobe.AxisValue = Convert.ToDouble(mme.prms["downStrobe"]);
                        }
                        else
                        {
                            crsUpStrobe.Visibility = System.Windows.Visibility.Visible;
                            crsDownStrobe.AxisValue = Convert.ToDouble(mme.prms["downStrobe"]);
                            crsUpStrobe.AxisValue = Convert.ToDouble(mme.prms["upStrobe"]);
                            downhillLvl = double.NaN; uphillLvl = double.NaN;
                        }
                        lastGrpExe = mme.Clone();
                        dispatcherTimer.Start();
                    }
                    break;
                case ("phaseAdjust"):
                    {
                        if (!chkFollowPID.IsChecked.Value)
                        {
                            log("Skip phase adjust: non-following mode!"); return true;
                        }
                        
                        double phase = Convert.ToDouble(mme.prms["phase"]);
                        if(rbSingle.IsChecked.Value)
                        {
                            crsDownStrobe.AxisValue = phase;
                            log("<< phaseAdjust to "+ phase.ToString("G5"));
                        }
                        else
                        {
                            int runID = Convert.ToInt32(mme.prms["runID"]);
                            if (runID > 0)
                            {                           
                                if ((runID % 2) == 0)
                                {
                                    log("<< phAdj downhill " + ((double)crsDownStrobe.AxisValue).ToString("G5")+ " -> "+ phase.ToString("G5"));
                                    crsDownStrobe.AxisValue = phase; 
                                }
                                else
                                {
                                    log("<< phAdj uphill " + ((double)crsUpStrobe.AxisValue).ToString("G5") + " -> " + phase.ToString("G5"));
                                    crsUpStrobe.AxisValue = phase; 
                                }
                            }
                        }
                        wait4adjust = false;                      
                    }
                    break;
                case ("abort"):
                    {
                        cancelRequest = true;
                        log("<< External ABORT!!!");
                    }
                    break;
            }
            return true;
        }

        /* Example base data + some noise
           N2	Ntot	B2	Btot	Bg		N2	Ntot	B2	Btot	Bg
            1	5	    1	3	    0		3	5	    1	3	    0
										
            NB2	NBtot					    NB2	NBtot			
            0	2					        2	2			
										
            A						        A				
            1						        -1		*/
        private bool SingleShot(double A, // A = 1 .. -1
                                ref MMexec mme) // group template
        {
            log("A = "+A.ToString("G5"));
            bool rslt = false;
            double ntot = 5; double b2 = 1; double btot = 3; double bg = 0;
            double n2 = (1-A)*(ntot-btot)/2 + b2;   // n2 = 1 .. 3 / A = 1 .. -1              

            lboxNB.Items[1] = "NTot = " + ntot.ToString();
            lboxNB.Items[2] = "B2 = " + b2.ToString();
            lboxNB.Items[3] = "BTot = " + btot.ToString();
            lboxNB.Items[4] = "Bg = " + bg.ToString();

            double d, scl = 100;
            List<Double> srsN2 = new List<Double>();
            List<Double> srsNTot = new List<Double>();
            List<Double> srsB2 = new List<Double>();
            List<Double> srsBTot = new List<Double>();
            List<Double> srsBg = new List<Double>();
            signalN.Clear(); srsN2.Clear(); srsNTot.Clear(); 
            signalB.Clear(); srsB2.Clear(); srsBTot.Clear();  srsBg.Clear(); 
            for (int i = 0; i < 3000; i++)
            {
                d = n2 + Gauss01() / scl;
                lboxNB.Items[0] = "N2 = " + d.ToString(remoteDoubleFormat);
                srsN2.Add(Utils.formatDouble(d, remoteDoubleFormat));
                d = ntot + Gauss01() / scl;
                srsNTot.Add(Utils.formatDouble(d, remoteDoubleFormat));

                d = b2 + Gauss01() / scl;
                srsB2.Add(Utils.formatDouble(d, remoteDoubleFormat));
                d = btot + Gauss01() / scl;
                srsBTot.Add(Utils.formatDouble(d, remoteDoubleFormat));

                d = bg + Gauss01() / scl;
                srsBg.Add(Utils.formatDouble(d, remoteDoubleFormat));                   
            }
            signalN.Append(srsN2); signalN.Append(srsNTot);
            signalB.Append(srsB2); signalB.Append(srsBTot); 

            if (remote.Connected)
            {               
                mme.prms["N2"] = srsN2.ToArray(); mme.prms["NTot"] = srsNTot.ToArray();
                mme.prms["B2"] = srsB2.ToArray(); mme.prms["BTot"] = srsBTot.ToArray();
                mme.prms["Bg"] = srsBg.ToArray();
                mme.id = rnd.Next(int.MaxValue);
               
                string msg = JsonConvert.SerializeObject(mme);
                rslt = remote.sendCommand(msg);
                mme.prms["runID"] = (int)mme.prms["runID"]+1;
                log(msg.Substring(1,80)+"...");
                do
                {
                    DoEvents(); Thread.Sleep(20); 
                } while (chkPause.IsChecked.Value && !pauseSingle);
                pauseSingle = false;
            }
            return rslt;
        } 
        
        private void RealScan(MMscan mms)
        {
            MMexec md = new MMexec();
            md.mmexec = "";
            md.cmd = "shotData";
            md.sender = "Axel-probe";
            md.prms["groupID"] = mms.groupID;
            md.prms["runID"] = 0;

            double n2 = 1; double ntot = 5; // n2 = 1 .. 3 / A = 1 .. -1              
            double b2 = 1; double btot = 3; double bg = 0;

            cancelRequest = false; double A = 0;  fringes.Clear();
            for (double ph = mms.sFrom; ph < mms.sTo + 0.01 * mms.sBy; ph += mms.sBy)
            {
                DoEvents();
                n2 = -Math.Cos(ph)+2;  // n2 = 1 .. 3
                A = ((ntot - btot) - 2 * (n2 - b2)) / (ntot - btot);
                fringes.Add(new Point(ph, A)); 

                System.Threading.Thread.Sleep((int)ndDelay.Value);
                log(ph.ToString());
                if (Utils.InRange(ph, mms.sTo - 0.99 * mms.sBy, mms.sTo + 0.99 * mms.sBy) || cancelRequest)
                {
                    md.prms["last"] = 1;
                }
                if (!SingleShot(A, ref md)) break; 
                if (cancelRequest) break;
            }               
        }

 /*       private async void SimpleRepeatAsync(bool jumbo, int cycles, string groupID)
        {
            // This method runs asynchronously.
            await Task.Run(() => SimpleRepeat(jumbo, cycles, groupID));
        }*/

        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            dispatcherTimer.Stop();
            crsDownStrobe.Visibility = System.Windows.Visibility.Visible;
            if (rbDouble.IsChecked.Value) crsUpStrobe.Visibility = System.Windows.Visibility.Visible;
            if(lastGrpExe.cmd.Equals("scan")) 
            {
                MMscan mms = new MMscan();
                if (mms.FromDictionary(lastGrpExe.prms)) RealScan(mms);
            }
            if(lastGrpExe.cmd.Equals("repeat")) 
            {
                string jumboGroupID = (string)lastGrpExe.prms["groupID"];
                int jumboCycles = Convert.ToInt32(lastGrpExe.prms["cycles"]);
 
                RealRepeat(true, jumboCycles, jumboGroupID);
            }
        }

        bool wait4adjust = false; double xDownPos = 0, xUpPos = 0;
        private void RealRepeat(bool jumbo, int cycles, string groupID)
        {
            MMexec md = new MMexec();
            md.mmexec = "";
            md.cmd = "shotData";
            md.sender = "Axel-probe";
            md.prms["groupID"] = groupID;
            md.prms["runID"] = 0;
            
            double n2 = 1; double ntot = 5; // n2 = 1 .. 3 / A = 1 .. -1              
            double b2 = 1; double btot = 3; double bg = 0;

            long realCycles = cycles;
            if (cycles == -1)
            {
                if(jumbo)
                {
                     realCycles = long.MaxValue;
                }
                else
                {
                    realCycles = (long)(driftPeriod/driftStep);
                }   
            }
            double A = 0, frAmpl = 0, frAmplI = 0, drift = 0, pos0, pos1 = 0, pos2 = 0;
            ramp.Clear(); corr.Clear(); corrList.Clear();
            cancelRequest = false; int smallLoop = (int)(driftPeriod/driftStep);
            int i = -1;
            for (long j = 0; j < realCycles; j++)
            {
                DoEvents();
                if ((j == (realCycles - 1)) || cancelRequest)
                {
                    md.prms["last"] = 1;
                }
                if (jumbo) // jumbo repeat 
                {
                    if (j % smallLoop == 0)
                    {
                        i = 0;
                        ramp.Clear(); corr.Clear();
                    }
                    else i += 1;
                    pos0 = i * driftStep;
                    drift = calcAtPos(jumbo, pos0, (i % 2) == 1) / ndOrderFactor.Value; // [rad]     fringeMg2rad(
                    if (rbSingle.IsChecked.Value)
                    {
                        pos1 = (double)crsDownStrobe.AxisValue + drift;
                        frAmpl = Math.Cos(pos1);
                        log("# pos1= " + pos1.ToString("G4") + "; frAmpl= " + frAmpl.ToString("G4") + "; idx= " + j.ToString());
                    }
                    else
                    {
                        int runID = (int)md.prms["runID"]; string side;
                        pos1 = (double)crsDownStrobe.AxisValue - drift;
                        pos2 = (double)crsUpStrobe.AxisValue - drift;
                        if ((runID % 2) == 0)
                        {
                            frAmpl = Math.Cos(pos1); side = "downhill";
                            frAmplI = fringes[getIfringe((double)crsDownStrobe.AxisValue)].Y;
                            log("# idxed= " + frAmplI.ToString("G4") + "; frAmpl= " + frAmpl.ToString("G4"));
                        }
                        else
                        {
                            frAmpl = Math.Cos(pos2); side = "uphill";
                            frAmplI = fringes[getIfringe((double)crsUpStrobe.AxisValue)].Y;
                            log("# idxed= " + frAmplI.ToString("G4") + "; frAmpl= " + frAmpl.ToString("G4"));
                        }
                    }   
                    wait4adjust = chkFollowPID.IsChecked.Value;
                    md.prms["time"] = (string)(remote.elapsedTime().ToString("G6")); 
                    if (lastDecomposedAccel.ContainsKey("mems")) md.prms["MEMS"] = (string)(lastDecomposedAccel["mems"].ToString("G5")); // in [mg]
                    if (!SingleShot(frAmplI, ref md)) break;
                    while (wait4adjust)
                    {
                        Thread.Sleep((int)ndDelay.Value);
                        DoEvents();
                    }
                    Thread.Sleep((int)ndDelay.Value);
                }
                else // simple repeat -> initiated by axel-probe 
                {
                    n2 = 1 + rnd.Next(200) / 100.0; // random from 1 to 3
                    A = ((ntot - btot) - 2 * (n2 - b2)) / (ntot - btot);
                    ramp.Add(new Point(j, A));
                    drift = calcAtPos(jumbo, A, (j % 2) == 1);
                    frAmpl = A;
                    if (!SingleShot(frAmpl, ref md)) break;
                }
                
                Thread.Sleep((int)ndDelay.Value);
                if (cancelRequest) break;
            }
        }

        private void btnScan_Click(object sender, RoutedEventArgs e)
        {
            string msg;
            MMexec mm = new MMexec();
            mm.mmexec = "test_scan";
            mm.cmd = "scan";
            mm.id = rnd.Next(int.MaxValue);
            mm.sender = "Axel-probe";
            mm.id = rnd.Next(int.MaxValue);

            MMscan mms = new MMscan();            
            mms.TestInit();
            mms.groupID = DateTime.Now.ToString("yy-MM-dd_H-mm-ss");
            mms.ToDictionary(ref mm.prms);

            if (remote.Enabled) // title command
            {
                msg = JsonConvert.SerializeObject(mm);
                if (!remote.sendCommand(msg)) MessageBox.Show("send json problem!");
                log(msg);
            }
            RealScan(mms); 
        }

        private void btnRepeat_Click(object sender, RoutedEventArgs e)
        {
            string msg;
            MMexec mm = new MMexec();
            mm.cmd = "repeat";
            mm.mmexec = "test_repeat";
            mm.sender = "Axel-probe";
            int cycles = (int)(driftPeriod / driftStep); 
            mm.prms["cycles"] = cycles;
            string groupID = DateTime.Now.ToString("yy-MM-dd_H-mm-ss");
            mm.prms["groupID"] = groupID;           
            if (remote.Enabled) // title command
            {
                msg = JsonConvert.SerializeObject(mm);
                if (!remote.sendCommand(msg)) MessageBox.Show("send json problem!");
                log(msg);
            }
            crsDownStrobe.Visibility = System.Windows.Visibility.Hidden; crsUpStrobe.Visibility = System.Windows.Visibility.Hidden;
            Thread.Sleep(1000);
            RealRepeat(false, cycles, groupID);
        }

        private void chkRemoteEnabled_Checked(object sender, RoutedEventArgs e)
        {
            DoEvents();
            remote.Enabled = chkRemoteEnabled.IsChecked.Value;
            remote.CheckConnection(true);
        }

        private void btnAbort_Click(object sender, RoutedEventArgs e)
        {
            DoEvents();
            cancelRequest = true;
            DoEvents();
            log("About to ABORT !!!");
            string msg;
            MMexec mm = new MMexec();
            mm.cmd = "abort";
            mm.mmexec = "abort_and_save_yourself!!!";
            mm.sender = "Axel-probe";            
            if (remote.Enabled) // title command
            {
                msg = JsonConvert.SerializeObject(mm);
                if (!remote.sendCommand(msg)) MessageBox.Show("send json problem!");
                log(msg);
            }
        }

        private void ndRange_ValueChanged(object sender, ValueChangedEventArgs<double> e)
        {
            if ((ndAmplitude == null) || (ndStep == null)) return;
            driftRange = ndAmplitude.Value; driftStep = ndStep.Value; 
        }

        List<double> ress; 
        private void btnCustomScan_Click(object sender, RoutedEventArgs e)
        {
            logger.Enabled = true; ress = new List<double>(); 
            for (double c = 0; c < 0.05; c = c + 0.002)
            {
                ress.Clear();
                ndGaussNoiseX.Value = c;
                btnRun_Click(null,null);
                double[] arr = ress.ToArray();
                logger.log(c.ToString("G5")+"\t"+arr.Average()+"\t"+Statistics.StandardDeviation(arr));
            }
            logger.Enabled = false;
        }

        private void frmMain_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key.Equals(Key.NumPad0)) chkPause.IsChecked = !chkPause.IsChecked.Value;
            if (e.Key.Equals(Key.NumPad1)) pauseSingle = true;
        }

        private void imgAbout_MouseDown(object sender, MouseButtonEventArgs e)
        {
            MessageBox.Show("           Axel Probe v1.6 \n         by Teodor Krastev \nfor Imperial College, London, UK\n\n   visit: http://axelsuite.com", "About");
        }
     }
}
