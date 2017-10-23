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

namespace Axel_hub
{
    public class GeneralOptions
    {
        public string SignalCursorPrec { get; set; }
        public string SignalTablePrec { get; set; }

        public void Save()
        {
            string fileJson = JsonConvert.SerializeObject(this);
            File.WriteAllText(Utils.configPath + "genOptions.cfg", fileJson);
        }
    }

    /// <summary>
    /// Interaction logic for Options.xaml
    /// </summary>
    public partial class OptionsWindow : Window
    {
        public OptionsWindow()
        {
            InitializeComponent();
            genOptions = new GeneralOptions();

            string fileJson = File.ReadAllText(Utils.configPath + "genOptions.cfg");
            genOptions = JsonConvert.DeserializeObject<GeneralOptions>(fileJson);
        }

        public GeneralOptions genOptions;
        private void OKButton_Click(object sender, RoutedEventArgs e) // visual to internal 
        {
            genOptions.SignalCursorPrec = tbSignalCursorPrec.Text;
            genOptions.SignalTablePrec = tbSignalTablePrec.Text;

            Hide();
        }

        private void frmOptions_Loaded(object sender, RoutedEventArgs e) // internal to visual
        {
            tbSignalCursorPrec.Text = genOptions.SignalCursorPrec;
            tbSignalTablePrec.Text = genOptions.SignalTablePrec;
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }
    }
}