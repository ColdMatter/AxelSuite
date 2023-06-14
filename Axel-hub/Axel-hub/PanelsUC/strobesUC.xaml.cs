using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using OptionsNS;
using UtilsNS;

namespace Axel_hub
{
    /// <summary>
    /// Library for calculating acceleration from fringes, phase, etc
    /// </summary>
    public static class calcAccel
    {
        /// <summary>
        /// Calculate phase ref for this fringe
        /// </summary>
        /// <param name="Down">down strobe</param>
        /// <param name="Up">up strobe</param>
        /// <param name="phi0">phase offset</param>
        /// <returns>phase ref for this fringe</returns>
        public static double zeroFringe(Point Down, Point Up, double phi0) // [rad] [-pi..pi] 
        {
            double cp = ((Up.X + Down.X) / 2) - phi0; // centerPos
            if (Down.X < Up.X) cp = Math.PI - cp;
            else cp = 2 * Math.PI - cp;
            while (cp < -Math.PI) cp += 2 * Math.PI;
            while (cp > Math.PI) cp -= 2 * Math.PI;

            return cp;
        }

        /// <summary>
        /// Calculate accleration order
        /// </summary>
        /// <param name="accel">acceleration [mg]</param>
        /// <param name="factor">convertion factor [mg/rad]</param>
        /// <param name="resid">residual [rad]</param>
        /// <returns>acceleration order</returns>
        public static double accelOrder(double accel, double factor, out double resid) 
        {
            double accelRad = accel / factor;
            if (Utils.InRange(accelRad, -Math.PI, Math.PI))
            {
                resid = accelRad;
                return 0;
            }
            else
            {
                double mag = Math.Truncate((Math.Abs(accelRad) - Math.PI) / (2 * Math.PI)) + 1; // 
                double aOrd = Math.Sign(accelRad) * mag;
                resid = Math.Sign(accelRad) * ((Math.Abs(accelRad) - Math.PI) % (2 * Math.PI));
                return aOrd;
            }
        }

        /// <summary>
        /// Calculate accleration, acceleration order and acceleration residual
        /// a = 2*PI * factor * order + factor * quant 
        /// </summary>
        /// <param name="order">order (from mems - int)</param>
        /// <param name="quant">quant (from MOT) [rad]</param>
        /// <param name="factor">convertion factor [mg/rad]</param>
        /// <param name="orderAccel">acceleration order (int)</param>
        /// <param name="residAccel">acceleration residual</param>
        /// <returns>acceleration [mg]</returns>
        public static double resultAccel(double order, double quant, double factor, out double orderAccel, out double residAccel) 
        {
            residAccel = quant * factor;  
            orderAccel = (Utils.InRange(order, -0.01, 0.01)) ? 0 : orderAccel = (2 * Math.PI) * factor * order;
            return orderAccel + residAccel;
        }

        /// <summary>
        /// Limit the strobe.X pos to 0..2pi
        /// </summary>
        /// <param name="down">down/up</param>
        /// <param name="newPos">value to be limited</param>
        /// <returns>limited value</returns>
        public static double between2piAndNewPos(bool down, double newPos) 
        {
            double cp = newPos;
            // correction for 2pi period
            while (cp < 0) cp += 2 * Math.PI;
            while (cp > 2 * Math.PI) cp -= 2 * Math.PI;
            if (Utils.InRange(cp, 0, 2 * Math.PI)) return cp;

            throw new Exception("Result phase out of range -> " + cp.ToString());
        }
    }
    /// <summary>
    /// Interaction logic for strobesUC.xaml user control
    /// Controlling and calculating strobes of fringe
    /// </summary>
    public partial class strobesUC : UserControl
    {
        private const string dPrec = "G4";
        private MMexec grpMME, lastMMEin, lastMMEout;
        GeneralOptions genOptions;

        /// <summary>
        /// PID follow the strobe position; now only for show
        /// </summary>
        public void SetPID_Enabled(bool pid)
        {
            if (pid) lbTitle.Content = "PID - ON";
            else lbTitle.Content = "PID - OFF";          
        }
        private bool LogPID { get { return chkPIDlog.IsChecked.Value; } }
        private bool Rpr2file { get { return chkRpr2file.IsChecked.Value; } }
        public string prefix { get; private set; }
        public double kP { get { return ndKP.Value; } private set { ndKP.Value = value; } }
        public double kI { get { return ndKI.Value; } private set { ndKI.Value = value; } }
        public double kD { get { return ndKD.Value; } private set { ndKD.Value = value; } }
        public int PiWeight { get { return ndPiWeight.Value; } private set { ndPiWeight.Value = value; } } // [%]
        public int kIdepth { get { return ndKIdepth.Value; } private set { ndKIdepth.Value = value; } }
        public int kDdepth { get { return ndKDdepth.Value; } private set { ndKDdepth.Value = value; } }
        public int FreqContrast { get { return ndFreqContrast.Value; } private set { ndFreqContrast.Value = value; } }

        public Dictionary<string, double> accelSet { get; private set; } // decomposed accelaration

        public double fringeScale { get; private set; } // [mg/rad]
        public double fringeShift { get; private set; } // [rad]
        public double disbalNorm { get; private set; }
        private double refContrast = -1; private double lastContrast = -1;
        int runI = 0;
        string configFile;
        List<double> iStack, dStack;
        private FileLogger logger;

        public Point Down; // even runID 
        public Point Up;   // odd runID 
        public Point Low;  // lowest point in the middle of 
        
        /// <summary>
        /// Class constructor
        /// </summary>
        public strobesUC()
        {
            InitializeComponent();
            iStack = new List<double>(); dStack = new List<double>();
            accelSet = new Dictionary<string,double>();
            Reset();
            lastMMEout = new MMexec();
        }

        /// <summary>
        /// Initaile strobe for axel-probe simulated fringes
        /// </summary>
        public void Reset()
        {
            Down = new Point(1.6, 0); // even runID 
            Up = new Point(4.7, 0);   // odd runID 
        }

        /// <summary>
        /// Exchange UP/DOWN strobe positions
        /// </summary>
        public void Flip() // flip Down and Up
        {
            Point tmp = new Point(Down.X, Down.Y);
            Down = new Point(Up.X, Up.Y);
            Up = new Point(tmp.X, tmp.Y);
        }

        /// <summary>
        /// Initiate strobe from file settings 
        /// </summary>
        /// <param name="_prefix"></param>
        public void Init(string _prefix, ref GeneralOptions _genOptions)
        {
            prefix = _prefix;
            genOptions = _genOptions;
            configFile = Utils.configPath + "PID_" + prefix + ".cfg";
            OpenConfigFile();
        }
        /// <summary>
        /// calculate Up/Down from fit coefficients 
        /// signal(x) = scale[0] * cos(K[1]*x + phi0[2]) + offset[3] -> idx in cf (coeffs)
        /// </summary>
        /// <param name="cf">fit coeff. </param>
        public void calcStrobesFromFit(double[] cf)           
        {
            double signal(double phi)
            { 
                return cf[0] * Math.Cos(cf[1] * phi + cf[2]) + cf[3]; 
            }
            if (!cf.Length.Equals(4)) return;
            double u, d, phi0 = cf[2];
            if (phi0 > Math.PI) phi0 -= 2*Math.PI;
            d = 0.5 * Math.PI / cf[1] - phi0; // 0.5 and 1.5 PI are zeros of cos function
            u = 1.5 * Math.PI / cf[1] - phi0;
            Down.X = d; Down.Y = signal(d);
            Up.X = u; Up.Y = signal(u);
        }
        /// <summary>
        /// Call this before each Jumbo Repeat for group MMexec and modes synchronization
        /// </summary>
        /// <param name="_fringeScale"></param>
        /// <param name="_fringeShift"></param>
        /// <param name="_grpMME"></param>
        /// <param name="contrastV"></param>
        public void OnJumboRepeat(double _fringeScale, double _fringeShift, MMexec _grpMME, double contrastV) 
        {
            if (!Double.IsNaN(_fringeScale)) fringeScale = _fringeScale;
            if (!Double.IsNaN(_fringeShift)) fringeShift = _fringeShift;
            if (!refContrast.Equals(0) && !Double.IsNaN(contrastV)) refContrast = calcContrast(contrastV);
            else refContrast = -1; // not avail.
            grpMME = _grpMME.Clone();
            
            if (Rpr2file)
            {
                logger = new FileLogger(prefix);
                logger.defaultExt = ".pid";
                logger.Enabled = true;
                logger.log("# " + JsonConvert.SerializeObject(grpMME));

                string ss = "# ";
                foreach (var item in Titles) ss += item + "\t";
                logger.log(ss);
            }
        }

        /// <summary>
        /// Log event for massage export
        /// </summary>
        /// <param name="txt"></param>
        /// <param name="clr"></param>
        public delegate void LogHandler(string txt, SolidColorBrush clr = null);
        public event LogHandler OnLog;

        public void LogEvent(string txt, SolidColorBrush clr = null)
        {
            if (!Utils.isNull(OnLog)) OnLog(txt, clr);
        }
        
        /// <summary>
        /// Calculating fringe centre
        /// </summary>
        /// <returns></returns>
        public double centreFringe() 
        {
            return  (Up.X + Down.X) / 2 ;
        }

        /// <summary>
        /// Calculating contrast
        /// </summary>
        /// <param name="A">Asymetry (signal)</param>
        /// <returns>Calculated contrast</returns>
        public double calcContrast(double A)
        {
            return  Math.Abs((Up.Y + Down.Y) / 2 - A);
        }

        /// <summary>
        /// Calculating zeroFringe - similar to centreFring but with phase shift locked in [-pi..pi]
        /// </summary>
        /// <returns>Calculated zeroFringe [rad] [-pi..pi]</returns>
        public double zeroFringe()  
        {
            return calcAccel.zeroFringe(Down, Up, fringeShift);
        }

        /// <summary>
        /// Deconstructing accaleration to acceleration components - see dictionary keys 
        /// </summary>
        /// <param name="accel">acceleration [mg] - target(real)</param>
        /// <param name="mems">mems accel.[mg] - measured (real + noise)</param>
        /// <returns></returns>
        public Dictionary<string, double> deconstructAccel(double accel, double mems)
        {
            // zeroFringe[rad] - from PID follow; factor [mg/rad]
            Dictionary<string, double> da = new Dictionary<string, double>(); double resid;
            
            // R for reference 
            if (!Double.IsNaN(accel))
            {
                da["accel.R"] = accel;                
                double orderR = calcAccel.accelOrder(accel, fringeScale, out resid);
                da["order.R"] = orderR;
                da["resid.R"] = resid; // [rad] residual for atomic interferometer
            }
            if (Double.IsNaN(mems)) return da;
            da["mems"] = mems; 

            // M for measure
            da["frgRad.M"] = zeroFringe(); // [rad]

            da["order.M"] = calcAccel.accelOrder(da["mems"] - da["frgRad.M"] * fringeScale, fringeScale, out resid); // order from mems
            da["resid.M"] = resid * fringeScale;

            calcAccel.accelOrder(da["frgRad.M"] * fringeScale, fringeScale, out resid); // resid [rad] from measured fringe pattern
            
            double orderAccel, residAccel;
            da["accel.M"] = calcAccel.resultAccel(da["order.M"], resid, fringeScale, out orderAccel, out residAccel); // [mg]
            //da["accel.O"] = orderAccel; da["accel.P"] = residAccel;
            da["diff"] = accel - da["accel.M"];
            return da;
        }

        /// <summary>
        /// Calculated phaseCorr - corrected Raman phase (0 if not PID)
        /// </summary>
        /// <param name="runID">Shot number</param>
        /// <param name="asymmetry">Asymetry value</param>
        /// <param name="correction">The correction value</param>
        /// <returns>The corrected position</returns>
        public double nextShot(int runID, double asymmetry, out double correction) // 
        {
            double phaseCorr = -11; double disbalance = 0; runI = runID;
            if (!Utils.isNull(lastMMEout))
            {
                if (lastMMEout.mmexec.Equals("contrastCheck"))
                {
                    lastContrast = calcContrast(asymmetry); 
                    correction = 0;
                    return phaseCorr;
                }
            }
            if (LogPID) LogEvent("Down: " + Down.X.ToString(dPrec) + " / " + Down.Y.ToString(dPrec) + "; Up: " + Up.X.ToString(dPrec) + " / " + Up.Y.ToString(dPrec));
            if ((runID % 2) == 0) { Down.Y = asymmetry; }
            else { Up.Y = asymmetry; }
            disbalance =  Up.Y - Down.Y; 
            disbalNorm = (disbalance / fringeScale) / 2; // normilized disbalance over fringeScale (vert.)
            SolidColorBrush clr1 = Brushes.MediumSeaGreen;
            if (Math.Abs(disbalNorm) > 0.8) clr1 = Brushes.Coral;
            double corr = 0, piCorr = 0;
            if (genOptions.followPID)
            {
                corr = PID(disbalNorm); // [rad]      
                if (PiWeight > 1)
                {
                    if ((runID % 2) == 0) 
                    {   // adjust Down
                        piCorr = (Down.X < Up.X) ? Up.X - Math.PI : Up.X + Math.PI;
                        piCorr -= Down.X;
                    }
                    else 
                    {   // adjust Up
                        piCorr = (Down.X < Up.X) ? Down.X + Math.PI : Down.X - Math.PI;
                        piCorr -= Up.X;
                    }
                    double npw = PiWeight/100;
                    if (Math.Abs(corr - piCorr) < (Math.PI / 3)) corr = piCorr * npw + corr * (1 - npw);
                    else { LogEvent("SKIP piCorr=" + piCorr.ToString("G4") + "; corr= " + corr.ToString("G4"), Brushes.Red); }
                }
            }
            if ((runID % 2) == 0) //MMDataConverter.Restrict2twoPI(
            {
                Down.X = calcAccel.between2piAndNewPos(true, Down.X + corr); phaseCorr = Down.X;
                LogEvent("new Down." + prefix + ": " + phaseCorr.ToString(dPrec), clr1);
            }
            else
            {
                Up.X = calcAccel.between2piAndNewPos(false, Up.X + corr); phaseCorr = Up.X;
                LogEvent("new Up." + prefix + ": " + phaseCorr.ToString(dPrec), clr1);
            }
            correction = corr;
            return phaseCorr;
        }

        /// <summary>
        /// Prepare back message with new Raman phase value
        /// </summary>
        /// <param name="runID">Shot number</param>
        /// <param name="asymmetry">Asymmetry</param>
        /// <param name="mme">mme is ONLY for incoming axel-probe feed</param>
        /// <returns></returns>
        public MMexec backMME(int runID, double asymmetry, MMexec mme = null) 
        {
            accelSet.Clear(); lastMMEin = null;
            if (!Utils.isNull(mme))
            {
                double accel_R = mme.prms.ContainsKey("accel") ? accel_R = Convert.ToDouble(mme.prms["accel"]) : Double.NaN; // modeled (reference) quantum acceleration from Axel-Probe       
                double mems_R = mme.prms.ContainsKey("MEMS") ? mems_R = Convert.ToDouble(mme.prms["MEMS"]) : Double.NaN;   // modeled (reference) MEMS acceleration from Axel-Probe 
                double mems = Double.NaN;
                if (mme.prms.ContainsKey("Interferometer") && genOptions.memsInJumbo.Equals(GeneralOptions.MemsInJumbo.PXI4462))
                    mems = ((double[])mme.prms["Interferometer"]).Average();
                else mems = mems_R;
                      
                if (!genOptions.Diagnostics) accelSet = deconstructAccel(accel_R, mems);
                if (!Double.IsNaN(accel_R)) accelSet["accel.R"] = accel_R;
                if (!Double.IsNaN(mems_R)) accelSet["mems.R"] = mems_R;  

                lastMMEin = mme.Clone();
            }            
            MMexec mmeOut = null;
            // mmeOut.mmexec is "downhill" : "uphill" : "contrastCheck" only for probe 
            // for MM2 only runID counts -> even:odd:-1 
            // runID 
            if (genOptions.Diagnostics)
            {
                mmeOut = new MMexec("diagnostics", "Axel-hub", "phaseAdjust");
                mmeOut.prms.Clear();
                mmeOut.prms["runID"] = runID;
            }
            else
            { 
                double newPhase = 0;
                bool bb = !FreqContrast.Equals(0) && !Utils.isNull(lastMMEout);
                if (bb) FreqContrast = Utils.EnsureRange(FreqContrast, 10, 1000);
                if (bb) bb &= (runID % FreqContrast).Equals(0) && (runID > 0); //!lastMMEout.mmexec.Equals("");
                if (bb) // contrastCheck !!!
                {
                    mmeOut = new MMexec("contrastCheck", "Axel-hub", "phaseAdjust");
                    mmeOut.prms.Clear();
                    mmeOut.prms["runID"] = -1;
                    newPhase = centreFringe();
                    if (Down.X > Up.X) newPhase += Math.PI; // (centreFringe() > 0) ? centreFringe() - Math.PI : centreFringe() + Math.PI;              
                    mmeOut.prms["phase." + prefix] = newPhase.ToString("G6");                
                }
                else // regular phaseAdjust
                {
                    mmeOut = new MMexec((runID % 2).Equals(0) ? "downhill" : "uphill", "Axel-hub", "phaseAdjust");
                    mmeOut.prms.Clear();
                    mmeOut.prms["runID"] = runID;
                    double correction = 0;                
                    newPhase = nextShot(runID, asymmetry, out correction);
                    if (newPhase > -10)
                    {
                        mmeOut.prms["phase." + prefix] = newPhase.ToString("G6");
                        mmeOut.prms["corr." + prefix] = correction.ToString("G6");
                    }
                }              
            }        
            lastMMEout = mmeOut.Clone(); return mmeOut;         
        }

        string[] Titles = { "runI", "tP", "tI", "tD", "Down.X", "Up.X", "disbal", "corr", "iSD-R", "contrast" };
        /// <summary>
        /// Update table with strobes/PID calculation results
        /// </summary>
        /// <param name="rpr"></param>
        private void fillReport(Dictionary<string, double> rpr)
        {
            string ss = "";
            lbReport.Items.Clear();
            foreach (var item in Titles)
            {
                if (!rpr.ContainsKey(item)) continue;
                ListBoxItem lbi = new ListBoxItem();
                lbi.Content = string.Format("{0}: {1:" + dPrec + "}", item, rpr[item]);
                if (item.Substring(0,1).Equals("t")) lbi.Foreground = Brushes.MediumBlue;
                if (item.Equals("disbal")) lbi.Foreground = Brushes.Maroon;
                if (item.Equals("corr")) lbi.Foreground = Brushes.Chocolate;
                if (item.Equals("iSD-R")) lbi.Foreground = Brushes.SeaGreen;
                if (item.Equals("contrast"))
                {
                    lbi.Foreground = Brushes.DarkOrange;
                    if (rpr[item] < 0.6) lbi.Foreground = Brushes.Red;                  
                }    
                    
                lbReport.Items.Add(lbi);
                if (Rpr2file)
                {
                    ss += rpr[item].ToString(dPrec) + "\t";
                }
            }
            if (!Utils.isNull(logger))
                if (Rpr2file) logger.log(ss);
        }

        /// <summary>
        /// Calculating the phase correction from the disbalance on strobes Ys
        /// </summary>
        /// <param name="disbalance"></param>
        /// <returns></returns>
        public double PID(double disbalance) // normalized disbalance
        {
            Dictionary<string, double> rpr = new Dictionary<string, double>();
            double pTerm = disbalance; // proportional

            iStack.Add(disbalance); while (iStack.Count > kIdepth) iStack.RemoveAt(0);
            double iTerm = iStack.Average();

            double iTermSD = 0;
            foreach (double d in iStack)
            {
                iTermSD += (d - iTerm)*(d - iTerm);
            }
            iTermSD = Math.Sqrt(iTermSD / (double)iStack.Count);

            dStack.Add(disbalance); while (dStack.Count > kDdepth) dStack.RemoveAt(0);
            double dTerm = 0;
            for (int i = 0; i < dStack.Count - 1; i++)
            {
                dTerm += dStack[i + 1] - dStack[i];
            }
            dTerm /= Math.Max(dStack.Count - 1, 1);

            double cr = kP * pTerm + kI * iTerm + kD * dTerm;

            rpr["runI"] = runI;
            rpr["kP"] = kP; rpr["kI"] = kI; rpr["kD"] = kD;
            rpr["tP"] = pTerm; rpr["tI"] = iTerm; rpr["tD"] = dTerm;
            rpr["Down.X"] = Down.X; rpr["Up.X"] = Up.X;
            rpr["disbal"] = disbalance; rpr["corr"] = cr; rpr["iSD-R"] = iTermSD / Math.Abs(iTerm);
            if (FreqContrast > 1) rpr["contrast"] = lastContrast;

            fillReport(rpr);
            if (LogPID)
            {
                LogEvent("PID> " + pTerm.ToString("G3") + "  " + iTerm.ToString("G3") + "  " + dTerm.ToString("G3"), Brushes.Navy);
                // PID X correction and Y value after the correction
                LogEvent("+corr " + cr.ToString(dPrec) + " DB " + disbalance.ToString(dPrec) + " iSD " + rpr["iSD-R"].ToString(dPrec), Brushes.Navy);
            }
            return cr;
        }
        
        /// <summary>
        /// Save Config file in Config directory of Axel-hub
        /// </summary>
        public void SaveConfigFile()
        {
            Dictionary<string, string> config = new Dictionary<string, string>();
            config["kP"] = kP.ToString(dPrec); config["kI"] = kI.ToString(dPrec); config["kD"] = kD.ToString(dPrec);
            config["kIdepth"] = kIdepth.ToString(); config["kDdepth"] = kDdepth.ToString();
            config["PiWeight"] = PiWeight.ToString(); config["FreqContrast"] = FreqContrast.ToString();
            config["PID2log"] = LogPID.ToString(); config["Rpr2file"] = Rpr2file.ToString();

            string fileJson = JsonConvert.SerializeObject(config);
            File.WriteAllText(configFile, fileJson);
        }
        /// <summary>
        /// Open Config file from Config directory of Axel-hub
        /// </summary>
        public void OpenConfigFile()
        {
            string fileJson = ""; Dictionary<string, string> config;
            if (File.Exists(configFile))
            {
                fileJson = File.ReadAllText(configFile);
                config = JsonConvert.DeserializeObject<Dictionary<string, string>>(fileJson);                
            }
            else
            {
                Utils.TimedMessageBox("No PID config file ("+configFile+"): assuming defaults");
                config = new Dictionary<string, string>();
            }
            kP = config.ContainsKey("kP") ? Utils.Convert2DoubleDef(config["kP"], 2) : 2;
            kI = config.ContainsKey("kI") ? Utils.Convert2DoubleDef(config["kI"], 0) : 0;
            kD = config.ContainsKey("kD") ? Utils.Convert2DoubleDef(config["kD"], 0) : 0;
            kIdepth = config.ContainsKey("kIdepth") ? Utils.Convert2IntDef(config["kIdepth"], 5) : 5;
            kDdepth = config.ContainsKey("kDdepth") ? Utils.Convert2IntDef(config["kDdepth"], 3) : 3;
            PiWeight = config.ContainsKey("PiWeight") ? Utils.Convert2IntDef(config["PiWeight"], 0) : 0;
            FreqContrast = config.ContainsKey("FreqContrast") ? Utils.Convert2IntDef(config["FreqContrast"], 5) : 5;

            chkPIDlog.IsChecked = config.ContainsKey("PID2log") ? Utils.Convert2BoolDef(config["PID2log"]) : false;
            chkRpr2file.IsChecked = config.ContainsKey("Rpr2file") ? Utils.Convert2BoolDef(config["Rpr2file"]) : false;
        }
    }
}
