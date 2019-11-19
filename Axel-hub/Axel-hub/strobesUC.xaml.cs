﻿using System;
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

using UtilsNS;

namespace Axel_hub
{
    public static class calcAccel
    {
        public static double zeroFringe(Point Down, Point Up, double phi0) // [rad] [-pi..pi] 
        {
            double cp = ((Up.X + Down.X) / 2) - phi0; // centerPos
            if (Down.X < Up.X) cp = Math.PI - cp;
            else cp = 2 * Math.PI - cp;
            while (cp < -Math.PI) cp += 2 * Math.PI;
            while (cp > Math.PI) cp -= 2 * Math.PI;

            return cp;
        }

        public static double accelOrder(double accel, double factor, out double resid) // accel [mg]; factor [mg/rad]; resid [rad]
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
        public static double resultAccel(double order, double quant, double factor, out double orderAccel, out double residAccel) 
                                    // order (from mems - int); quant (from MOT) [rad]; factor [mg/rad]; returns accel [mg]
                                    // 2*PI * factor * order + factor * quant 
        {
            residAccel = quant * factor;  
            orderAccel = (Utils.InRange(order, -0.01, 0.01)) ? 0 : orderAccel = (2 * Math.PI) * factor * order;
            return orderAccel + residAccel;
        }

        public static double between2piAndNewPos(bool down, double newPos) // limit the strobe.X pos to 0..2pi
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
    /// Interaction logic for strobesUC.xaml
    /// </summary>
    public partial class strobesUC : UserControl
    {
        private const string dPrec = "G4";
        private MMexec grpMME, lastMMEin, lastMMEout;

        private bool _PID_Enabled;
        public bool PID_Enabled
        {
            get { return _PID_Enabled; }
            set
            {
                _PID_Enabled = value;
                if (value) lbTitle.Content = "PID - ON";
                else lbTitle.Content = "PID - OFF";
            }
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

        public strobesUC()
        {
            InitializeComponent();
            iStack = new List<double>(); dStack = new List<double>();
            accelSet = new Dictionary<string,double>();
            Reset();
            lastMMEout = new MMexec();
        }

        public void Reset()
        {
            Down = new Point(1.6, 0); // even runID 
            Up = new Point(4.7, 0);   // odd runID 
        }

        public void Flip() // flip Down and Up
        {
            Point tmp = new Point(Down.X, Down.Y);
            Down = new Point(Up.X, Up.Y);
            Up = new Point(tmp.X, tmp.Y);
        }

        public void Init(string _prefix)
        {
            prefix = _prefix;
            configFile = Utils.configPath + "PID_" + prefix + ".cfg";
            OpenConfigFile();
        }

        public void OnJumboRepeat(double _fringeScale, double _fringeShift, MMexec _grpMME, double contrastV) // before each Jumbo Repeat
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

        public delegate void LogHandler(string txt, Color? clr = null);
        public event LogHandler OnLog;

        public void LogEvent(string txt, Color? clr = null)
        {
            if (!Utils.isNull(OnLog)) OnLog(txt, clr);
        }
        
        public double centreFringe() 
        {
            return  (Up.X + Down.X) / 2 ;
        }

        public double calcContrast(double A)
        {
            return  Math.Abs((Up.Y + Down.Y) / 2 - A);
        }

        public double zeroFringe() // [rad] [-pi..pi] 
        {
            return calcAccel.zeroFringe(Down, Up, fringeShift);
        }

        public Dictionary<string, double> deconstructAccel(double accel, double mems)
        {
            // accel[mg] - target(real); mems[mg] - measured (real + noise); zeroFringe[rad] - from PID follow; factor [mg/rad]
            Dictionary<string, double> da = new Dictionary<string, double>(); double resid;
            if (!Double.IsNaN(accel))
            {
                da["accel.R"] = accel; // R for reference                
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

        public double nextShot(int runID, double asymmetry, out double correction) // return phaseCorr - corrected Raman phase (0 if not PID)
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
            Color clr1 = Brushes.MediumSeaGreen.Color;
            if (Math.Abs(disbalNorm) > 0.8) clr1 = Brushes.Red.Color;
            double corr = 0, piCorr = 0;
            if (PID_Enabled)
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
                    else { LogEvent("SKIP piCorr=" + piCorr.ToString("G4") + "; corr= " + corr.ToString("G4"), Brushes.Red.Color); }
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

        public MMexec backMME(int runID, double asymmetry, MMexec mme = null) // mme is ONLY for incoming probe feed
        {
            accelSet.Clear(); lastMMEin = null;
            if (!Utils.isNull(mme))
            {
                if (mme.sender.Equals("Axel-probe")) // take MEMS if probeMode
                {
                    lastMMEin = mme.Clone();
                    double mems = 0;
                    if (mme.prms.ContainsKey("MEMS")) mems = Convert.ToDouble(mme.prms["MEMS"]);
                    double accel = mems;
                    if (mme.prms.ContainsKey("accel")) accel = Convert.ToDouble(mme.prms["accel"]); // the noiseless accel, only to compare
                    accelSet = deconstructAccel(accel, mems);
                }                 
            }            
            MMexec mmeOut = null;
            // mmeOut.mmexec is "downhill" : "uphill" : "contrastCheck" only for probe 
            // for MM2 only runID counts -> even:odd:-1 
            // runID 
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
            lastMMEout = mmeOut.Clone(); return mmeOut;
            //if (newPhase > -10) 
            //else return null;
        }

        string[] Titles = { "runI", "tP", "tI", "tD", "Down.X", "Up.X", "disbal", "corr", "iSD-R", "contrast" };
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
                if (item.Equals("contrast")) lbi.Foreground = Brushes.DarkOrange;
                lbReport.Items.Add(lbi);
                if (Rpr2file)
                {
                    ss += rpr[item].ToString(dPrec) + "\t";
                }
            }
            if (!Utils.isNull(logger))
                if (Rpr2file) logger.log(ss);
        }

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
                LogEvent("PID> " + pTerm.ToString("G3") + "  " + iTerm.ToString("G3") + "  " + dTerm.ToString("G3"), Brushes.Navy.Color);
                // PID X correction and Y value after the correction
                LogEvent("+corr " + cr.ToString(dPrec) + " DB " + disbalance.ToString(dPrec) + " iSD " + rpr["iSD-R"].ToString(dPrec), Brushes.Navy.Color);
            }
            return cr;
        }

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