using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
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
    /// Interaction logic for MainWindow.xaml  -> test 2
    /// </summary>
    public partial class MainWindow : Window
    {     
        public MainWindow()
        {
            InitializeComponent();
            tmprCompX.OnLog += new TmprCompClass.LogHandler(log);
            tmprCompY.OnLog += new TmprCompClass.LogHandler(log);
            JoinOptim1.OnLog += new JoinOptimClass.LogHandler(log);
            QuantVsMems1.OnLog += new QuantVsMems.LogHandler(log);
            QuantVsMems1.QMfit.OnLog += new QMfitUC.LogHandler(log);

            QuantVsMems1.OnProgress += new QuantVsMems.ProgressHandler(progress);
        }

        private void btnLogClear_Click(object sender, RoutedEventArgs e)
        {
            tbLog.Document.Blocks.Clear();
        }

        private void log(string txt, bool detail = true, SolidColorBrush clr = null)
        {
            if (!chkLog.IsChecked.Value) return;
            string printOut;
            if ((chkDetail.IsChecked.Value) || (txt.Length < 81)) printOut = txt;
            else printOut = txt.Substring(0, 80) + "..."; //
            if (detail)
            {
                if (chkDetail.IsChecked.Value) Utils.log(tbLog, txt, clr);
            }
            else Utils.log(tbLog, txt, clr);
        }

        private int progIdx = 0; private int progMax = 0;
        // prog <> 0 the iteration counter (progIdx) is set to 0
        // -2: hide progress bar
        // -1: normal count including progress bar, when the final count is unknown
        // 0: one iteration (moves the iteration count 1 up)
        // n: (n>0) the expected number of iterations, with progress bar
        private void progress(int prog = 0) // 
        {
            if (prog == -2)
            {
                lbProgBar.Visibility = Visibility.Collapsed; progBar.Visibility = Visibility.Collapsed;
            }
            else
            {
                lbProgBar.Visibility = Visibility.Visible; progBar.Visibility = Visibility.Visible;
            }
            if (prog == -1)
            {
                progMax = prog; progIdx = 0; lbProgBar.Content = "0"; progBar.Value = 0; progBar.Maximum = 30; 
            }
            if (prog > 0)
            {
                progMax = prog; progIdx = 0; lbProgBar.Content = "0 %"; progBar.Value = 0; progBar.Maximum = prog; 
            }
            if (prog == 0)
            {
                progIdx++;
                if (progMax < 0) // max unknown
                {
                    lbProgBar.Content = progIdx.ToString(); progBar.Value = (progIdx % 30);
                }
                if (progMax > 0) 
                {
                    lbProgBar.Content = (100.0 * progIdx/progMax).ToString("G3")+"%"; progBar.Value = progIdx;
                }
            }
        }

        private void AxelDataWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key.Equals(Key.F1)) System.Diagnostics.Process.Start("http://www.axelsuite.com");
        }

        private void AxelDataWindow_Loaded(object sender, RoutedEventArgs e)
        {
            QuantVsMems1.Initialize();
            tmprCompY.Initialize();
            JoinOptim1.Initialize();
        }
    }
}
