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
using NationalInstruments.Controls;
using dotMath;
using UtilsNS;

namespace Axel_hub.PanelsUC
{
    /// <summary>
    /// Interaction logic for MultiScanUC.xaml
    /// </summary>
    public partial class MultiScanUC : UserControl
    {
        public MultiScanUC()
        {
            InitializeComponent();
            
        }
        private void graphFringes_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            (sender as Graph).ResetZoomPan();
        }
        DictFileLogger logger;
        List<baseMMscan> scans;
        DataStack srsY1, srsY2;
        public void Init(MMexec mme)
        {
            List<string> ls = new List<string>();
            // script it
            scans = new List<baseMMscan>();
            for (int i = 1; i < 6; i++)
            {
                string p = "P"+i.ToString();
                if (mme.prms.ContainsKey(p)) scans.Add(new baseMMscan(Convert.ToString(mme.prms[p])));
                else continue;
                ls.Add(scans[i - 1].sParam);
            }
            srsY1 = new DataStack(); srsY2 = new DataStack();
            ls.Add("Xvalue");
            if (chkY1.IsChecked.Value) ls.Add("Y1value"); if (chkY2.IsChecked.Value) ls.Add("Y2value");
            logger = new DictFileLogger(ls.ToArray()); logger.defaultExt = ".msn";
        }
        public void Clear() // clear all but expressions
        {
            logger.Enabled = false;
            scans.Clear();
            srsY1.Clear(); srsY2.Clear();
            graphFringes.Data[0] = null; graphFringes.Data[1] = null;
            lbInfo.Content = "Info:";
            lbY1value.Content = "- - -"; lbY2value.Content = "- - -"; lbXvalue.Content = "- - -";
        } 
        public void NextShot(MMexec mme)
        {
            var compilerX = new EquationCompiler(tbX.Text);
            string sh = "Info: ";
            Dictionary<string, double> ddct = new Dictionary<string, double>();
            for (int i = 1; i < 6; i++)
            {
                if (Utils.InRange(i, 1,scans.Count)) break;
                string p = "P" + i.ToString();
                if (mme.prms.ContainsKey(p)) scans[i - 1].Value = Convert.ToDouble(mme.prms[p]);
                else continue;
                sh += scans[i - 1].sParam + " = " + scans[i - 1].Value + " ; ";
                compilerX.SetVariable(p, scans[i - 1].Value);
                compilerX.SetVariable(scans[i - 1].sParam, scans[i - 1].Value);
                ddct[scans[i - 1].sParam] = scans[i - 1].Value;
            }
            lbInfo.Content = sh;     
            
            double Xvalue = compilerX.Calculate(); lbXvalue.Content = Xvalue.ToString("G7"); ddct["Xvalue"] = Xvalue;

            var compilerY1 = new EquationCompiler(tbY1.Text); var compilerY2 = new EquationCompiler(tbY2.Text);
            Dictionary<string, double> dct = MMDataConverter.AverageShotSegments(mme, true);
            foreach (string dc in dct.Keys)
            {
                compilerY1.SetVariable(dc, dct[dc]); compilerY2.SetVariable(dc, dct[dc]);
            }
            if (chkY1.IsChecked.Value)
            {
                double Y1value = compilerY1.Calculate(); lbY1value.Content = Y1value.ToString("G7");
                srsY1.AddPoint(Y1value, Xvalue); graphFringes.Data[0] = srsY1; ddct["Y1value"] = Y1value;
            }
            if (chkY2.IsChecked.Value)
            {
                double Y2value = compilerY2.Calculate(); lbY2value.Content = Y2value.ToString("G7");
                srsY1.AddPoint(Y2value, Xvalue); graphFringes.Data[1] = srsY2; ddct["Y2value"] = Y2value;
            }
            logger.dictLog(ddct);
        }
    }
}
