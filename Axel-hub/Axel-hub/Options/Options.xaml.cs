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
using OptionsTypeNS;

namespace OptionsNS
{
    /// <summary>
    /// Interaction logic for Options.xaml
    /// </summary>
    public partial class OptionsWindow : Window
    {
        public OptionsWindow()
        {
            InitializeComponent();
            genOptions = new GeneralOptions();
            if (File.Exists(Utils.configPath + "genOptions.cfg"))
            {
                string fileJson = File.ReadAllText(Utils.configPath + "genOptions.cfg");
                genOptions = JsonConvert.DeserializeObject<GeneralOptions>(fileJson);
            }
        }

        public GeneralOptions genOptions;
        private void OKButton_Click(object sender, RoutedEventArgs e) // visual to internal 
        {
            genOptions.SignalCursorPrec = tbSignalCursorPrec.Text;
            genOptions.SignalTablePrec = tbSignalTablePrec.Text;
            genOptions.SaveFilePrec = tbSaveFilePrec.Text;

            genOptions.intN2 = chkInitN2.IsChecked.Value;
            genOptions.visualDataLength = Convert.ToInt32(tbVisualDataLength.Text);
            genOptions.saveVisuals = chkSaveVisuals.IsChecked.Value;

            genOptions.JumboScan = rbScanOnly.IsChecked.Value || rbBothModes.IsChecked.Value;
            genOptions.JumboRepeat = rbRepeatOnly.IsChecked.Value || rbBothModes.IsChecked.Value;

            if (rbSingle.IsChecked.Value) genOptions.MemsChannels = 0;
            if (rbDouble.IsChecked.Value) genOptions.MemsChannels = 2;

            if (rbSaveSeqYes.IsChecked.Value) genOptions.saveModes = GeneralOptions.SaveModes.save;
            if (rbSaveSeqAsk.IsChecked.Value) genOptions.saveModes = GeneralOptions.SaveModes.ask;
            if (rbSaveSeqNo.IsChecked.Value) genOptions.saveModes = GeneralOptions.SaveModes.nosave;

            Hide();
        }

        private void frmOptions_Loaded(object sender, RoutedEventArgs e) // internal to visual
        {
            tbSignalCursorPrec.Text = genOptions.SignalCursorPrec;
            tbSignalTablePrec.Text = genOptions.SignalTablePrec;
            tbSaveFilePrec.Text = genOptions.SaveFilePrec;

            chkInitN2.IsChecked = genOptions.intN2;
            tbVisualDataLength.Text = genOptions.visualDataLength.ToString();
            chkSaveVisuals.IsChecked = genOptions.saveVisuals;

            rbScanOnly.IsChecked = genOptions.JumboScan;
            rbRepeatOnly.IsChecked = genOptions.JumboRepeat;
            rbBothModes.IsChecked = genOptions.JumboScan && genOptions.JumboRepeat;

            rbSingle.IsChecked = genOptions.MemsChannels == 0;
            rbDouble.IsChecked = genOptions.MemsChannels == 2;

            rbSaveSeqYes.IsChecked = genOptions.saveModes.Equals(GeneralOptions.SaveModes.save);
            rbSaveSeqAsk.IsChecked = genOptions.saveModes.Equals(GeneralOptions.SaveModes.ask);
            rbSaveSeqNo.IsChecked = genOptions.saveModes.Equals(GeneralOptions.SaveModes.nosave);
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }
    }
}
