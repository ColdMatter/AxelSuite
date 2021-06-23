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
    /// visualize the raw signal and signal trends {"N1", "N2", "RN1", "RN2", "NTot", "B2", "Btot"}
    /// table with last trend position and (optionally) some stats
    /// </summary>
    public partial class signalClass : UserControl
    {
        private const int dd = 5000; // default depth
        private const bool writeHeadersFlag = false;
        DataStack stackN1 = new DataStack(dd);
        DataStack stackN2 = new DataStack(dd);
        DataStack stackRN1 = new DataStack(dd);
        DataStack stackRN2 = new DataStack(dd);
        DataStack stackNtot = new DataStack(dd);
        DataStack stackB2 = new DataStack(dd);
        DataStack stackBtot = new DataStack(dd);

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

        public DictFileLogger logger, SDlog; public FileLogger rawDataLog;
        private double NsYmin = 10, NsYmax = -10, signalYmin = 10, signalYmax = -10;

        /// <summary>
        /// Class constructor
        /// </summary>
        public signalClass()
        {
            InitializeComponent();           
        }

        GeneralOptions genOptions = null;
        Modes genModes = null;
        AxelMems axelMems = null;
        public string prefix { get; private set; }
        /// <summary>
        /// Initialization with options from Options dialog and modes from last used ones 
        /// </summary>
        /// <param name="_genOptions">from Options dialog</param>
        /// <param name="_genModes">last used ones</param>
        /// <param name="_prefix">X/Y</param>
        public void Init(ref GeneralOptions _genOptions, ref Modes _genModes, ref AxelMems _axelMems, string _prefix = "")
        {
            genOptions = _genOptions; genModes = _genModes; axelMems = _axelMems; prefix = _prefix;
        }

        /// <summary>
        /// Call when new series starts
        /// </summary>
        /// <param name="GrpMme"></param>
        public void Init(MMexec GrpMme, MMexec mm2status) // 
        {
            grpMme = GrpMme.Clone();
            if (!Utils.isNull(mm2status))
            {
                grpMme.prms["params"] = mm2status.prms["params"]; grpMme.prms["steps"] = mm2status.prms["steps"];
            }
                
            scanMode = grpMme.cmd.Equals("scan");
            repeatMode = grpMme.cmd.Equals("repeat");
            
            string timeName = Utils.dataPath + Utils.timeName();
            logger = new DictFileLogger(new string[] {"Index", "XAxis", "N1", "N2", "RN1", "RN2", "NTot", "B2", "BTot", "Bg" }, prefix, timeName);
            logger.Enabled = false;
            logger.setMMexecAsHeader(grpMme.Clone());
            logger.defaultExt = ".ahs";
            logger.Enabled = genModes.SignalLog;
            writeHeaders(logger);

            SDlog = new DictFileLogger(new string[] { "XAxis","N2_std","NTot_std" ,"B2_std","BTot_std", "Bg_std" }, prefix, timeName);
            SDlog.Enabled = false;
            SDlog.setMMexecAsHeader(grpMme.Clone());
            SDlog.defaultExt = ".asd";
            SDlog.Enabled = chkSDSave.IsChecked.Value;

            rawDataLog = new FileLogger(prefix, timeName);
            rawDataLog.Enabled = false;
            rawDataLog.defaultExt = ".rws";
            rawDataLog.Enabled = chkRawSave.IsChecked.Value;

        }

        /// <summary>
        /// copy log headers in a separate file 
        /// </summary>
        /// <param name="logg"></param>
        private void writeHeaders(FileLogger logg)
        {
            if (!writeHeadersFlag) return;
            FileLogger fl = new FileLogger(logg.prefix, System.IO.Path.ChangeExtension(logg.LogFilename, ".ahh"));
            fl.header = logg.header;
            fl.subheaders.AddRange(logg.subheaders);
            fl.Enabled = logg.Enabled;
            fl.Enabled = false;
        }

        /// <summary>
        /// Clean everyting up 
        /// </summary>
        public void Clear()
        {
            stackN1.Clear(); stackN2.Clear();
            stackRN1.Clear(); stackRN2.Clear();
            stackB2.Clear(); stackBtot.Clear();
            stackNtot.Clear(); stackNtot_std.Clear();
            stackN2_std.Clear(); stackN2_int.Clear();
            graphNs.Data[0] = stackN1; graphNs.Data[1] = stackN2;
            graphNs.Data[2] = stackRN1; graphNs.Data[3] = stackRN2;
            graphNs.Data[4] = stackNtot;
            NsYmin = 10; NsYmax = -10;

            signalDataStack.Clear();
            backgroundDataStack.Clear();
            signalYmin = 10; signalYmax = -10;

            lboxNB.Items.Clear();
        }

        /// <summary>
        /// Import a shot with unpacked arrays and update signal/trends
        /// </summary>
        /// <param name="mme">shot with unpacked arrays</param>
        /// <param name="currX">Last horiz coordinate</param>
        /// <param name="A">Asymetry calculated</param>
        public void Update(MMexec mme, out double currX, out double A) // 
        {
            if (!Utils.isNull(rawDataLog)) rawDataLog.log(JsonConvert.SerializeObject(mme));
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
            
            double asymmetry = MMDataConverter.Asymmetry(avgs, false, chkDarkcurrent.IsChecked.Value);
            //
            // oscilo-signal chart (rigth)
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
                d = signalYmax == signalYmin ? 0.5 : (signalYmax - signalYmin) * 0.02;
                signalYaxis.Range = new Range<double>(signalYmin - d, signalYmax + d);
            }
            //
            // Ns chart (left)

            // corrected with background
            //double cNtot = NTot - BTot; double cN2 = N2 - B2; double cN1 = cNtot - cN2; double N1 = NTot - N2;
            //double A = cN2 / cNtot;//(N2 - B2) / (NTot - BTot); //
            A = 1 - 2 * (N2 - B2) / (NTot - BTot);
            currX = 1; //double cN2_std = 1, cNtot_std = 1;

            /*if (chkStdDev.IsChecked.Value)
            {
                cN2_std = Math.Sqrt(Math.Pow(avgs["N2_std"], 2) + Math.Pow(avgs["B2_std"], 2));
                cNtot_std = Math.Sqrt(Math.Pow(avgs["NTot_std"], 2) + Math.Pow(avgs["BTot_std"], 2));
            }*/
            double cinitN2 = 0;
            if (avgs.ContainsKey("initN2")) cinitN2 = avgs["initN2"] - B2;
            Dictionary<string, double> cavgs = MMDataConverter.SignalCorrected(avgs, chkDarkcurrent.IsChecked.Value);           

            if (scanMode) currX = lastScan.sFrom + runID * lastScan.sBy;
            if (repeatMode)
            {   // remote time; if not - the default is axelChart.axelMems.TimeElapsed() from nextMeasure method
                if (mme.prms.ContainsKey("iTime")) // remote time
                {  
                    if (grpMme.getWhichSender().Equals(MMexec.SenderType.AxelHub)) // jumbo
                    {
                        if (theTime.isTimeRunning)
                        {
                            double sh = 0; if (mme.prms.ContainsKey("bTime") && mme.prms.ContainsKey("samplingRate")) sh = Convert.ToDouble(mme.prms["bTime"]) / Convert.ToDouble(mme.prms["samplingRate"]);
                            currX = theTime.relativeTime(((long)Convert.ToInt64(mme.prms["iTime"]))) + sh; // in sec
                        }
                    }
                    else
                    {
                        if (theTime.isTimeRunning)
                            currX = theTime.relativeTime(((long)Convert.ToInt64(mme.prms["iTime"]))); // in sec
                        else theTime.startTime();
                    }
                }
                else currX = runID;
            }
            avgs["Index"] = runID; avgs["XAxis"] = currX; if (!Utils.isNull(SDlog)) SDlog.dictLog(avgs, genOptions.SaveFilePrec);
            //stackN1.Add(new Point(currX, cN1)); stackN2.Add(new Point(currX, cN2)); stackNtot.Add(new Point(currX, cNtot));
            //stackRN1.Add(new Point(currX, cN1 / cNtot)); stackRN2.Add(new Point(currX, cN2 / cNtot));

            stackN1.Add(new Point(currX, cavgs["N1"])); stackN2.Add(new Point(currX, cavgs["N2"])); stackNtot.Add(new Point(currX, cavgs["NTot"]));
            stackRN1.Add(new Point(currX, cavgs["RN1"])); stackRN2.Add(new Point(currX, cavgs["RN2"]));
            stackB2.Add(new Point(currX, cavgs["B2"])); stackBtot.Add(new Point(currX, cavgs["BTot"]));

            if (chkAutoScaleMiddle.IsChecked.Value) // Ns auto-Y-limits
            {
                List<double> ld = new List<double>();

                if (chkN1.IsChecked.Value) ld.Add(stackN1.pointYs().Min());
                if (chkN2.IsChecked.Value) ld.Add(stackN2.pointYs().Min());
                if (chkRN1.IsChecked.Value) ld.Add(stackRN1.pointYs().Min());
                if (chkRN2.IsChecked.Value) ld.Add(stackRN2.pointYs().Min());
                if (chkNtot.IsChecked.Value) ld.Add(stackNtot.pointYs().Min());
                if (chkB2.IsChecked.Value) ld.Add(stackB2.pointYs().Min());
                if (chkBtot.IsChecked.Value) ld.Add(stackBtot.pointYs().Min());
                double d = ld.Min();
                d = Math.Floor(10 * d) / 10;
                NsYmin = Math.Min(d, NsYmin);

                ld.Clear();
                if (chkN1.IsChecked.Value) ld.Add(stackN1.pointYs().Max());
                if (chkN2.IsChecked.Value) ld.Add(stackN2.pointYs().Max());
                if (chkRN1.IsChecked.Value) ld.Add(stackRN1.pointYs().Max());
                if (chkRN2.IsChecked.Value) ld.Add(stackRN2.pointYs().Max());
                if (chkNtot.IsChecked.Value) ld.Add(stackNtot.pointYs().Max());
                if (chkB2.IsChecked.Value) ld.Add(stackB2.pointYs().Max());
                if (chkBtot.IsChecked.Value) ld.Add(stackBtot.pointYs().Max());
                d = ld.Max();
                d = Math.Ceiling(10 * d) / 10;
                NsYmax = Math.Max(d, NsYmax);

                d = (NsYmax - NsYmin) * 0.02;
                if (!Double.IsNaN(NsYmin) && !Double.IsNaN(NsYmax) && !Double.IsNaN(d))
                    NsYaxis.Range = new Range<double>(NsYmin - d, NsYmax + d);
            }
            if (Showing)
            {
                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,
                    new Action(() =>
                    {
                        graphNs.Data[0] = stackN1.Portion(genOptions.TrendSignalLen);
                        graphNs.Data[1] = stackN2.Portion(genOptions.TrendSignalLen);
                        graphNs.Data[2] = stackRN1.Portion(genOptions.TrendSignalLen);
                        graphNs.Data[3] = stackRN2.Portion(genOptions.TrendSignalLen);
                        graphNs.Data[4] = stackNtot.Portion(genOptions.TrendSignalLen);
                        graphNs.Data[5] = stackB2.Portion(genOptions.TrendSignalLen);
                        graphNs.Data[6] = stackBtot.Portion(genOptions.TrendSignalLen);
                    }));
            }
            if (stackN2.Count > 0)
            {
                logger.dictLog(avgs, genOptions.SaveFilePrec);
            }
        }

        /// <summary>
        /// Open table text file with trends {"N1", "N2", "RN1", "RN2", "NTot"}
        /// </summary>
        /// <param name="fn"></param>
        /// <param name="rem"></param>
        /// <returns></returns>
        public bool OpenSignal(string fn, out string rem)
        {
            rem = ""; Clear();
            if (!File.Exists(fn)) throw new Exception("File <" + fn + "> does not exist.");
            DictFileReader dlog = new DictFileReader(fn, new string[] { "XAxis", "N1", "N2", "RN1", "RN2", "NTot", "B2", "BTot", "Bg" });
            if (dlog.header.StartsWith("{")) grpMme = JsonConvert.DeserializeObject<MMexec>(dlog.header);
            if (dlog.subheaders.Count > 0)
            {
                string sh = dlog.subheaders[0];
                if (sh.StartsWith("Rem=")) rem = sh.Substring(4);
            }
            double x;
            Dictionary<string, double> row = new Dictionary<string, double>();
            while (dlog.doubleIterator(ref row))
            {
                if (row.ContainsKey("XAxis")) x = row["XAxis"];
                else continue;
                if (row.ContainsKey("N1")) stackN1.Add(new Point(x, row["N1"]));
                if (row.ContainsKey("N2")) stackN2.Add(new Point(x, row["N2"]));
                if (row.ContainsKey("RN1")) stackRN1.Add(new Point(x, row["RN1"]));
                if (row.ContainsKey("RN2")) stackRN2.Add(new Point(x, row["RN2"]));
                if (row.ContainsKey("Ntot")) stackNtot.Add(new Point(x, row["Ntot"]));
                if (row.ContainsKey("B2")) stackRN2.Add(new Point(x, row["B2"]));
                if (row.ContainsKey("Btot")) stackNtot.Add(new Point(x, row["Btot"]));
            }
            graphNs.Data[0] = stackN1; graphNs.Data[1] = stackN2;
            graphNs.Data[2] = stackRN1; graphNs.Data[3] = stackRN2; graphNs.Data[4] = stackNtot;
            graphNs.Data[5] = stackB2; graphNs.Data[6] = stackBtot;
            return true;
        }

        /// <summary>
        /// Save table text file with trends {"N1", "N2", "RN1", "RN2", "NTot"}
        /// </summary>
        /// <param name="fn"></param>
        /// <param name="rem"></param>
        public void SaveSignal(string fn, string rem)
        {
            if (stackN1.Count == 0)
            {
                MessageBox.Show("Error: No signal data to be saved");
                return;
            }
            DictFileLogger dlog = new DictFileLogger(new string[] { "XAxis", "N1", "N2", "RN1", "RN2", "NTot", "B2", "BTot" }, prefix, fn);
            dlog.setMMexecAsHeader(grpMme.Clone());
            if (!String.IsNullOrEmpty(rem)) dlog.subheaders.Add("Rem=" + rem);
            dlog.Enabled = true;
            writeHeaders(dlog);
            for (int i = 0; i < stackN1.Count; i++)
            {
                Dictionary<string, double> row = new Dictionary<string, double>();
                row["XAxis"] = stackN1[i].X; row["N1"] = stackN1[i].Y; row["N2"] = stackN2[i].Y;
                row["RN1"] = stackRN1[i].Y; row["RN2"] = stackRN2[i].Y; row["NTot"] = stackNtot[i].Y;
                row["B2"] = stackB2[i].Y; row["BTot"] = stackBtot[i].Y;
                dlog.dictLog(row, genOptions.SaveFilePrec);
            }
            dlog.Enabled = false;
        }

        /// <summary>
        /// Update visuals modes from internal ones
        /// </summary>
        public void OpenDefaultModes()
        {
            chkAutoScaleMiddle.IsChecked = genModes.AutoScaleMiddle;
            chkDarkcurrent.IsChecked = genModes.DarkCurrent;
            chkStdDev.IsChecked = genModes.StdDev;
            chkN1.IsChecked = genModes.N1;
            chkN2.IsChecked = genModes.N2;
            chkRN1.IsChecked = genModes.RN1;
            chkRN2.IsChecked = genModes.RN2;
            chkNtot.IsChecked = genModes.Ntot;
            chkB2.IsChecked = genModes.B2;
            chkBtot.IsChecked = genModes.Btot;
        }

        /// <summary>
        /// Update internal from visuals modes 
        /// </summary>
        public void SaveDefaultModes()
        {
            genModes.AutoScaleMiddle = chkAutoScaleMiddle.IsChecked.Value;
            genModes.DarkCurrent = chkDarkcurrent.IsChecked.Value;
            genModes.StdDev = chkStdDev.IsChecked.Value;
            genModes.N1 = chkN1.IsChecked.Value;
            genModes.N2 = chkN2.IsChecked.Value;
            genModes.RN1 = chkRN1.IsChecked.Value;
            genModes.RN2 = chkRN2.IsChecked.Value;
            genModes.Ntot = chkNtot.IsChecked.Value;
            genModes.B2 = chkB2.IsChecked.Value;
            genModes.Btot = chkBtot.IsChecked.Value;
        }

        /// <summary>
        /// copyGraphToClipboard(graphNs)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void graphNs_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) && (e.Key == Key.C))
                Utils.copyGraphToClipboard(graphNs);
        }

        /// <summary>
        /// Reset zoom on double click
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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

        /// <summary>
        /// Select/deselect individual trend series
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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
            if (plotB2 != null)
            {
                if (chkB2.IsChecked.Value) plotB2.Visibility = System.Windows.Visibility.Visible;
                else plotB2.Visibility = System.Windows.Visibility.Hidden;
            }
            if (plotBtot != null)
            {
                if (chkBtot.IsChecked.Value) plotBtot.Visibility = System.Windows.Visibility.Visible;
                else plotBtot.Visibility = System.Windows.Visibility.Hidden;
            }
        }
    }
}
