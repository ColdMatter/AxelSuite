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
        public delegate void LogHandler(string txt, Color? clr = null);
        public event LogHandler OnLog;

        protected void LogEvent(string txt, Color? clr = null)
        {
            if (OnLog != null) OnLog(txt, clr);
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
                shotListDly = new ShotList(true, false, dlg.FileName);
            }
            btnJDlyScan.IsEnabled = File.Exists(shotListDly.filename) && !shotListDly.savingMode;

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
            if (Utils.isNull(shotListDly)) return;
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
            /*if (!shotListDly.conditions.Count.Equals(0))
            {
                OnLog(">> processing conditions:", Brushes.DarkSlateGray.Color);
                foreach (KeyValuePair<string, double> pair in shotList.conditions)
                {
                    OnLog(pair.Key + " = " + pair.Value, Brushes.Teal.Color);
                }
            }*/
            shotListDly.resetScan(); if (!shotListDly.archiveMode) OnLog("The file is loaded in memory -> " + shotListDly.FileCount.ToString() + " shots.", Brushes.DarkSlateGray.Color);
            while ((wd <= (numJTo.Value / 1000.0)) && btnJDlyScan.Value)
            {
                srsMotAccel.Clear(); srsCorr.Clear(); srsMems.Clear(); srsAccel.Clear();
                shotListDly.resetScan(); j = 0;
                do
                {
                    ss = shotListDly.archiScan(out next);
                    srsMotAccel.Add(ss.quant); xMin = Math.Min(xMin, ss.quant.X); xMax = Math.Max(xMax, ss.quant.X);
                    double m = ss.memsWeightAccel(wd, genOptions.Mems2SignLen / 1000.0, true);

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
                lbInfoAccelTrend.Content = "Info: shot # " + j.ToString();
                if (btnJDlyScan.Value)
                {
                    if (chkChartEachIter.IsChecked.Value)
                    {
                        double d = 0.02 * (xMax - xMin);
                        accelXaxis.Adjuster = RangeAdjuster.None;
                        accelXaxis.Range = new Range<double>(xMin - d, xMax + d);
                    }
                    srsMdiffQ.AddPoint(srsCorr.pointYs().Average(), wd * 1000.0);
                    graphJoinOptim.Data[0] = srsMdiffQ;
                }
                lbJoinLogInfo.Content = "File: " + shotListDly.filename + " ; Delay = " + (wd * 1000.0).ToString("G4");
                Utils.DoEvents();
                wd += numJBy.Value / 1000.0;
            }
            accelXaxis.Adjuster = RangeAdjuster.FitLoosely;
            btnJDlyScan.Value = false;
            double xm = 0; double ym = 1e6;
            foreach (Point pnt in srsMdiffQ)
            {
                if (pnt.Y < ym)
                {
                    xm = pnt.X; ym = pnt.Y;
                }
            }
            lbJoinLogInfo.Content = "File: " + shotList.filename + " ; Minimum at " + xm.ToString("G5") + " / " + ym.ToString("G5");
            lbInfoAccelTrend.Content = "Info:";*/
        }
        #endregion

    }
}
