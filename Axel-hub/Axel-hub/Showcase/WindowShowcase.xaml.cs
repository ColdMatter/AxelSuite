using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using NationalInstruments.Controls.Primitives;
using NationalInstruments.Controls;
using UtilsNS;

namespace Axel_hub
{
    /// <summary>
    /// Showcase addition g from MEMS = 9.81792; acoording to International Service of Weights and Measures 9.80665
    /// </summary>
    public partial class ShowcaseClassWindow : Window
    {
        public ShowcaseClassWindow()
        {
            InitializeComponent();           
        }
        public DataStack scanSrs;
        public void InitScan(baseMMscan scan)
        {
            int k = Convert.ToInt32((scan.sTo - scan.sFrom) / scan.sBy) + 2; 
            scanSrs = new DataStack(2*k+10);
            axisXscan.Range = new Range<double>(scan.sFrom, scan.sTo);
            graphScan.Data[0] = null;
            btnAcceptStrobes.Value = false;
        }
        public int nextScanPoint(double xVl, double A)
        {
            scanSrs.AddPoint(A, xVl);
            graphScan.Data[0] = scanSrs;
            return scanSrs.Count;
        }
        protected DataStack quantSrs, memsSrs, accelSrs, diffSrs;
        public void InitRun(int depth = 60)
        {
            axisXrun.Range = new Range<double>(0, depth);
            quantSrs = new DataStack(3*depth); graphRun.Data[0] = null;
            memsSrs = new DataStack(3*depth); graphRun.Data[1] = null;
            accelSrs = new DataStack(3*depth); graphRun.Data[2] = null;
            diffSrs = new DataStack(3*depth); graphRun.Data[3] = null;
        }
        public int nextRunPoint(double xVl, Dictionary <string, double> dp)
        {  
            if (btnPause.Value) return quantSrs.Count;
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Send,
                new Action(() =>
                {
                    if (dp.ContainsKey("PhiMg"))
                    {               
                        quantSrs.AddPoint(dp["PhiMg"], xVl);
                        graphRun.Data[0] = quantSrs;
                    }
                    if (dp.ContainsKey("MEMS"))
                    {
                        memsSrs.AddPoint(dp["MEMS"], xVl);
                        graphRun.Data[1] = memsSrs;
                    }
                    if (dp.ContainsKey("Accel"))
                    {
                        accelSrs.AddPoint(dp["Accel"], xVl);
                        graphRun.Data[2] = accelSrs;
                    }

                }));
            if (dp.ContainsKey("Diff"))
            {
                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,
                    new Action(() =>
                    {
                        diffSrs.AddPoint(dp["Diff"], xVl); graphRun.Data[3] = diffSrs;
                    }));
            }
            return quantSrs.Count;
        }
        public delegate bool ShowcaseEventHandler(string msg);
        public event ShowcaseEventHandler OnShowcaseEvent;
        protected bool ShowcaseEvent(string msg)
        {
            if (OnShowcaseEvent != null) return OnShowcaseEvent(msg);
            else return false;
        }
        private void btnAcceptStrobes_Click(object sender, RoutedEventArgs e)
        {
            btnAcceptStrobes.Value = !btnAcceptStrobes.Value;
            if (btnAcceptStrobes.Value)
            {
                ShowcaseEvent("Accept Strobes:"+crsUpStrobe.AxisValue.ToString()+","+ crsDownStrobe.AxisValue.ToString());
            }
        }
        private void btnPause_Click(object sender, RoutedEventArgs e)
        {
            btnPause.Value = !btnPause.Value;
        }

        private void btnScan_Click(object sender, RoutedEventArgs e)
        {
            if (sender is BooleanButton)
            {
                string title = ((BooleanButton)sender).Content.ToString();
                ShowcaseEvent(title);
            }              
        }
        private void ShowcaseWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ShowcaseEvent("Stop"); ShowcaseEvent("Closed");
        }
    }
}