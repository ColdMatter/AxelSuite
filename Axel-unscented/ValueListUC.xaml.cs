using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
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

namespace Axel_unscented
{
    /// <summary>
    /// Interaction logic for ValueListUC.xaml
    /// </summary>
    public partial class ValueListUC : UserControl
    {
        private DataTable dt;
        public ValueListUC()
        {
            InitializeComponent();
        }
        private string keyHeader, valueHeader;
        public Dictionary<string, double> items
        {
            get
            {
                Dictionary<string, double> itm = new Dictionary<string, double>();
                foreach (DataRow row in dt.Rows)
                {
                    double val;
                    string keyStr = Convert.ToString(row[0]); string valStr = Convert.ToString(row[1]);
                    if (!Double.TryParse(valStr, out val))
                    {
                        Utils.TimedMessageBox("Problem with row:" + keyStr + " | " + valStr, "Conversion problem", 2500); continue;
                    }
                    itm[keyStr] = val;
                }
                return itm;
            }
            set
            {
                dt.Rows.Clear();
                foreach (KeyValuePair<string, double> entry in value)
                {
                    dt.Rows.Add(entry.Key, Utils.formatDouble(entry.Value,"G4"));
                }
            }
        }
        public void SetHeaders(string key, string value) // initialize
        {
            keyHeader = key; valueHeader = value;
            dt = new DataTable();
            DataColumn dc1 = new DataColumn(key, typeof(string)); dc1.ReadOnly = true;
            DataColumn dc2 = new DataColumn(value, typeof(double));          
            dt.Columns.Add(dc1);  dt.Columns.Add(dc2);
            dataGrid.ItemsSource = dt.DefaultView;
        }
        private void dataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            // if some manipulation is required
            // if (Convert.ToString(e.Column.Header) != "__Volts__") return;
        }

    }
}