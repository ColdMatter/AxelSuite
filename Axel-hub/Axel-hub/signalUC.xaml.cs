using NationalInstruments.Net;
using NationalInstruments.Analysis;
using NationalInstruments.Analysis.Conversion;
using NationalInstruments.Analysis.Dsp;
using NationalInstruments.Analysis.Dsp.Filters;
using NationalInstruments.Analysis.Math;
using NationalInstruments.Analysis.Monitoring;
using NationalInstruments.Analysis.SignalGeneration;
using NationalInstruments.Analysis.SpectralMeasurements;
using NationalInstruments;
using NationalInstruments.DAQmx;
using NationalInstruments.NetworkVariable;
using NationalInstruments.NetworkVariable.WindowsForms;
using NationalInstruments.Tdms;
using NationalInstruments.Controls;
using NationalInstruments.Controls.Rendering;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using OptionsNS;
using UtilsNS;

namespace Axel_hub
{
    /// <summary>
    /// Interaction logic for signalUC.xaml
    /// </summary>
    public partial class signalClass : UserControl
    {
        private const int dd = 1000; // default depth
        DataStack stackN1 = new DataStack(dd);
        DataStack stackN2 = new DataStack(dd);
        DataStack stackRN1 = new DataStack(dd);
        DataStack stackRN2 = new DataStack(dd);
        DataStack stackNtot = new DataStack(dd);
        DataStack stackNtot_std = new DataStack(dd);
        DataStack stackN2_std = new DataStack(dd);
        DataStack stackN2_int = new DataStack(dd);
        DataStack signalDataStack = new DataStack(10000);
        DataStack backgroundDataStack = new DataStack(10000);

        private bool scanMode, repeatMode;
        public bool Showing;
        MMexec grpMme;
        private int runID;
        public MMscan lastScan;

        public FileLogger logger; 
        private double NsYmin = 10, NsYmax = -10, signalYmin = 10, signalYmax = -10;

        public signalClass()
        {
            InitializeComponent();
        }

        GeneralOptions genOptions = null;
        Modes genModes = null;
        public string prefix { get; private set; }
        public void InitOptions(ref GeneralOptions _genOptions, ref Modes _genModes, string _prefix = "")
        {
            genOptions = _genOptions; genModes = _genModes; prefix = _prefix;
            logger = new FileLogger(prefix);
        }

        public void Init(MMexec GrpMme) // new series
        {
            grpMme = GrpMme.Clone();
            scanMode = grpMme.cmd.Equals("scan");
            repeatMode = grpMme.cmd.Equals("repeat");

            logger.Enabled = false; 
            logger.log("# " + JsonConvert.SerializeObject(grpMme));
            logger.log("# XAxis\tN1\tN2\tRN1\tRN2\tNTot\tB2\tBtot");
        }

       public void Clear()
        {
            stackN1.Clear(); stackN2.Clear();
            stackRN1.Clear(); stackRN2.Clear();
            stackNtot.Clear(); stackNtot_std.Clear();
            stackN2_std.Clear(); stackN2_int.Clear();
            graphNs.Data[0] = stackN1;  graphNs.Data[1] = stackN2;
            graphNs.Data[2] = stackRN1; graphNs.Data[3] = stackRN2;
            graphNs.Data[4] = stackNtot;
            NsYmin = 10; NsYmax = -10;

            signalDataStack.Clear();
            backgroundDataStack.Clear();
            signalYmin = 10; signalYmax = -10;

            lboxNB.Items.Clear();
        }

        public void Update(MMexec mme, out double currX, out double A) // with unpacked arrays
        {
            runID = Convert.ToInt32(mme.prms["runID"]);
            Dictionary<string, double> avgs = MMDataConverter.AverageShotSegments(mme, genOptions.intN2, chkStdDev.IsChecked.Value);
            if (Showing)
            {
                lboxNB.Items.Clear();
                foreach (var item in avgs)
                {
                    ListBoxItem lbi = new ListBoxItem();
                    lbi.Content = string.Format("{0}: {1:" + genOptions.SignalTablePrec + "}", item.Key, item.Value);
                    if (item.Key.IndexOf("_std") > 0) lbi.Foreground = Brushes.Green;
                    else lbi.Foreground = Brushes.Blue;
                    lboxNB.Items.Add(lbi);
                }
            }
            double asymmetry = MMDataConverter.Asymmetry(avgs, chkBackgroung.IsChecked.Value, chkDarkcurrent.IsChecked.Value);
            //
            // signal chart (rigth)
            if (Utils.isNull(signalDataStack)) signalDataStack = new DataStack();
            else signalDataStack.Clear();
            if (Utils.isNull(backgroundDataStack)) backgroundDataStack = new DataStack();
            else backgroundDataStack.Clear();

            int xVal = 0; double N2 = avgs["N2"];
            if (Showing)
                foreach (double yVal in (double[])mme.prms["N2"])
                {
                    signalDataStack.Add(new Point(xVal, yVal));
                    xVal++;
                }
            double NTot = avgs["NTot"];
            if (Showing)
                foreach (double yVal in (double[])mme.prms["NTot"])
                {
                    signalDataStack.Add(new Point(xVal, yVal));
                    xVal++;
                }

            xVal = 0; double B2 = avgs["B2"];
            if (Showing)
                foreach (double yVal in (double[])mme.prms["B2"])
                {
                    backgroundDataStack.Add(new Point(xVal, yVal));
                    xVal++;
                }
            double BTot = avgs["BTot"];
            if (Showing)
                foreach (double yVal in (double[])mme.prms["BTot"])
                {
                    backgroundDataStack.Add(new Point(xVal, yVal));
                    xVal++;
                }
            if (Showing) // skip the show
            {
               /* Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,
                new Action(() =>
                {*/
                    graphSignal.Data[0] = signalDataStack.Compress(genOptions.RawSignalAvg);
                    graphSignal.Data[1] = backgroundDataStack.Compress(genOptions.RawSignalAvg);
               // }));
            }
            // readjust Y axis
            if (chkAutoScaleMiddle.IsChecked.Value && Showing) // signal auto-Y-limits
            {
                double d = Math.Min(signalDataStack.pointYs().Min(), backgroundDataStack.pointYs().Min());
                d = Math.Floor(10 * d) / 10;
                signalYmin = Math.Min(d, signalYmin);
                d = Math.Max(signalDataStack.pointYs().Max(), backgroundDataStack.pointYs().Max());
                d = Math.Ceiling(10 * d) / 10;
                signalYmax = Math.Max(d, signalYmax);
                d = (signalYmax - signalYmin) * 0.02;
                signalYaxis.Range = new Range<double>(signalYmin - d, signalYmax + d);
            }
            //
            // Ns chart (left)

            double corr, disbalance;
            // corrected with background
            double cNtot = NTot - BTot; double cN2 = N2 - B2; double cN1 = cNtot - cN2;
            //double A = cN2 / cNtot;//(N2 - B2) / (NTot - BTot); //
            A = 1 - 2 * (N2 - B2) / (NTot - BTot);
            currX = 1; double cN2_std = 1, cNtot_std = 1;
            if (chkStdDev.IsChecked.Value)
            {
                cN2_std = Math.Sqrt(Math.Pow(avgs["N2_std"], 2) + Math.Pow(avgs["B2_std"], 2));
                cNtot_std = Math.Sqrt(Math.Pow(avgs["NTot_std"], 2) + Math.Pow(avgs["BTot_std"], 2));
            }
            double cinitN2 = avgs["initN2"] - B2;
            
            if (scanMode) currX = lastScan.sFrom + runID * lastScan.sBy;
            if (repeatMode) currX = runID;
                
            stackN1.Add(new Point(currX, cN1)); stackN2.Add(new Point(currX, cN2)); stackNtot.Add(new Point(currX, cNtot));
            stackRN1.Add(new Point(currX, cN1 / cNtot)); stackRN2.Add(new Point(currX, cN2 / cNtot));
            if (chkStdDev.IsChecked.Value)
            {
                stackN2_std.Add(new Point(currX, cN2_std)); stackNtot_std.Add(new Point(currX, cNtot_std));
            }
            stackN2_int.Add(new Point(currX, cinitN2));

            if (chkAutoScaleMiddle.IsChecked.Value) // Ns auto-Y-limits
            {
                List<double> ld = new List<double>();
                ld.Add(stackN1.pointYs().Min()); ld.Add(stackN2.pointYs().Min()); ld.Add(stackNtot.pointYs().Min());
                ld.Add(stackRN1.pointYs().Min()); ld.Add(stackRN2.pointYs().Min());
                double d = ld.Min();
                d = Math.Floor(10 * d) / 10;
                NsYmin = Math.Min(d, NsYmin);
                ld.Clear();
                ld.Add(stackN1.pointYs().Max()); ld.Add(stackN2.pointYs().Max()); ld.Add(stackNtot.pointYs().Max());
                ld.Add(stackRN1.pointYs().Max()); ld.Add(stackRN2.pointYs().Max());
                d = ld.Max();
                d = Math.Ceiling(10 * d) / 10;
                NsYmax = Math.Max(d, NsYmax);
                d = (NsYmax - NsYmin) * 0.02;
                NsYaxis.Range = new Range<double>(NsYmin - d, NsYmax + d);
            }
            if (Showing)
            {
                /*Application.Current.Dispatcher.BeginInvoke( DispatcherPriority.Background,
                    new Action(() =>
                    {*/
                graphNs.Data[0] = stackN1.Portion(genOptions.TrendSignalLen);
                graphNs.Data[1] = stackN2.Portion(genOptions.TrendSignalLen);
                graphNs.Data[2] = stackRN1.Portion(genOptions.TrendSignalLen);
                graphNs.Data[3] = stackRN2.Portion(genOptions.TrendSignalLen);
                graphNs.Data[4] = stackNtot.Portion(genOptions.TrendSignalLen);
                   // }));
            }

            //#XAxis\tN1\tN2\tRN1\tRN2\tNTot\tB2\tBtot
            if (stackN1.Count > 0)
            {
                string ss = currX.ToString(genOptions.SaveFilePrec) + "\t" + stackN1.Last.Y.ToString(genOptions.SaveFilePrec) +
                    "\t" + stackN2.Last.Y.ToString(genOptions.SaveFilePrec) + "\t" + stackRN1.Last.Y.ToString(genOptions.SaveFilePrec) +
                    "\t" + stackRN2.Last.Y.ToString(genOptions.SaveFilePrec) + "\t" + stackNtot.Last.Y.ToString(genOptions.SaveFilePrec) +
                    "\t" + B2.ToString(genOptions.SaveFilePrec) + "\t" + BTot.ToString(genOptions.SaveFilePrec);
                logger.log(ss);
            }
        }
        //TODO Change this to add new columns to corresponding datastacks (N2_std, Ntot_std etc) - maybe using a list?
        public bool OpenSignal(string fn, out string rem)
        {
            if (!File.Exists(fn)) throw new Exception("File <" + fn + "> does not exist.");

            Clear(); rem = "";           
            string[] ns; int j = 0; double x, d;
            foreach (string line in File.ReadLines(fn))
            {
                if (line.Contains("#{"))
                {
                    grpMme = JsonConvert.DeserializeObject<MMexec>(line.Substring(1));
                }
                if (line.Contains("#Rem="))
                {
                    rem = line.Substring(5); lbInfoSignal.Content = rem;
                }
                if (line[0] == '#') continue; //skip comments/service info
                ns = line.Split('\t');
                if (ns.Length < 6) continue;
                if (!double.TryParse(ns[0], out x)) throw new Exception("Wrong double at line " + j.ToString());
                if (!double.TryParse(ns[1], out d)) throw new Exception("Wrong double at line " + j.ToString());
                stackN1.Add(new Point(x, d));
                if (!double.TryParse(ns[2], out d)) throw new Exception("Wrong double at line " + j.ToString());
                stackN2.Add(new Point(x, d));
                if (!double.TryParse(ns[3], out d)) throw new Exception("Wrong double at line " + j.ToString());
                stackRN1.Add(new Point(x, d));
                if (!double.TryParse(ns[4], out d)) throw new Exception("Wrong double at line " + j.ToString());
                stackRN2.Add(new Point(x, d));
                if (!double.TryParse(ns[5], out d)) throw new Exception("Wrong double at line " + j.ToString());
                stackNtot.Add(new Point(x, d));
                j++;
            }
            graphNs.Data[0] = stackN1; graphNs.Data[1] = stackN2;
            graphNs.Data[2] = stackRN1; graphNs.Data[3] = stackRN2; graphNs.Data[4] = stackNtot;                
            return true;
        }

        public void SaveSignal(string fn, string rem)
        {
            if (stackN1.Count == 0)
            {
                MessageBox.Show("Error: No signal data to be saved");
                return;
            }
            System.IO.StreamWriter file = new System.IO.StreamWriter(fn);
            if (!Utils.isNull(grpMme)) file.WriteLine("#" + JsonConvert.SerializeObject(grpMme));
            if (!String.IsNullOrEmpty(rem)) file.WriteLine("#Rem=" + rem);
            file.WriteLine("#XAxis\tN1\tN2\tRN1\tRN2\tNTot\tN2_std\tNtot_std\tN2int");
            for (int i = 0; i < stackN1.Count; i++)
            {
                string ss = stackN1[i].X.ToString(genOptions.SaveFilePrec) + "\t" + stackN1[i].Y.ToString(genOptions.SaveFilePrec) + "\t" +
                    stackN2[i].Y.ToString(genOptions.SaveFilePrec) + "\t" + stackRN1[i].Y.ToString(genOptions.SaveFilePrec) + "\t" +
                    stackRN2[i].Y.ToString(genOptions.SaveFilePrec) + "\t" + stackNtot[i].Y.ToString(genOptions.SaveFilePrec) + "\t" +
                    stackN2_int[i].Y.ToString(genOptions.SaveFilePrec);
                if (stackN2_std.Count > i) ss += "\t" + stackN2_std[i].Y.ToString(genOptions.SaveFilePrec) + "\t" + stackNtot_std[i].Y.ToString(genOptions.SaveFilePrec);
                file.WriteLine(ss);
            }
            file.Close();
        }

        public void OpenDefaultModes()
        {        
            chkAutoScaleMiddle.IsChecked = genModes.AutoScaleMiddle;
            chkBackgroung.IsChecked = genModes.Background;
            chkDarkcurrent.IsChecked = genModes.DarkCurrent;
            chkStdDev.IsChecked = genModes.StdDev;
            chkN1.IsChecked = genModes.N1;
            chkN2.IsChecked = genModes.N2;
            chkRN1.IsChecked = genModes.RN1;
            chkRN2.IsChecked = genModes.RN2;
            chkNtot.IsChecked = genModes.Ntot;
        }

        public void SaveDefaultModes()
        {
            genModes.AutoScaleMiddle = chkAutoScaleMiddle.IsChecked.Value;
            genModes.Background = chkBackgroung.IsChecked.Value;
            genModes.DarkCurrent = chkDarkcurrent.IsChecked.Value;
            genModes.StdDev = chkStdDev.IsChecked.Value;
            genModes.N1 = chkN1.IsChecked.Value;
            genModes.N2 = chkN2.IsChecked.Value;
            genModes.RN1 = chkRN1.IsChecked.Value;
            genModes.RN2 = chkRN2.IsChecked.Value;
            genModes.Ntot = chkNtot.IsChecked.Value;
        }

        private void graphNs_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                if (e.Key == Key.C)
                {
                    Rect bounds; RenderTargetBitmap bitmap;
                    bounds = System.Windows.Controls.Primitives.LayoutInformation.GetLayoutSlot(graphNs);
                    bitmap = new RenderTargetBitmap((int)bounds.Width, (int)bounds.Height, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(graphNs);
                    Clipboard.SetImage(bitmap);
                    Utils.TimedMessageBox("The image is in the clipboard");
                }
            }
        }

        private void graphNs_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            (sender as Graph).ResetZoomPan();
            if (sender == graphNs)
            {
                NsYmin = 10; NsYmax = -10;
            }
            if (sender == graphSignal)
            {
                signalYmin = 10; signalYmax = -10;
            }
        }

        public void chkN1_Checked(object sender, RoutedEventArgs e)
        {
            if (plotN1 != null)
            {
                if (chkN1.IsChecked.Value) plotN1.Visibility = System.Windows.Visibility.Visible;
                else plotN1.Visibility = System.Windows.Visibility.Hidden;
            }
            if (plotN2 != null)
            {
                if (chkN2.IsChecked.Value) plotN2.Visibility = System.Windows.Visibility.Visible;
                else plotN2.Visibility = System.Windows.Visibility.Hidden;
            }
            if (plotRN1 != null)
            {
                if (chkRN1.IsChecked.Value) plotRN1.Visibility = System.Windows.Visibility.Visible;
                else plotRN1.Visibility = System.Windows.Visibility.Hidden;
            }
            if (plotRN2 != null)
            {
                if (chkRN2.IsChecked.Value) plotRN2.Visibility = System.Windows.Visibility.Visible;
                else plotRN2.Visibility = System.Windows.Visibility.Hidden;
            }
            if (plotNtot != null)
            {
                if (chkNtot.IsChecked.Value) plotNtot.Visibility = System.Windows.Visibility.Visible;
                else plotNtot.Visibility = System.Windows.Visibility.Hidden;
            }
        }
    }
}
