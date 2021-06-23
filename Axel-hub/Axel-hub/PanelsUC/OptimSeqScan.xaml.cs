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

namespace Axel_hub.PanelsUC
{
    /// <summary>
    /// Interaction logic for OptimSeqScan.xaml
    /// </summary>
    public partial class OptimSeqScan : UserControl, IOptimization
    {
        public OptimSeqScan()
        {
            InitializeComponent();
        }
        #region Common
        public event EventHandler ParamSetEvent;
        protected virtual void OnParamSet(OptimEventArgs e)
        {
            ParamSetEvent?.Invoke(this, e);
        }
        public event CostEventHandler TakeAShotEvent;
        protected virtual double OnTakeAShot(OptimEventArgs e)
        {
            if (Utils.isNull(TakeAShotEvent)) return Double.NaN;
            return TakeAShotEvent.Invoke(this, e);
        }
        public event EventHandler LogEvent;
        protected virtual void OnLog(OptimEventArgs e)
        {
            LogEvent?.Invoke(this, e);
        }
        bool CancelRequest = false;
        public List<baseMMscan> prms { get; set; }
        public string Optimize(bool start, ref List<baseMMscan> _prms, Dictionary<string, double> opts)      
        {
            CancelRequest = !start;
            if (CancelRequest) return "";
            prms = _prms;
            baseMMscan prm = prms[0];
            double d = prm.sFrom; //
            while ((d < prm.sTo * 1.0001) && !CancelRequest)
            {
                OnTakeAShot(new OptimEventArgs(prm.sParam, d, ""));
                bool bb = Utils.InRange(d, prm.sFrom * 0.999, prm.sTo * 1.0001);
                d += prm.sBy;
            }
            return ""; // report
        }
        #endregion Common
        private void button_Click(object sender, RoutedEventArgs e)
        {
            button.Content = OnTakeAShot(new OptimEventArgs("Male", 1.88, "more text")).ToString();


        }
    }
}
