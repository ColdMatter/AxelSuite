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
        DispatcherTimer dispatcherTimer;
        double driftRange, driftPeriod, driftStep;
        RemoteMessaging remote;
        string remoteDoubleFormat = "G12";
        Random rnd = new Random();

        public MainWindow()
        {
            InitializeComponent();

            driftRange = ndRange.Value; driftPeriod = ndPeriod.Value; driftStep = ndStep.Value;

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

        private int getIfringe(double x)
        {
            for (int i = 0; i < fringes.Count-1; i++)
            {
                if ((fringes[i].X < x) && (x <= fringes[i+1].X)) return i;
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

        Random rand = new Random(); 
        public double Gauss()
        {
            if ((!chkAddGauss.IsChecked.Value) || (ndGaussSigma.Value <= 0.1)) return 0;           
            double u1 = 1.0 - rand.NextDouble(); //uniform(0,1] random doubles
            double u2 = 1.0 - rand.NextDouble();
            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) *
                         Math.Sin(2.0 * Math.PI * u2); //random normal(0,1)
            return randStdNormal * ndGaussSigma.Value / 100;
        }

        public double Gauss01()
        {
            double u1 = 1.0 - rand.NextDouble(); //uniform(0,1] random doubles
            double u2 = 1.0 - rand.NextDouble();
            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) *
                         Math.Sin(2.0 * Math.PI * u2); //random normal(0,1)
            return randStdNormal;
        }

        public double Breathing(double x)
        {
            if (!chkVaryAmpl.IsChecked.Value) return 0;
            double per = Math.Sin(Math.PI * x / ndBreathePeriod.Value);
            double norm = ndBreatheAmpl.Value / 100;
            return per * norm;
        }

        private double accelDrift(double pos) // drift at pos for selected accel. trend
        {
            double halfPrd = ndPeriod.Value / 2;
            double rng = ndRange.Value;
            double slope = rng / halfPrd;
            double drift = 0;
            switch (cbDriftType.SelectedIndex)
            {
                case 0: drift = 0.5*rng;
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
                    break;
                case 2: // sine
                    drift = rng * Math.Sin(2 * Math.PI * pos / ndPeriod.Value);
                    break;
            }
            return drift;
        }

        private void fringesGenerator(double driftPos, double drift)
        {
            double sp = 0; //k = 0;
            fringes.Clear();
            while ((sp < 4 * Math.PI) && !stopRequest)
            {
                fringes.Add(new Point(sp, Breathing(driftPos) + Math.Cos(sp + drift) + Gauss()));
                sp += Math.PI / 200;
                //if ((k % 10) == 0) { DoEvents(); k++; }
            }
            grFringes.DataSource = fringes;
        }

        private double calcAtPos(bool jumbo, double driftPos, bool odd) // glPos - phase; odd - odd/even number for 2 strobe mode
            // returns drift
        {
            double err;
            double drift = accelDrift(driftPos); // 
            if (jumbo) ramp.Add(new Point(driftPos, drift));

            fringesGenerator(driftPos, drift);
            DoEvents();

            if (chkFollowPID.IsChecked.Value)
            {
                if (rbSingle.IsChecked.Value)
                {
                    err = SingleAdjust(drift);
                    if (!double.IsNaN(err) && jumbo)
                    {
                        corr.Add(new Point(driftPos, err)); corrList.Add(err);
                    }
                }
                if (rbDouble.IsChecked.Value)
                {
                    err = DoubleAdjust(!odd, drift);
                    if (!double.IsNaN(err) && odd && jumbo)
                    {
                        corr.Add(new Point(driftPos, err)); corrList.Add(err);
                    }
                }
            }
            return drift;
        } 

        bool stopRequest = false; 
        private void btnRun_Click(object sender, RoutedEventArgs e)
        {
            DoEvents();
            if (btnRun.Content.Equals("R U N"))
            {
                btnRun.Content = "S t o p"; btnRun.Foreground = Brushes.DarkRed;
            }
            else
            {
                btnRun.Content = "R U N"; btnRun.Foreground = Brushes.DarkGreen;
            }
            stopRequest = (btnRun.Content.Equals("R U N"));
            DoEvents();
            if (stopRequest) return;

            double rng = ndRange.Value;
            double step = ndStep.Value;
            leftLvl = double.NaN; rightLvl = double.NaN;
            ((AxisDouble)grRslt.Axes[0]).Range = new Range<double>(0, ndPeriod.Value);
            ((AxisDouble)grRslt.Axes[1]).Range = new Range<double>(-rng, rng);
            ((AxisDouble)grRslt.Axes[2]).Range = new Range<double>(-0.1, 0.1);
            
            double drift, pos; int j;
             //tbLog.Text = ""; 
            do
            {
                ramp.Clear(); corr.Clear(); corrList.Clear();
                pos = 0; j = 0;  
                while ((pos < (ndPeriod.Value + 0.0001)) && !stopRequest)
                {
                    drift = calcAtPos(true, pos, (j % 2) == 1);
                    pos += step; j++;
                    do
                    {
                        DoEvents(); 
                    } while (chkPause.IsChecked.Value);
                    System.Threading.Thread.Sleep((int)ndDelay.Value);
                }                
                if (corrList.Count > 0)
                    log("Aver=" + corrList.Average().ToString("G4") + "; StDev= " + Statistics.StandardDeviation(corrList.ToArray()).ToString("G4") + "\n====================");
            } while ((cbFinite.SelectedIndex == 1) && !stopRequest);
            btnRun.Content = "R U N"; btnRun.Foreground = Brushes.DarkGreen;
        }

        private void frmMain_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            stopRequest = true;
        }

        private void grFringes_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ((Graph)sender).ResetZoomPan();
        }

        int iStDepth = 5; int dStDepth = 3;  
        public double PID(double curY)
        {
            int hillSide = 1;
            //if (cbHillPos.SelectedIndex == 0) hillSide = -1;
            double pTerm = curY;
            iStack.Add(curY); while (iStack.Count > iStDepth) iStack.RemoveAt(0);
            double iTerm = iStack.Average();
            dStack.Add(curY); while (dStack.Count > dStDepth) dStack.RemoveAt(0);
            double dTerm = 0;
            for (int i = 0; i < dStack.Count - 1; i++)
            {
                dTerm += dStack[i + 1] - dStack[i];
            }
            //dTerm /= Math.Max(dStack.Count - 1, 1);
            
            double cr = hillSide * (ndKP.Value * pTerm + ndKI.Value * iTerm + ndKD.Value * dTerm);
            
            int curIdx1 = getIfringe((double)crsFringes1.AxisValue+cr);
            if (curIdx1 == -1) log("invalid index curIdx1");
            else log("PID> " + pTerm.ToString("G3") + "; " + iTerm.ToString("G3") + "; " + dTerm.ToString("G3") +
                     // PID X correction and Y value after the correction
                     "| new:" + fringes[curIdx1].X.ToString("G3") + "/" + fringes[curIdx1].Y.ToString("G3"));  // new pos Y
            return cr;
        }
        
        private double SingleAdjust(double drift) // return error in fringe position/phase
        {
            double curX = (double)crsFringes1.AxisValue;
            int curIdx = getIfringe(curX);
            if (curIdx == -1)
            {
                log("Error: index cannot be found"); return double.NaN;
            }          
            if (!remote.Enabled)
            {
                double corr = PID(fringes[curIdx].Y);
                crsFringes1.AxisValue = curX + corr;
            }
            curIdx = getIfringe((double)crsFringes1.AxisValue);
            if (curIdx > -1) return drift - (2.5 * Math.PI - fringes[curIdx].X); 
            else return double.NaN;
        }

        double leftLvl = double.NaN, rightLvl = double.NaN;
        private double DoubleAdjust(bool even, double drift) // return error in fringe position/phase
        {
            int curIdx1, curIdx2; double cx, curX1, curX2;
            if (even) // even
            {
                curX1 = (double)crsFringes1.AxisValue;
                curIdx1 = getIfringe(curX1);
                if (curIdx1 == -1)
                {
                    log("Error: index cannot be found"); return double.NaN;
                }
                leftLvl = fringes[curIdx1].Y;
                if(double.IsNaN(rightLvl)) return double.NaN;
            }
            else // odd
            {            
                curX2 = (double)crsFringes2.AxisValue;
                curIdx2 = getIfringe(curX2);
                if (curIdx2 == -1)
                {
                    log("Error: index cannot be found"); return double.NaN;
                }
                rightLvl = fringes[curIdx2].Y;
            }
            if (!remote.Enabled)
            {   
                double corr = PID(leftLvl - rightLvl);
                crsFringes1.AxisValue = (double)crsFringes1.AxisValue + corr; // adjust left
                crsFringes2.AxisValue = (double)crsFringes2.AxisValue + corr; // adjust right
            }
            cx = ((double)crsFringes2.AxisValue + (double)crsFringes1.AxisValue) / 2;
            return drift - (2 * Math.PI - cx);
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
                log("Accel= " + ndRange.Value.ToString("G3") + " / " + ndPeriod.Value.ToString("G3") + " / " + ndStep.Value.ToString("G3") + " ;");
            }
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            tbLog.Text = "";
        }

        private void rbSingle_Checked(object sender, RoutedEventArgs e)
        {
            if ((crsFringes2 == null) || (crsFringes1 == null)) return;
            if (rbSingle.IsChecked.Value)
            {
                crsFringes2.Visibility = System.Windows.Visibility.Hidden;
                crsFringes1.AxisValue = 7.85;
            }
            else
            {
                crsFringes2.Visibility = System.Windows.Visibility.Visible;
                crsFringes1.AxisValue = 4.71;
                crsFringes1.AxisValue = 7.85;
                leftLvl = double.NaN; rightLvl = double.NaN;
            }
        }

        private void frmMain_Loaded(object sender, RoutedEventArgs e)
        {
            remote = new RemoteMessaging("Axel Hub");
            remote.ActiveComm += new RemoteMessaging.ActiveCommHandler(DoActiveComm);
            remote.OnReceive += new RemoteMessaging.RemoteHandler(OnReceive);
            remote.Enabled = chkRemoteEnabled.IsChecked.Value;
        }

        private MMexec lastGrpExe;
        private void DoActiveComm(bool active, bool forced)
        {
            if (!remote.Enabled) return;
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
            if (remote.CheckConnection()) grpRemote.Header = "Remote - is ready <->";
            else {
                grpRemote.Header = "Remote - not found! ...starting it";
                System.Diagnostics.Process.Start(File.ReadAllText(Utils.configPath + "axel-hub.bat"), "-remote");
                Thread.Sleep(1000);
                remote.CheckConnection();
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
                        if ((crsFringes1 == null) || (crsFringes2 == null)) return false;
                        if (Convert.ToInt32(mme.prms["strobes"]) == 1) rbSingle.IsChecked = true;
                        else rbDouble.IsChecked = true;
                        if (rbSingle.IsChecked.Value)
                        {
                            crsFringes2.Visibility = System.Windows.Visibility.Hidden;
                            crsFringes1.AxisValue = (double)mme.prms["strobe1"];
                        }
                        else
                        {
                            crsFringes2.Visibility = System.Windows.Visibility.Visible;
                            crsFringes1.AxisValue = (double)mme.prms["strobe1"];
                            crsFringes2.AxisValue = (double)mme.prms["strobe2"];
                            leftLvl = double.NaN; rightLvl = double.NaN;
                        }
                        lastGrpExe = mme.Clone();
                        dispatcherTimer.Start();
                    }
                    break;
                case ("phaseAdjust"):
                    {
                        log("<< phaseAdjust to "+mme.prms["phaseCorrection"]);
                        double corr = Convert.ToDouble(mme.prms["phaseCorrection"]);
                        if(rbSingle.IsChecked.Value)
                        {
                            crsFringes1.AxisValue = (double)crsFringes1.AxisValue + corr;
                        }
                        else
                        {
                            int runID = Convert.ToInt32(mme.prms["runID"]);                           
                            if ((runID % 2) == 0) crsFringes1.AxisValue = (double)crsFringes1.AxisValue + corr;
                            else crsFringes2.AxisValue = (double)crsFringes2.AxisValue + corr;
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

            if (remote.Enabled)
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
                    DoEvents();
                } while (chkPause.IsChecked.Value);
            }
            return rslt;
        } 
        
        private void SimpleScan(MMscan mms)
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
            crsFringes1.Visibility = System.Windows.Visibility.Visible;
            if (rbDouble.IsChecked.Value) crsFringes2.Visibility = System.Windows.Visibility.Visible;
            if(lastGrpExe.cmd.Equals("scan")) 
            {
                MMscan mms = new MMscan();
                if (mms.FromDictionary(lastGrpExe.prms)) SimpleScan(mms);
            }
            if(lastGrpExe.cmd.Equals("repeat")) 
            {
                string jumboGroupID = (string)lastGrpExe.prms["groupID"];
                int jumboCycles = Convert.ToInt32(lastGrpExe.prms["cycles"]);
 
                SimpleRepeat(true, jumboCycles, jumboGroupID);
            }
        }

        bool wait4adjust = false;
        private void SimpleRepeat(bool jumbo, int cycles, string groupID)
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
            if(jumbo)
            {
                if (cycles == -1) realCycles = long.MaxValue;
            }
            else
            {
                realCycles = (long)(driftPeriod/driftStep);
            }               
            double A = 0, frAmpl = 0, drift = 0, pos0, pos1 = 0, pos2 = 0, xPos1 = 0, xPos2 = 0;
            ramp.Clear(); corr.Clear(); corrList.Clear();
            cancelRequest = false; int smallLoop = (int)(driftPeriod/driftStep);
            int i = -1;
            for (long j = 0; j < realCycles; j++)
            {
                DoEvents();                
                if (jumbo) // jumbo repeat
                {
                    if (j % smallLoop == 0)
                    {
                        i = 0;
                        ramp.Clear(); corr.Clear();
                    }
                    else i += 1;
                    pos0 = i * driftStep;                   
                    drift = calcAtPos(jumbo, pos0, (i % 2) == 1); // 
                    if (rbSingle.IsChecked.Value)
                    {
                        pos1 = (double)crsFringes1.AxisValue + drift;
                        frAmpl = Math.Cos(pos1);
                        log("# pos1= " + pos1.ToString("G4") + "; frAmpl= " + frAmpl.ToString("G4") + "; idx= " + j.ToString());
                    }
                    else
                    {
                        pos1 = (double)crsFringes1.AxisValue + drift;
                        pos2 = (double)crsFringes2.AxisValue + drift;
                        if ((j % 2) == 0)
                        {
                            frAmpl = Math.Cos(pos1);
                        }
                        else
                        {
                            frAmpl = Math.Cos(pos2);
                        }
                        log("# pos1= " + pos1.ToString("G4") + "; pos2= " + pos2.ToString("G4") + "; frAmpl= " + frAmpl.ToString("G4") + "; idx= " + j.ToString());
                    }   
                    wait4adjust = true;
                    if (!SingleShot(frAmpl, ref md)) break;
                    while (wait4adjust)
                    {
                        Thread.Sleep((int)ndDelay.Value);
                        DoEvents();
                    }
                    Thread.Sleep((int)ndDelay.Value);
                }
                else // simple repeat
                {
                    n2 = 1 + rnd.Next(200) / 100.0; // random from 1 to 3
                    A = ((ntot - btot) - 2 * (n2 - b2)) / (ntot - btot);
                    ramp.Add(new Point(j, A));
                    drift = calcAtPos(jumbo, A, (j % 2) == 1);
                    frAmpl = A;
                }
                
                if ((j == (realCycles - 1)) || cancelRequest)
                {
                    md.prms["last"] = 1;
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
            SimpleScan(mms); 
        }

        private void btnRepeat_Click(object sender, RoutedEventArgs e)
        {
            string msg;
            MMexec mm = new MMexec();
            mm.cmd = "repeat";
            mm.mmexec = "test_repeat";
            mm.sender = "Axel-probe";
            int cycles = 200;
            mm.prms["cycles"] = cycles;
            string groupID = DateTime.Now.ToString("yy-MM-dd_H-mm-ss");
            mm.prms["groupID"] = groupID;           
            if (remote.Enabled) // title command
            {
                msg = JsonConvert.SerializeObject(mm);
                if (!remote.sendCommand(msg)) MessageBox.Show("send json problem!");
                log(msg);
            }
            crsFringes1.Visibility = System.Windows.Visibility.Hidden; crsFringes2.Visibility = System.Windows.Visibility.Hidden;
            SimpleRepeat(false, cycles, groupID);
        }

        private void chkRemoteEnabled_Checked(object sender, RoutedEventArgs e)
        {
            remote.Enabled = chkRemoteEnabled.IsChecked.Value;
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
            if ((ndRange == null) || (ndPeriod == null) || (ndStep == null)) return;
            driftRange = ndRange.Value; driftPeriod = ndPeriod.Value; driftStep = ndStep.Value; 
        }
     }
}
