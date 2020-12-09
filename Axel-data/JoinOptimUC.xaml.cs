﻿using System;
using System.Collections.Generic;
using System.IO;
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
    /// Interaction logic for JoinOptimClass.xaml
    /// </summary>
    public partial class JoinOptimClass : UserControl
    {
        public JoinOptimClass()
        {
            InitializeComponent();
        }
        public void Initialize()
        {

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
        ShotList shotListDly;
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
            dlg.DefaultExt = ".jlg"; // Default file extension
            dlg.Filter = "Join Log File (.jlg)|*.jlg"; // Filter files by extension

            Nullable<bool> result = dlg.ShowDialog();
            if (result == true)
            {
                lbJoinLogInfo.Content = "File: " + dlg.FileName;
                shotListDly = new ShotList(false, dlg.FileName);
            }
            btnJDlyScan.IsEnabled = File.Exists(shotListDly.filename) && !shotListDly.savingMode;

            if (btnJDlyScan.IsEnabled)
            {
                LogEvent("Opened: " + shotListDly.filename);               
            }
            //shotList = new ShotList(true, false, fn);
            //shotList.enabled = true;
            //setConditions(ref shotList.conditions);
            }

        DataStack srsMdiffQ = new DataStack(); //ShotList shotList;
        public DataStack srsFringes = null; DataStack srsMotAccel = null; DataStack srsCorr = null; DataStack srsMems = null; DataStack srsAccel = null;

        /// <summary>
        /// Scan delay between MOT accel data point and MEMS accel array  
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnJDlyScan_Click(object sender, RoutedEventArgs e)
        {
            if (Utils.isNull(shotListDly)) throw new Exception("No shotList opened");
            btnJDlyScan.Value = !btnJDlyScan.Value;
            if (!btnJDlyScan.Value) return;
            if (shotListDly.savingMode || shotListDly.filename.Equals("")) throw new Exception("problem with shotList");
            //tabLowPlots.SelectedIndex = 1;
            if (Utils.isNull(srsMotAccel)) srsMotAccel = new DataStack(dataLength);
            if (Utils.isNull(srsCorr)) srsCorr = new DataStack(dataLength);
            if (Utils.isNull(srsMems)) srsMems = new DataStack(dataLength);
            if (Utils.isNull(srsAccel)) srsAccel = new DataStack(dataLength);

            srsMdiffQ.Clear(); double xMin = 1E6, xMax = -1e6;
            double wd = numJFrom.Value / 1000.0;
            SingleShot ss; bool next; int j;
            shotListDly.resetScan();
            if (!shotListDly.conditions.Count.Equals(0))
            {
                OnLog(">> processing conditions:", true, Brushes.DarkSlateGray.Color);
                foreach (KeyValuePair<string, double> pair in shotListDly.conditions)
                {
                    OnLog(pair.Key + " = " + pair.Value, true, Brushes.Teal.Color);
                }
            }
            if (shotListDly.archiveMode == false) OnLog("The file is loaded in memory -> " + shotListDly.Count.ToString() + " shots.", false, Brushes.DarkSlateGray.Color);
            while ((wd <= (numJTo.Value / 1000.0)) && btnJDlyScan.Value)
            {
                srsMotAccel.Clear(); srsCorr.Clear(); srsMems.Clear(); srsAccel.Clear();
                shotListDly.resetScan(); j = 0; // next wd for the scan
                do
                {
                    ss = shotListDly.archiScan(out next); if (Utils.isNull(ss) || !next) break;
                    srsMotAccel.Add(new Point(ss.quant.X, ss.quant.Y)); xMin = Math.Min(xMin, ss.quant.X); xMax = Math.Max(xMax, ss.quant.X);
                    double m = ss.memsWeightAccel(wd, -1, true);

                    srsMems.AddPoint(m, ss.quant.X + wd);
                    srsCorr.AddPoint((m - ss.quant.Y) * (m - ss.quant.Y), ss.quant.X);
                    if (chkChartEachIter.IsChecked.Value)
                    {
                        if (!srsMems.Count.Equals(0)) graphAccelTrend.Data[0] = srsMems;
                        if (!srsCorr.Count.Equals(0)) graphAccelTrend.Data[1] = srsCorr;
                        if (!srsMotAccel.Count.Equals(0)) graphAccelTrend.Data[2] = srsMotAccel;
                        if (!srsAccel.Count.Equals(0)) graphAccelTrend.Data[3] = srsAccel;
                    }
                    Utils.DoEvents();
                    j++;
                } while (next && (numJNPnts.Value.Equals(-1) || (j < numJNPnts.Value)) && btnJDlyScan.Value);
                LogEvent("shot #" + j.ToString());
                if (btnJDlyScan.Value)
                {
                     srsMdiffQ.AddPoint(srsCorr.pointYs().Average(), wd * 1000.0);
                     graphJoinOptim.Data[0] = srsMdiffQ;
                }
                lbJoinLogInfo.Content = "File: " + shotListDly.filename + " ; Delay = " + (wd * 1000.0).ToString("G4");
                Utils.DoEvents();
                wd += numJBy.Value / 1000.0;
            } // wd
            btnJDlyScan.Value = false;
            double xm = 0; double ym = 1e6;
            foreach (Point pnt in srsMdiffQ)
            {
                if (pnt.Y < ym)
                {
                    xm = pnt.X; ym = pnt.Y;
                }
            }
            LogEvent("Minimum at " + xm.ToString("G5") + " / " + ym.ToString("G5"));
            LogEvent("=================================");
        }
        #endregion
        private void chkMEMS_Checked(object sender, RoutedEventArgs e)
        {
            if (Utils.isNull(plotMems)) return;
            if (chkMEMS.IsChecked.Value) plotMems.Visibility = System.Windows.Visibility.Visible;
            else plotMems.Visibility = System.Windows.Visibility.Hidden;
            if (chkCorr.IsChecked.Value) plotCorr.Visibility = System.Windows.Visibility.Visible;
            else plotCorr.Visibility = System.Windows.Visibility.Hidden;
            if (chkMOT.IsChecked.Value) plotMotAccel.Visibility = System.Windows.Visibility.Visible;
            else plotMotAccel.Visibility = System.Windows.Visibility.Hidden;
            if (chkAccel.IsChecked.Value) plotAccel.Visibility = System.Windows.Visibility.Visible;
            else plotAccel.Visibility = System.Windows.Visibility.Hidden;
        }

    }
}
