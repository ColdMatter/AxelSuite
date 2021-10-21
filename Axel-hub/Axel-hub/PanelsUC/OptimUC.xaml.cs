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
using Newtonsoft.Json.Linq;
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
        public string objFunc;
        public Dictionary<string, double> convOpts;
        public List<Dictionary<string, double>> procOpts;
        public OptimSetting()
        {
            sParams = new List<EnabledMMscan>();
            convOpts = new Dictionary<string, double>();
            procOpts = new List<Dictionary<string, double>>();
        }
    }
    /// <summary>
    /// Interaction logic for simplexUC.xaml
    /// </summary>
    public partial class OptimUC_Class : UserControl
    {
        public bool simulation = false; // Utils.TheosComputer();

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
            if (simulation) Init(new Dictionary<string, object>());
            if (Utils.TheosComputer()) tiSimplex.Visibility = Visibility.Visible;
            else tiSimplex.Visibility = Visibility.Collapsed;
        }
        #region Settings
        public void Init(Dictionary<string, object> _mmParams)
        {
            if (Utils.isNull(_mmParams)) { log("Err: Cannot load parameters (check connection to MM2)."); IsEnabled = false; return; }
            if (!simulation && !_mmParams.ContainsKey("params")) { log("Err: Cannot load parameters (params are missing)."); IsEnabled = false; return; }
            IsEnabled = true; paramList = new ObservableCollection<string>();
            OpenSetting();
            if (simulation)
            {
                mmParams = new Dictionary<string, double>();
                mmParams.Add("DetAttn", 1.11);
                mmParams.Add("DetFreq", 2.22);
                mmParams.Add("3DCoil", 3.33);

                rowSimul.Height = new GridLength(30);
                string[] pl = { "DetAttn", "DetFreq", "3DCoil" };
                paramList = new ObservableCollection<string>(pl);
            }
            else mmParams = ((JObject)_mmParams["params"]).ToObject<Dictionary<string, double>>();
            
            if (_mmParams.ContainsKey("scanPrms"))
            {
                string[] _paramList = ((JArray)_mmParams["scanPrms"]).ToObject<string[]>();
                paramList = new ObservableCollection<string>(_paramList);
            }
                          
            updateDataTable(os.sParams);
            this.DataContext = this;
            if (os.procOpts.Count.Equals(0)) return;
            cbObjectiveFunc.Text = os.objFunc;
            if (os.convOpts.ContainsKey("ConvPrec")) numConvPrec.Value = os.convOpts["ConvPrec"];

            if (optimProcs.Count != os.procOpts.Count)  { log("Err: some optimization options missing."); return; }
            for (int i = 0; i < optimProcs.Count; i++)
                optimProcs[i].Init(os.procOpts[i]);
        }
        public void Final()
        {
            if (!IsEnabled) return;
            ClearStatus();
            os.objFunc = cbObjectiveFunc.Text;
            os.convOpts["ConvPrec"] = numConvPrec.Value;

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
            string fileJson = JsonConvert.SerializeObject(os);
            File.WriteAllText(fn, fileJson);
        }
        #endregion Setting

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
                mmParams[ex.Prm] = ex.Value;
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
        public delegate MMexec SendMMexecHandler(MMexec mme);
        public event SendMMexecHandler SendMMexecEvent;
        protected virtual MMexec OnSendMMexec(MMexec mme)
        {
            return SendMMexecEvent?.Invoke(mme);
        }
        protected double TakeAShotEvent(object sender, EventArgs e)
        {
            double d, rslt = Double.NaN; OptimEventArgs ex = (OptimEventArgs)e;
            ParamSetEvent(sender, e); Thread.Sleep(100); Utils.DoEvents(); // update prm (here & in MM)
            
            if (simulation)
            {
                switch (cbSimulFunction.SelectedIndex)
                {
                    case 0: // quadratic
                        rslt = 0; int j = -1; 
                        foreach (EnabledMMscan prm in os.sParams)
                        {
                            if (prm.Enabled)
                            {
                                d = prm.Value + 2 * j;
                                rslt +=  d * d; j++;
                            }                       
                        }
                        rslt = 150 - rslt + numGaussNoise.Value*Utils.Gauss01(); 
                        break;
                }
            }
            else
            {
                MMexec mme = TakeAShotMM(ex.Prm, ex.Value);
                if (Utils.isNull(mme)) { log("Error: no replay from MM2."); return Double.NaN; }
                Dictionary<string, double> dct = MMDataConverter.AverageShotSegments(mme,true);
                // script it
                var compiler = new EquationCompiler(Utils.skimRem(cbObjectiveFunc.Text));                
                var vns = compiler.GetVariableNames();
                foreach (string vn in vns)
                {
                    if (!dct.ContainsKey(vn)) { log("Err: No variable <" + vn + "> in stats"); return Double.NaN; }
                    compiler.SetVariable(vn, dct[vn]);
                }
                rslt = compiler.Calculate();
            }           
            if (chkDetails.IsChecked.Value) log("obj.function = " + rslt.ToString("G5")+" at "+ex.Prm+" = "+ex.Value.ToString("G5"), Brushes.Navy);
            return rslt;
        }

        protected MMexec TakeAShotMM(string Param, double Value) // arg not used
        {
            MMexec mme = new MMexec("", "Axel-hub", "shoot"); //mme.prms[Param] = Value;
            foreach(var os1 in os.sParams)
            {
                if (os1.Enabled)
                    mme.prms[os1.sParam] = os1.Value; // take pair from os
            }           
            return OnSendMMexec(mme);
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
            if (Utils.isNull(cbObjectiveFunc.Text)) { log("Err: No objective function"); return; }
            if (cbObjectiveFunc.Text.Equals("")) { log("Err: No objective function"); return; }
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

        private void dgParams_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            btnDel.IsEnabled = (dgParams.SelectedIndex > -1);
            btnDown.IsEnabled = btnDel.IsEnabled; btnUp.IsEnabled = btnDel.IsEnabled;
        }
    }
}
