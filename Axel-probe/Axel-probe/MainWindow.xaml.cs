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
using Newtonsoft.Json;
using UtilsNS;

namespace Axel_probe
{
    /// <summary>
    /// Axel-probe is replacement simulator for MotMaster2 
    /// generating signal according to a settings corresponding to some experimental conditions
    /// 
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {   
        ChartCollection<Point> ramp, corr;
        ChartCollection<double> signalN, signalB;
        List<double> iStack, dStack, corrList;
        bool cancelRequest = false;
        bool pauseSingle = false;
        DispatcherTimer dispatcherTimer;
        double driftRange, driftStep; 
        const double driftPeriod = 100; // %
        RemoteMessaging remote;
        string remoteDoubleFormat = "G6";
        Random rnd = new Random();
        FileLogger logger;
        Stopwatch sw;

        ProbeEngine pe; 

        /// <summary>
        /// Class contructor
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            driftRange = ndAmplitude.Value; driftStep = ndStep.Value;

            dispatcherTimer = new DispatcherTimer(DispatcherPriority.Send);
            dispatcherTimer.Tick += dispatcherTimer_Tick;
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);

            ramp = new ChartCollection<Point>();
            grRslt.Data[0] = ramp;
            corr = new ChartCollection<Point>();
            grRslt.Data[1] = corr;

            iStack = new List<double>(); dStack = new List<double>(); corrList = new List<double>();
            logger = new FileLogger();
            sw = new Stopwatch();
            Running = false;

            pe = new ProbeEngine(); pe.stopWatch.Start();
            pe.OnChange += new ProbeEngine.ChangeHandler(OnChangeEvent);
            pe.OnLog += new ProbeEngine.LogHandler(log);
            pe.lboxNB = lboxNB;
            
            grFringes.DataSource = pe.srsFringes;
            graphNs.Data[0] = pe.srsSignalN;
            graphNs.Data[1] = pe.srsSignalB;
        }
        
        /// <summary>
        /// Update internal PE params from visual ones
        /// </summary>
        void Vis2PEparams()
        {
            if (Utils.isNull(pe)) return;
            if (pe.LockParams) return;
            pe.bpps["pattern"] = cbDriftType.SelectedIndex;
            pe.bpps["ampl"] = ndAmplitude.Value;
            pe.bpps["factor"] = ndOrderFactor.Value;
            pe.bpps["step"] = ndStep.Value;
            pe.bpps["TimeGap"] = ndTimeGap.Value;

            pe.dps["Xnoise"] = ndGaussNoiseX.Value;
            pe.dps["Ynoise"] = ndGaussNoiseY.Value;
            pe.dps["XnoiseIO"] = chkAddGaussX.IsChecked.Value;
            pe.dps["YnoiseIO"] = chkAddGaussY.IsChecked.Value;
            pe.dps["BrthPattern"] = cbBrthPattern.SelectedIndex;
            pe.dps["BrthApmpl"] = ndBreatheAmpl.Value;
            pe.dps["BrthPeriod"] = ndBreathePeriod.Value;
            pe.dps["BrthIO"] = chkBreathing.IsChecked.Value;
        }

        /// <summary>
        /// Update visual components values from internal PE ones
        /// </summary>
        void PEparams2Vis()
        {
            if (Utils.isNull(pe)) return;
            pe.LockParams = true;
            cbDriftType.SelectedIndex = (int)pe.bpps["pattern"];
            ndAmplitude.Value = pe.bpps["ampl"];
            ndOrderFactor.Value = pe.bpps["factor"];
            ndStep.Value = pe.bpps["step"];
            ndTimeGap.Value = pe.bpps["TimeGap"];

            ndGaussNoiseX.Value = Convert.ToDouble(pe.dps["Xnoise"]);
            ndGaussNoiseY.Value = Convert.ToDouble(pe.dps["Ynoise"]);
            chkAddGaussX.IsChecked = Convert.ToBoolean(pe.dps["XnoiseIO"]);
            chkAddGaussY.IsChecked = Convert.ToBoolean(pe.dps["YnoiseIO"]);
            cbBrthPattern.SelectedIndex = Convert.ToInt32(pe.dps["BrthPattern"]);
            ndBreatheAmpl.Value = Convert.ToDouble(pe.dps["BrthApmpl"]);
            ndBreathePeriod.Value = Convert.ToDouble(pe.dps["BrthPeriod"]);
            chkBreathing.IsChecked = Convert.ToBoolean(pe.dps["BrthIO"]);
            pe.LockParams = false;
        }

        /// <summary>
        /// Update simulation params from visual ones (double values)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ndGaussNoiseX_ValueChanged(object sender, ValueChangedEventArgs<double> e)
        {
            if (Utils.isNull(pe)) return;
            if (pe.LockParams) return;
            pe.dps["Xnoise"] = ndGaussNoiseX.Value;
            pe.dps["Ynoise"] = ndGaussNoiseY.Value;
            pe.dps["BrthApmpl"] = ndBreatheAmpl.Value;
            pe.dps["BrthPeriod"] = ndBreathePeriod.Value;
        }
        /// <summary>
        /// Update simulation params from visual ones (boolean values)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ndGaussNoiseX_ValueChanged(object sender, RoutedEventArgs e)
        {
            if (Utils.isNull(pe)) return;
            if (pe.LockParams) return;
            pe.dps["XnoiseIO"] = chkAddGaussX.IsChecked.Value;
            pe.dps["YnoiseIO"] = chkAddGaussY.IsChecked.Value;
            pe.dps["BrthIO"] = chkBreathing.IsChecked.Value;
        }
        /// <summary>
        /// Update simulation params from visual ones (selection values)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ndGaussNoiseX_ValueChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Utils.isNull(pe)) return;
            if (pe.LockParams) return;
            pe.dps["BrthPattern"] = cbBrthPattern.SelectedIndex;
        }
        /// <summary>
        /// Pause the execution for diagnostics
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void chkPause_Checked(object sender, RoutedEventArgs e)
        {
            pe.Pause = chkPause.IsChecked.Value;
        }

        /// <summary>
        /// Event method when acceleration changes
        /// </summary>
        /// <param name="newAccel"></param>
        void OnChangeEvent(Point newAccel)
        {
            if (newAccel.X < ndStep.Value) ramp.Clear();
            if ((newAccel.X > (100 - ndStep.Value)) && (cbFinite.SelectedIndex == 0))
            {
                pe.Stop(); if (Running) btnRun_Click(null, null);
            }
            ramp.Add(newAccel);
        }

        /// <summary>
        /// Log some text on the text-box on the right
        /// </summary>
        /// <param name="txt"></param>
        /// <param name="clr"></param>
        private void log(string txt, System.Windows.Media.Color? clr = null)
        {
            if (txt.Length > 0)
                if (txt[0].Equals("@"))
                {
                    switch (txt)
                    {
                        case "@stop":
                            if (Running) btnRun_Click(null, null);
                            break;
                    }
                    //return;
                }

            if (!chkLog.IsChecked.Value) return;
            Utils.log(tbLog, txt, clr);
        }
        
        /// <summary>
        /// Extensive logs for debuging switchable by debugMode
        /// </summary>
        private bool debugMode = true;
        private void dlog(string txt)
        {
            if(debugMode) log(txt);
        }

        /// <summary>
        /// New acceleration point with optional noise by X and Y
        /// </summary>
        /// <returns></returns>
        public Point Gauss()
        {
            Point g = new Point(0, 0);
            if (chkAddGaussX.IsChecked.Value)
            {
                g.X = Utils.Gauss01() * ndGaussNoiseX.Value;
            }
            if (chkAddGaussY.IsChecked.Value)
            {
                g.Y = Utils.Gauss01() * ndGaussNoiseY.Value / 100;
            }
            return g;
        }

        /// <summary>
        /// Add rescaling of the fringes in Sin pattern
        /// to simulate changing the overall conditions 
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        public double Breathing(double x)
        {
            if (!chkBreathing.IsChecked.Value) return 0;
            double per = Math.Sin(Math.PI * x / ndBreathePeriod.Value);
            double norm = ndBreatheAmpl.Value / 100;
            return per * norm;
        }

        /// <summary>
        /// Calculates drift [mg] at pos for selected accel. trend
        /// </summary>
        /// <param name="pos">[0..driftPeriod]</param>
        /// <returns>Result drift [mg]</returns>
        private double accelDrift(double pos) 
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
                    drift = rng * Math.Sin(2 * Math.PI * pos / driftPeriod);
                    break;
            }
            return drift;
        }
        /// <summary>
        /// Claculates centre of the frindge
        /// </summary>
        /// <returns></returns>
        private double centreFringe()
        {
            return (((double)crsUpStrobe.AxisValue + (double)crsDownStrobe.AxisValue) / 2);
        }
        /// <summary>
        /// ZeroFringe is centre fringe but limited to [-pi..pi] (by adding / substr 2*Pi)
        /// </summary>
        /// <returns>[rad] </returns>
        public double zeroFringe() 
        {
            double cp = Double.NaN;
            if ((double)crsDownStrobe.AxisValue < (double)crsUpStrobe.AxisValue) cp = Math.PI - centreFringe();
            else cp = 2 * Math.PI - centreFringe(); //Math.Sign(cp)*
            if (Utils.InRange(cp, -3 * Math.PI, -Math.PI)) cp += 2 * Math.PI;
            if (Utils.InRange(cp, Math.PI, 3 * Math.PI)) cp -= 2 * Math.PI;
            return cp;
        }

        /// <summary>
        /// Calculates acceleration order
        /// </summary>
        /// <param name="accel">acceleration [mg]</param>
        /// <param name="factor">conversion factor [mg/rad]</param>
        /// <param name="resid">residual [rad]</param>
        /// <returns>acceleration order</returns>
        private double accelOrder(double accel, double factor, out double resid) // ; ; 
        {
            double accelRad = accel / factor;
            if (Utils.InRange(accelRad, -Math.PI, Math.PI))
            {
                resid = accelRad;
                return 0;
            }                
            else
            {
                double mag = Math.Truncate((Math.Abs(accelRad) - Math.PI) / (2 * Math.PI)) + 1;
                double aOrd = Math.Sign(accelRad) * mag;
                resid = Math.Sign(accelRad)*((Math.Abs(accelRad) - Math.PI) % (2 * Math.PI)); 
                return aOrd; 
            }
        }

        /// <summary>
        /// Calculates acceleration from MEMS and Quantum
        /// </summary>
        /// <param name="order">order (from mems)</param>
        /// <param name="resid">resid (from quantum) [rad]</param>
        /// <param name="factor">factor [mg/rad]</param>
        /// <param name="orderAccel">acceleration order</param>
        /// <param name="residAccel">acceleration residual</param>
        /// <returns>accel [mg]</returns>
        public double resultAccel(double order, double resid, double factor, out double orderAccel, out double residAccel) // ; ; ; returns 
        {
            residAccel = resid * factor;
            if (Utils.InRange(order, -0.01, 0.01)) // 0 order
            {
                orderAccel = 0;                           
            }
            else
            {
                double factor2pi = factor * (2 * Math.PI);
                orderAccel = order * factor2pi; 
            }       
            return orderAccel + residAccel; 
        }
        
        /// <summary>
        /// Decompose acceleration to all possible components (incl. mems &  
        /// </summary>
        /// <param name="accel">accelarion [mg] - target(real)</param>
        /// <param name="mems">mems accel.[mg] - measured (real + noise)</param>
        /// <param name="factor">conversion factor [mg/rad]</param>
        /// <returns>Dictionary with all the components</returns>
        public Dictionary<string, double> decomposeAccel(double accel, double mems, double factor)
        {
            Dictionary<string, double> da = new Dictionary<string, double>();
            da["accel.R"] = accel; // R for refer
            double resid;
            double orderR = accelOrder(accel,factor, out resid);
            da["order.R"] = orderR;
            da["resid.R"] = resid; // [rad] residual for atomic interferometer

            da["mems"] = mems;

            // M for measure
            da["frgRad.M"] = zeroFringe(); // [rad]

            da["order.M"] = accelOrder(mems, factor, out resid); // order from mems
            accelOrder(zeroFringe() * factor, factor, out resid); // resid from measured fringe pattern
            da["resid.M"] = resid;
            double orderAccel, residAccel;
            da["accel.M"] = resultAccel(da["order.M"], resid, factor, out orderAccel, out residAccel); // [mg]
            //da["accel.O"] = orderAccel; da["accel.P"] = residAccel;
            da["diff"] = accel - da["accel.M"];
            if (!Utils.isNull(ress)) ress.Add(da["diff"]);
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
            //logger.log(tm.ToString("G5")+"\t"+ mems.ToString("G6") + "\t" + fg.ToString("G6") + "\t" + da["accel.M"].ToString("G6"));
            return da;
        }

        Dictionary<string, double> lastDecomposedAccel = new Dictionary<string, double>();
        /// <summary>
        /// Calculate acceleration at specific acceleration position in [0..driftPeriod] interval
        /// </summary>
        /// <param name="jumbo">Simple or Jumbo mode</param>
        /// <param name="driftPos">driftPos - [0..driftPeriod]</param>
        /// <param name="odd">odd - odd/even number for 2 strobe mode</param>
        /// <returns></returns>
        private double calcAtPos(bool jumbo, double driftPos, bool odd) // ; 
            // returns drift [mg]
        {
            double err;
            double orderFactor = ndOrderFactor.Value;
            double driftAcc = accelDrift(driftPos); // noiseless accel. [mg] from driftPos
            if (jumbo) ramp.Add(new Point(driftPos, driftAcc));

            double accel = pe.fringes(driftAcc); // accel. [mg] with noise
            //log("pos.X: "+driftPos.ToString("G5")+"; acc: " + driftAcc.ToString("G5") + "; +ns: " + accel.ToString("G5"));
            Utils.DoEvents();
            double midFringe = -1; //double fringeTopRad = -1;
            if (chkFollowPID.IsChecked.Value && (pe.contrPhase < -10))
            {                
                err = DoubleAdjust(!odd, driftAcc);
                if (!double.IsNaN(err) && jumbo)
                {
                    corr.Add(new Point(driftPos, err)); corrList.Add(err);
                }
            }
            midFringe = (((double)crsUpStrobe.AxisValue + (double)crsDownStrobe.AxisValue) / 2);
            if ((double)crsUpStrobe.AxisValue < (double)crsDownStrobe.AxisValue) midFringe += Math.PI;
            double midFringeMg = fringeRad2mg(midFringe); // from rad to mg
            lastDecomposedAccel = decomposeAccel(driftAcc, accel, orderFactor); //2*Math.PI * 
            devList.Add(Math.Abs(midFringe - fringeMg2rad(accel))); Utils.DoEvents(); 
            //log("fringe drift[mg]/[rad]= " + midFringeMg.ToString("G5") + " / " + midFringe.ToString("G5"));
            log("-----------------------------------");
            return driftAcc;
        }
        
        /// <summary>
        /// Clear the visual components
        /// </summary>
        private void ClearVis()
        {
            double rng = ndAmplitude.Value;
            double step = ndStep.Value;
            downhillLvl = double.NaN; uphillLvl = double.NaN;
            ((AxisDouble)grRslt.Axes[0]).Range = new Range<double>(0, driftPeriod);
            ((AxisDouble)grRslt.Axes[1]).Range = new Range<double>(-rng * 1.05, rng * 1.05);
            ((AxisDouble)grRslt.Axes[2]).Range = new Range<double>(-0.1, 0.1);

            ramp.Clear(); corr.Clear(); corrList.Clear(); devList.Clear();
        }

        /// <summary>
        /// Wrap Run button value as property
        /// </summary>
        public bool Running 
        {
            get { return bbtnRun.Value; }
            set { bbtnRun.Value = value; }
        }
 
        bool stopRequest = false; List<double> devList = new List<double>();
        /// <summary>
        /// Run (stop) something according the active mode 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnRun_Click(object sender, RoutedEventArgs e)
        {
            Running = !Running;
            if (Running)
            {
                Vis2PEparams();
                ClearVis();
                pe.Start((int)ndTimeGap.Value);
            }
            else
            {
                pe.Stop();
            }
        }
        
        /// <summary>
        /// Stuff to do when app closes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void frmMain_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Vis2PEparams();
            pe.SaveParams();
            stopRequest = true;
        }

        private void grFringes_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ((Graph)sender).ResetZoomPan();
        }

        int iStDepth = 5; int dStDepth = 3; // history depths 
        /// <summary>
        /// PID for internal testing
        /// </summary>
        /// <param name="diffY"></param>
        /// <returns></returns>
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

            cr /= 2;
            int curIdx1 = pe.getIfringe((double)crsDownStrobe.AxisValue+cr);
            if (curIdx1 == -1) log("invalid index curIdx1");
            else log("PID> " + pTerm.ToString("G3") + "; " + iTerm.ToString("G3") + "; " + dTerm.ToString("G3") + " ("+cr.ToString("G3") +")"+
                     // PID X correction and Y value after the correction
                     "| new:" + pe.srsFringes[curIdx1].X.ToString("G3") + "/" + pe.srsFringes[curIdx1].Y.ToString("G3"));  // new pos X,Y
            return cr;
        }

        /// <summary>
        /// Convert [rad] in [mg] in fringe
        /// </summary>
        /// <param name="rad"></param>
        /// <returns></returns>
        public double fringeRad2mg(double rad)
        {
            double cp = rad - Math.PI;
            // correction for 2pi period
            while (cp < 0) cp += 2 * Math.PI;
            while (cp > 2 * Math.PI) cp -= 2 * Math.PI;
            if (Utils.InRange(cp, 0, 2 * Math.PI)) return cp * ndOrderFactor.Value;

            throw new Exception("Result phase out of range -> " + cp.ToString());
        }
        /// <summary>
        /// Convert [mg] in [rad] in fringe
        /// </summary>
        /// <param name="rad"></param>
        /// <returns></returns>
        public double fringeMg2rad(double mg)
        {
            double cp = mg / ndOrderFactor.Value;
            while (cp < 0) cp += 2 * Math.PI;
            while (cp > 2 * Math.PI) cp -= 2 * Math.PI;
            if (Utils.InRange(cp, 0, 2 * Math.PI)) return cp;
            
            throw new Exception("Result phase out of range -> " + cp.ToString());
        }

        /// <summary>
        /// For single strobe (obsolete)
        /// </summary>
        /// <param name="drift"></param>
        /// <returns></returns>
        private double SingleAdjust(double drift) // return error in fringe position/phase
        {
            double curX = (double)crsDownStrobe.AxisValue;
            int curIdx = pe.getIfringe(curX);
            if (curIdx == -1)
            {
                log("Error: index cannot be found"); return double.NaN;
            }          
            if (!remote.Enabled)
            {
                double corr = PID(pe.srsFringes[curIdx].Y);
                crsDownStrobe.AxisValue = curX + corr;
            }
            curIdx = pe.getIfringe((double)crsDownStrobe.AxisValue);
            if (curIdx > -1) return drift - (2.5 * Math.PI - pe.srsFringes[curIdx].X); 
            else return double.NaN;
        }

        double downhillLvl = double.NaN, uphillLvl = double.NaN;
        /// <summary>
        /// Calculates the error in fringe position/phase
        /// </summary>
        /// <param name="even"></param>
        /// <param name="accel">accel [mg] - set in fringes </param>
        /// <returns></returns>
        private double DoubleAdjust(bool even, double accel) 
        {
            // simulation of measurement at cursor position
            int curIdx1, curIdx2; double cx, curX1, curX2;
            if (even) // even (down)
            {
                curX1 = (double)crsDownStrobe.AxisValue;
                curIdx1 = pe.getIfringe(curX1);
                if (curIdx1 == -1)
                {
                    log("Error: index cannot be found"); return double.NaN;
                }
                downhillLvl = pe.srsFringes[curIdx1].Y;
            }
            else // odd
            {            
                curX2 = (double)crsUpStrobe.AxisValue;
                curIdx2 = pe.getIfringe(curX2);
                if (curIdx2 == -1)
                {
                    log("Error: index cannot be found"); return double.NaN;
                }
                uphillLvl = pe.srsFringes[curIdx2].Y;
            }
            if (!remote.Connected) // local run
            {
                if (double.IsNaN(uphillLvl) || double.IsNaN(downhillLvl)) return double.NaN;
                double corr = PID(downhillLvl - uphillLvl);
                crsDownStrobe.AxisValue = (double)crsDownStrobe.AxisValue + corr; // adjust down-hill
                crsUpStrobe.AxisValue = (double)crsUpStrobe.AxisValue + corr; // adjust up-hill
            }
            cx = ((double)crsUpStrobe.AxisValue + (double)crsDownStrobe.AxisValue) / 2;
            if ((double)crsUpStrobe.AxisValue < (double)crsDownStrobe.AxisValue) cx += Math.PI;
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
        /// <summary>
        /// Clear log text-box
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            tbLog.Document.Blocks.Clear(); 
        }
        /// <summary>
        /// Single strobe (obsolete)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void rbSingle_Checked(object sender, RoutedEventArgs e)
        {
            if ((crsUpStrobe == null) || (crsDownStrobe == null)) return;
            crsUpStrobe.Visibility = System.Windows.Visibility.Visible;
            crsDownStrobe.AxisValue = 4.71;
            crsDownStrobe.AxisValue = 7.85;
            downhillLvl = double.NaN; uphillLvl = double.NaN;
            
        }
        
        /// <summary>
        /// Actions after the form is loaded
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void frmMain_Loaded(object sender, RoutedEventArgs e)
        {
            remote = new RemoteMessaging();           
            remote.OnActiveComm += new RemoteMessaging.ActiveCommHandler(OnActiveComm);
            remote.OnReceive += new RemoteMessaging.ReceiveHandler(OnReceive);
            remote.Enabled = chkRemoteEnabled.IsChecked.Value;
            remote.Connect("Axel Hub");
            pe.remote = remote;
            pe.LoadParams();
            rbXaxis_Checked(null, null);
            PEparams2Vis();
        }

        private MMexec lastGrpExe;
        /// <summary>
        /// Event when the communication goes on/off 
        /// </summary>
        /// <param name="active"></param>
        /// <param name="forced"></param>
        private void OnActiveComm(bool active, bool forced)
        {
            ledComm.Value = active;
            if (active)
            {
                grpRemote.Foreground = Brushes.Green;
                grpRemote.Header = "Remote - is ready <->";
                btnCommCheck.Content = "Connected";
            }
            else
            {
                if (chkRemoteEnabled.IsChecked.Value)
                {
                    grpRemote.Foreground = Brushes.Red;
                    grpRemote.Header = "Remote - problem -X-";
                    btnCommCheck.Content = "Disconnected";
                }
            }
        }

        /// <summary>
        /// Check the communication validity
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnCommCheck_Click(object sender, RoutedEventArgs e)        
        {
            chkRemoteEnabled.SetCurrentValue(CheckBox.IsCheckedProperty, true);
            if (remote.CheckConnection(true)) grpRemote.Header = "Remote - is ready <->";
            else {
                grpRemote.Header = "Remote - not found! ...starting it";
                System.Diagnostics.Process.Start(File.ReadAllText(Utils.configPath + "axel-hub.bat"), "-remote:\"Axel Probe\"");
                Thread.Sleep(1000);
                remote.CheckConnection(true);
                btnCommCheck.Content = "Wait for it...";
           }
        }

        /// <summary>
        /// Get next message
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>
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
                            //chkFollowPID.IsChecked = (Convert.ToInt32(mme.prms["follow"]) == 1);
                        }
                        crsUpStrobe.Visibility = System.Windows.Visibility.Visible;
                        if (mme.prms.ContainsKey("downStrobe")) crsDownStrobe.AxisValue = Convert.ToDouble(mme.prms["downStrobe"]);
                        if (mme.prms.ContainsKey("downStrobe.X")) crsDownStrobe.AxisValue = Convert.ToDouble(mme.prms["downStrobe.X"]);
                        if (mme.prms.ContainsKey("upStrobe")) crsUpStrobe.AxisValue = Convert.ToDouble(mme.prms["upStrobe"]);
                        if (mme.prms.ContainsKey("upStrobe.X")) crsUpStrobe.AxisValue = Convert.ToDouble(mme.prms["upStrobe.X"]);
                        downhillLvl = double.NaN; uphillLvl = double.NaN;
                        
                        lastGrpExe = mme.Clone();
                        dispatcherTimer.Start();
                    }
                    break;
                case ("phaseAdjust"):
                    {
                        if (!chkFollowPID.IsChecked.Value)
                        {
                            //log("Skip phase adjust: non-following mode!"); return true;
                        }
                        double phase = -11;
                        if (mme.prms.ContainsKey("phase")) phase = Convert.ToDouble(mme.prms["phase"]);
                        string chn = "";
                        // crude approximation, if it doesn't work create separate simul for X and Y
                        if (mme.prms.ContainsKey("phase.X"))
                        {
                            phase = Convert.ToDouble(mme.prms["phase.X"]); chn = "X";
                        }
                        if (mme.prms.ContainsKey("phase.Y")) 
                        {
                            phase = Convert.ToDouble(mme.prms["phase.Y"]); chn = "Y";
                        }
                        int runID = Convert.ToInt32(mme.prms["runID"]);
                        if (runID > -1) 
                        {
                            if (phase > -10)
                            {
                                if ((runID % 2) == 0)
                                {
                                    log("<< phAdj."+chn+" Down " + ((double)crsDownStrobe.AxisValue).ToString("G5")+ " -> "+ phase.ToString("G5"));
                                    if (chkFollowPID.IsChecked.Value) crsDownStrobe.AxisValue = phase; 
                                }
                                else
                                {
                                    log("<< phAdj." + chn + " Up " + ((double)crsUpStrobe.AxisValue).ToString("G5") + " -> " + phase.ToString("G5"));
                                    if (chkFollowPID.IsChecked.Value) crsUpStrobe.AxisValue = phase; 
                                }
                            }                            
                        }
                        else
                        { 
                            pe.contrPhase = phase; // contrast
                            log("<< phAdj." + chn + " contrast @ " + phase.ToString("G5"));
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

        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            dispatcherTimer.Stop();
            crsDownStrobe.Visibility = System.Windows.Visibility.Visible;
            crsUpStrobe.Visibility = System.Windows.Visibility.Visible;
            if(lastGrpExe.cmd.Equals("scan")) 
            {
                MMscan mms = new MMscan();
                if (mms.FromDictionary(lastGrpExe.prms))
                {
                    pe.remoteMode = RemoteMode.Jumbo_Scan;
                    pe.DoScan(mms);
                    pe.remoteMode = RemoteMode.Ready_To_Remote;
                }
            }
            if (lastGrpExe.cmd.Equals("repeat") && !dispatcherTimer.IsEnabled) 
            {
                string jumboGroupID = (string)lastGrpExe.prms["groupID"];
                int jumboCycles = Convert.ToInt32(lastGrpExe.prms["cycles"]);
 
                JumboRepeat(jumboCycles, jumboGroupID);
            }
        }

        /// <summary>
        /// Start Jumbo repeat group
        /// </summary>
        bool wait4adjust = false; double xDownPos = 0, xUpPos = 0;
        private void JumboRepeat(int cycles, string groupID)
        {
            
            MMexec md = new MMexec();
            md.mmexec = "";
            md.cmd = "shotData";
            md.sender = "Axel-probe";
            md.prms["groupID"] = groupID;
            md.prms["runID"] = 0;

            //remote.stopwatch.Restart();
            long realCycles = cycles;
            if (cycles == -1) realCycles = long.MaxValue;
            double A = 0, drift = 0, pos0;
            ramp.Clear(); corr.Clear(); corrList.Clear();
            cancelRequest = false; int smallLoop = (int)(driftPeriod/driftStep);
            int i = -1; log("*** Jumbo repeat ***");
            for (long j = 0; j < realCycles; j++)
            {
                Utils.DoEvents();
                if ((j == (realCycles - 1)) || cancelRequest)
                {
                    md.prms["last"] = 1;
                }
                if (j % smallLoop == 0)
                {
                    i = 0;
                    ramp.Clear(); corr.Clear();
                }
                else i += 1;
                
                pos0 = i * driftStep;
                double acc = calcAtPos(true, pos0, (i % 2) == 1);
                string logAcc = i.ToString()+" acc=" + acc.ToString("G4")+ ": ";
                drift = acc / ndOrderFactor.Value; // [rad]     fringeMg2rad(
                //-------------------------------------------------
                wait4adjust = chkFollowPID.IsChecked.Value;
                md.prms["iTime"] = DateTime.Now.Ticks; md.prms["iTime"] = 50* TimeSpan.TicksPerMillisecond; // 50[ms]
                if (lastDecomposedAccel.ContainsKey("mems")) md.prms["MEMS"] = (string)(lastDecomposedAccel["mems"].ToString("G5")); // in [mg]
                              
                if (pe.contrPhase > -10) md.prms["runID"] = -1;
                else pe.b4ConstrID = (int)md.prms["runID"];
                logAcc += md.prms["runID"].ToString();

                if (!pe.SingleShot(A, pe.axis, ref md)) break; // reply with signal

                // ============================
                int idx = 0; string side;
                if (pe.contrPhase > -10)
                {
                    idx = pe.getIfringe(pe.contrPhase); side = "middle";
                }
                else
                {    
                    int runID = (int)md.prms["runID"];
                    if ((runID % 2) == 0)
                    {
                        idx = pe.getIfringe((double)crsDownStrobe.AxisValue); side = "down";
                    }
                    else
                    {
                        idx = pe.getIfringe((double)crsUpStrobe.AxisValue); side = "up";
                    }

                }
                A = pe.srsFringes[idx].Y;
                log(logAcc + " "+ side + " >> " + pe.srsFringes[idx].X.ToString("G4") + "; A= " + A.ToString("G4"));
                //=================================   
                while (wait4adjust && !cancelRequest) // wait for adjust
                {
                    Thread.Sleep((int)ndTimeGap.Value);
                    Utils.DoEvents();
                }                                
                Thread.Sleep((int)ndTimeGap.Value);
                if (cancelRequest) break;

                md.prms["runID"] = pe.b4ConstrID + 1; // move to next

                if (pe.contrPhase > -10) pe.contrPhase = -11;// recover normalcy 
            }
        }
        /**** Jumbo repeat ***
-----------------------------------
<< phAdj.X Down 1.6085 -> 1.6014
0 acc=1.2: 0 down >> 1.583; A= -0.1287
-----------------------------------
<< phAdj.X Up 4.5993 -> 4.6662
1 acc=1.2: 1 up >> 4.65; A= 0.203
-----------------------------------
<< phAdj.X Down 1.6085 -> 1.7674
2 acc=1.2: 2 down >> 1.734; A= 0.02177
-----------------------------------
<< phAdj.X Up 4.6747 -> 4.7575
3 acc=1.2: 3 up >> 4.725; A= 0.1287
-----------------------------------
<< phAdj.X Down 1.7593 -> 1.8225
4 acc=1.2: 4 down >> 1.81; A= 0.09702
-----------------------------------
<< phAdj.X Up 4.7501 -> 4.7732
5 acc=1.2: 5 up >> 4.75; A= 0.1037
-----------------------------------
<< phAdj.X Down 1.8347 -> 1.8246
6 acc=1.2: 6 down >> 1.81; A= 0.09702
-----------------------------------
<< phAdj.X Up 4.7752 -> 4.7759
7 acc=1.2: 7 up >> 4.75; A= 0.1037
-----------------------------------
<< phAdj.X Down 1.8347 -> 1.8277
8 acc=1.2: 8 down >> 1.81; A= 0.09702
-----------------------------------
<< phAdj.X Up 4.7752 -> 4.7793
9 acc=1.2: 9 up >> 4.75; A= 0.1037
-----------------------------------
<< phAdj.X contrast @ 3.3035
10 acc=1.2: 10 middle >> 3.292; A= 1
-----------------------------------
<< phAdj.X Up 4.7752 -> 0
11 acc=1.2: 11 up >> 0; A= -0.99
-----------------------------------
<< phAdj.X Down 1.8347 -> 1.2842
12 acc=1.2: 12 down >> 1.257; A= -0.4401
-----------------------------------
*/
        /// <summary>
        /// Simple scan mode initiation
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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
                pe.remoteMode = RemoteMode.Simple_Scan;
                msg = JsonConvert.SerializeObject(mm);
                if (!remote.sendCommand(msg)) MessageBox.Show("send json problem!");
                log(msg);
                pe.DoScan(mms);
                pe.remoteMode = RemoteMode.Ready_To_Remote;
            }
        }
        /// <summary>
        /// Simple repeat mode initiation
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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
            pe.remoteMode = RemoteMode.Simple_Repeat;
            pe.SimpleRepeat(cycles, groupID);
            pe.remoteMode = RemoteMode.Ready_To_Remote;
        }

        /// <summary>
        /// Connect/disconnect
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void chkRemoteEnabled_Checked(object sender, RoutedEventArgs e)
        {
            if (Utils.isNull(remote)) return;
            Utils.DoEvents();
            remote.Enabled = chkRemoteEnabled.IsChecked.Value;
            remote.CheckConnection(true);
        }

        /// <summary>
        /// Abort here and send Abort to partner
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnAbort_Click(object sender, RoutedEventArgs e)
        {
            switch (pe.remoteMode)
            {
                case RemoteMode.Simple_Scan: pe.CancelScan();
                    break;
                case RemoteMode.Simple_Repeat: pe.CancelRepeat();
                    break;
            }
            Utils.DoEvents();

            cancelRequest = true;
            Utils.DoEvents();
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
        /// <summary>
        /// Customizable from code (HERE) scan
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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

        /// <summary>
        /// Control the flow (pause) with NumPad keys 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void frmMain_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key.Equals(Key.NumPad0)) chkPause.IsChecked = !chkPause.IsChecked.Value;
            if (e.Key.Equals(Key.NumPad1)) pe.pauseSingle();
        }
        
        /// <summary>
        /// About applilcation message box
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void imgAbout_MouseDown(object sender, MouseButtonEventArgs e)
        {
            MessageBox.Show("           Axel Probe v2.0 \n         by Teodor Krastev \nfor Imperial College, London, UK\n\n   visit: http://axelsuite.com", "About");
        }

        private void rbXaxis_Checked(object sender, RoutedEventArgs e)
        {
            if (Utils.isNull(pe)) return;
            pe.axis = -1;
            if (rbXaxis.IsChecked.Value) pe.axis = 0;
            if (rbYaxis.IsChecked.Value) pe.axis = 1;
            if (rbXYaxes.IsChecked.Value) pe.axis = 2;
        }
        /// <summary>
        /// Control the speed by changing the time gap
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ndTimeGap_ValueChanged(object sender, ValueChangedEventArgs<double> e)
        {
            if (!Utils.isNull(pe)) pe.bpps["TimeGap"] = ndTimeGap.Value;
        }
     }
}
