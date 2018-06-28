using System;
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

namespace OptionsTypeNS
{
    public class GeneralOptions
    {
        public enum SaveModes { save, ask, nosave }

        public SaveModes saveModes;

        public string SignalCursorPrec { get; set; }
        public string SignalTablePrec { get; set; }
        public string SaveFilePrec { get; set; }
        public bool intN2 { get; set; }
        public int visualDataLength { get; set; }
        public bool saveVisuals { get; set; }

        public void Save()
        {
            string fileJson = JsonConvert.SerializeObject(this);
            File.WriteAllText(Utils.configPath + "genOptions.cfg", fileJson);
        }
    }

    public class Modes
    {
        // visuals
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double TopFrame { get; set; } // relative height
        public double TopOfTopFrame { get; set; } // in pixels
        public double MiddleFrame { get; set; }

        // scanUC
        public int SamplingFreq { get; set; } // in Hz
        public bool TimeLimitMode { get; set; } // false - finite; true - cont.
        public int TimeLimit { get; set; } // in sec
        public bool SizeLimitMode { get; set; } // false - finite; true - cont.
        public int SizeLimit { get; set; } // in points

        // Top
        public int ShowFreq { get; set; }
        public int RollMean { get; set; }
        public int StackDepth { get; set; }
        public bool VisUpdate { get; set; }

        // Middle
        public bool ManualYAxisMiddle { get; set; }
        public bool Background { get; set; }
        public bool DarkCurrent { get; set; }
        public bool StdDev { get; set; }
        public bool N1 { get; set; }
        public bool N2 { get; set; }
        public bool RN1 { get; set; }
        public bool RN2 { get; set; }
        public bool Ntot { get; set; }

        // Bottom
        public double JumboFrom { get; set; }
        public double JumboTo { get; set; }
        public double JumboBy { get; set; }
        public int JumboCycles { get; set; }
        public bool VibrEnabled { get; set; }

        public bool ManualYAxisBottom { get; set; }
        public double kP { get; set; }
        public double kI { get; set; }
        public double kD { get; set; }
        public bool PID_Enabled { get; set; }
        public bool DoubleStrobe { get; set; }

        public void Save()
        {
            string fileJson = JsonConvert.SerializeObject(this);
            File.WriteAllText(Utils.configPath + "Defaults.cfg", fileJson);
        }
    }
}