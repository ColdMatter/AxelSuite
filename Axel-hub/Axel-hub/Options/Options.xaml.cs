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
    /// some changes 12
    /// </summary>
    public partial class OptionsWindow : Window
    {
        /// <summary>
        /// dialog box constructor; reads from file or creates new options object
        /// </summary>
        public OptionsWindow()
        {
            InitializeComponent();           
            if (File.Exists(Utils.configPath + "genOptions"+".cfg"))
            {
                string fileJson = File.ReadAllText(Utils.configPath + "genOptions.cfg");
                genOptions = JsonConvert.DeserializeObject<GeneralOptions>(fileJson);
            }
            else genOptions = new GeneralOptions();
            Title = "  Axel Hub Options  v" + Utils.getRunningVersion();
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

            genOptions.Diagnostics = tcJumboRepeatModes.SelectedIndex == 1;
            genOptions.followPID = chkFollowPID.IsChecked.Value;
            genOptions.logJoin = chkJoinLog.IsChecked.Value;
            genOptions.logRawJdt = chkLogRaw.IsChecked.Value;

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

            genOptions.Mems2SignLen = numMems2SignalLen.Value;

            genOptions.MemsHw = (cbMemsHw.Items[cbMemsHw.SelectedIndex] as ComboBoxItem).Content.ToString();
            if (cbTemperatureHw.SelectedIndex > -1)
                genOptions.TemperatureHw = (cbTemperatureHw.Items[cbTemperatureHw.SelectedIndex] as ComboBoxItem).Content.ToString();

            genOptions.TemperatureEnabled = chkTemperatureEnabled.IsChecked.Value;
            genOptions.TemperatureCompensation = chkTemperatureCompensation.IsChecked.Value;           

            genOptions.Save();
            genOptions.ChangeEvent(genOptions);
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
            rbSingle.IsChecked = (genOptions.AxesChannels == 0) || Utils.isSingleChannelMachine;
            rbDoubleTabs.IsChecked = genOptions.AxesChannels == 1; rbDoubleTabs.IsEnabled = !Utils.isSingleChannelMachine;
            rbDoublePanels.IsChecked = genOptions.AxesChannels == 2; rbDoublePanels.IsEnabled = !Utils.isSingleChannelMachine;

            tbSignalCursorPrec.Text = genOptions.SignalCursorPrec;
            tbSignalTablePrec.Text = genOptions.SignalTablePrec;
            tbSaveFilePrec.Text = genOptions.SaveFilePrec;
            tbLogFilePrec.Text = genOptions.LogFilePrec;

            chkInitN2.IsChecked = genOptions.intN2;
            chkSaveVisuals.IsChecked = genOptions.saveVisuals;

            if (genOptions.Diagnostics) tcJumboRepeatModes.SelectedIndex = 1;
            else tcJumboRepeatModes.SelectedIndex = 0; 

            chkLogRaw.IsChecked = genOptions.logRawJdt;
            chkFollowPID.IsChecked = genOptions.followPID;
            chkJoinLog.IsChecked = genOptions.logJoin;

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

            numMems2SignalLen.Value = genOptions.Mems2SignLen;

            chkTemperatureEnabled.IsChecked = genOptions.TemperatureEnabled;
            chkTemperatureCompensation.IsChecked = genOptions.TemperatureCompensation;

            //(cbMemsHw.Items[cbMemsHw.SelectedIndex] as ComboBoxItem).Content = genOptions.MemsHw;
            //(cbTemperatureHw.Items[cbTemperatureHw.SelectedIndex] as ComboBoxItem).Content = genOptions.TemperatureHw;
            if (Utils.isSingleChannelMachine)
            {
                cbTemperatureHw.IsEnabled = false; cbTemperatureHw.SelectedIndex = -1;
            }
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
