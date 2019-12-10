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

namespace OptionsNS
{
    /// <summary>
    /// Interaction logic, load & save for GeneralOptions genOptions
    /// </summary>
    public partial class OptionsWindow : Window
    {
        /// <summary>
        /// dialog box constructor; reads from file or creates new options object
        /// </summary>
        public OptionsWindow()
        {
            InitializeComponent();           
            if (File.Exists(Utils.configPath + "genOptions.cfg"))
            {
                string fileJson = File.ReadAllText(Utils.configPath + "genOptions.cfg");
                genOptions = JsonConvert.DeserializeObject<GeneralOptions>(fileJson);
            }
            else genOptions = new GeneralOptions();
        }

        /// <summary>
        /// the point of the dialog, readable everywhere
        /// </summary>
        public GeneralOptions genOptions;

        /// <summary>
        /// Accepting and saving the changes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OKButton_Click(object sender, RoutedEventArgs e) // visual to internal 
        {
            // General
            if (rbSingle.IsChecked.Value) genOptions.AxesChannels = 0;
            if (rbDoubleTabs.IsChecked.Value) genOptions.AxesChannels = 1;
            if (rbDoublePanels.IsChecked.Value) genOptions.AxesChannels = 2;

            genOptions.SignalCursorPrec = tbSignalCursorPrec.Text;
            genOptions.SignalTablePrec = tbSignalTablePrec.Text;
            genOptions.SaveFilePrec = tbSaveFilePrec.Text;
            genOptions.LogFilePrec = tbLogFilePrec.Text;

            genOptions.intN2 = chkInitN2.IsChecked.Value;            
            genOptions.saveVisuals = chkSaveVisuals.IsChecked.Value;
            genOptions.followPID = chkFollowPID.IsChecked.Value;
            
            genOptions.TrendSignalLen = numTrendSignalLen.Value;
            genOptions.RawSignalAvg = numRawSignalAvg.Value;

            genOptions.JumboScan = rbScanOnly.IsChecked.Value || rbBothModes.IsChecked.Value;
            genOptions.JumboRepeat = rbRepeatOnly.IsChecked.Value || rbBothModes.IsChecked.Value;

            if (rbSaveSeqYes.IsChecked.Value) genOptions.saveModes = GeneralOptions.SaveModes.save;
            if (rbSaveSeqAsk.IsChecked.Value) genOptions.saveModes = GeneralOptions.SaveModes.ask;
            if (rbSaveSeqNo.IsChecked.Value) genOptions.saveModes = GeneralOptions.SaveModes.nosave;

            // MEMS
            genOptions.MemsInJumbo = chkRunMemsInJumbo.IsChecked.Value;
            genOptions.ShowMemsIfRunning = chkShowMemsIfRunning.IsChecked.Value;

            genOptions.Mems2SignDelay = numMems2SignalDelay.Value;
            genOptions.Mems2SignLen = numMems2SignalLen.Value;

            genOptions.MemsHw = (cbMemsHw.Items[cbMemsHw.SelectedIndex] as ComboBoxItem).Content.ToString();
            genOptions.TemperatureHw = (cbTemperatureHw.Items[cbTemperatureHw.SelectedIndex] as ComboBoxItem).Content.ToString();

            genOptions.TemperatureEnabled = chkTemperatureEnabled.IsChecked.Value;
            genOptions.TemperatureCompensation = chkTemperatureCompensation.IsChecked.Value;

            genOptions.Save();
            
            Hide();
        }
        /// <summary>
        /// Updating visuals from genOptions
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void frmOptions_Activated(object sender, EventArgs e)
        {
            // General
            rbSingle.IsChecked = genOptions.AxesChannels == 0;
            rbDoubleTabs.IsChecked = genOptions.AxesChannels == 1;
            rbDoublePanels.IsChecked = genOptions.AxesChannels == 2;

            tbSignalCursorPrec.Text = genOptions.SignalCursorPrec;
            tbSignalTablePrec.Text = genOptions.SignalTablePrec;
            tbSaveFilePrec.Text = genOptions.SaveFilePrec;
            tbLogFilePrec.Text = genOptions.LogFilePrec;

            chkInitN2.IsChecked = genOptions.intN2;
            chkSaveVisuals.IsChecked = genOptions.saveVisuals;
            chkFollowPID.IsChecked = genOptions.followPID;

            numTrendSignalLen.Value = genOptions.TrendSignalLen;
            numRawSignalAvg.Value = genOptions.RawSignalAvg;

            rbScanOnly.IsChecked = genOptions.JumboScan;
            rbRepeatOnly.IsChecked = genOptions.JumboRepeat;
            rbBothModes.IsChecked = genOptions.JumboScan && genOptions.JumboRepeat;

            rbSaveSeqYes.IsChecked = genOptions.saveModes.Equals(GeneralOptions.SaveModes.save);
            rbSaveSeqAsk.IsChecked = genOptions.saveModes.Equals(GeneralOptions.SaveModes.ask);
            rbSaveSeqNo.IsChecked = genOptions.saveModes.Equals(GeneralOptions.SaveModes.nosave);

            // MEMS
            chkRunMemsInJumbo.IsChecked = genOptions.MemsInJumbo;
            chkShowMemsIfRunning.IsChecked = genOptions.ShowMemsIfRunning;

            numMems2SignalDelay.Value = genOptions.Mems2SignDelay;
            numMems2SignalLen.Value = genOptions.Mems2SignLen;

            chkTemperatureEnabled.IsChecked = genOptions.TemperatureEnabled;
            chkTemperatureCompensation.IsChecked = genOptions.TemperatureCompensation;

            //(cbMemsHw.Items[cbMemsHw.SelectedIndex] as ComboBoxItem).Content = genOptions.MemsHw;
            //(cbTemperatureHw.Items[cbTemperatureHw.SelectedIndex] as ComboBoxItem).Content = genOptions.TemperatureHw;
        }

        /// <summary>
        /// Cancel without modifications
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }
    }
}
