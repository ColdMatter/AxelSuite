using System;
using System.Windows;
using System.Windows.Media;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OptionsNS;
using UtilsNS;
using AxelHMemsNS;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace Axel_hub
{
    public class AxelAxesClass : List<AxelAxisClass>
    {      
        public AxelMems axelMems = null;
        private AxelMems axelMemsTemperature = null;
        private scanClass ucScan; 

        Random rnd = new Random();

        private int _rCount = 1;
        public int rCount // number of real (active) Axes
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

        public int prfIdx(string prf)
        {
            for (int i = 0; i < rCount; i++)
                if (this[i].prefix.Equals(prf))
                {
                    return i;
                }
            return -1;
        }

        public bool memsRunning
        {
            get 
            { 
                bool r = true;
                for (int i = 0; i < rCount; i++) r &= this[i].axelChart.Running;
                return r;
            }
            set
            {
                for (int i = 0; i < rCount; i++) this[i].axelChart.Running = value;
            }
        }

        public void Clear(bool Top = true, bool Middle = true, bool Bottom = true)
        {
            for (int i = 0; i < rCount; i++) this[i].Clear(Top, Middle, Bottom);
        }

        private GeneralOptions genOptions;
        public AxelAxesClass(ref GeneralOptions _genOptions, ref scanClass _ucScan)
        {
            genOptions = _genOptions;
            ucScan = _ucScan;
            axelMems = new AxelMems(genOptions.MemsHw);
            axelMems.OnAcquire += new AxelMems.AcquireHandler(DoAcquire);
            axelMems.OnRealSampling += new AxelMems.RealSamplingHandler(ucScan.OnRealSampling);

            axelMemsTemperature = new AxelMems(genOptions.TemperatureHw);
            axelMemsTemperature.OnAcquire += new AxelMems.AcquireHandler(DoAcquire2);
        }

        public void AddAxis(ref AxelAxisClass AxelAxis, string prefix)
        {
            AxelAxis.Init(prefix, ref genOptions, ref ucScan.scanModes, ref axelMems);
            Add(AxelAxis);
            this[Count - 1].OnLog += new AxelAxisClass.LogHandler(LogEvent);
            this[Count - 1].strobes.OnLog += new strobeClass.LogHandler(LogEvent);
            this[Count - 1].OnSend += new AxelAxisClass.SendHandler(ucScan.SendJson);
        }

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

        public void UpdateFromOptions(bool activeComm)
        {
            if (genOptions.AxesChannels == 0) rCount = 1;
            else rCount = 2;
            for (int i = 0; i < rCount; i++)
            {
                this[i].UpdateFromOptions(activeComm);
            }
        }

        public delegate void LogHandler(string txt, Color? clr = null);
        public event LogHandler OnLog;
        protected void LogEvent(string txt, Color? clr = null)
        {
            if (OnLog != null) OnLog(txt, clr);
        }

        public void set2startADC24(bool down, double samplingPeriod, int InnerBufferSize)
        {
            for (int i = 0; i < rCount; i++)
            {
                this[i].axelChart.set2startADC24(down, samplingPeriod, InnerBufferSize);
            }
        }

        public void SaveDefaultModes()
        {
            foreach (AxelAxisClass aa in this)
                aa.SaveDefaultModes();
        }

        public void startADC(bool down, double period, int InnerBufferSize)
        {
            double SamplingPeriod = 1 / axelMems.RealConvRate(1 / period);
            set2startADC24(down, SamplingPeriod, InnerBufferSize);
            if (!down) // user cancel
            {
                axelMems.StopAqcuisition();
                if (genOptions.TemperatureEnabled && axelMemsTemperature.isDevicePlugged())
                    axelMemsTemperature.StopAqcuisition();
                LogEvent("End of series!", Brushes.Red.Color);
                return;
            }
            else LogEvent("Start MEMS series...", Brushes.Navy.Color);
            //nSamples = InnerBufferSize;

            for (int i = 0; i < rCount; i++)
                if (this[i].axelChart.Waveform.TimeSeriesMode)
                {
                    axelMems.TimingMode = AxelMems.TimingModes.byStopwatch;
                    axelMemsTemperature.TimingMode = AxelMems.TimingModes.byStopwatch;
                }
                else
                {
                    axelMems.TimingMode = AxelMems.TimingModes.byNone;
                    axelMemsTemperature.TimingMode = AxelMems.TimingModes.byNone;
                }

            if (genOptions.AxesChannels == 0) axelMems.activeChannel = 0;
            else axelMems.activeChannel = 2;

            if (ucScan.remoteMode == RemoteMode.Jumbo_Repeat)
            {
                axelMems.SetStopwatch(ucScan.remote.stopwatch);
                ucScan.remote.synchroClock(true);
            }
            else
            {
                axelMems.StartStopwatch();
                axelMemsTemperature.StartStopwatch();
            }                 
                
            axelMems.StartAcquisition(InnerBufferSize, 1 / SamplingPeriod);    // async acquisition            
            if(genOptions.TemperatureEnabled && axelMemsTemperature.isDevicePlugged())
                axelMemsTemperature.StartAcquisition(InnerBufferSize, 1 / SamplingPeriod);
        }

        public void DoAcquire(List<Point> dt, out bool next)
        { 
            next = ucScan.EndlessMode() && memsRunning &&
                (ucScan.Running || (ucScan.remoteMode == RemoteMode.Simple_Repeat) || (ucScan.remoteMode == RemoteMode.Jumbo_Repeat));
            bool isCombineQuantMems = genOptions.MemsInJumbo && !probeMode && (ucScan.remoteMode == RemoteMode.Jumbo_Repeat);

            if (axelMems.activeChannel == 0)
            {
                this[0].axelChart.SetIncomingBufferSize(dt.Count);
                this[0].axelChart.Waveform.AddRange(dt);
                if (isCombineQuantMems)
                    this[0].CombineQuantMems(dt, genOptions.Mems2SignLen / 1000.0); 
            }
            if (axelMems.activeChannel == 2)
            {
                List<Point> ds = new List<Point>(); List<Point> ds2 = new List<Point>();
                for (int i = 0; i < dt.Count; i++)
                {
                    if ((i % 2) == 0) ds.Add(dt[i]);  // channel 0
                    else ds2.Add(dt[i]);              // channel 1
                }
                
                this[0].axelChart.SetIncomingBufferSize(ds.Count);
                this[1].axelChart.SetIncomingBufferSize(ds2.Count);

                this[0].axelChart.Waveform.AddRange(ds);
                this[1].axelChart.Waveform.AddRange(ds2);
                if (isCombineQuantMems)
                {
                    this[0].CombineQuantMems(ds, genOptions.Mems2SignLen / 1000.0);
                    this[0].CombineQuantMems(ds2, genOptions.Mems2SignLen / 1000.0); 
                }
            }           
            if (!next)
            {
                for (int i = 0; i < rCount; i++) this[i].axelChart.Running = false;
                ucScan.Running = false;
                axelMems.StopAqcuisition();
                for (int i = 0; i < rCount; i++) this[i].axelChart.Refresh();
            }
        }
        public void DoAcquire2(List<Point> dt, out bool next)
        {
            if (!genOptions.TemperatureEnabled) 
            {
                next = false; return;
            } 
            next = ucScan.EndlessMode() && memsRunning &&
                (ucScan.Running || (ucScan.remoteMode == RemoteMode.Simple_Repeat) || (ucScan.remoteMode == RemoteMode.Jumbo_Repeat));
            if (!next)
            {
                axelMemsTemperature.StopAqcuisition();
                next = false; return;
            }
            DataStack ds = new DataStack();
            if (axelMems.activeChannel == 0)
            {
                for (int i = 0; i < dt.Count; i++)
                    ds.Add(dt[i]);
                this[0].axelChart.memsTemperature = ds.pointYs().Average();
            }
            if (axelMems.activeChannel == 2)
            {
                DataStack ds2 = new DataStack();
                for (int i = 0; i < dt.Count; i++)
                {
                    if ((i % 2) == 0) ds.Add(dt[i]);  // channel 0
                    else ds2.Add(dt[i]); // channel 1
                }

                this[0].axelChart.memsTemperature = ds.pointYs().Average();
                this[1].axelChart.memsTemperature = ds2.pointYs().Average();
            }
        }

        private bool probeMode // simulation of MM2 with AxelProbe
        {
            get
            {
                if (Utils.isNull(ucScan.remote)) return false;
                return ucScan.remote.partner.Equals("Axel Probe") && ucScan.remote.Connected;
            }
        }

        private MMexec lastGrpExe;  
        public void DoRemote(string json)
        {
            MMexec mme = JsonConvert.DeserializeObject<MMexec>(json);

            // format shot.X / shot.Y OR shotData. IMPORTANT for Jumbo-repeat only .X/.Y 
            // if .X / .Y runID's are independant for each axis
            string pref = ""; int runID = 0;
            if (mme.prms.ContainsKey("runID")) runID = Convert.ToInt32(mme.prms["runID"]);
            if (mme.cmd.Substring(0, 4).Equals("shot"))
            {               
                if ((mme.cmd.Equals("shot.X")) || (mme.cmd.Equals("shot.Y"))) 
                {
                    pref = mme.cmd.Substring(mme.cmd.Length-1);
                }
                if (mme.cmd.Equals("shotData")) 
                {
                    if ((runID % 2) == 0) pref = "X";
                    else pref = "Y";
                }
                mme.cmd = "shot";
            }
            switch (mme.cmd)
            {
                case ("shot"): // incomming MM2 data
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
                        this[idx].DoShot(mme, lastGrpExe);
                        int li = (rCount == 2) ? 1 : 0;
                        if (mme.prms.ContainsKey("last") && (li == idx)) // after the last axis
                        {
                            if (Convert.ToInt32(mme.prms["last"]) == 1)
                            {
                                LogEvent(">LAST SHOT", Brushes.DarkRed.Color);
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
                        LogEvent(json, Brushes.Blue.Color);
                        lastGrpExe = mme.Clone();
                        if (!mme.sender.Equals("Axel-hub")) ucScan.remoteMode = RemoteMode.Simple_Repeat;
                        Clear();
                        if (genOptions.MemsInJumbo)
                        {
                            ucScan.SetActivity("Data acquisition");
                            ucScan.Running = true;
                            axelMems.Reset(); 
                            startADC(true, ucScan.GetSamplingPeriod(), ucScan.GetBufferSize());
                        }
                        else
                        {
                            ucScan.SetActivity("");
                        }
                        Clear(genOptions.MemsInJumbo && !probeMode, true, true);
                        for (int i = 0; i < rCount; i++) this[i].DoPrepare(mme);                       
                    }
                    break;
                case ("scan"):
                    {
                        LogEvent(json, Brushes.DarkGreen.Color);
                        lastGrpExe = mme.Clone();
                        MMscan lastScan = new MMscan();
                        if (!lastScan.FromDictionary(mme.prms))
                        {
                            LogEvent("Error in incomming json", Brushes.Red.Color);
                            ucScan.Abort(true);
                            return;
                        }
                        if (!mme.sender.Equals("Axel-hub")) ucScan.remoteMode = RemoteMode.Simple_Scan;
                        Clear();
                        for (int i = 0; i < rCount; i++) this[i].DoPrepare(mme);
                    }
                    break;
                case ("message"):
                    {
                        string txt = (string)mme.prms["text"];
                        int errCode = -1;
                        if (mme.prms.ContainsKey("error")) errCode = Convert.ToInt32(mme.prms["error"]);
                        if (txt.Contains("Error") || (errCode > -1))
                        {
                            LogEvent("!!! " + txt, Brushes.Red.Color);
                            if (errCode > -1) LogEvent("Error code: " + errCode.ToString(), Brushes.Red.Color);
                        }
                        else LogEvent("! " + txt, Brushes.Coral.Color);
                    }
                    break;
                case ("abort"):
                    {
                        LogEvent(json, Brushes.Red.Color);
                        if (ucScan.remoteMode == RemoteMode.Simple_Scan || ucScan.remoteMode == RemoteMode.Simple_Repeat || 
                            ucScan.remoteMode == RemoteMode.Jumbo_Scan || ucScan.remoteMode == RemoteMode.Jumbo_Repeat)
                        {
                            ucScan.Abort(false);
                        }
                    }
                    break;
            }
        }

        public void DoJumboScan(bool down)
        {
            if (!down) // user jumbo cancel
            {
                for (int i = 0; i < rCount; i++) this[i].axelChart.Running = false;
                axelMems.StopAqcuisition();
                LogEvent("Jumbo END !", Brushes.Red.Color);
                return;
            }
            lastGrpExe = new MMexec();
            lastGrpExe.mmexec = "test_drive";
            lastGrpExe.sender = "Axel-hub";

            if (genOptions.JumboScan)
            {
                Clear();
                for (int i = 0; i < rCount; i++) this[i].tabLowPlots.SelectedIndex = 0; // fringes page
                lastGrpExe.cmd = "scan";
                lastGrpExe.id = rnd.Next(int.MaxValue);
                // take params from X 
                int iX = prfIdx("X");
                this[iX].lastScan = this[iX].jumboScan();
                this[iX].lastScan.ToDictionary(ref lastGrpExe.prms);
                
                string json = JsonConvert.SerializeObject(lastGrpExe);
                LogEvent("<< " + json, Brushes.Green.Color);
                ucScan.remoteMode = RemoteMode.Jumbo_Scan;
                ucScan.SendJson(json);

                for (int i = 0; i < rCount; i++) this[i].DoPrepare(lastGrpExe);
                if (ucScan.remoteMode == RemoteMode.Ready_To_Remote) return; // end mission
            }
            else
            {
                Utils.TimedMessageBox("Open a fringe file, adjust the strobes and confirm.", "To be implemented !!!", 3500); //Jumbo-Repeat Requirements
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

        public void SetChartStrobes()
        {
            for (int i = 0; i < rCount; i++)
            {
                this[i].crsDownStrobe.AxisValue = this[i].strobes.Down.X;
                this[i].crsUpStrobe.AxisValue = this[i].strobes.Up.X;
            }
            Utils.TimedMessageBox("Please adjust the strobes and confirm to continue.", "Information", 2500);
        }

        public void jumboRepeat(int cycles) // pnt.X - down; pnt.Y - up
        {
            Clear(); // visual stuff
            //quantList.Clear(); // [time,MOTaccel] list of pairs
            //errList.Clear(); // accumulates errors
            //shotList = new ShotList(chkJoinLog.IsChecked.Value); 
            //setConditions(ref shotList.conditions); !!!

            for (int i = 0; i < rCount; i++) this[i].tabLowPlots.SelectedIndex = 1;
            // set jumbo-repeat conditions & format MMexec
            lastGrpExe.cmd = "repeat";
            lastGrpExe.id = rnd.Next(int.MaxValue);
            lastGrpExe.prms.Clear();
            lastGrpExe.prms["groupID"] = DateTime.Now.ToString("yy-MM-dd_H-mm-ss");
            lastGrpExe.prms["cycles"] = cycles;
            if (genOptions.followPID) { lastGrpExe.prms["follow"] = 1; }
            else { lastGrpExe.prms["follow"] = 0; }
            for (int i = 0; i<rCount; i++) 
            {
                this[i].strobes.Down.X = Utils.formatDouble(Convert.ToDouble(this[i].crsDownStrobe.AxisValue),"G4");
                this[i].strobes.Up.X = Utils.formatDouble(Convert.ToDouble(this[i].crsUpStrobe.AxisValue),"G4");
                lastGrpExe.prms["downStrobe." + this[i].prefix] = this[i].strobes.Down.X;
                lastGrpExe.prms["upStrobe." + this[i].prefix] = this[i].strobes.Up.X;
            }
            string jsonR = JsonConvert.SerializeObject(lastGrpExe);
            LogEvent("<< " + jsonR, Brushes.Blue.Color);
            ucScan.remoteMode = RemoteMode.Jumbo_Repeat;            
              // set ADC24 and corr. visuals
          /*    if (jumboADCFlag && chkMemsEnabled.IsChecked.Value)
              {
                  if (chkFollowPID.IsChecked.Value) ucScan.SetActivity("Data acquis. with PID feedback");
                  else ucScan.SetActivity("Data acquis. (no PID feedback)");
                  ucScan.Running = true;
                  AxelChart1.Waveform.TimeSeriesMode = true;
                  plotcursorAccel.Visibility = System.Windows.Visibility.Collapsed;

                  axelMems.Reset(); timeStack.Clear();
                  if (probeMode) AxelChart1.SetInfo("Axel-Probe feeds the MEMS");
                  else startADC24(true, ucScan.GetSamplingPeriod(), ucScan.GetBufferSize());
                  Thread.Sleep(1000); Utils.DoEvents();
              }*/
             ucScan.SendJson(jsonR);
          }

    }
}
