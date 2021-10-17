using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using NationalInstruments.Controls;
using Newtonsoft.Json;
using UtilsNS;

namespace Axel_probe
{
    /// <summary>
    /// The current mode (group command) of incomming shots
    /// </summary>
    public enum RemoteMode
    {
        Disconnected,
        Jumbo_Scan, // scan as part of Jumbo Run
        Jumbo_Repeat, // repeat as part of Jumbo Run
        Simple_Scan, // scan initiated by MM
        Simple_Repeat, // repeat initiated by MM
        Ready_To_Remote // Connected and standby       
    }

    /// <summary>
    /// An calculating engine for generating a fringes patterns according simulated experimental conditions
    /// the engine runs by its timer and behaives similar to Axel-tilt
    /// its state evolve according to the set parameters (accel.; disturbances) 
    /// and axel-probe queries the engine for its state
    /// OnChange event fires when the state is recalculated
    /// </summary>
    public class ProbeEngine
    {
        /// <summary>
        /// behaviour patterns parameters
        /// pattern => 0 -> constant; 1 -> trapeze; 2 -> sine
        /// ampl [mg] - accel. amplitude
        /// factor [mg/rad]
        /// step [%] percent from the pattern period
        /// time gap 
        /// </summary>
        public Dictionary<string, double> bpps; 

        /// <summary>
        /// disturbances - fringe noise and Y breathing simulating experimental conditions
        /// Xnoise - [mg] noise in accel. (fringe shifting)
        /// Ynoise - [%] Y noise in fringe signal 
        /// XnoiseIO - (bool) Xnoise switch
        /// YnoiseIO - (bool) Ynoise switch
        /// BrthPattern - (int) Breathing pattern
        /// BrthApmpl - Breathing amplitude
        /// BrthPeriod - Breathing period
        /// BrthIO - (bool) Breathing switch
        /// </summary>
        public Dictionary<string, object> dps;  

        /// <summary>
        /// Axis mode
        /// -1 - old style; 0 - X; 1 - Y; 2 - X/Y
        /// </summary>
        public int axis { get; set; }  

        public Boolean LockParams = false;
        /// <summary>
        /// Package bpps & dps in one struct for file operations
        /// </summary>
        struct Params 
        {
            public Dictionary<string, double> bp;
            public Dictionary<string, object> dp;
        }

        /// <summary>
        /// Save Params in json file (params.cfg)
        /// </summary>
        public void SaveParams()
        {
            Params prms;
            prms.bp = new Dictionary<string, double>(bpps);
            prms.dp = new Dictionary<string, object>(dps);
            string json = JsonConvert.SerializeObject(prms);
            File.WriteAllText(Utils.configPath+"params.cfg", json);
        }

        /// <summary>
        /// Load Params from json file (params.cfg)
        /// </summary>
        public void LoadParams()
        {
            string json = File.ReadAllText(Utils.configPath + "params.cfg");
            Params prms = JsonConvert.DeserializeObject<Params>(json);
            bpps = new Dictionary<string, double>(prms.bp);
            dps = new Dictionary<string, object>(prms.dp);            
        }

        public RemoteMessaging remote = null;

        public ChartCollection<Point> srsFringes = null;
        public ListBox lboxNB = null;
        public ChartCollection<double> srsSignalN = null;
        public ChartCollection<double> srsSignalB = null;

        string remoteDoubleFormat = "G6";
        Random rnd = new Random();
        public RemoteMode remoteMode = RemoteMode.Disconnected; // initial state

        /// <summary>
        /// Time period of generated  
        /// </summary>
        public double period // one cicle [sec]
        {
            get { return dTimer.Interval.TotalSeconds * 100.0 / bpps["step"]; }
        }

        private Boolean _Enabled = false;
        /// <summary>
        /// One way to start/stop a simulation
        /// </summary>
        public Boolean Enabled
        {
            get { return _Enabled; }
            set 
            {
                if (value) Start();
                else Stop();
            }
        }

        double Remainder(double a, double b) // dupllicate of (a % b)
        {
            return a - Math.Floor(a / b) * b;
        }

        DispatcherTimer dTimer; 
        public Stopwatch stopWatch;
        /// <summary>
        /// Class constructor
        /// </summary>
        public ProbeEngine()
        {
            bpps = new Dictionary<string, double>();
            dps = new Dictionary<string, object>();

            axis = -1;

            srsFringes = new ChartCollection<Point>();            
            srsSignalN = new ChartCollection<double>();            
            srsSignalB = new ChartCollection<double>();           
            
            dTimer = new DispatcherTimer(DispatcherPriority.Send);
            dTimer.Interval = new TimeSpan(300 * 10000);
            dTimer.Tick += new EventHandler(dTimer_Tick);
            stopWatch = new Stopwatch();
        }

        /// <summary>
        /// Start simulation
        /// </summary>
        /// <param name="dur"></param>
        public void Start(int dur = 500) // [ms]
        {
            _Enabled = true; 
            dTimer.Interval = new TimeSpan(dur * 10000);
            dTimer.Start();
            stopWatch.Restart();
        }

        /// <summary>
        /// Stop simulation
        /// </summary>
        public void Stop()
        {
            _Enabled = false;
            dTimer.Stop();
            stopWatch.Stop();
        }

        private Boolean _Pause = false;
        /// <summary>
        /// Temporary pause the simulation
        /// </summary>
        public Boolean Pause
        {
            get { return _Pause; }
            set 
            {
                if (!Enabled) return;
                _Pause = value;
                if (value) stopWatch.Stop();
                else stopWatch.Start();
            }
        }
        private Boolean _pauseSingle = false;
        /// <summary>
        /// While in pause mode, shoot a single shot by calling this
        /// </summary>
        public void pauseSingle()
        {
            if (!Pause) return;
            _pauseSingle = true;
            stopWatch.Start();
        }

        public delegate void ChangeHandler(Point newAccel);
        public event ChangeHandler OnChange;
        Boolean busy = false;
        protected void ChangeEvent(Point newAccel)
        {
            if (busy) return;
            busy = true;
            if (OnChange != null) OnChange(newAccel);
            busy = false;
        }

        public delegate void LogHandler(string txt, SolidColorBrush clr = null); // ...and general message up; commands with @ 
        public event LogHandler OnLog;
        public void LogEvent(string txt, SolidColorBrush clr = null)
        {
            if (OnLog != null) OnLog(txt, clr);
        }

        Random rand = new Random();
        /// <summary>
        /// New acceleration point with optional noise by X and Y
        /// </summary>
        /// <returns></returns>
        public Point Gauss()
        {
            Point g = new Point(0, 0);
            if (Convert.ToBoolean( dps["XnoiseIO"]))
            {
                g.X = Utils.Gauss01() * Convert.ToDouble(dps["Xnoise"]);
            }
            if (Convert.ToBoolean(dps["YnoiseIO"]))
            {
                g.Y = Utils.Gauss01() * Convert.ToDouble(dps["Ynoise"]) / 100;
            }
            return g;
        }
        /// <summary>
        /// Percent or sec within current period
        /// </summary>
        /// <param name="percent"></param>
        /// <returns></returns>
        double inPeriod(bool percent = true) 
        {
            if (!stopWatch.IsRunning) return Double.NaN;
            double r = Remainder(stopWatch.Elapsed.TotalSeconds,period);
            if (percent) return 100 * (r / period);
            return r;
        }
        public double accelV(double accelMg) // in mg; out Volts
        {
            double cK0 = 486.0; double cK1 = 1.295046; double rAccel = 6000; // example values
            return ((accelMg - cK0 / 1000) * rAccel) / (1e6 * cK1);
        }

        /// <summary>
        /// Generates specific pattern with set params
        /// </summary>
        /// <param name="pos">pos in current pattern [sec]</param>
        /// <returns></returns>
        public double acceleration(double pos)  
        {
            double halfPrd = 0.5 * period; 
            double rng = bpps["ampl"];
            double slope = rng / halfPrd;
            double drift = 0;
            switch ((int)bpps["pattern"])
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
                    double inPos = pos / period;
                    drift = rng * Math.Sin(2 * Math.PI * inPos);
                    break;
            }
            return drift; // ampl [mg]
        }

        /// <summary>
        /// Simulates signal loosing contrast (various reassons)
        /// factor to multiply the fringes with
        /// </summary>
        /// <returns></returns>
        public double breathing() // 
        {
            if (Convert.ToBoolean(dps["BrthIO"]))
            {
                double x = Remainder(stopWatch.Elapsed.TotalSeconds, Convert.ToDouble(dps["BrthPeriod"])) / Convert.ToDouble(dps["BrthPeriod"]); // 0-1
                return 1 - (Convert.ToDouble(dps["BrthApmpl"]) / 100) * Math.Sin(x * 2 * Math.PI);
            }
            else return 1;
        }

        /// <summary>
        /// Individual for each point from the fringe
        /// </summary>
        /// <param name="scanPhase">[rad]</param>
        /// <param name="curAccel">[mg]</param>
        /// <returns>result point</returns>
        public Point fringesPoint(double scanPhase, double curAccel)  
        {
            Point g = Gauss(); 
            double brt = breathing();
            return new Point(scanPhase, (Math.Cos(scanPhase + curAccel / bpps["factor"]) + g.Y) * brt);
        }

        /// <summary>
        /// (re)Generates the fringes series
        /// </summary>
        /// <param name="curAccel">curAccel [mg] for illustration</param>
        /// <returns>accel with noise</returns>
        public double fringes(double curAccel) 
        {
            if (Utils.isNull(srsFringes)) return 0;
            int np = 500; // number of points
            double step = 4 * Math.PI / np;
            double cr = 0; Point g0 = Gauss(); // common for the whole fringe
            srsFringes.Clear();
            while (cr < 4 * Math.PI) // generate fringes !
            {
                srsFringes.Add(fringesPoint(cr, curAccel + g0.X));
                cr += step;
            }
            return curAccel + g0.X;
        }

        /// <summary>
        /// Get index from phase in fringes series
        /// </summary>
        /// <param name="x">[rad]</param>
        /// <returns></returns>
        public int getIfringe(double x) // 
        {
            for (int i = 0; i < srsFringes.Count - 1; i++)
            {
                if (Utils.InRange(x, srsFringes[i].X, srsFringes[i + 1].X)) return i;
            }
            return -1;
        } 
        public double contrPhase = -11; public int b4ConstrID = 0;
        /// <summary>
        /// Generates the simulated to photo diode signal to be send to Axel-hub 
        /// </summary>
        /// <example>
        /// Example base data + some noise
        /// N2	Ntot	B2	Btot	Bg		N2	Ntot	B2	Btot	Bg
        /// 1	5	    1	3	    0		3	5	    1	3	    0
        ///  NB2	NBtot					    NB2	NBtot
        ///  0	2					        2	2	
        /// A						        A	
        /// 1						        -1	
        /// </example>
        /// <param name="curAccel"></param>
        /// <returns></returns>
        public bool SingleShot(double A, // A = 1 .. -1
                 int toAxis, ref MMexec mme )// group template
              
        {
            //log("A = " + A.ToString("G5"));
            List<string> rsltList = new List<string>();
            bool rslt = false;
            double ntot = 5; double b2 = 1; double btot = 3; double bg = 0;
            double n2 = (1 - A) * (ntot - btot) / 2 + b2;   // n2 = 1 .. 3 / A = 1 .. -1              

            lboxNB.Items[1] = "NTot = " + ntot.ToString(remoteDoubleFormat);
            lboxNB.Items[2] = "B2 = " + b2.ToString(remoteDoubleFormat);
            lboxNB.Items[3] = "BTot = " + btot.ToString(remoteDoubleFormat);
            lboxNB.Items[4] = "Bg = " + bg.ToString(remoteDoubleFormat);

            double d, scl = 100;
            List<Double> srsN2 = new List<Double>();
            List<Double> srsNTot = new List<Double>();
            List<Double> srsB2 = new List<Double>();
            List<Double> srsBTot = new List<Double>();
            List<Double> srsBg = new List<Double>();
            srsSignalN.Clear(); srsN2.Clear(); srsNTot.Clear();
            srsSignalB.Clear(); srsB2.Clear(); srsBTot.Clear(); srsBg.Clear();
            for (int i = 0; i < 100; i++)
            {
                d = n2 + Utils.Gauss01() / scl;
                lboxNB.Items[0] = "N2 = " + d.ToString(remoteDoubleFormat);
                srsN2.Add(Utils.formatDouble(d, remoteDoubleFormat));
                d = ntot + Utils.Gauss01() / scl;
                srsNTot.Add(Utils.formatDouble(d, remoteDoubleFormat));

                d = b2 + Utils.Gauss01() / scl;
                srsB2.Add(Utils.formatDouble(d, remoteDoubleFormat));
                d = btot + Utils.Gauss01() / scl;
                srsBTot.Add(Utils.formatDouble(d, remoteDoubleFormat));

                d = bg + Utils.Gauss01() / scl;
                srsBg.Add(Utils.formatDouble(d, remoteDoubleFormat));
            }
            srsSignalN.Append(srsN2); srsSignalN.Append(srsNTot);
            srsSignalB.Append(srsB2); srsSignalB.Append(srsBTot);

            if (remote.Connected)
            {
                mme.prms["N2"] = srsN2.ToArray(); mme.prms["NTot"] = srsNTot.ToArray();
                mme.prms["B2"] = srsB2.ToArray(); mme.prms["BTot"] = srsBTot.ToArray();
                mme.prms["Bg"] = srsBg.ToArray();
                mme.id = rnd.Next(int.MaxValue);
                string msg = "";

                if (toAxis == 2) // both axes
                {
                    mme.cmd = "shot.X";
                    msg = JsonConvert.SerializeObject(mme);
                    rslt = remote.sendCommand(msg);

                    mme.cmd = "shot.Y";
                    msg = JsonConvert.SerializeObject(mme);
                    rslt &= remote.sendCommand(msg);
                }
                else // signle axis
                {
                    switch (toAxis)
                    {
                        case -1: mme.cmd = "shotData";
                            break;
                        case 0: mme.cmd = "shot.X";
                            break;
                        case 1: mme.cmd = "shot.Y";
                            break;
                    }
                    msg = JsonConvert.SerializeObject(mme);
                    rslt = remote.sendCommand(msg);
                }
            }
            return rslt;
        }

        private Boolean cancelScan = false;
        public void CancelScan() { cancelScan = true; }
        /// <summary>
        /// Generates scan series of shots by group MMexec envelope
        /// </summary>
        /// <param name="mms">group MMexec</param>
        public void DoScan(MMscan mms) 
        {
            MMexec md = new MMexec();
            md.mmexec = "";
            md.cmd = "shotData";
            md.sender = "Axel-probe";
            md.prms["groupID"] = mms.groupID;
            md.prms["runID"] = 0;

            double n2 = 1; double ntot = 5; // n2 = 1 .. 3 / A = 1 .. -1              
            double b2 = 1; double btot = 3; double bg = 0;

            double A = 0; srsFringes.Clear(); cancelScan = false; int idx = 0;
            for (double ph = mms.sFrom; ph < mms.sTo + 0.01 * mms.sBy; ph += mms.sBy)
            {
                if (bpps.ContainsKey("TimeGap")) Thread.Sleep((int)bpps["TimeGap"]);
                Utils.DoEvents();
                double noise = 0;
                if (Convert.ToBoolean(dps["YnoiseIO"]))
                {
                    noise = Utils.Gauss01() * Convert.ToDouble(dps["Ynoise"]) / 100;
                }
                n2 = - Math.Cos(ph) + 2 + noise;  // n2 = 1 .. 3
                A = ((ntot - btot) - 2 * (n2 - b2)) / (ntot - btot);
                srsFringes.Add(new Point(ph, A));

                LogEvent(" Ph/Amp= " + ph.ToString("G4") + " / " + A.ToString("G5"), Brushes.DarkGreen);
                if (Utils.InRange(ph, mms.sTo - 0.9 * mms.sBy, mms.sTo + 0.9 * mms.sBy) || cancelScan)
                {
                    md.prms["last"] = 1;
                }
                md.prms["runID"] = idx;
                if (!SingleShot(A, axis, ref md) || cancelScan) break;
                idx++;
            }
        }

        private Boolean cancelRepeat = false;
        public void CancelRepeat() { cancelRepeat = true; }
        /// <summary>
        /// Generate simple (initialted from here) repeat series of shots
        /// </summary>
        /// <param name="cycles">Number of shots (-1 if continious)</param>
        /// <param name="groupID"></param>
        public void SimpleRepeat(int cycles, string groupID) 
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
            if (cycles == -1) realCycles = int.MaxValue;

            double A = 0, frAmpl = 0, frAmplI = 0, drift = 0, pos0, pos1 = 0, pos2 = 0;
            cancelRepeat = false;
            int i = -1; srsFringes.Clear();
            for (long j = 0; j < realCycles; j++)
            {
                if (bpps.ContainsKey("TimeGap")) Thread.Sleep((int)bpps["TimeGap"]);
                Utils.DoEvents();
                if ((j == (realCycles - 1)) || cancelRepeat)
                {
                    md.prms["last"] = 1;
                }
                n2 = 1 + rnd.Next(200) / 100.0; // random from 1 to 3
                A = ((ntot - btot) - 2 * (n2 - b2)) / (ntot - btot);
                srsFringes.Add(new Point(j, A));
                //drift = calcAtPos(jumbo, A, (j % 2) == 1);
                md.prms["runID"] = j; frAmpl = A;
                LogEvent(" #/A= " + j.ToString() + " / " + A.ToString("G5"), Brushes.Navy);
                if (!SingleShot(frAmpl, axis, ref md)) break;

                if (cancelRepeat) break; 
            }
        }
       
        /// <summary>
        /// Advertise (event) the new acceleration value
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dTimer_Tick(object sender, EventArgs e)
        {
            if (Pause && !_pauseSingle) return;

            ChangeEvent(new Point(inPeriod(true), acceleration(inPeriod(false))));
            fringes(acceleration(inPeriod(false)));
            // Forcing the CommandManager to raise the RequerySuggested event
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();

            if (_pauseSingle)
            {
                _pauseSingle = false;
                stopWatch.Stop();
            }
        }
    }
}
