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
    /// Interaction logic for OptimSimplexUC.xaml
    /// </summary>
    public partial class OptimSimplexUC : UserControl, IOptimization
    {
        public OptimSimplexUC()
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
        public List<baseMMscan> prms { get; set; }
        public string Optimize(bool start, ref List<baseMMscan> _prms, Dictionary<string, double> opts)
        {
            prms = _prms;
            return "";
        }
        #endregion Common

    }
}
