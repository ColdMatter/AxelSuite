using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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
using System.Data;
using System.ComponentModel;
using System.Collections.ObjectModel;
using Newtonsoft.Json;
using dotMath;
using UtilsNS;

namespace Axel_hub.PanelsUC
{
    public delegate double CostEventHandler(object sender, EventArgs e);
    public enum optimState { idle, running, cancelRequest, paused }
    public interface IOptimization
    {
        Dictionary<string, double> opts { get; set; }
        void Init(Dictionary<string, double> _opts);
        void Final();
        optimState state { get; set; }

        event EventHandler ParamSetEvent;
        event CostEventHandler TakeAShotEvent;
        event EventHandler LogEvent;
        event EventHandler EndOptimEvent;
        void log(string txt, bool detail);
        List<baseMMscan> scans { get; set; }
        string report(bool lastIter);
        void Optimize(bool? start, List<baseMMscan> _prms, Dictionary<string,double> opts); 
    } 

    public class OptimEventArgs : EventArgs
    {
        public string Prm;
        public double Value;
        public string Text;
        public OptimEventArgs(string _Prm, double _Value, string _Text)
        {
            Prm = _Prm; Value = _Value; Text = _Text;
        }
    }
 
    public class EnabledMMscan : baseMMscan
    {
        public EnabledMMscan()
        {            
            Enabled = true; // default
        }
        public EnabledMMscan(string inStr, bool _Enabled = true)
        {
            base.setAsString(inStr);
            Enabled = _Enabled; // default
        }
        public bool Enabled { get; set; } 
        public EnabledMMscan clone()
        {
            return new EnabledMMscan(getAsString(), Enabled);
        }
    }
    public class OptimSetting
    {
        public List<EnabledMMscan> sParams;
        public string cost;
        public Dictionary<string, double> opts;
        public List<Dictionary<string, double>> procOpts;
        public OptimSetting()
        {
            sParams = new List<EnabledMMscan>();
            opts = new Dictionary<string, double>();
        }
    }
    /// <summary>
    /// Interaction logic for simplexUC.xaml
    /// </summary>
    public partial class OptimUC_Class : UserControl
    {
        Dictionary<string, double> mmParams;
        public OptimSetting os;
        List<IOptimization> optimProcs;

        public OptimUC_Class()
        {
            InitializeComponent();
            os = new OptimSetting(); 

            optimProcs = new List<IOptimization>();
            optimProcs.Add(SeqScanUC);
            optimProcs.Add(GridScanUC);
            optimProcs.Add(OptSimplexUC);
            foreach(IOptimization io in optimProcs)
            {
                io.LogEvent += new EventHandler(LogEvent);
                io.ParamSetEvent += new EventHandler(ParamSetEvent);
                io.TakeAShotEvent += new CostEventHandler(TakeAShotEvent);
                io.EndOptimEvent += new EventHandler(EndOptimEvent);
            }
            if (Utils.TheosComputer())
            {
                Dictionary<string, object> dct = new Dictionary<string, object>();
                dct.Add("Param1", 1.11);
                dct.Add("Param2", 2.22);
                dct.Add("Param3", 3.33);
                Init(dct);
            }
        }
        #region Settings
        public void Init(Dictionary<string, object> _mmParams)
        {
            if (Utils.isNull(_mmParams)) { log("Err: Cannot load parameters (check connection to MM2)."); IsEnabled = false; return; }
            IsEnabled = true;
            OpenSetting();

            Update(_mmParams);
            paramList = new ObservableCollection<string>();
            foreach (var prm in mmParams)
                paramList.Add(Convert.ToString(prm.Key));

            updateDataTable(os.sParams);
            this.DataContext = this;
            if (Utils.isNull(os.procOpts)) return;
            for (int i = 0; i < optimProcs.Count; i++)
            {
                if (os.procOpts.Count < i) continue;
                optimProcs[i].Init(os.procOpts[i]);
            }
        }
        public void Final()
        {
            if (!IsEnabled) return;
            ClearStatus();

            foreach (IOptimization io in optimProcs)
                io.Final();
            if (Utils.isNull(os.procOpts)) os.procOpts = new List<Dictionary<string, double>>();
            else os.procOpts.Clear();
            for (int i = 0; i < optimProcs.Count; i++)
                os.procOpts.Add(new Dictionary<string, double>(optimProcs[i].opts));
            SaveSetting();
        }
        public void OpenSetting(string fn = "")
        {
            if (fn.Equals("")) fn = Utils.configPath + "Optim.CFG";
            if (!File.Exists(fn)) { log("Err: No file <" + fn + ">"); return; }
            string json = File.ReadAllText(fn);            
            os = JsonConvert.DeserializeObject<OptimSetting>(json);
        }

        public void SaveSetting(string fn = "")
        {
            if (fn.Equals("")) fn = Utils.configPath + "Optim.CFG";
            UpdateParamsFromTable();
            os.cost = tbCostFunc.Text;
            os.opts["ConvPrec"] = numConvPrec.Value;
            string fileJson = JsonConvert.SerializeObject(os);
            File.WriteAllText(fn, fileJson);
        }
        #endregion Setting

        public void Update(Dictionary<string, object> _mmParams) // parameters from MM2
        {
            mmParams = new Dictionary<string, double>();
            foreach (var prm in _mmParams)
                mmParams.Add(prm.Key, Convert.ToDouble(prm.Value));
        }
        public int sParamIdx(string prmName) // idx from dt
        {
            var rslt = -1;
            for (int i = 0; i < dt.Rows.Count; i++)
            {
                if (prmName.Equals(dt.Rows[i].ItemArray[1]))
                {
                    rslt = i; break;
                }
            }
            return rslt;
        }

        #region Events
        protected void LogEvent(object sender, EventArgs e)
        {
            OptimEventArgs ex = (OptimEventArgs)e;
            if (ex.Value > 0) log(ex.Text, Brushes.Maroon);
            else 
                if (chkDetails.IsChecked.Value) log(ex.Text, Brushes.Navy);
        }

        protected void log(string txt, SolidColorBrush clr = null)
        {
            if (txt.Substring(0, 3).Equals("Err")) { Utils.log(richLog, txt, Brushes.Red); return; } // errors always
            if (chkLog.IsChecked.Value) Utils.log(richLog, txt, clr);
        }

        protected void ParamSetEvent(object sender, EventArgs e) // update visual (dt) and internal (os.sParams)
        {
            OptimEventArgs ex = (OptimEventArgs)e;
            int j = sParamIdx(ex.Prm);
            if (j == -1)
            {
                log("Err: No such parameter (" + ex.Prm + ")"); return;
            }
            var tr = dt.Rows[j].ItemArray;
            if (!Double.IsNaN(ex.Value))
            {
                os.sParams[j].Value = ex.Value; tr[5] = ex.Value; //log("set value of " + ex.Prm + " at " + ex.Value.ToString("G4")); 
            }
            if (!ex.Text.Equals(""))
            {
                os.sParams[j].comment = ex.Text; tr[6] = ex.Text; // for no text "---"
            }
            dt.Rows[j].ItemArray = tr;
            //this.DataContext = this;
            RaisePropertyChanged("dt");
        }

        public void ClearStatus()
        {
            foreach (EnabledMMscan prm in os.sParams)
            {
                ParamSetEvent(this, new OptimEventArgs(prm.sParam, Double.NaN, "---"));
            }
        }
        public delegate void SendMMexecHandler(MMexec mme);
        public event SendMMexecHandler SendMMexecEvent;
        protected virtual void OnSendMMexec(MMexec mme)
        {
            SendMMexecEvent?.Invoke(mme);
        }
        protected double TakeAShotEvent(object sender, EventArgs e)
        {
            double d, rslt = Double.NaN; OptimEventArgs ex = (OptimEventArgs)e;
            ParamSetEvent(sender, e); Thread.Sleep(30); Utils.DoEvents(); // update prm (both ways)
            
            if (Utils.TheosComputer())
            {
                rslt = 0; int j = 0; 
                foreach (EnabledMMscan prm in os.sParams)
                {
                    if (prm.Enabled)
                    {
                        d = prm.Value + 2 * j + Utils.Gauss01();
                        rslt +=  d * d; j++;
                    }                       
                }
                rslt = 150 - rslt; 
            }
            else
            {               
                Dictionary<string, double> dct = MMDataConverter.AverageShotSegments(TakeAShotMM(ex.Prm, ex.Value),true);
                // script it
                var compiler = new EquationCompiler(tbCostFunc.Text);
                var vns = compiler.GetVariableNames();
                foreach (string vn in vns)
                {
                    if (!dct.ContainsKey(vn)) { log("Err: No variable <" + vn + "> in stats"); return Double.NaN; }
                    compiler.SetVariable(vn, dct[vn]);
                }
                rslt = compiler.Calculate();
            }           
            if (chkDetails.IsChecked.Value) log("cost = " + rslt.ToString("G5")+" at "+ex.Prm+" = "+ex.Value.ToString("G5"), Brushes.Navy);
            return rslt;
        }

        protected MMexec TakeAShotMM(string Param, double Value)
        {
            MMexec mme = new MMexec("", "Axel-hub", "shoot");
            mme.prms[Param] = Value;
            
            OnSendMMexec(mme);
            // take incomming data...
            return new MMexec();
        }
        protected void EndOptimEvent(object sender, EventArgs e)
        {
            bcbOptimize.Value = false; 
            if (!Utils.isNull(e)) 
            {
                OptimEventArgs ex = (OptimEventArgs)e;
                log("...and the optimization result is (" + ex.Text+" )", Brushes.Teal);
            }
        }
        #endregion Events

        #region table stuff
        private DataTable _dt;
        public DataTable dt
        {
            get { return _dt; }
            set
            {
                _dt = value;
                RaisePropertyChanged("dt");
            }
        }

        public ObservableCollection<string> paramList { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public void RaisePropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
            double h = topGrid.RowDefinitions[0].Height.Value + (dt.Rows.Count + 1) * dgParams.RowHeight + 10 + topGrid.RowDefinitions[2].Height.Value;
            topRow.Height = new GridLength(h);
        }

        protected DataTable updateDataTable(List<EnabledMMscan> prms)
        {
            _dt = new DataTable();
            _dt.Columns.Add("Enabled", typeof(bool));
            _dt.Columns.Add("Parameter", typeof(string));
            _dt.Columns.Add("From", typeof(double));
            _dt.Columns.Add("To", typeof(double));
            _dt.Columns.Add("By", typeof(double));
            _dt.Columns.Add("Value", typeof(double));
            _dt.Columns.Add("Status", typeof(string));

            foreach (EnabledMMscan prm in prms)
            {
                _dt.Rows.Add(new object[] { prm.Enabled, prm.sParam, prm.sFrom, prm.sTo, prm.sBy, prm.Value, prm.comment });
            }
            RaisePropertyChanged("dt");
            return _dt;
        }
        
        public bool UpdateParamsFromTable() // return validation check
        {
            DataView view = (DataView)dgParams.ItemsSource;
            if (Utils.isNull(view)) return false;
            DataTable dataTable = view.Table.Clone(); bool bb = true;
            os.sParams.Clear(); EnabledMMscan mms; 
            foreach (DataRowView dataRowView in view)
            {
                DataRow dr = dataRowView.Row;
                mms = new EnabledMMscan();
                mms.Enabled = Convert.ToBoolean(dr.ItemArray[0]);
                mms.sParam = Convert.ToString(dr.ItemArray[1]);
                if (mms.sParam.Equals("")) { log("Err: Parameter name is missing"); bb = false; continue; }
                mms.sFrom = Convert.ToDouble(dr.ItemArray[2]);
                mms.sTo = Convert.ToDouble(dr.ItemArray[3]);
                if (mms.sFrom > mms.sTo) { log("Err: Wrong order of limits"); bb = false; continue; }
                mms.sBy = Convert.ToDouble(dr.ItemArray[4]);
                int np = Convert.ToInt32((mms.sTo - mms.sFrom) / mms.sBy);
                if (np < 2) { log("Err: Too few points -> "+np.ToString()); bb = false; continue; }
                if (np > 1000) { log("Err: Too many (max=1000) points -> " + np.ToString()); bb = false; continue; }
                mms.Value = Convert.ToDouble(dr.ItemArray[5]);
                mms.comment = Convert.ToString(dr.ItemArray[6]);
                os.sParams.Add(mms);
            }
            return bb;
        }
        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (sender == btnAdd)
            {
                DataRow newRow = dt.NewRow();
                // Insert code to fill the row with values. (optional)

                // Add the row to the DataRowCollection.
                dt.Rows.Add(newRow);
            }
            else
            {
                int idx = dgParams.SelectedIndex;
                if (idx > -1) dt.Rows.RemoveAt(idx);
            }
            RaisePropertyChanged("dt");
        }
        private void btnUp_Click(object sender, RoutedEventArgs e)
        {
            if (dgParams.SelectedIndex == -1) { log("Err: No row selected"); return; }
            int idx = dgParams.SelectedIndex;
            int np = (sender == btnUp) ? idx - 1 : idx + 1;
            if (!Utils.InRange(np, 0,dt.Rows.Count-1)) { log("Err: Moving out of range"); return; }
            DataRow selectedRow = dt.Rows[idx];
            DataRow newRow = dt.NewRow();
            newRow.ItemArray = selectedRow.ItemArray; // copy data
            dt.Rows.Remove(selectedRow);
            dt.Rows.InsertAt(newRow, np); 
        }
        #endregion table stuff
        private void bcbOptimize_Click(object sender, RoutedEventArgs e)
        {
            bcbOptimize.Value = !bcbOptimize.Value;  
            List<baseMMscan> sPrms = new List<baseMMscan>(); var opts = new Dictionary<string, double>();
            if (bcbOptimize.Value)
            {
                if (!UpdateParamsFromTable()) { bcbOptimize.Value = false; return; }
                ClearStatus();
                foreach (EnabledMMscan mms in os.sParams)
                {
                    if (mms.Enabled) sPrms.Add(new baseMMscan(mms.getAsString()));
                }
                opts["ConvPrec"] = numConvPrec.Value;
            }
            int idx = tcOptimProcs.SelectedIndex;
            optimProcs[idx].Optimize(bcbOptimize.Value, sPrms, opts);
            if (!bcbOptimize.Value && optimProcs[idx].state.Equals(optimState.cancelRequest))
            {
                log("User interruption !!!", Brushes.Tomato); 
            }
        }
        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            richLog.Document.Blocks.Clear();
        }
    }
}
