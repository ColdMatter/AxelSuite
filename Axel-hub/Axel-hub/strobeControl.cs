using System;
using System.Windows;
using System.Windows.Media;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using UtilsNS;

namespace Axel_hub
{
    public class strobeClass
    {
        private const bool LogPID = true;
        private const string dPrec = "G4";
        public double kP { get; set; }
        public double kI { get; set; }
        public double kD { get; set; }
        public bool PID_Enabled { get; set; }
        public double fringeScale { get; set; }
        public double disbalCorr { get; private set; }

        List<double> iStack, dStack;

        public Point Down = new Point(1.6,0); // even runID 
        public Point Up = new Point(4.7,0);   // odd runID 
        public strobeClass()
        {
            iStack = new List<double>(); dStack = new List<double>();
        }
        public delegate void LogHandler(string txt, Color? clr = null);
        public event LogHandler OnLog;

        public void LogEvent(string txt, Color? clr = null)
        {
            if (!Utils.isNull(OnLog)) OnLog(txt, clr);
        }

        public double centralPhase() { return (Up.X + Down.X) / 2; }
        public double nextShot(int runID, double asymmetry, string prefix, out double correction) // return phaseCorr - correct Raman phase (0 if not PID)
        {
            
            double phaseCorr = 0; double disbalance = 0;
            if (LogPID) LogEvent("Down: " + Down.X.ToString(dPrec) + " ; " + Down.Y.ToString(dPrec) + "; Up: " + Up.X.ToString(dPrec) + " ; " + Up.Y.ToString(dPrec));
            if ((runID % 2) == 0) { Down.Y = asymmetry; }
            else { Up.Y = asymmetry; }
            disbalance = Down.Y - Up.Y; // absolute
            disbalCorr = (disbalance / fringeScale) / 2; // corrected disbalance for fringeScale (vert.)
            Color clr1 = Brushes.Black.Color;
            if (Math.Abs(disbalCorr) > 0.8) clr1 = Brushes.Red.Color;
            double corr = 0;
            if (PID_Enabled)
            {
                corr = PID(disbalCorr);// / numScale.Value //+disbalCorr; // in rad       
            }        
            if ((runID % 2) == 0) //MMDataConverter.Restrict2twoPI(
            {
                Down.X = Down.X + corr; phaseCorr = Down.X;
                LogEvent("new Down."+prefix+": "+ phaseCorr.ToString(dPrec));
            }
            else
            {
                Up.X = Up.X + corr; phaseCorr = Up.X;
                LogEvent("new Up." + prefix + ": " + phaseCorr.ToString(dPrec));
            }                 
            correction = corr;
            return phaseCorr;
        }

        public MMexec backMME(int runID, double asymmetry, string prefix)
        {
            MMexec mmeOut = new MMexec("", "Axel-hub", "phaseAdjust");
            mmeOut.prms.Clear();
            mmeOut.prms["runID"] = runID;
            double correction = 0;
            double newPhase = nextShot(runID, asymmetry, prefix, out correction);
            mmeOut.prms["phase." + prefix] = newPhase.ToString("G6");
            mmeOut.prms["corr." + prefix] = correction.ToString("G6");
            return mmeOut;
        }

        int iStDepth = 5; int dStDepth = 3;
        public double PID(double disbalance)
        {
            double pTerm = disbalance;
            iStack.Add(disbalance); while (iStack.Count > iStDepth) iStack.RemoveAt(0);
            double iTerm = iStack.Average();
            dStack.Add(disbalance); while (dStack.Count > dStDepth) dStack.RemoveAt(0);
            double dTerm = 0;
            for (int i = 0; i < dStack.Count - 1; i++)
            {
                dTerm += dStack[i + 1] - dStack[i];
            }
            dTerm /= Math.Max(dStack.Count - 1, 1);

            double cr = kP * pTerm + kI * iTerm + kD * dTerm;
            if (LogPID) LogEvent("PID> " + pTerm.ToString("G3") + "  " + iTerm.ToString("G3") + " " + dTerm.ToString("G3") +
                // PID X correction and Y value after the correction
                " corr " + cr.ToString("G4") + " for " + disbalance.ToString("G4"), Brushes.Navy.Color);
            return cr;
        }
    }
}
