using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows.Controls.Primitives;
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
using System.Timers;
using System.Windows.Threading;
using NationalInstruments.ModularInstruments.Interop;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

using UtilsNS;

namespace Axel_digit
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        niHSDIO hsTaskIn, hsTaskOut;
        List<NamedDIO> namedDIO; bool nLock = false; // Lock is on when internal change
        List<SingleDIO> allDIO; 
        List<ByteDIO> byteDIO; bool aLock = false;
        DispatcherTimer dTimer;
        readonly bool debugMode;
        bool hardwareSet = false;

        public MainWindow()
        {
            InitializeComponent();
            debugMode = false;
            readConfig();
            if (!configHardware(true))
            {
                if (Utils.TheosComputer()) ErrorMsg("Simulation");
                else ErrorMsg("Hardware problem");
            }           
        }

        #region COMMON
        string configFile = Utils.basePath + "\\config.json";
        Dictionary<string, object> config;
        public void readConfig()
        {
            string json = System.IO.File.ReadAllText(configFile);
            config = JsonConvert.DeserializeObject< Dictionary<string, object>>(json);

            var namedData = (JArray)config["named"];
            namedDIO = namedData.ToObject<List<NamedDIO>>();

            byteDIO = new List<ByteDIO>();
            var allData = (JArray)config["all"];
            allDIO = allData.ToObject<List<SingleDIO>>();
            all2byte();             
        }
        private void frmAxelDigit_Loaded(object sender, RoutedEventArgs e)
        {
            chkLog.IsChecked = Convert.ToBoolean(config["log"]); tbDvc.Text = Convert.ToString(config["device"]);

            Left = Convert.ToInt32(config["Left"]); Top = Convert.ToInt32(config["Top"]);
            Width = Convert.ToInt32(config["Width"]); Height = Convert.ToInt32(config["Height"]);
            tcMain.Tag = -1; // lock
            tcMain.SelectedIndex = Convert.ToInt32(config["tab"]);
            tcMain.Tag = 1; // unlock
        }

        private void frmAxelDigit_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            config["log"] = chkLog.IsChecked.Value; config["device"] = tbDvc.Text;

            config["Left"] = Left; config["Top"] = Top;
            config["Width"] = Width; config["Height"] = Height;

            config["tab"] = tcMain.SelectedIndex;
            config["named"] = namedDIO;
            byte2all();
            config["all"] = allDIO;
            string json = JsonConvert.SerializeObject(config);
            System.IO.File.WriteAllText(configFile, json);
        }

         public void addTextCol(DataGrid dg, string col)
        {
            DataGridTextColumn textcol = new DataGridTextColumn();
            //Create a Binding object to define the path to the DataGrid.ItemsSource property
            Binding b = new Binding(col);
            //Set the properties on the new column
            b.UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged;
            textcol.Binding = b;
            textcol.Header = col;
            dg.Columns.Add(textcol);
        }
        public void addCheckCol(DataGrid dg, string col)
        {
            DataGridCheckBoxColumn checkcol = new DataGridCheckBoxColumn();
            //Create a Binding object to define the path to the DataGrid.ItemsSource property
            Binding b = new Binding(col);
            //Set the properties on the new column
            b.UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged;
            checkcol.Binding = b;
            checkcol.Header = col;
            dg.Columns.Add(checkcol);
        }
        public string boolArr2string(bool[] bArray)
        {
            string rslt = "";
            for (int i = 0; i < bArray.Length; i++)
            {
                if (bArray[i])
                    rslt += '1';
                else
                    rslt += '0';
            }
            return rslt;
        }
        private uint BoolArrayToUInt(bool[] bArray)
        {
            string rsltStr = boolArr2string(bArray);
            return System.Convert.ToUInt32(rsltStr, 2);
        }
        public delegate void CheckChangeHandler(object sender, int ID, bool check);
        protected void OnCheckChange(object sender, int ID, bool check)
        {
            if (nLock || aLock) return;
            Utils.log(tbLog, "change #"+ID.ToString() + " to " + check.ToString(),Brushes.Maroon.Color);
            if (chkAutoUpdate.IsChecked.Value)
            {
                if (Utils.isNull(dTimer))
                {
                    dTimer = new DispatcherTimer();
                    dTimer.Tick += new EventHandler(dTimer_Tick);
                    dTimer.Interval = new TimeSpan(500 * 10000);
                }
                dTimer.Start();
                Utils.DoEvents();
            }            
        }
        private void dTimer_Tick(object sender, EventArgs e)
        {
            btnWriteOut_Click(null, null); dTimer.Stop();
        }

        public void dLog(string msg)
        {
            if (debugMode) Utils.log(tbLog, msg, Brushes.Teal.Color);
        }
        #endregion COMMON

        #region NamedDIO
        public class NamedDIO
        {
            public event CheckChangeHandler OnCheckChange;
            protected void CheckChange(object sender, int ID, bool check)
            {
                if (OnCheckChange != null) OnCheckChange(sender, ID, check);
            }

            public string DIO { get; set; }  public string Name { get; set; }  public bool Enabled { get; set; }  public bool ReadIn { get; set; }
            private bool bb;
            public bool WriteOut
            {
                get { return bb; }
                set
                {
                    bb = value; int idx;
                    if (!Int32.TryParse(DIO, out idx))
                        Utils.TimedMessageBox("Wrong DIO format for named channel " + Name, "Warning", 2500);                        
                    else        
                        if (Enabled) CheckChange((object)this, idx, bb);
                }
            }

        }
        public bool UpdateNamed(bool[] dt, bool inclOut) // "ReadIn" column and optionally Out
        {
            if (dt.Length != 32) return false;
            int idx; 
            for (int i = 0; i < namedDIO.Count; i++)
            {
                if(!Int32.TryParse(namedDIO[i].DIO, out idx))
                {
                    ErrorMsg("Wrong DIO format for named channel " + namedDIO[i].Name);
                    return false;
                }                  
                if (!Utils.InRange(idx, 0, 31))
                {
                    ErrorMsg("Index out of range for named channel " + namedDIO[i].Name);
                    return false;
                }
                namedDIO[i].ReadIn = dt[idx];
                nLock = true;
                if (inclOut) namedDIO[i].WriteOut = dt[idx];
                nLock = false;
                
            }
            dgNamedDIO.CommitEdit(); dgNamedDIO.CommitEdit();
            dgNamedDIO.Items.Refresh(); 
            return true;
        }
        private void dgNamedDIO_Loaded(object sender, RoutedEventArgs e)
        {
            if (dgNamedDIO.Columns.Count > 0) return; // after first call ignored
 
            dgNamedDIO.ItemsSource = namedDIO;

            addTextCol(dgNamedDIO, "DIO");
            addTextCol(dgNamedDIO, "Name");
            addCheckCol(dgNamedDIO, "Enabled");
            addCheckCol(dgNamedDIO, "ReadIn");
            addCheckCol(dgNamedDIO, "WriteOut");

            dgNamedDIO.Columns[3].Header = "[ReadIn]";
            dgNamedDIO.Columns[3].IsReadOnly = true;

            for (int i = 0; i< namedDIO.Count; i++)
            {
                namedDIO[i].OnCheckChange += new CheckChangeHandler(OnCheckChange);
            }
            bool[] dt;
            ReadIn(out dt);
            UpdateNamed(dt, false);
            dgNamedDIO.Items.Refresh();
        }       
        #endregion NamedDIO

        #region AllDIO
        public class SingleDIO
        {

            public string DIO { get; set; }
            public bool Enabled { get; set; }
            public bool ReadIn { get; set; }
            public bool WriteOut { get; set; }
        }
        private void dgAllDIO_Loaded(object sender, RoutedEventArgs e)
        {
            if (dgAllDIO.Columns.Count > 0) return; // after first call ignored

            dgAllDIO.ItemsSource = byteDIO;
            for (int i=0; i<4; i++)
            {
                addTextCol(dgAllDIO, "DIO" + i.ToString());
                addCheckCol(dgAllDIO, "Enb" + i.ToString());
                addCheckCol(dgAllDIO, "In" + i.ToString());
                addCheckCol(dgAllDIO, "Out" + i.ToString());
            }           

            dgAllDIO.Columns[0].IsReadOnly = true;
            dgAllDIO.Columns[2].Header = "[In0]";
            dgAllDIO.Columns[2].IsReadOnly = true;

            dgAllDIO.Columns[4].IsReadOnly = true;
            dgAllDIO.Columns[6].Header = "[In1]";
            dgAllDIO.Columns[6].IsReadOnly = true;

            dgAllDIO.Columns[8].IsReadOnly = true;
            dgAllDIO.Columns[10].Header = "[In2]";
            dgAllDIO.Columns[10].IsReadOnly = true;

            dgAllDIO.Columns[12].IsReadOnly = true;
            dgAllDIO.Columns[14].Header = "[In2]";
            dgAllDIO.Columns[14].IsReadOnly = true;

            bool[] dt;
            ReadIn(out dt);
            UpdateAll(dt, false);
            all2byte();
        }

        public class ByteDIO
        {
            public event CheckChangeHandler OnCheckChange;
            protected void CheckChange(object sender, int ID, bool check)
            {
                if (OnCheckChange != null) OnCheckChange(sender, ID, check);
            }
            public string DIO0 { get; set; }
            public bool Enb0 { get; set; }
            public bool In0 { get; set; }
            private bool bb0;
            public bool Out0
            {
                get { return bb0; }
                set
                {
                    bb0 = value;
                    if (Enb0) CheckChange((object)this, Convert.ToInt32(DIO0), bb0);
                }
            }
            public string DIO1 { get; set; }
            public bool Enb1 { get; set; }
            public bool In1 { get; set; }
            private bool bb1;
            public bool Out1
            {
                get { return bb1; }
                set
                {
                    bb1 = value;
                    if (Enb1) CheckChange((object)this, Convert.ToInt32(DIO1), bb1);
                }
            }
            public string DIO2 { get; set; }
            public bool Enb2 { get; set; }
            public bool In2 { get; set; }
            private bool bb2;
            public bool Out2
            {
                get { return bb2; }
                set
                {
                    bb2 = value;
                    if (Enb2) CheckChange((object)this, Convert.ToInt32(DIO2), bb2);
                }
            }
            public string DIO3 { get; set; }
            public bool Enb3 { get; set; }
            public bool In3 { get; set; }
            private bool bb3;
            public bool Out3
            {
                get { return bb3; }
                set
                {
                    bb3 = value;
                    if (Enb3) CheckChange((object)this, Convert.ToInt32(DIO3), bb3);
                }
            }
        }

        public void UpdateAll(bool[] dt, bool inclOut) // "In*" columns and optionally Out to allDIO
        {
            if (dt.Length != 32) return;
            for (int i = 0; i < 32; i++)
            {
                allDIO[i].ReadIn = dt[i];
                if (inclOut) allDIO[i].WriteOut = dt[i];
            }
            all2byte();
            dgAllDIO.CommitEdit(); dgAllDIO.CommitEdit();
            dgAllDIO.Items.Refresh(); 
        }

        private void byte2all()
        {
            allDIO.Clear();
            for (int i = 0; i < 8; i++)           
                allDIO.Add(new SingleDIO() { DIO = byteDIO[i].DIO0, Enabled = byteDIO[i].Enb0, ReadIn = byteDIO[i].In0, WriteOut = byteDIO[i].Out0 });
            for (int i = 0; i < 8; i++)
                allDIO.Add(new SingleDIO() { DIO = byteDIO[i].DIO1, Enabled = byteDIO[i].Enb1, ReadIn = byteDIO[i].In1, WriteOut = byteDIO[i].Out1 });
            for (int i = 0; i < 8; i++)
                allDIO.Add(new SingleDIO() { DIO = byteDIO[i].DIO2, Enabled = byteDIO[i].Enb2, ReadIn = byteDIO[i].In2, WriteOut = byteDIO[i].Out2 });
            for (int i = 0; i < 8; i++)
                allDIO.Add(new SingleDIO() { DIO = byteDIO[i].DIO3, Enabled = byteDIO[i].Enb3, ReadIn = byteDIO[i].In3, WriteOut = byteDIO[i].Out3 });
        }
        private void all2byte()
        {
            aLock = true;
            byteDIO.Clear(); 
            for (int i = 0; i < 8; i++)
            {
                byteDIO.Add(new ByteDIO()
                {
                    DIO0 = i.ToString(), Enb0 = allDIO[i].Enabled, In0 = allDIO[i].ReadIn, Out0 = allDIO[i].WriteOut,
                    DIO1 = (i + 8).ToString(), Enb1 = allDIO[i + 8].Enabled, In1 = allDIO[i + 8].ReadIn, Out1 = allDIO[i + 8].WriteOut,
                    DIO2 = (i + 16).ToString(), Enb2 = allDIO[i + 16].Enabled, In2 = allDIO[i + 16].ReadIn, Out2 = allDIO[i + 16].WriteOut,
                    DIO3 = (i + 24).ToString(), Enb3 = allDIO[i + 24].Enabled, In3 = allDIO[i + 24].ReadIn, Out3 = allDIO[i + 24].WriteOut,
                });
                byteDIO[i].OnCheckChange += new CheckChangeHandler(OnCheckChange);
            }
            aLock = false;
        }
        #endregion AllDIO

        #region Trans tabs
        public void all2named()
        {
            int idx;
            for (int i = 0; i < namedDIO.Count; i++)
            {
                if ((namedDIO[i].DIO == "") && (i == namedDIO.Count-1)) continue;
                if (!Int32.TryParse(namedDIO[i].DIO, out idx))
                {
                    ErrorMsg("Wrong DIO format for named channel " + namedDIO[i].Name);
                    continue;
                }
                if (!Utils.InRange(idx, 0, 31))
                {
                    ErrorMsg("Index out of range for named channel " + namedDIO[i].Name);
                    continue;
                }
                namedDIO[i].Enabled = allDIO[idx].Enabled;
                namedDIO[i].ReadIn = allDIO[idx].ReadIn;
                nLock = true;
                namedDIO[i].WriteOut = allDIO[idx].WriteOut;
                nLock = false;
            }
        }
        public void named2all()
        {
            int idx;
            for (int i = 0; i < namedDIO.Count; i++)
            {
                if ((namedDIO[i].DIO == "") && (i == namedDIO.Count-1)) continue;
                if (!Int32.TryParse(namedDIO[i].DIO, out idx))
                {
                    ErrorMsg("Wrong DIO format for named channel " + namedDIO[i].Name);
                    continue;
                }
                if (!Utils.InRange(idx, 0, 31))
                {
                    ErrorMsg("Index out of range for named channel " + namedDIO[i].Name);
                    continue;
                }
                allDIO[idx].Enabled = namedDIO[i].Enabled;
                allDIO[idx].ReadIn = namedDIO[i].ReadIn;
                allDIO[idx].WriteOut = namedDIO[i].WriteOut;
            }
        }
        private void tcMain_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Convert.ToInt32(tcMain.Tag) < 0) return;
            switch (tcMain.SelectedIndex)
            {
                case 0: // from all to named
                    byte2all();
                    all2named();
                    dgNamedDIO.Items.Refresh();
                    break;
                case 1: // from named to all
                    named2all();
                    all2byte();
                    dgAllDIO.Items.Refresh();
                    break;
            }
        }
        #endregion

        #region hardware
        public bool configHardware(bool forced = false) // if false look for hardwareSet; if true - do it
        {
            string ChannelList = "0-31";
            string dvc = (string)config["device"];
            if ((dvc == "") || Utils.TheosComputer()) return false;
            if (!forced && hardwareSet) return true;
            hsTaskIn = null; hsTaskOut = null;
            // read in
            hsTaskIn = niHSDIO.InitAcquisitionSession(dvc, false, false, "");
            hsTaskIn.AssignStaticChannels(ChannelList);
            hsTaskIn.ConfigureDataVoltageLogicFamily(ChannelList, niHSDIOConstants._33vLogic);
            // write out
            hsTaskOut = niHSDIO.InitGenerationSession(dvc, false, false, "");
            hsTaskOut.AssignStaticChannels(ChannelList);
            hsTaskOut.ConfigureDataVoltageLogicFamily(ChannelList, niHSDIOConstants._33vLogic);
            hardwareSet = true;
            Utils.log(tbLog, "! Hardware ("+dvc+") initialized" , Brushes.Coral.Color);
            return true;
        }
        public bool ReadIn(out bool[] dt)
        {
            configHardware(false);
            dt = new bool[32];
            uint dataRead;
            if (!Utils.isNull(hsTaskOut))
            {
                hsTaskIn.ReadStaticU32(out dataRead);
                var bitArray = new BitArray(BitConverter.GetBytes(dataRead));
                bitArray.CopyTo(dt, 0);
            }    
            if (chkLog.IsChecked.Value) Utils.log(tbLog, "< " + boolArr2string(dt), Brushes.DarkGreen.Color);
            return true;
        }
        public bool WriteOut(bool[] dt, bool[] mask)
        {
            configHardware(false);
            if ((dt.Length != 32) || (mask.Length != 32)) return false;
            uint dataOut = BoolArrayToUInt(dt); uint uMask = BoolArrayToUInt(mask);
            if (!Utils.isNull(hsTaskOut)) hsTaskOut.WriteStaticU32(dataOut, uMask);
            if (chkLog.IsChecked.Value)
            {
                Utils.log(tbLog, "> " + boolArr2string(mask), Brushes.BlueViolet.Color);
                Utils.log(tbLog, "> " + boolArr2string(dt), Brushes.Blue.Color);
            }               
            return true;
        }
        #endregion hw

        #region GUI
        bool[] lastIn;
        private void btnReadIn_Click(object sender, RoutedEventArgs e)
        {
            bool[] dt;
            ReadIn(out dt);
            switch (tcMain.SelectedIndex) 
            {
                case 0:
                    if (!UpdateNamed(dt, false)) return;
                    break;
                case 1:
                    byte2all();
                    UpdateAll(dt, false); 
                    all2byte();
                    break;
            }
            if (Utils.isNull(lastIn)) lastIn = new bool[32];
            dt.CopyTo(lastIn, 0); 
        }

        private void btnWriteOut_Click(object sender, RoutedEventArgs e)
        {
            bool[] dt = new bool[32]; bool[] mask = new bool[32];
            //ReadIn(out dt);
            for (int i = 0; i < 32; i++) dt[i] = false;
            switch (tcMain.SelectedIndex)
            {
                case 0:                    
                    UpdateNamed(dt, false);
                    mask = NamedBoolCol(1); dt = NamedBoolCol(3);                   
                    break;
                case 1:
                    //UpdateAll(dt, false);
                    byte2all();
                    for (int i = 0; i < 32; i++)
                    {
                        if (allDIO[i].Enabled) dt[i] = allDIO[i].WriteOut;
                    }
                    mask = AllBoolCol(1);
                    break;
            }
            if (Utils.TheosComputer())
            {
                for (int i = 0; i < 32; i++) dt[i] = false;
                dt[29] = true; dt[30] = true; dt[31] = true;
            }               
            WriteOut(dt,mask); 
            //btnReadIn_Click(null, null); string ss = boolArr2string(dt);
            //if (boolArr2string(lastIn) != ss) ErrorMsg("In/Out mismatch."); !!!
        }
        private void btnLogClear_Click(object sender, RoutedEventArgs e)
        {
            tbLog.Document.Blocks.Clear();
        }
        public void ErrorMsg(string msg)
        {
            if (chkLog.IsChecked.Value) Utils.log(tbLog, msg, Brushes.Red.Color);
            else Utils.TimedMessageBox(msg, "Warning", 2500);
        }
        private void chkLog_Checked(object sender, RoutedEventArgs e)
        {
            int shift = 270;
            if (chkLog.IsChecked.Value)
            {
                cdLog.Width = new GridLength(shift);
                Width = Width + shift;
            }
            else
            {
                cdLog.Width = new GridLength(0);
                Width = Width - shift;
            }
        }
        private void chkAutoUpdate_Checked(object sender, RoutedEventArgs e)
        {
            //allDIO[3].ReadIn = chkAutoUpdate.IsChecked.Value;
            //all2byte();          
        }
        #endregion GUI
        public bool[] NamedBoolCol(int idx) // same col. idx as AllBoolCol
        {
            bool[] rslt = new bool[32];
            for (int i = 0; i < 32; i++) rslt[i] = false;
            int j;
            for (int i = 0; i < namedDIO.Count; i++)
            {
                if ((namedDIO[i].DIO == "") && (i == namedDIO.Count - 1)) continue;
                if (!Int32.TryParse(namedDIO[i].DIO, out j))
                {
                    ErrorMsg("Wrong DIO format for named channel " + namedDIO[i].Name);
                    continue;
                }
                if (!Utils.InRange(j, 0, 31))
                {
                    ErrorMsg("Index out of range for named channel " + namedDIO[i].Name);
                    continue;
                }
                switch (idx)
                {
                    case 1:
                        rslt[j] = namedDIO[i].Enabled;
                        break;
                    case 2:
                        rslt[j] = namedDIO[i].ReadIn;
                        break;
                    case 3:
                        rslt[j] = namedDIO[i].WriteOut;
                        break;
                }
            }
            return rslt;
        }

        public bool[] AllBoolCol(int idx) // column index
        {
            bool[] rslt = new bool[32];
            for (int i = 0; i < 32; i++)
                switch (idx)
                {
                    case 1: 
                        rslt[i] = allDIO[i].Enabled;
                        break;
                    case 2:
                        rslt[i] = allDIO[i].ReadIn;
                        break;
                    case 3:
                        rslt[i] = allDIO[i].WriteOut;
                        break;
                }
            return rslt;
        }

        private void dgNamedDIO_UnloadingRow(object sender, DataGridRowEventArgs e)
        {
          dLog("unloading " + namedDIO.Count.ToString());
        }

        private void dgNamedDIO_LoadingRow(object sender, DataGridRowEventArgs e)
        {
           dLog("loading "+ namedDIO.Count.ToString());
        }

        private void dgNamedDIO_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            e.Handled = true;
        }

        private void tbDvc_TextChanged(object sender, TextChangedEventArgs e)
        {
            hardwareSet = false;
            if (!Utils.isNull(config)) config["device"] = tbDvc.Text;
        }
    }
}
