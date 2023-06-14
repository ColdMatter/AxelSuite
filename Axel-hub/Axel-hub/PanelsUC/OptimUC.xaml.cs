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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Data;
using System.ComponentModel;
using System.Collections.ObjectModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using dotMath;
using UtilsNS;
using System.Windows.Threading;

namespace Axel_hub.PanelsUC
{
    public delegate double CostEventHandler(object sender, EventArgs e);
    public enum optimState { idle, running, cancelRequest, paused }
    public interface IOptimization
    {
        Dictionary<string, double> opts { get; set; }
        void Init(Dictionary<string, double> _opts, bool report_enabled);
        void Final();
        optimState state { get; set; }

        event EventHandler ParamSetEvent;
        event CostEventHandler TakeAShotEvent;
        event EventHandler LogEvent;
        event EventHandler EndOptimEvent;
        void log(string txt, bool detail);
        List<baseMMscan> scans { get; set; }
        string objFunc { get; set; }
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
        public bool rpr_enabled;
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
        public bool simulation = Utils.TheosComputer();

        Dictionary<string, double> mmParams;
        public OptimSetting os;
        List<IOptimization> optimProcs;
        string SettingFile = "";
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
            else
            {
                rowSimul.Height = new GridLength(1);
                mmParams = ((JObject)_mmParams["params"]).ToObject<Dictionary<string, double>>();
            }
                          
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

            if (os.convOpts.ContainsKey("OptimMode")) tcOptimProcs.SelectedIndex = Convert.ToInt32(os.convOpts["OptimMode"]);
            if (optimProcs.Count != os.procOpts.Count)  { log("Err: some optimization options missing."); return; }
            chkRpr.IsChecked = os.rpr_enabled;
            for (int i = 0; i < optimProcs.Count; i++)
                optimProcs[i].Init(os.procOpts[i], os.rpr_enabled);
        }
        public void Final()
        {
            if (!IsEnabled || SettingFile.Equals("")) return;
            ClearStatus();
            os.objFunc = cbObjectiveFunc.Text;
            os.convOpts["ConvPrec"] = numConvPrec.Value;

            os.convOpts["OptimMode"] = tcOptimProcs.SelectedIndex;
            foreach (IOptimization io in optimProcs)
                io.Final();
            if (Utils.isNull(os.procOpts)) os.procOpts = new List<Dictionary<string, double>>();
            else os.procOpts.Clear();
            for (int i = 0; i < optimProcs.Count; i++)
                os.procOpts.Add(new Dictionary<string, double>(optimProcs[i].opts));
            os.rpr_enabled = chkRpr.IsChecked.Value;
            SaveSetting();
        }
        public void OpenSetting(string fn = "")
        {
            if (fn.Equals("")) SettingFile = Utils.configPath + "Optim.CFG";
            else SettingFile = fn;
            if (!File.Exists(SettingFile)) { log("Err: No file <" + SettingFile + ">"); return; }
            string json = File.ReadAllText(SettingFile);            
            os = JsonConvert.DeserializeObject<OptimSetting>(json);
        }
        public void SaveSetting(string fn = "")
        {
            if (fn.Equals("")) 
                fn = SettingFile.Equals("") ? Utils.configPath + "Optim.CFG" : SettingFile;
            UpdateParamsFromTable();            
            string fileJson = JsonConvert.SerializeObject(os);
            File.WriteAllText(fn, fileJson);
        }
        #endregion Setting
        public int sParamIdx(string prmName) // idx from name
        {
            var rslt = -1;
            for (int i = 0; i < os.sParams.Count; i++)
            {
                if (prmName.Equals(os.sParams[i].sParam))
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
            if (ex.Value > 0) log(ex.Text, Brushes.DarkBlue);
            else 
                if (chkDetails.IsChecked.Value) log(ex.Text, Brushes.DarkGreen);
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
            if (j < dt.Rows.Count)
            {
                var tr = dt.Rows[j].ItemArray;
                tr[1] = ex.Prm;
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
                
                RaisePropertyChanged("dt"); 
                Utils.DoEvents(); 
            }
            else 
            {
                //log("Err: internal #4387"); 
            }
            
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
        protected double TakeAShotEvent(object sender, EventArgs e) // e is OptimEventArgs
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
                        rslt = Utils.NextGaussian(150 - rslt, numGaussNoise.Value); 
                        break;
                }
                foreach (IOptimization io in optimProcs)
                    io.objFunc = os.objFunc;
            }
            else
            {
                MMexec mme = TakeAShotMM();
                if (Utils.isNull(mme)) { log("Error: no replay from MM2."); return Double.NaN; }
                Dictionary<string, double> dct = MMDataConverter.AverageShotSegments(mme,true);
                // script it
                os.objFunc = Utils.skimRem(cbObjectiveFunc.Text);
                foreach (IOptimization io in optimProcs)
                    io.objFunc = os.objFunc;
                var compiler = new EquationCompiler(os.objFunc);
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
        protected MMexec TakeAShotMM(bool getData = true) // arg not used; params taken from os.sParams
        {
            MMexec mme = new MMexec("", "Axel-hub", getData ? "shoot": "set"); //mme.prms[Param] = Value;
            foreach(var os1 in os.sParams)
            {
                if (os1.Enabled)
                    mme.prms[os1.sParam] = os1.Value; // take pair from os
            }           
            return OnSendMMexec(mme);
        }
        protected void EndOptimEvent(object sender, EventArgs e)
        {
            TakeAShotMM(false);
            bcbOptimize.Value = false; 
            if (!Utils.isNull(e)) 
            {
                OptimEventArgs ex = (OptimEventArgs)e;
                if (!ex.Text.Equals(""))
                    log("...and the optimization result is -> " + ex.Text, Brushes.Maroon);
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
            dgParams.ItemsSource = dt.DefaultView;
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
            bool bb = true; int k = 0;
            DataView view = (DataView)dgParams.ItemsSource;
            if (Utils.isNull(view)) return false;
            os.sParams.Clear(); EnabledMMscan mms;
            foreach (DataRowView dataRowView in view)
            {
                k++;
                DataRow dr = dataRowView.Row; mms = new EnabledMMscan();
                try
                {
                    var chk = DataGridHelper.GetCellByIndices(dgParams, k-1, 0).FindVisualChild<CheckBox>();
                    if (Utils.isNull(chk)) return false;
                    if (chk.IsChecked == null) chk.IsChecked = true;
                    mms.Enabled = Convert.ToBoolean(chk.IsChecked);
                }
                catch (InvalidCastException e)
                {
                    log("Err: undefine Checked state at param.#" + k.ToString());
                    mms.Enabled = false; bb = false; continue;
                }
                try
                {                      
                    var cb = DataGridHelper.GetCellByIndices(dgParams, k-1, 1).FindVisualChild<ComboBox>();
                    if (Utils.isNull(cb)) return false;
                    mms.sParam = Convert.ToString(cb.SelectedValue); dr.ItemArray[1] = mms.sParam;
                    if (mms.sParam.Equals("")) { log("Err: Parameter name is missing at param.#" + k.ToString()); bb = false; continue; }
                    mms.sFrom = Convert.ToDouble(dr.ItemArray[2]);
                    mms.sTo = Convert.ToDouble(dr.ItemArray[3]);
                    if (mms.sFrom > mms.sTo) { log("Err: Wrong order of limits at param.#" + k.ToString()); bb = false; continue; }
                    mms.sBy = Convert.ToDouble(dr.ItemArray[4]);
                    int np = Convert.ToInt32((mms.sTo - mms.sFrom) / mms.sBy);
                    if (np < 2) { log("Err: Too few points -> " + np.ToString() + " at param.#" + k.ToString()); bb = false; continue; }
                    if (np > 1000) { log("Err: Too many (max=1000) points -> " + np.ToString() + " at param.#" + k.ToString()); bb = false; continue; }
                }
                catch (InvalidCastException e)
                {
                    log("Err: wrong limits/step at param.#" + k.ToString());
                    bb = false; continue;
                }
                try
                {
                    mms.Value = Convert.ToDouble(dr.ItemArray[5]);
                }
                catch (InvalidCastException e)
                {
                    log("Err: missing Value at param.#" + k.ToString());
                    bb = false; continue;
                }
                try
                {
                    mms.comment = Convert.ToString(dr.ItemArray[6]);
                }
                catch (InvalidCastException e)
                {
                    mms.comment = "";
                }
                os.sParams.Add(mms);
            }
            return bb;
        }
        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (sender == btnAdd) // add row
            {
                _dt.Rows.Add(_dt.NewRow());
                int j = _dt.Rows.Count - 1;
                var tr = _dt.Rows[j].ItemArray;
                tr[0] = true; tr[2] = -5; tr[3] = 5; tr[4] = 1; tr[5] = 0; tr[6] = "---";
                _dt.Rows[j].ItemArray = tr;
            }
            else // delete row
            {
                if (dt.Rows.Count.Equals(0))
                {
                    log("Err: no param row to remove"); return;
                } 
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
            // copy data
            //selectedRow.ItemArray.CopyTo(newRow.ItemArray,0); 
            //Array.Copy(selectedRow.ItemArray, newRow.ItemArray, 7);
            newRow.ItemArray = selectedRow.ItemArray;
            dt.Rows.Remove(selectedRow);
            dt.Rows.InsertAt(newRow, np); 
        }
        #endregion table stuff
        private void bcbOptimize_Click(object sender, RoutedEventArgs e)
        {
            if (Utils.isNull(cbObjectiveFunc.Text)) { log("Err: No objective function"); return; }
            if (cbObjectiveFunc.Text.Equals("")) { log("Err: No objective function"); return; }
            bool oldOptimizeValue = bcbOptimize.Value;
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
            if (oldOptimizeValue && !bcbOptimize.Value && optimProcs[idx].state.Equals(optimState.cancelRequest))
            {
                log("User cancelation !!!", Brushes.Tomato); 
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
