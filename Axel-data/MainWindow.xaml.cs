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
    /// Interaction logic for MainWindow.xaml
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
        }

        private void btnLogClear_Click(object sender, RoutedEventArgs e)
        {
            tbLog.Document.Blocks.Clear();
        }

        private void log(string txt, Color? clr = null)
        {
            if (!chkLog.IsChecked.Value) return;
            string printOut;
            if ((chkVerbatim.IsChecked.Value) || (txt.Length < 81)) printOut = txt;
            else printOut = txt.Substring(0, 80) + "..."; //

            Utils.log(tbLog, printOut, clr);
        }
        /*            FileLogger fl = new FileLogger("", "C:/temp/test11.log");
                    DateTime start, finish; TimeSpan time;
                    string buff = Utils.randomString(1024);
                    int dly = (int)(1000 / tbiSpeed.Value); 
                    start = DateTime.Now; log("start at: " + start.ToString() +" / interval = "+dly.ToString());

                    fl.Enabled = true;          
                    for (int i = 0; i < 100000; i++)
                    {
                        //Thread.Sleep(dly);
                        fl.log(buff);
                    }
                    finish = DateTime.Now;
                    log("finish at: " + finish.ToString());
                    time = finish - start;
                    log("end of it: " + fl.stw.Elapsed.Milliseconds.ToString() + " / " + time.Seconds.ToString()+" [s]");
                    if (fl.missingData) log("skip some data!!!");
                    fl.Enabled = false;
                    fl = null;
         ===================================================================================
                     List<string> rec = new List<string>();
                    rec.Add("aaa"); rec.Add("bbb"); rec.Add("ccc"); 
                    DictFileLogger fl = new DictFileLogger(rec, "", "C:/temp/test12.log");

                    fl.Enabled = true;
                    Dictionary<string, double> row = new Dictionary<string, double>();
                    row["aaa"] = 111; row["bbb"] = 222; row["ccc"] = 333;
                    for (int i = 0; i < 1000; i++)
                    {              
                        fl.dictLog(row);
                    }
                    if (fl.missingData) log("skiped some data!!!");
                    fl.Enabled = false;
                    fl = null;
                    log("Done!");

        */
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            string[] rec = new string[]{"aaa","bbb","ccc"};
            DictFileReader fl = new DictFileReader("C:/temp/test12.log", rec);
       
            Dictionary<string, double> row = new Dictionary<string, double>();
            while (fl.doubleIterator(ref row))            
            {                              
                log(row["aaa"].ToString()+" | "+row["bbb"].ToString()+" | "+row["ccc"].ToString());               
            }
            fl = null;
            log("Done!");


        }
    }
}
