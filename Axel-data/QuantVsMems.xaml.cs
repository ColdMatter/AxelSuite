using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Axel_hub;
using UtilsNS;


namespace Axel_data
{
    /// <summary>
    /// Interaction logic for QuantVsMems.xaml
    /// </summary>
    public partial class QuantVsMems : UserControl
    {
        const string prec = "G5";
        public QuantVsMems()
        {
            InitializeComponent();

        }
        public void Initialize()
        {
            Window window = Window.GetWindow(this);
            window.Closing += window_Closing;

            numSectionSize_ValueChanged(null, null);
        }

        public delegate void LogHandler(string txt, bool detail = false, Color? clr = null);
        public event LogHandler OnLog;

        protected void LogEvent(string txt, bool detail = false, Color? clr = null)
        {
            if (OnLog != null) OnLog(txt, detail, clr);
        }
        public delegate void ProgressHandler(int prog);
        public event ProgressHandler OnProgress;

        protected void ProgressEvent(int prog)
        {
            if (OnProgress != null) OnProgress(prog);
        }

        #region Scan timing of MEMS vs quant. Delay
        ShotList shotList; List<SingleShot> validShotList;
        private const int dataLength = 10000; // default length of data kept in
        /// <summary>
        /// Open joint log file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnOpenJLog_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.FileName = ""; // Default file name
            dlg.InitialDirectory = Utils.dataPath;
            dlg.DefaultExt = ".jdt"; // Default file extension
            dlg.Filter = "Join Data File (.jdt)|*.jdt"; // Filter files by extension

            Nullable<bool> result = dlg.ShowDialog();
            if (result == false) return;
            //dlg.FileName = @"f:\Work\AxelSuite\Axel-data\Data\5120Hz.jdt";
            //lbJoinLogInfo.Content = "File: " + dlg.FileName;
            shotList = new ShotList(false, dlg.FileName); 
            btnScan.IsEnabled = File.Exists(shotList.filename) && !shotList.savingMode;
            if (btnScan.IsEnabled)
            {
                LogEvent("Opened: " + shotList.filename);
            }
            gbSectScroll.Header = "Sect.Scroll";
            //shotList = new ShotList(true, false, fn);
            //shotList.enabled = true;
            //setConditions(ref shotList.conditions);
        }

        DataStack srsMdiffQ = new DataStack(); //ShotList shotList;
        public DataStack srsFringes = null; DataStack srsPeriod = null; DataStack srsPhase = null; DataStack srsAmpl = null; DataStack srsRMSE = null;

        public List<DataStack> M_size()
        {
            List<DataStack> all = new List<DataStack>(); SingleShot ss;
            DataStack srs = new DataStack(dataLength); DataStack srs2 = new DataStack(dataLength);  

            shotList.resetScan(); double prT = -1;
            bool next; int k = 0;
            do
            {                
                ss = shotList.archiScan(out next); 
                k++; if (k < 4) continue;
                if (next)
                {
                    srs.AddPoint(ss.mems.Count);
                    if (prT > 0) srs2.AddPoint(ss.quant.X - prT);
                    prT = ss.quant.X;
                }                                      
            } while (next);
            all.Add(srs); all.Add(srs2);
            return all; 
        }
 
        private bool ShowResults(Dictionary<string, double> rFit, bool inLog, bool inChart)
        {
            if (Utils.isNull(rFit)) return false;
            if (!rFit.ContainsKey("rmse")) return false;
            if (inLog)
            {   
                LogEvent("============= sect: "+QMfit.curSectIdx.ToString(), true, Brushes.Blue.Color);
                foreach (KeyValuePair<string, double> keyVal in rFit)
                {
                    LogEvent(keyVal.Key + " = " + keyVal.Value.ToString(prec), true, Brushes.Navy.Color);
                }
                //LogEvent("-------------------", true, Brushes.Blue.Color);
            }
            if (inChart)
            {
                
                if (Double.IsNaN(rFit["rmse"])) return false;
                srsPeriod.AddPoint(rFit["period"]); graphTrends.Data[0] = srsPeriod;
                srsPhase.AddPoint(rFit["phase"]);   graphTrends.Data[1] = srsPhase;
                srsAmpl.AddPoint(rFit["ampl"]);     graphTrends.Data[2] = srsAmpl;
                srsRMSE.AddPoint(rFit["rmse"]);     graphTrends.Data[3] = srsRMSE;               
            }
            return true;
        }

        private int ValidateMemsData(bool detail)
        {
           validShotList = new List<SingleShot>(); bool next; SingleShot ss;
           shotList.resetScan(); if (shotList.Count == 0) LogEvent("Join -Data (.jdt) file seems to be empty.",false,Brushes.Red.Color);
           LogEvent("Estim.aqc.range: " + shotList.defaultAqcTime.ToString(prec) + " [s]", detail, Brushes.DarkGreen.Color);
           do
            {
                ss = shotList.archiScan(out next);
                if (next)
                    if (ss.timeValidation(shotList.defaultAqcTime)) validShotList.Add(new SingleShot(ss));
            } while (next);
            LogEvent("Total shots: " + shotList.Count.ToString() + "; valid: " + validShotList.Count.ToString(), detail, Brushes.Navy.Color);
            
            return validShotList.Count;
        }
        /// <summary>
        /// Scan delay between MOT accel data point and MEMS accel array  
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnJFitScan_Click(object sender, RoutedEventArgs e)
        {
            if (cbAction.SelectedIndex != 2)
            {
                if (Utils.isNull(shotList)) throw new Exception("No Join-Data (.jdt) file loaded.");                
            }
            btnScan.Value = !btnScan.Value;
            switch (cbAction.SelectedIndex)
            {
                case(0): // Scan Fit                    
                    bool next; 
                    shotList.resetScan(); 
                    // validation stats
                     ProgressEvent((int)(ValidateMemsData(true)/ numSectionSize.Value) + 1);
                    // initialte series
                    if (Utils.isNull(srsPeriod)) srsPeriod = new DataStack();
                    else srsPeriod.Clear();
                    if (Utils.isNull(srsPhase)) srsPhase = new DataStack();
                    else srsPhase.Clear();                   
                    if (Utils.isNull(srsAmpl)) srsAmpl = new DataStack();
                    else srsAmpl.Clear();                   
                    if (Utils.isNull(srsRMSE)) srsRMSE = new DataStack();
                    else srsRMSE.Clear();
                    
                    // the real action
                    shotList.resetScan();  Dictionary<string, double> rFit; 
                    do
                    {
                        rFit = QMfit.ScanSection(ref shotList, numSectionSize.Value, out next);
                        ShowResults(rFit, true, true); //LogEvent("error at sect: "+((int)(shotList.lastIdx/ numSectionSize.Value)).ToString(), false, Brushes.Red.Color);
                        ProgressEvent(0);
                    } while (next);
                    
                    btnScan.Value = false;
                    break;
                case(1): // M-size
                    ValidateMemsData(false);
                    List<DataStack> all = M_size();
                    graphTrends.Data[0] = all[0]; graphTrends.Data[1] = all[1];
                    btnScan.Value = false;
                    break;
                case(2): // Simulation
                    while (btnScan.Value)
                    {
                        btnScrollRight_Click(null, null);
                        //Utils.DoEvents();
                        Thread.Sleep(500);
                    }
                    break;
            }
            return;
        }
        #endregion
        private void chkMEMS_Checked(object sender, RoutedEventArgs e)
        {
            if (Utils.isNull(plotPeriod)) return;
            if (chkPeriod.IsChecked.Value) plotPeriod.Visibility = System.Windows.Visibility.Visible;
            else plotPeriod.Visibility = System.Windows.Visibility.Hidden;
            if (chkPhase.IsChecked.Value) plotPhase.Visibility = System.Windows.Visibility.Visible;
            else plotPhase.Visibility = System.Windows.Visibility.Hidden;
            if (chkAmpl.IsChecked.Value) plotAmpl.Visibility = System.Windows.Visibility.Visible;
            else plotAmpl.Visibility = System.Windows.Visibility.Hidden;
            if (chkRMSE.IsChecked.Value) plotRMSE.Visibility = System.Windows.Visibility.Visible;
            else plotRMSE.Visibility = System.Windows.Visibility.Hidden;
        }

        private int actIdx = 0; // active section index
        private void btnScrollRight_Click(object sender, RoutedEventArgs e)
        {
            if (cbAction.SelectedIndex != 2)
            {
                  if (Utils.isNull(shotList)) LogEvent("No Join-Data (.jdt) file loaded.", false, Brushes.Red.Color); return;
                  //if (shotList.Count == 0) throw new Exception("Join-Data (.jdt) file is empty.");
            }
            switch (cbAction.SelectedIndex)
            {
                case (0): // Scan Fit
                    int sz = numSectionSize.Value;
                    if (sender == btnScrollRight) actIdx++;
                    else actIdx--;
                    actIdx = Utils.EnsureRange(actIdx, 0, shotList.Count/sz);
                    gbSectScroll.Header = "Sect.Scroll #" + actIdx.ToString();
                    QMfit.LoadFromShotList(ref shotList, sz, actIdx * sz, false);
                    break;
                case (1): // M-size
                    break;
                case (2): // Simulation
                    List<Point> lp = new List<Point>();
                    Dictionary<string, double> ini = QMfit.GetInitials(); 
                    double st = 26.0 / numSectionSize.Value; // step in rad (4 periods)
                    for (int i = 0; i < numSectionSize.Value; i++)
                    {                    
                        double x = i * st;
                        double y = Math.Cos(ini["period"] * x + ini["phase"]) * ini["ampl"] + ini["offset"];
                        lp.Add(new Point(x, y+Utils.Gauss01()* numSimulNoise.Value/100));
                    }
                    QMfit.LoadFromPoints(lp, Utils.isNull(sender));
                    break;
            }
        }

        private void window_Closing(object sender, global::System.ComponentModel.CancelEventArgs e)
        {
            if (btnScan.Value)
            {
                btnScan.Value = false; Thread.Sleep(600);
            }
        }

        private void numSectionSize_ValueChanged(object sender, NationalInstruments.Controls.ValueChangedEventArgs<int> e)
        {
            if (Utils.isNull(QMfit)) return;
            QMfit.sectSize = numSectionSize.Value;
        }
    }
}
