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
        public Dictionary<string, double> opts { get; set; }
        public void Init(Dictionary<string, double> _opts)
        {
            if (Utils.isNull(_opts)) return;
            opts = new Dictionary<string, double>(_opts);
            if (opts.Count.Equals(0)) return;
        }
        public void Final()
        {
            if (Utils.isNull(opts)) opts = new Dictionary<string, double>();
            opts["moduleIdx"] = 2;
        }
        #region Events
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
        public void log(string txt, bool detail)
        {
            OnLog(new OptimEventArgs("", detail ? -1 : 1, txt));
        }
        public event EventHandler EndOptimEvent;
        protected virtual void OnEndOptim(OptimEventArgs e)
        {
            EndOptimEvent?.Invoke(this, e);
        }
        #endregion Events
        public optimState state { get; set; }
        public List<baseMMscan> scans { get; set; }
        public string report(bool lastIter)
        {
            return "";
        }

        public void Optimize(bool? start, List<baseMMscan> _scans, Dictionary<string, double> opts)
        {
            scans = new List<baseMMscan>(_scans);           
        }
        #endregion Common

    }
}
