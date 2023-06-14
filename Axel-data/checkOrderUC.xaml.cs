using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using Path = System.IO.Path;
using UtilsNS;

namespace Axel_data
{
    /// <summary>
    /// Interaction logic for checkOrderUC.xaml
    /// </summary>
    public partial class checkOrderUC : UserControl
    {
        public checkOrderUC()
        {
            InitializeComponent();
        }
        string dataFolder;
        private void btnFolder_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            //dialog.InitialDirectory = "";
            dialog.Title = "Select a data folder";
            dialog.IsFolderPicker = true;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok) dataFolder = dialog.FileName; tbFolder.Text = dataFolder;
            List<string> fullFN = new List<string>(Directory.GetFiles(dataFolder, "*.ahs"));

            lbDataFiles.Items.Clear();
            foreach (string fn in fullFN)
            {
                ListBoxItem lbi = new ListBoxItem() { Content = Path.GetFileName(fn) };
                if (checkOrder(fn)) lbi.Foreground = Brushes.Black;
                else lbi.Foreground = Brushes.Red;
                lbDataFiles.Items.Add(lbi);                
            }
        }
        private bool checkOrder(string fn)
        {
            bool bb = true;
            List<string> dt = Utils.readList(fn);
            for (int i = 1; i < dt.Count; i++)
            {
                string[] dta = dt[i].Split('\t');
                if (dta.Length < 1) continue;
                int idx = Convert.ToInt32(dta[0]);
                if (!idx.Equals(i - 1)) return false;
            }
            return bb;
        }
        private void lbDataFiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string fn = Path.Combine(dataFolder, (lbDataFiles.SelectedItem as ListBoxItem).Content.ToString());
            rtbData.Document.Blocks.Clear();
            List<string> dt = Utils.readList(fn);
            Utils.log(rtbData, dt);
        }
    }
}
