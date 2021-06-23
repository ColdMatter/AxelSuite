using System;
using System.Collections.Generic;
using System.IO;
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
using System.Data;
using System.ComponentModel;
using System.Collections.ObjectModel;
using Newtonsoft.Json;
using UtilsNS;

namespace Axel_hub.PanelsUC
{
    public delegate double CostEventHandler(object sender, EventArgs e);
    public interface IOptimization
    {
        event EventHandler ParamSetEvent;
        event CostEventHandler TakeAShotEvent;
        event EventHandler LogEvent;
        List<baseMMscan> prms { get; set; }
        string Optimize(bool start, ref List<baseMMscan> _prms, Dictionary<string,double> opts); 
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
        public OptimSetting()
        {
            sParams = new List<EnabledMMscan>();
            opts = new Dictionary<string, double>();
        }
    }
    /// <summary>
    /// Interaction logic for simplexUC.xaml
    /// </summary>
    public partial class optimUC : UserControl
    {
        bool simulation = true;
        Dictionary<string, double> gParams;
        OptimSetting os;
        List<IOptimization> optimProcs;

        public optimUC()
        {
            InitializeComponent();
            os = new OptimSetting(); Init();

            optimProcs = new List<IOptimization>();
            optimProcs.Add(SeqScanUC);
            optimProcs.Add(GridScanUC);
            optimProcs.Add(OptSimplexUC);
            foreach(IOptimization io in optimProcs)
            {
                io.LogEvent += new EventHandler(LogEvent);
                io.ParamSetEvent += new EventHandler(ParamSetEvent);
                io.TakeAShotEvent += new CostEventHandler(TakeAShotEvent);
            }           
        }
        #region Settings
        public void Init()
        {
            //OpenSetting();
            os.sParams.Add(new EnabledMMscan("Param1" + "\t" + "-10..10;1=2.5#some text 1"));
            os.sParams.Add(new EnabledMMscan("Param2" + "\t" + "-10..10;1=3.5#some text 2"));
            os.sParams.Add(new EnabledMMscan("Param3" + "\t" + "-10..10;1=4.5#some text 3"));

            //dgParams.ItemsSource = GetDataTable(sParams).DefaultView;
            paramList = new ObservableCollection<string>() { "Param1", "Param2", "Param3" };
            updateDataTable(os.sParams);
            this.DataContext = this;
        }
        public void Final()
        {
            SaveSetting();
        }
        public void OpenSetting(string fn = "")
        {
            if (fn.Equals("")) fn = Utils.configPath + "Optim.CFG";
            if (!File.Exists(fn)) { log("Err: No file <" + fn + ">", Brushes.Red); return; }
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

        public void Update(Dictionary<string, double> _gParams) // parameters from MM2
        {
            gParams = new Dictionary<string, double>(_gParams);
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
            log(((OptimEventArgs)e).Text);
        }

        protected void log (string txt, SolidColorBrush clr = null)
        {
            Utils.log(richLog, txt, clr);
        }

        protected void ParamSetEvent(object sender, EventArgs e)
        {
            OptimEventArgs ex = (OptimEventArgs)e;
            int j = sParamIdx(ex.Prm);
            if (j == -1)
            {
                log("Err: No such parameter (" + ex.Prm + ")", Brushes.Red); return;
            }
            var tr = dt.Rows[j].ItemArray;
            if (!Double.IsNaN(ex.Value))
            {
                os.sParams[j].Value = ex.Value; tr[5] = ex.Value; log("set value of " + ex.Prm + " at " + ex.Value.ToString("G4")); 
            }
            if (!ex.Text.Equals(""))
            {
                os.sParams[j].comment = ex.Text; tr[6] = ex.Text; // for no text "---"
            }
            dt.Rows[j].ItemArray = tr;
            RaisePropertyChanged("dt");
        }

        protected double TakeAShotEvent(object sender, EventArgs e)
        {
            double rslt = Double.NaN;
            ParamSetEvent(sender, e);
            if (simulation)
            {
                rslt = 0;
                foreach (EnabledMMscan prm in os.sParams)
                {
                    rslt += prm.Value * prm.Value; 
                }
                rslt = 150 - rslt; 
            }
            else
            { 
                OptimEventArgs ex = (OptimEventArgs)e;
                Dictionary<string, double> dct = MMDataConverter.AverageShotSegments(TakeAShotMM(ex.Prm, ex.Value),true);
                // script it
                rslt = 1;
            }
            log("cost = " + rslt.ToString("G5"), Brushes.Navy);
            return rslt;
        }

        protected MMexec TakeAShotMM(string Param, double Value)
        {
            return new MMexec();
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
        
        public bool UpdateParamsFromTable()
        {
            DataView view = (DataView)dgParams.ItemsSource;
            DataTable dataTable = view.Table.Clone(); bool bb = true;
            os.sParams.Clear(); EnabledMMscan mms; 
            foreach (DataRowView dataRowView in view)
            {
                DataRow dr = dataRowView.Row;
                mms = new EnabledMMscan();
                mms.Enabled = Convert.ToBoolean(dr.ItemArray[0]);
                mms.sParam = Convert.ToString(dr.ItemArray[1]);
                if (mms.sParam.Equals("")) { log("Err: Parameter name is missing", Brushes.Red); bb = false; continue; }
                mms.sFrom = Convert.ToDouble(dr.ItemArray[2]);
                mms.sTo = Convert.ToDouble(dr.ItemArray[3]);
                if (mms.sFrom > mms.sTo) { log("Err: Wrong order of limits", Brushes.Red); bb = false; continue; }
                mms.sBy = Convert.ToDouble(dr.ItemArray[4]);
                int np = Convert.ToInt32((mms.sTo - mms.sFrom) / mms.sBy);
                if (np < 2) { log("Err: Too few points -> "+np.ToString(), Brushes.Red); bb = false; continue; }
                if (np > 1000) { log("Err: Too many (max=1000) points -> " + np.ToString(), Brushes.Red); bb = false; continue; }
                mms.Value = Convert.ToDouble(dr.ItemArray[5]);
                mms.comment = Convert.ToString(dr.ItemArray[6]);
                os.sParams.Add(mms);
            }
            return bb;
        }
        private void btnDown_Click(object sender, RoutedEventArgs e)
        {
            UpdateParamsFromTable();
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
            if (dgParams.SelectedIndex == -1) { log("No row selected", Brushes.Red); return; }
            int idx = dgParams.SelectedIndex;
            int np = (sender == btnUp) ? idx - 1 : idx + 1;
            if (!Utils.InRange(np, 0,dt.Rows.Count-1)) { log("Moving out of range", Brushes.Red); return; }
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
                foreach (EnabledMMscan mms in os.sParams)
                {
                    if (mms.Enabled) sPrms.Add(new baseMMscan(mms.getAsString()));
                }
                opts["ConvPrec"] = numConvPrec.Value;
            }           
            optimProcs[tcOptimProcs.SelectedIndex].Optimize(bcbOptimize.Value, ref sPrms, opts);
            bcbOptimize.Value = false;
        }
    }
}
