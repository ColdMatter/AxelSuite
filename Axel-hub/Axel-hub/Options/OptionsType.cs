﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UtilsNS;

namespace OptionsNS
{
    /// <summary>
    /// The mode negotiated with MM2
    /// </summary>
    public enum RemoteMode
    {
        Disconnected,  // initial neutral state
        Jumbo_Scan,    // scan as part of Jumbo Run
        Jumbo_Repeat,  // repeat as part of Jumbo Run
        Simple_Scan,   // scan initiated by MM
        Simple_Repeat, // repeat initiated by MM
        Ready_To_Remote //neutral connected state       
    }


    /// <summary>
    /// general options from Options dialog window accesable everywhere
    /// </summary>
    public class GeneralOptions
    {
        public enum SaveModes { save, ask, nosave }
        public enum MemsInJumbo  { None, USB9251, PXI4462 }
        
        // General
        public int AxesChannels { get; set; } // X -> 0; Y -> 1; X/Y -> 2

        public string SignalCursorPrec { get; set; }
        public string SignalTablePrec { get; set; }
        public string SaveFilePrec { get; set; }
        public string LogFilePrec { get; set; }

        public bool intN2 { get; set; }        
        public bool saveVisuals { get; set; }

        public bool Diagnostics { get; set; }
        public bool followPID { get; set; }
        public bool logJoin { get; set; }
        public bool logRawJdt { get; set; }

        public int TrendSignalLen { get; set; }
        public int RawSignalAvg { get; set; }

        public bool JumboScan { get; set; }
        public bool JumboRepeat { get; set; }
        public SaveModes saveModes;

        // MEMS options
        public MemsInJumbo memsInJumbo;
        public bool ShowMemsIfRunning { get; set; }

        public double Mems2SignDelay { get; set; }
        public double Mems2SignLen { get; set; }
        public int Mems2SignLenMult { get; set; }
        public double Mems2ExtInfCap { get; set; }
        public int MemsAverOver { get; set; }

        public bool TemperatureEnabled { get; set; }
        public bool TemperatureCompensation { get; set; }

        public string MemsHw { get; set; }
        public string TemperatureHw { get; set; }

        public void Save()
        {
            string fileJson = JsonConvert.SerializeObject(this);
            File.WriteAllText(Utils.configPath + "genOptions.cfg", fileJson);
        }

        public delegate void ChangeHandler(GeneralOptions opts);
        public event ChangeHandler OnChange;
        public void ChangeEvent(GeneralOptions opts)
        {
            if (OnChange != null) OnChange(opts);
        }
    }
    /// <summary>
    /// visuals for the app, MEMS aqcuisition params and scan modes
    /// </summary>
    public class ScanModes // 
    {
        // visuals
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }

        // scanUC
        public int SamplingFreq { get; set; } // in Hz
        public bool TimeLimitMode { get; set; } // false - finite; true - cont.
        public double TimeLimit { get; set; } // in sec
        public int SizeLimit { get; set; } // in points

        // working modes (not to load back)
        public RemoteMode remoteMode = RemoteMode.Disconnected;

        public void Save()
        {
            string fileJson = JsonConvert.SerializeObject(this);
            File.WriteAllText(Utils.configPath + "scanDefaults.cfg", fileJson);
        }
    }
    /// <summary>
    /// Visuals and prameters for
    /// Top: Axel-chart 
    /// Middle: Signal panel charts
    /// Bottom: Scan and Accel trend tabs/charts
    /// </summary>
    public class Modes
    {
        // Top
        public double TopFrame { get; set; } // relative height
        public double TopOfTopFrame { get; set; } // in pixels
        public int ShowFreq { get; set; }
        public int RollMean { get; set; }
        public int StackDepth { get; set; }
        public bool ChartUpdate { get; set; }
        public bool TblUpdate { get; set; }
        public double PowerCoeff { get; set; }

        // Middle
        public double MiddleFrame { get; set; }
        public bool AutoScaleMiddle { get; set; }
        public bool DarkCurrent { get; set; }
        public bool StdDev { get; set; }
        public bool N1 { get; set; }
        public bool N2 { get; set; }
        public bool RN1 { get; set; }
        public bool RN2 { get; set; }
        public bool Ntot { get; set; }
        public bool B2 { get; set; }
        public bool Btot { get; set; }
        public bool RsltTblUpdate { get; set; }
        public bool RsltChrtUpdate { get; set; }
        public bool SignalLog { get; set; }

        // Bottom
        public double JumboFrom { get; set; }
        public double JumboTo { get; set; }
        public double JumboBy { get; set; }
        public int JumboCycles { get; set; }

        public bool MemsEnabled { get; set; }
        public double Kcoeff { get; set; }
        public double phi0 { get; set; }
        public double scale { get; set; }
        public double offset { get; set; }

        public bool AutoScaleBottom { get; set; }
        public double kP { get; set; }
        public double kI { get; set; }
        public double kD { get; set; }
        public bool DoubleStrobe { get; set; }

        public void Save(string prefix)
        {
            string fileJson = JsonConvert.SerializeObject(this);
            File.WriteAllText(Utils.configPath + prefix+"_defaults.cfg", fileJson);
        }
    }
}