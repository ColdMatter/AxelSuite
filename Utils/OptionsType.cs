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

        public void Save()
        {
            string fileJson = JsonConvert.SerializeObject(this);
            File.WriteAllText(Utils.configPath + "genOptions.cfg", fileJson);
        }
    }

    public class Modes
    {
        // Top
        public int StackDepth { get; set; }
        public int ShowFreq { get; set; }

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
        public bool ManualYAxisBottom { get; set; }

        public void Save()
        {
            string fileJson = JsonConvert.SerializeObject(this);
            File.WriteAllText(Utils.configPath + "Defaults.cfg", fileJson);
        }
    }
}