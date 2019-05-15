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
    /// Interaction logic for Options.xaml
    /// </summary>
    public partial class OptionsWindow : Window
    {
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

        public GeneralOptions genOptions;
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
            genOptions.visualDataLength = Convert.ToInt32(tbVisualDataLength.Text);
            genOptions.saveVisuals = chkSaveVisuals.IsChecked.Value;

            genOptions.JumboScan = rbScanOnly.IsChecked.Value || rbBothModes.IsChecked.Value;
            genOptions.JumboRepeat = rbRepeatOnly.IsChecked.Value || rbBothModes.IsChecked.Value;

            if (rbSaveSeqYes.IsChecked.Value) genOptions.saveModes = GeneralOptions.SaveModes.save;
            if (rbSaveSeqAsk.IsChecked.Value) genOptions.saveModes = GeneralOptions.SaveModes.ask;
            if (rbSaveSeqNo.IsChecked.Value) genOptions.saveModes = GeneralOptions.SaveModes.nosave;

            // MEMS
            genOptions.MemsInJumbo = chkRunMemsInJumbo.IsChecked.Value;
            genOptions.MemsHw = (cbMemsHw.Items[cbMemsHw.SelectedIndex] as ComboBoxItem).Content.ToString();
            genOptions.TemperatureHw = (cbTemperatureHw.Items[cbTemperatureHw.SelectedIndex] as ComboBoxItem).Content.ToString();

            genOptions.TemperatureEnabled = chkTemperatureEnabled.IsChecked.Value;
            genOptions.TemperatureCompensation = chkTemperatureCompensation.IsChecked.Value;

            genOptions.Save();
            
            Hide();
        }

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
            tbVisualDataLength.Text = genOptions.visualDataLength.ToString();
            chkSaveVisuals.IsChecked = genOptions.saveVisuals;

            rbScanOnly.IsChecked = genOptions.JumboScan;
            rbRepeatOnly.IsChecked = genOptions.JumboRepeat;
            rbBothModes.IsChecked = genOptions.JumboScan && genOptions.JumboRepeat;

            rbSaveSeqYes.IsChecked = genOptions.saveModes.Equals(GeneralOptions.SaveModes.save);
            rbSaveSeqAsk.IsChecked = genOptions.saveModes.Equals(GeneralOptions.SaveModes.ask);
            rbSaveSeqNo.IsChecked = genOptions.saveModes.Equals(GeneralOptions.SaveModes.nosave);

            // MEMS
            chkRunMemsInJumbo.IsChecked = genOptions.MemsInJumbo;
            //(cbMemsHw.Items[cbMemsHw.SelectedIndex] as ComboBoxItem).Content = genOptions.MemsHw;
            //(cbTemperatureHw.Items[cbTemperatureHw.SelectedIndex] as ComboBoxItem).Content = genOptions.TemperatureHw;

            chkTemperatureEnabled.IsChecked = genOptions.TemperatureEnabled;
            chkTemperatureCompensation.IsChecked = genOptions.TemperatureCompensation;
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }
    }
}
