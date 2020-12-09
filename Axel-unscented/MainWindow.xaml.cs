using System;
using System.Collections.Generic;
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
using UtilsNS;

namespace Axel_unscented
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            kalmanUC1.OnLog += new kalmanUC.LogHandler(log);
        }
        private void btnLogClear_Click(object sender, RoutedEventArgs e)
        {
            tbLog.Document.Blocks.Clear();
        }
        private void log(string txt, bool detail = true, Color? clr = null)
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

        private void Axel_ubscened_Loaded(object sender, RoutedEventArgs e)
        {
            kalmanUC1.Init();
        }
    }
}
