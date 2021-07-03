using System;
using System.Windows;
using System.Windows.Media;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NationalInstruments.Controls;
using OptionsNS;
using UtilsNS;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace Axel_hub
{
    /// <summary>
    /// Intermediator between incomming data flow from ucScan user component and AxelAxis user components
    /// - encapsulate not-axis-specific objects (e.g axelMems) and operations (e.g. DoAcquire)
    /// </summary>
    public class AxelAxesClass : List<AxelAxisClass>
    {      
        public AxelMems axelMems = null;
        private AxelMemsTemperature axelMemsTemperature = null;
        private scanClass ucScan;
        public MMexec mm2status = null;

        Random rnd = new Random();

        private int _rCount = 1;
        /// <summary>
        /// number of real (active) Axes
        /// </summary>
        public int rCount 
        {
            get { return _rCount;  }
            set 
            {
                if (memsRunning && (!_rCount.Equals(value))) 
                {
                    Utils.TimedMessageBox("Mems is running, switch it off to change number of axes.");
                    return;
                }                  
                _rCount = Utils.EnsureRange(value, 1, Count); 
            }
        }

        /// <summary>
        /// Get an index from a prefix (X/Y)
        /// </summary>
        /// <param name="prf"></param>
        /// <returns></returns>
        public int prfIdx(string prf)
        {
            for (int i = 0; i < rCount; i++)
                if (this[i].prefix.Equals(prf))
                {
                    return i;
                }
            return -1;
        }

        /// <summary>
        /// Mask running for the active axelChart 
        /// </summary>
        public bool memsRunning
        {
            get 
            { 
                bool r = true;
                for (int i = 0; i < rCount; i++) r &= this[i].axelChart.Running; // all must be true
                return r;
            }
            set
            {
                for (int i = 0; i < rCount; i++) this[i].axelChart.Running = value;
            }
        }

        /// <summary>
        /// Clear and initialize visuals according to the switches
        /// </summary>
        /// <param name="Top">top panel</param>
        /// <param name="Middle">middle panel</param>
        /// <param name="Bottom">bottom panel</param>
        public void Clear(bool Top = true, bool Middle = true, bool Bottom = true)
        {
            for (int i = 0; i < rCount; i++) this[i].Clear(Top, Middle, Bottom);
        }

        private GeneralOptions genOptions;

        /// <summary>
        /// Class constructor
        /// </summary>
        /// <param name="_genOptions">general for the app options</param>
        /// <param name="_ucScan">the scan user user component ref</param>
        public AxelAxesClass(ref GeneralOptions _genOptions, ref scanClass _ucScan)
        {
            genOptions = _genOptions;
            genOptions.OnChange += new GeneralOptions.ChangeHandler(OnOptionsChange);
            ucScan = _ucScan;
            axelMems = new AxelMems(genOptions.MemsHw, "", false);
            axelMems.OnAcquire += new AxelMems.AcquireHandler(DoAcquire);

            axelMemsTemperature = new AxelMemsTemperature(genOptions.TemperatureHw);
        }

        /// <summary>
        /// The correct way to introduce new axis
        /// </summary>
        /// <param name="AxelAxis"></param>
        /// <param name="prefix">X or Y</param>
        public void AddAxis(ref AxelAxisClass AxelAxis, string prefix)
        {
            AxelAxis.Init(prefix, ref genOptions, ref ucScan.scanModes, ref axelMems);
            Add(AxelAxis);
            this[Count - 1].OnLog += new AxelAxisClass.LogHandler(LogEvent);
            this[Count - 1].strobes.OnLog += new strobesUC.LogHandler(LogEvent);
            this[Count - 1].OnSend += new AxelAxisClass.SendHandler(ucScan.SendJson);
            this[Count - 1].SendMMexecEvent += new AxelAxisClass.SendMMexecHandler(SendMMexec);
            this[Count - 1].OptimUC1.SendMMexecEvent += new PanelsUC.OptimUC_Class.SendMMexecHandler(SendMMexec);
        }
        /// <summary>
        /// Get an axis by prefix
        /// </summary>
        /// <param name="prefix"></param>
        /// <returns></returns>
        public AxelAxisClass byName(char prefix)
        {
            AxelAxisClass rslt = null;
            foreach (AxelAxisClass aa in this)
            {
                if (aa.prefix.Equals(prefix)) 
                {
                    rslt = aa; break;
                }
            }
            return rslt;
        }

        /// <summary>
        /// When the options change, make everybody knows
        /// </summary>
        /// <param name="activeComm"></param>
        /// 

        public void OnOptionsChange(GeneralOptions opts)
        {           
            if (genOptions.AxesChannels == 0) rCount = 1;
            else rCount = 2;
            bool conn = Utils.isNull(ucScan.remote) ? false : (ucScan.remote.Enabled && ucScan.remote.Connected);
            for (int i = 0; i < rCount; i++)
            {
                this[i].UpdateFromOptions(conn);
            }
        }
        /// <summary>
        /// The correct way to log a text on the text-box on the left
        /// </summary>
        /// <param name="txt"></param>
        /// <param name="clr"></param>
        public delegate void LogHandler(string txt, SolidColorBrush clr = null);
        public event LogHandler OnLog;
        protected void LogEvent(string txt, SolidColorBrush clr = null)
        {
            if (OnLog != null) OnLog(txt, clr);
        }
        public void set2chartActive(bool start, double samplingPeriod)
        {
            for (int i = 0; i < rCount; i++)
            {
                this[i].axelChart.set2active(start, (samplingPeriod > 0) ? samplingPeriod : 1/200000.0);
            }
        }

        /// <summary>
        /// Initialize ADC24 for a new measurement
        /// </summary>
        /// <param name="down"></param>
        /// <param name="samplingPeriod"></param>
        /// <param name="InnerBufferSize"></param>
        public void set2startADC24(bool down, double samplingPeriod, int InnerBufferSize)
        {
            for (int i = 0; i < rCount; i++)
            {
                this[i].axelChart.set2startADC24(down, samplingPeriod, InnerBufferSize);
            }
        }

        /// <summary>
        /// Save the visual options 
        /// </summary>
        public void SaveDefaultModes()
        {
            foreach (AxelAxisClass aa in this)
                aa.SaveDefaultModes();
        }

        /// <summary>
        /// Start new measurement with ADC24 
        /// </summary>
        /// <param name="down"></param>
        /// <param name="period"></param>
        /// <param name="InnerBufferSize"></param>
        public void startADC(bool down, double period, int InnerBufferSize)
        {
            double SamplingPeriod = 1 / MEMSmodel.RealConvRate(1 / period);
            set2startADC24(down, SamplingPeriod, InnerBufferSize); 
            if (!down) // user cancel
            {
                axelMems.StopAcquisition();
                if (!Utils.isNull(axelMemsTemperature))
                    if (genOptions.TemperatureEnabled && axelMemsTemperature.isDevicePlugged())
                        axelMemsTemperature.StopAcquisition();
                theTime.stopTime();
                return;
            }
            else LogEvent("Start MEMS series...", Brushes.Teal);
            //nSamples = InnerBufferSize;

            for (int i = 0; i < rCount; i++)
                if (this[i].axelChart.Waveform.TimeSeriesMode)
                {
                    axelMems.TimingMode = AxelMems.TimingModes.byStopwatch;
                }
                else
                {
                    axelMems.TimingMode = AxelMems.TimingModes.byNone;
                }

            if ((genOptions.AxesChannels == 0) && !Utils.isSingleChannelMachine) axelMems.activeChannel = 0;
            else axelMems.activeChannel = 2;
            
            if ((ucScan.remoteMode == RemoteMode.Jumbo_Repeat) || (ucScan.remoteMode == RemoteMode.Simple_Repeat)
                || (ucScan.remoteMode == RemoteMode.Disconnected))
            {
                theTime.startTime();
            }               
            axelMems.StartAcquisition(InnerBufferSize, 1 / SamplingPeriod);    // async acquisition  
            if (!Utils.isNull(axelMemsTemperature))
                if (genOptions.TemperatureEnabled && axelMemsTemperature.isDevicePlugged())
                    axelMemsTemperature.StartAcquisition();
        }

        /// <summary>
        /// Get the acquisition buffer and split the data to axes
        /// </summary>
        /// <param name="dt"></param>
        /// <param name="next"></param>
        public void DoAcquire(List<Point> dt, out bool next)
        {
            next = ucScan.EndlessMode() && memsRunning && (axelMems.activeChannel.Equals(0) || axelMems.activeChannel.Equals(2)) &&
                (ucScan.Running || (ucScan.remoteMode == RemoteMode.Simple_Repeat) || (ucScan.remoteMode == RemoteMode.Jumbo_Repeat));
            bool isCombineQuantMems = genOptions.memsInJumbo.Equals(GeneralOptions.MemsInJumbo.USB9251) && (ucScan.remoteMode == RemoteMode.Jumbo_Repeat); 

            if (!next)
            {
                for (int i = 0; i < rCount; i++) this[i].axelChart.Running = false;
                ucScan.Running = false;
                axelMems.StopAcquisition();
                for (int i = 0; i < rCount; i++) this[i].axelChart.Refresh();
                return;
            }
            if (genOptions.TemperatureEnabled)
            {
                if (!axelMemsTemperature.isDevicePlugged()) 
                    LogEvent("No temperature device connected !", Brushes.Red);
                else
                {
                    double[] temper = axelMemsTemperature.TakeTheTemperature();
                    this[0].axelChart.memsTemperature = temper[0]; this[1].axelChart.memsTemperature = temper[1];
                }
            } 
            switch (axelMems.activeChannel)
            {
                case 0:
                    this[0].axelChart.SetIncomingBufferSize(dt.Count);
                    this[0].axelChart.Waveform.AddRange(dt);
                    if (isCombineQuantMems)
                        this[0].CombineQuantMems(dt, genOptions.Mems2SignLen / 1000.0);
                    break;
                case 1: Utils.TimedMessageBox("Y channel mode is not implemented", "Error", 2500);                    
                    break;
                case 2:
                    List<Point> ds = new List<Point>(); List<Point> ds2 = new List<Point>();
                    for (int i = 0; i < dt.Count; i++) // split channels
                    {
                        if ((i % 2) == 0) ds.Add(dt[i]);  // channel 0
                        else ds2.Add(dt[i]);              // channel 1
                    }                
                    this[0].axelChart.SetIncomingBufferSize(ds.Count);
                    if (!Utils.isSingleChannelMachine)
                        this[1].axelChart.SetIncomingBufferSize(ds2.Count);
                    
                    this[0].axelChart.Waveform.AddRange(ds);
                    if (!Utils.isSingleChannelMachine) 
                        this[1].axelChart.Waveform.AddRange(ds2);
                    if (isCombineQuantMems)
                    {
                        this[0].CombineQuantMems(ds, genOptions.Mems2SignLen / 1000.0);
                        if (!Utils.isSingleChannelMachine) 
                            this[1].CombineQuantMems(ds2, genOptions.Mems2SignLen / 1000.0); 
                    }
                    break;
            }           
        }

        /// <summary>
        /// It should be in very limited (ideally none) use
        /// </summary>
        private bool probeMode // simulation of MM2 with AxelProbe
        {
            get
            {
                if (Utils.isNull(ucScan.remote)) return false;
                return ucScan.remote.partner.Equals("Axel Probe") && ucScan.remote.Connected;
            }
        }

        private MMexec lastGrpExe;  
        /// <summary>
        /// The main MOT data getting method
        /// format shot.X / shot.Y OR shotData. IMPORTANT for Jumbo-repeat only .X/.Y 
        /// if .X / .Y runID's are independant for each axis
        /// </summary>
        /// <param name="json">Whatever is comming, it must be formatted according to "Book of JaSON"</param>
        public void DoRemote(string json)
        {
            MMexec mme = JsonConvert.DeserializeObject<MMexec>(json);
            string pref = ""; int runID = -1;
            if (mme.prms.ContainsKey("runID")) runID = Convert.ToInt32(mme.prms["runID"]);
            if (mme.cmd.Substring(0, 4).Equals("shot"))
            {               
                if ((mme.cmd.Equals("shot.X")) || (mme.cmd.Equals("shot.Y"))) 
                {
                    pref = mme.cmd.Substring(mme.cmd.Length-1);
                }
                if (mme.cmd.Equals("shotData")) // backward compatibility
                {
                    if ((runID % 2) == 0) pref = "X";
                    else pref = "Y";
                }
                mme.cmd = "shot";
                if (mme.prms.ContainsKey("iTime") && (runID == 0) && (Convert.ToInt64(mme.prms["iTime"]) > 0)) theTime.startSeqSeries = Convert.ToInt64(mme.prms["iTime"]);
            }
            switch (mme.cmd)
            {
                case ("shot"): // incomming MM2/probe data
                    {
                        if (ucScan.remoteMode == RemoteMode.Ready_To_Remote) return;
                        if (Utils.isNull(lastGrpExe))
                        {
                            if (MessageBox.Show("Abort sequence?", "Question", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                            {
                                ucScan.Abort(true);
                            }
                            throw new Exception("Wrong sequence of MMexec's");
                        }
                        int idx = prfIdx(pref); if (idx == -1) return;
                        this[idx].DoShot(mme, lastGrpExe); // current, group frame
                        int li = (rCount == 2) ? 1 : 0;
                        if (mme.prms.ContainsKey("last") && (li == idx)) // after the last axis
                        {
                            if (Convert.ToInt32(mme.prms["last"]) == 1)
                            {
                                LogEvent(">LAST SHOT", Brushes.DarkRed);
                                if ((ucScan.remoteMode == RemoteMode.Jumbo_Repeat) ||
                                    ((ucScan.remoteMode == RemoteMode.Jumbo_Scan) && !genOptions.JumboRepeat))
                                {
                                    ucScan.Abort(false);
                                    lastGrpExe.Clear();
                                }
                                if (ucScan.remoteMode == RemoteMode.Simple_Scan || ucScan.remoteMode == RemoteMode.Simple_Repeat)
                                {
                                    ucScan.Running = false;
                                    startADC(false, 1.0 / 2133, 200);
                                }
                                ucScan.remoteMode = RemoteMode.Ready_To_Remote;
                            }
                        }
                    }
                    break;
                case ("repeat"):
                    {
                        LogEvent(json, Brushes.Blue);
                        lastGrpExe = mme.Clone();
                        if (!mme.getWhichSender().Equals(MMexec.SenderType.AxelHub)) ucScan.remoteMode = RemoteMode.Simple_Repeat;
                        if (ucScan.remoteMode != RemoteMode.Simple_Repeat) 
                        {
                            switch (genOptions.memsInJumbo)
                            {
                                case GeneralOptions.MemsInJumbo.USB9251:
                                    ucScan.SetActivity("MEMS data acquisition");
                                    ucScan.Running = true;
                                    axelMems.Reset(); 
                                    startADC(true, 1/ucScan.GetSamplingFreq(true), ucScan.GetBufferSize());
                                    break;
                                case GeneralOptions.MemsInJumbo.PXI4462:
                                    ucScan.SetActivity("MEMS data from MM2");
                                    int sr = (mme.prms.ContainsKey("samplingRate")) ? Convert.ToInt32(mme.prms["samplingRate"]) : 200000;
                                    set2chartActive(true, sr);
                                    break;
                            }                               
                        }
                        else
                        {
                            ucScan.SetActivity("");
                        }
                        Clear(!genOptions.memsInJumbo.Equals(GeneralOptions.MemsInJumbo.None), true, true);
                        for (int i = 0; i < rCount; i++) this[i].DoPrepare(mme);                       
                    }
                    break;
                case ("scan"):
                    {
                        LogEvent(json, Brushes.DarkGreen);
                        lastGrpExe = mme.Clone();
                        MMscan lastScan = new MMscan();
                        if (!lastScan.FromDictionary(mme.prms))
                        {
                            LogEvent("Error in incomming json", Brushes.Red);
                            ucScan.Abort(true);
                            return;
                        }
                        if (!mme.getWhichSender().Equals(MMexec.SenderType.AxelHub)) ucScan.remoteMode = RemoteMode.Simple_Scan;
                        Clear();
                        for (int i = 0; i < rCount; i++) this[i].DoPrepare(mme);
                    }
                    break;
                case "status":
                    //mm2status = mme.Clone();
                    for (int i = 0; i < rCount; i++) this[i].mm2status = mme.Clone();
                    break;
                case ("message"):
                    {
                        string txt = (string)mme.prms["text"];
                        int errCode = -1;
                        if (mme.prms.ContainsKey("error")) errCode = Convert.ToInt32(mme.prms["error"]);
                        if (txt.Contains("Error") || (errCode > -1))
                        {
                            LogEvent("!!! " + txt, Brushes.Red);
                            if (errCode > -1) LogEvent("Error code: " + errCode.ToString(), Brushes.Red);
                        }
                        else LogEvent("! " + txt, Brushes.Coral);
                    }
                    break;
                case ("abort"):
                    {
                        LogEvent(json, Brushes.Red);
                        if (ucScan.remoteMode == RemoteMode.Simple_Scan || ucScan.remoteMode == RemoteMode.Simple_Repeat || 
                            ucScan.remoteMode == RemoteMode.Jumbo_Scan || ucScan.remoteMode == RemoteMode.Jumbo_Repeat)
                        {
                            ucScan.Abort(false);
                        }
                    }
                    break;
            }
        }
        public void SendMMexec(MMexec mme)
        {            
            string json = JsonConvert.SerializeObject(mme);
            ucScan.SendJson(json);
        }

        /// <summary>
        /// When in Jumbo mode Start/Stop the first part of it
        /// </summary>
        /// <param name="down"></param>
        public void DoJumboScan(bool down)
        {
            if (!down) // user jumbo cancel
            {
                for (int i = 0; i < rCount; i++) this[i].axelChart.Running = false;
                if (axelMems.running) axelMems.StopAcquisition();
                LogEvent("Jumbo END !", Brushes.Red);
                return;
            }
            lastGrpExe = new MMexec();
            if (probeMode) lastGrpExe.mmexec = "simulation";
            else lastGrpExe.mmexec = "real_drive";
            lastGrpExe.setWhichSender(MMexec.SenderType.AxelHub);

            if (genOptions.JumboScan)
            {
                Clear();
                for (int i = 0; i < rCount; i++) this[i].tabLowPlots.SelectedIndex = 0; // fringes page
                lastGrpExe.cmd = "scan";
                lastGrpExe.id = rnd.Next(int.MaxValue);
                // take params from X for both
                int iX = prfIdx("X");
                this[iX].lastScan = this[iX].jumboScan();
                lastGrpExe.prms["axis"] = (genOptions.AxesChannels.Equals(0)) ? "1" : "2";
                this[iX].lastScan.ToDictionary(ref lastGrpExe.prms);
                
                string json = JsonConvert.SerializeObject(lastGrpExe);
                LogEvent("<< " + json, Brushes.Green);
                ucScan.remoteMode = RemoteMode.Jumbo_Scan;
                ucScan.SendJson(json);

                for (int i = 0; i < rCount; i++) 
                    this[i].DoPrepare(lastGrpExe);
                if (ucScan.remoteMode == RemoteMode.Ready_To_Remote) return; // end of mission
            }
            else
            {
              /*  if (Utils.isNull(srsFringes)) srsFringes = new DataStack();
                else srsFringes.Clear();
                if (probeMode)
                {
                    GroupBox gb = null; tabLowPlots.SelectedIndex = 0;
                    srsFringes.OpenPair(Utils.configPath+"fringes.ahf", ref gb);
                    graphFringes.DataSource = srsFringes;
                    lbInfoFringes.Content = srsFringes.rem;
                    tbRemFringes.Text = srsFringes.rem;
                    crsDownStrobe.AxisValue = 1.6; crsUpStrobe.AxisValue = 4.8;
                }
                // else btnOpenFringes_Click(null, null); MOVE to ucScan
                if (srsFringes.Count == 0)
                {
                    Utils.TimedMessageBox("No fringes for Jumbo-repeat", "Error", 5000);
                    ucScan.Running = false;
                    return;
                }
            */
            }
        }

        public void SetChartStrobes(bool enabled)
        {
            for (int i = 0; i < rCount; i++) this[i].visStrobes(enabled);
            if (enabled) Utils.TimedMessageBox("Please adjust the strobes and confirm to continue.", "Information");
        }

        /// <summary>
        /// When in Jumbo mode start the second part of it
        /// </summary>
        /// <param name="cycles">set the number of shots or -1 to continues measurement</param>
        public void DoJumboRepeat(bool begin, int cycles) // pnt.X - down; pnt.Y - up
        {
            //Clear(); // visual stuff
            // set jumbo-repeat conditions & format MMexec
            if (begin) theTime.startTime(true);
            else
            {
                theTime.stopTime(); return;
            }
            if (Utils.isNull(lastGrpExe)) lastGrpExe = new MMexec();            
            lastGrpExe.sender = "Axel-hub";
            if (genOptions.Diagnostics) lastGrpExe.mmexec = "diagnostics";
            lastGrpExe.cmd = "repeat"; 
            lastGrpExe.id = rnd.Next(int.MaxValue);
            lastGrpExe.prms.Clear();
            lastGrpExe.prms["groupID"] = Utils.timeName();
            lastGrpExe.prms["cycles"] = cycles;
            lastGrpExe.prms["axis"] = (genOptions.AxesChannels.Equals(0)) ? "1" : "2";
            if (genOptions.followPID && !genOptions.Diagnostics) { lastGrpExe.prms["follow"] = 1; }
            else { lastGrpExe.prms["follow"] = 0; }
            set2chartActive(begin, -1);
            for (int i = 0; i<rCount; i++) // loop axes
            {
                if (genOptions.Diagnostics)
                {
                    this[i].DoPrepare(lastGrpExe);
                }
                else
                {           
                    double dv = Convert.ToDouble(this[i].crsDownStrobe.AxisValue);
                    this[i].strobes.Down.X = Utils.formatDouble(dv,"G4");
                    lastGrpExe.prms["downStrobe." + this[i].prefix] = this[i].strobes.Down.X;
                    int di = -1; int j = 0;
                    while (!Utils.InRange(this[i].srsFringes[j].X, 0.99 * dv, 1.01 * dv) && j < this[i].srsFringes.Count) j++;
                    if (j < this[i].srsFringes.Count)
                    {
                        di = j;
                        this[i].strobes.Down.Y = this[i].srsFringes[di].Y;
                    }
                    double uv = Convert.ToDouble(this[i].crsUpStrobe.AxisValue);
                    this[i].strobes.Up.X = Utils.formatDouble(uv,"G4");                
                    lastGrpExe.prms["upStrobe." + this[i].prefix] = uv;
                    int ui = -1; j = 0;
                    while (!Utils.InRange(this[i].srsFringes[j].X, 0.99 * uv, 1.01 * uv) && j < this[i].srsFringes.Count) j++;
                    if (j < this[i].srsFringes.Count)
                    {
                        ui = j;
                        this[i].strobes.Down.Y = this[i].srsFringes[ui].Y;
                    }
                    double mv = Double.NaN;
                    if (di > -1 && ui > -1)
                    {
                        int mi = (int)((di + ui) / 2 + 0.5);                 
                        mv = this[i].srsFringes[mi].Y;
                    }
                    
                    this[i].DoPrepare(lastGrpExe);             
                    this[i].strobes.OnJumboRepeat(this[i].numKcoeff.Value, this[i].numPhi0.Value, lastGrpExe, mv);                
                }
            }
            string jsonR = JsonConvert.SerializeObject(lastGrpExe);
            LogEvent("<< " + jsonR, Brushes.Blue);
            ucScan.remoteMode = RemoteMode.Jumbo_Repeat;

            // set ADC24 and corr. visuals
            if (genOptions.JumboRepeat && genOptions.memsInJumbo.Equals(GeneralOptions.MemsInJumbo.USB9251))
            {
                if (genOptions.followPID) ucScan.SetActivity("Data acquis. with PID feedback");
                else ucScan.SetActivity("Data acquis.(no PID feedback)");
                if (genOptions.Diagnostics) ucScan.SetActivity("Data acquis.(diagnostics)");
                ucScan.Running = true;
                axelMems.Reset();
                //if (!probeMode) 
                startADC(true, 1/ucScan.GetSamplingFreq(true), ucScan.GetBufferSize());
                for (int i = 0; i < rCount; i++)
                {
                    this[i].axelChart.Waveform.TimeSeriesMode = true;
                    //plotcursorAccel.Visibility = System.Windows.Visibility.Collapsed;
                    this[i].timeStack.Clear();
                    if (genOptions.memsInJumbo.Equals(GeneralOptions.MemsInJumbo.PXI4462)) this[i].axelChart.SetInfo("Remote data source for MEMS");
                    Thread.Sleep(500); Utils.DoEvents();
                }
             }
             ucScan.SendJson(jsonR);
                         
             for (int i = 0; i < rCount; i++)
             {
                 this[i].tabLowPlots.SelectedIndex = 1;
                 this[i].resetQuantList(Utils.dataPath+Utils.timeName());
             }
        }
        /// <summary>
        /// Not destroying anything, just preparing for closing
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void Closing(object sender, System.ComponentModel.CancelEventArgs e)  
        {
            axelMems.StopAcquisition(); Thread.Sleep(200);
            for (int i = 0; i < rCount; i++) this[i].Closing(sender, e);
        }
    }
}
