using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Diagnostics;
using System.Windows.Controls.Primitives;
using System.Threading.Tasks.Dataflow;

using NationalInstruments.Net;
using NationalInstruments;
using NationalInstruments.NetworkVariable;
using NationalInstruments.NetworkVariable.WindowsForms;
using NationalInstruments.Tdms;
using NationalInstruments.Controls;
using NationalInstruments.Controls.Rendering;
using NationalInstruments.Analysis;
using NationalInstruments.Analysis.Math;
using NationalInstruments.Analysis.SpectralMeasurements;
using NationalInstruments.Analysis.Monitoring;
using NationalInstruments.Analysis.Dsp;
using NationalInstruments.Controls.Primitives;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using AxelHMemsNS;
using UtilsNS;
using OptionsNS;

namespace Axel_hub
{
    /// <summary>
    /// Interaction logic for AxelChart.xaml
    /// </summary>
    public partial class AxelChartClass : UserControl
    {
        public AxelChartClass()
        {
            InitializeComponent();

            _Running = false;
            tabSecPlots.SelectedIndex = 4;
            //
            lblRange.Content = "Vis.Range = " + curRange.ToString() + " pnts";

            Waveform = new DataStack(DataStack.maxDepth);
        }

        GeneralOptions genOptions = null;
        Modes modes = null;
        public AxelMems axelMems = null; 
        public string prefix { get; private set; }
        public void InitOptions(ref GeneralOptions _genOptions, ref Modes _modes, ref AxelMems _axelMems, string _prefix = "")
        {
            if (Utils.isNull(_genOptions)) Utils.TimedMessageBox("Non-existant options");
            else genOptions = _genOptions;
            modes = _modes;
            prefix = _prefix;
            axelMems = _axelMems;
            seRollMean.Value = modes.RollMean;
            seShowFreq.Value = modes.ShowFreq;
            seStackDepth.Value = modes.StackDepth;
            chkChartUpdate.IsChecked = modes.ChartUpdate;
            chkTblUpdate.IsChecked = modes.TblUpdate;
            nbPowerCoeff.Value = modes.PowerCoeff;
            if (genOptions.saveVisuals) rowScroll.Height = new GridLength(modes.TopOfTopFrame);     
    
            Waveform = new DataStack(DataStack.maxDepth,prefix);
            Waveform.OnRefresh += new DataStack.RefreshHandler(Refresh);            
            Waveform.TimeSeriesMode = !rbPoints.IsChecked.Value;          
            
            resultStack = new DataStack(1000,prefix); resultStack.visualCountLimit = -1;   
            Refresh();
        }
        public DataStack resultStack;
        public int GetStackDepth() { return (int)seStackDepth.Value; }
        public void SetHWfile(string fl) 
        { 
            if (fl.Equals("")) lbHWfile.Content = "Hardware: (default)";
            else lbHWfile.Content = "Hardware: "+fl; 
        }
        private int IncomingBufferSize = 1000;
        public void SetIncomingBufferSize(int bf)
        {
            IncomingBufferSize = bf;
        }

        public double memsTemperature { get; set; }

        public double convertV2mg(double accelV, double temperV = double.NaN)
        {
            double rslt = double.NaN; bool tempComp = genOptions.TemperatureEnabled && genOptions.TemperatureCompensation && !temperV.Equals(double.NaN);
            if (prefix.Equals("X")) rslt = axelMems.memsX.accel(accelV, temperV, tempComp);
            if (prefix.Equals("Y")) rslt = axelMems.memsY.accel(accelV, temperV, tempComp);
            return rslt;
        }

        public bool testProp { get; set; }
        public void Clear() 
        {
            if(!Utils.isNull(Waveform)) Waveform.Clear(); 
            if(!Utils.isNull(resultStack)) resultStack.Clear();
            memsTemperature = double.NaN;

            graphScroll.Data[0] = null; graphOverview.Data[0] = null; graphPower.Data[0] = null; graphHisto.Data[0] = null; graphHisto.Data[1] = null;
            lboxGaussFit.Items.Clear();
            grpData.Header = "Data";
            tbRemFile.Text = "";
            lbInfo.Content = "Info: ";
            if (curRange > 1024) curRange = 1024;
            chkWindowMode.IsChecked = false; chkVisWindow_Checked(null, null);
        }

        public void SetInfo(string info = "")
        {
            Waveform.logger.header = info;
            lbInfo.Content = "Info: " + info;
        }

        public void SetWaveformDepth(int depth)
        {
            if(seStackDepth.Value < (depth*1.2)) seStackDepth.Value = (int)(depth*1.2);
        }

        public double SamplingPeriod
        {
            get;
            set;
        }

        public DataStack Waveform
        {
            get; 
            set;
        }

        private bool _Running;
        public bool Running
        {
            get { return _Running; }
            set 
            {
                Waveform.Running = value;
                if (value)
                {
                    Clear();
                    btnPause.Visibility = Visibility.Visible;
                    Waveform.TimeSeriesMode = !rbPoints.IsChecked.Value;
                    Waveform.visualCountLimit = (int)seStackDepth.Value;
                    Waveform.Depth = (int)seStackDepth.Value;
                    totalCount = 0;
                }
                else
                {
                    btnPause.Visibility = Visibility.Hidden;
                    Waveform.logger.Enabled = false;
                    Refresh(null, null);
                }
                if (Running == value) return;
                bool bb = value && chkResultsLog.IsChecked.Value;
                // turn it on
                resultStack.logger.Enabled = false;
                if (bb) resultStack.logger = new FileLogger(prefix); 
                resultStack.logger.Enabled = bb;
                if (value)
                {
                    resultStack.logger.log("# " + lbHWfile.Content);
                    if (genOptions.TemperatureEnabled) resultStack.logger.log("#time\tMEMS\tTemperature");
                    else resultStack.logger.log("#time\tMEMS\tStDsp");
                    resultStack.Clear();
                } 
                _Running = value;
            }
        }

        private DataStack rescaleX(DataStack pntsIn) 
        {
            // internally x must be in sec !!!
            if (rbSec.IsChecked.Value) return pntsIn;
            DataStack pntsOut = new DataStack(DataStack.maxDepth);
            if (rbPoints.IsChecked.Value)
            {
                if (!Waveform.TimeSeriesMode) return pntsIn;
                else
                {
                    for (int i = 0; i < pntsIn.Count; i++)
                    {
                        pntsOut.Add(new Point(i, pntsIn[i].Y));
                    }
                    return pntsOut;
                }
            }
            double factor = 1;
            if (rbMiliSec.IsChecked.Value) factor = 1000;
            if (rbMicroSec.IsChecked.Value) factor = 1000000;
            for (int i = 0; i < pntsIn.Count; i++)
            {
                pntsOut.Add(new Point(factor * pntsIn[i].X, pntsIn[i].Y));
            }
            return pntsOut;
        }

        private int curRange = 256; // number of points shown
        private const int maxVisual = 10000;
        private bool pauseFlag = false;
        private int totalCount = 0;
        public void Refresh()
        {
            Refresh(null, null);
        }

        private void Refresh(object sender, RoutedEventArgs e)
        {
            if (Utils.isNull(Waveform)) return;
            if (Waveform.Count == 0)
            {
                Clear();
                return;
            } 
            double mn = 0.0, dsp = 0.0, mV = 1000.0; bool bb,bc = true; 
            if (numTimeSlice.Value < 0) bb = Waveform.statsByIdx(Waveform.Count - 1 - IncomingBufferSize, Waveform.Count - 1, false, out mn, out dsp);
            else bb = Waveform.statsByTime(double.NaN, numTimeSlice.Value / 1000, false, out mn, out dsp);
            if (genOptions.TemperatureEnabled) 
            {
                if (!Double.IsNaN(memsTemperature))
                    resultStack.AddPoint(mV * memsTemperature, mV * mn);
            }
            else 
            {
                resultStack.AddPoint(mV * dsp, mV * mn); 
            }

            if (Visibility != System.Windows.Visibility.Visible) return; // cut short if collapsed
            
            if((bb && bc) && (chkTblUpdate.IsChecked.Value && (tabSecPlots.SelectedIndex == 4)))
            {
                Application.Current.Dispatcher.BeginInvoke( DispatcherPriority.Background,
                    new Action(() =>
                    {
                        ListBoxItem lbi;                            
                        
                        lbi = (ListBoxItem)lbMean.Items[0]; lbi.Content = (mV * mn).ToString("G7");
                        lbi = (ListBoxItem)lbStDev.Items[0]; lbi.Content = (mV * dsp).ToString("G7"); // mV

                        double k = 1e6 / 6000.12;
                        lbi = (ListBoxItem)lbMean.Items[1]; lbi.Content = (k * mn).ToString("G7");
                        lbi = (ListBoxItem)lbStDev.Items[1]; lbi.Content = (k * dsp).ToString("G7"); // uA

                        lbi = (ListBoxItem)lbMean.Items[2]; lbi.Content = convertV2mg(mn,memsTemperature).ToString("G7");
                        lbi = (ListBoxItem)lbStDev.Items[2]; lbi.Content = convertV2mg(dsp).ToString("G7"); // mg

                        totalCount++;
                        lbi = (ListBoxItem)lbMean.Items[3]; lbi.Content = "# " + totalCount.ToString();
                        lbi = (ListBoxItem)lbStDev.Items[3]; lbi.Content = (Waveform.stopWatch.ElapsedMilliseconds / 1000.0).ToString("G5"); //resultStack.pointSDev().Y.ToString("G7"); // # and StDev

                        if (genOptions.TemperatureEnabled)
                        {
                            lbi = (ListBoxItem)lbMean.Items[4]; lbi.Content = (mV * memsTemperature).ToString("G7");
                            lbi = (ListBoxItem)lbStDev.Items[4]; lbi.Content = "---"; 
                        }
                        else
                        {
                            lbi = (ListBoxItem)lbMean.Items[4]; lbi.Content = "Temperature"; 
                            lbi = (ListBoxItem)lbStDev.Items[4]; lbi.Content = "<disabled>"; 
                        }
                        numTimeSlice.Background = Brushes.White;
                    }));
            }
            lbErrorStatus.Content = "Error status: "+Waveform.lastError;    
            
            if (!chkChartUpdate.IsChecked.Value) return;
            //Console.WriteLine("refresh at " + (Waveform.stopWatch.ElapsedMillseconds/1000.0).ToString());
            lblRange.Content = "Vis.Range = " + curRange.ToString() + " pnts";

            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                DataStack pA, pB;
                pA = Waveform.CopyEach((int)seShowFreq.Value);
                pB = rescaleX(pA);

                if (!rbPoints.IsChecked.Value) // time
                {
                    int k = 0;
                    if (rbSec.IsChecked.Value) k = 1;
                    if (rbMiliSec.IsChecked.Value) k = 1000;
                    if (rbMicroSec.IsChecked.Value) k = 1000000;

                    if (curRange >= pB.Count)
                    {
                        if (pB.Count > 0)
                            ((AxisDouble)graphScroll.Axes[0]).Range = new Range<double>(k * pB[0].X, k * (pB[0].X + curRange * SamplingPeriod));
                    }
                    else
                    {
                        double xEnd = pB[pB.Count - 1].X;
                        ((AxisDouble)graphScroll.Axes[0]).Range = new Range<double>(k * (xEnd - curRange * SamplingPeriod), k * xEnd);
                    }
                }
                else // points
                {
                    double xEnd = pB[pB.Count-1].X; 
                    ((AxisDouble)graphScroll.Axes[0]).Range = new Range<double>(Math.Max(0,xEnd - curRange), xEnd);
                }
                Application.Current.Dispatcher.BeginInvoke( DispatcherPriority.Background, 
                  new Action(() => { graphScroll.Data[0] = pB; }));
            
                switch (tabSecPlots.SelectedIndex) 
                {
                    case 0: if(!Utils.isNull(graphOverview.Data[0])) graphOverview.Data[0] = null; // disable
                            break;
                    case 1: if (pB.Count > maxVisual) // OVERVIEW
                            {
                                Utils.TimedMessageBox("The data length is too high (" + pB.Count.ToString() + "). Showing the last "+maxVisual.ToString()+" points.");
                                while (pB.Count > maxVisual) pB.RemoveAt(0);
                            }
                            graphOverview.Data[0] = pB;
                            break;
                    case 2: double df; // FFT TRANSFORM
                            if (SamplingPeriod == 0) SamplingPeriod = (Waveform.pointXs()[Waveform.Count - 1] - Waveform.pointXs()[0]) / Waveform.Count;
                            //Measurements.AmplitudePhaseSpectrum(Ys, false, SamplingPeriod , out ampl, out phase, out df); 
                            double[] ps = Measurements.AutoPowerSpectrum(Waveform.pointYs(), SamplingPeriod, out df);  
                            DataStack pl = new DataStack(DataStack.maxDepth);
                            int len = Math.Min(ps.Length, 300000); // limit the lenght for visual speed
                            for (int i = 1; i < len; i++) // skip DC component at position 0
                            {
                                if (ps[i] < 0.0) continue; // skip negatives
                                pl.Add(new Point(i * df, nbPowerCoeff.Value * Math.Sqrt(ps[i]))); 
                            }                        
                            graphPower.Data[0] = pl;
                            break;
                    case 3: graphHisto.Data[0] = Histogram(Waveform); // HISTOGRAM
                            break;
                }
                //DoEvents();
                if (btnPause.Foreground == Brushes.Black) btnPause.Foreground = Brushes.Navy;
                else btnPause.Foreground = Brushes.Black;;
                while (pauseFlag) 
                {
                    Utils.DoEvents();
                    System.Threading.Thread.Sleep(10);
                }
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }
  
        private double defaultRowRatio = 0;
        private double hiddenHeight = 28;

        private void tabSecPlots_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Utils.isNull(Waveform)) return;
            if (tabSecPlots.SelectedIndex == 0)
            {
                defaultRowRatio = gridAC.RowDefinitions[1].ActualHeight / gridAC.ActualHeight;
                gridAC.RowDefinitions[1].Height = new GridLength(hiddenHeight);
            }
            else
            {
                if ((defaultRowRatio > 0) && (gridAC.RowDefinitions[1].ActualHeight < (hiddenHeight+5)))
                {
                    gridAC.RowDefinitions[1].Height = new GridLength(gridAC.ActualHeight * defaultRowRatio); 
                }
            }
            Refresh();
        }

        private void btnZoomOut_Click(object sender, RoutedEventArgs e)
        {
            curRange = (int)(curRange * 2);            
            Refresh();
        }

        private void btnZoomIn_Click(object sender, RoutedEventArgs e)
        {
            if (curRange > 2) curRange = (int)(curRange / 2);
            Refresh();
        }

        private void btnPause_Click(object sender, RoutedEventArgs e)
        {
            pauseFlag = !pauseFlag; 
            if (pauseFlag) 
            {
                btnPause.Content = "Cont...";
            }
            else 
            {
                btnPause.Content = "Pause";
            }    
        }
        #region File Operations
        //ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff
        private void btnSaveAs_Click(object sender, RoutedEventArgs e)
        {
            if (Waveform.Count == 0) throw new Exception("No data to be saved");
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.FileName = ""; // Default file name
            dlg.DefaultExt = ".ahf"; // Default file extension
            dlg.Filter = "Axel Hib File (.ahf)|*.ahf|"+"Axel Boss File (.abf)|*.abf"; // Filter files by extension

            // Show save file dialog box
            Nullable<bool> result = dlg.ShowDialog();

            // Process save file dialog box results
            if (result == true)
            {
                // Save file                
                Waveform.SavePair(dlg.FileName, tbRemFile.Text);
                lbInfo.Content = "Saved: " + dlg.FileName; 
            }
        }

        public void Open(string fn)
        {
            if (!File.Exists(fn)) throw new Exception("File <" + fn + "> does not exist.");
            Clear();
            string ext = System.IO.Path.GetExtension(fn);
            //if (ext.Equals(".abf") || ext.Equals(".ahf"))
            {
                
                foreach (string line in File.ReadLines(fn))
                {
                    if (line.Contains("#{"))
                    {
                        SetInfo(line.Substring(1));                    
                    }
                    if (line.Contains("#Rem="))
                    {
                        tbRemFile.Text = line.Substring(5); 
                    }
                }
                if (!Waveform.OpenPair(fn, ref grpData, modes.RollMean)) Utils.TimedMessageBox("Some data might be missing");
                grpData.Header = "Data pnts: " + Waveform.Count.ToString();
                string info = (string)lbInfo.Content; info = info.Replace("Info: ", "");
                if (String.IsNullOrEmpty(info))
                {
                    SamplingPeriod = (Waveform.Last.X - Waveform.First.X) / Waveform.Count;
                }
                else
                {
                    Dictionary<string, object> remotePrms = JsonConvert.DeserializeObject<Dictionary<string, object>>(info);
                    SamplingPeriod = (double)remotePrms["SamplingPeriod"];
                }
                Waveform.TimeSeriesMode = !(Utils.InRange(SamplingPeriod, 0.99, 1.01));  // sampling with 1 Hz is reserved for 
                if (Waveform.TimeSeriesMode) 
                {
                    if (!rbSec.IsChecked.Value && !rbMiliSec.IsChecked.Value && !rbMicroSec.IsChecked.Value) rbSec.IsChecked = true;
                }
                rbPoints.IsChecked = !Waveform.TimeSeriesMode;
              //  tbLog.AppendText("Count= " + Waveform.Count.ToString() + "\n");
              //  tbLog.AppendText("Sampling period= " + SamplingPeriod.ToString("G3") + "\n");
              //  tbLog.AppendText("^^^^^^^^^^^^^^^^^^^\n");
            }
                
        }

        private void btnOpen_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.FileName = ""; // Default file name
            dlg.DefaultExt = ".ahf"; // Default file extension
            dlg.Filter = "Axel Hub File (.ahf)|*.ahf|" + "All files (*.*)|*.*"; ; // Filter files by extension

            // Show save file dialog box
            Nullable<bool> result = dlg.ShowDialog();

            // Process save file dialog box results
            if (result == true)
            {
                // Open file                
                Open(dlg.FileName);
                Refresh();
                lbInfo.Content = "Opened: " + dlg.FileName; 
            }
        }
        #endregion
        //+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        private void graphOverview_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ((Graph)sender).ResetZoomPan();
        }

        public void btnCpyPic_Click(object sender, RoutedEventArgs e)
        {
            Graph graph = null; Rect bounds; RenderTargetBitmap bitmap;
            if (sender is Button)
            {
                Button btn = sender as Button;
                if (sender == btnCpyPic1) graph = graphOverview;
                if (sender == btnCpyPic2) graph = graphPower;
                if (sender == btnCpyPic3) graph = graphHisto;
                if (Utils.isNull(graph)) return;
                bounds = LayoutInformation.GetLayoutSlot(tabSecPlots);//graph ); 
                bitmap = new RenderTargetBitmap((int)bounds.Width, (int)bounds.Height, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(tabSecPlots);
                Clipboard.SetImage(bitmap);
                Utils.TimedMessageBox("The image is in the clipboard");
            }
            if (sender is Graph)
            {
                graph = (sender as Graph);
                if (Utils.isNull(graph)) return;
                bounds = LayoutInformation.GetLayoutSlot(graph);
                bitmap = new RenderTargetBitmap((int)bounds.Width, (int)bounds.Height, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(graph);
                Clipboard.SetImage(bitmap);
                Utils.TimedMessageBox("The image is in the clipboard");
            }
        }

        public DataStack Histogram(DataStack src, int numBins = 300)
        {
            if (src.Count == 0) throw new Exception("No data for the histogram");
            DataStack rslt = new DataStack(DataStack.maxDepth);
            double[] centerValues, Ys;
            Ys = src.pointYs();
            double mean = Ys.Average(); double stDev = src.pointSDev().Y;
            int[] histo = Statistics.Histogram(Ys, mean - 2.5 * stDev, mean + 2.5 * stDev, numBins, out centerValues);
            if (histo.Length != centerValues.Length) throw new Exception("histogram trouble!");
            for (int i = 0; i < histo.Length; i++)
            {
                rslt.Add(new Point(centerValues[i], histo[i]));
            }
            return rslt;
        }

        public void modesFromVisual()
        {
            if(Utils.isNull(modes)) return;

            modes.RollMean = (int)seRollMean.Value;
            modes.ShowFreq = (int)seShowFreq.Value;
            modes.StackDepth = (int)seStackDepth.Value;
            modes.ChartUpdate = (bool)chkChartUpdate.IsChecked.Value;
            modes.TblUpdate = (bool)chkTblUpdate.IsChecked.Value;
            modes.PowerCoeff = nbPowerCoeff.Value;

            if (Utils.isNull(seStackDepth) || Utils.isNull(Waveform)) return;
            Waveform.Depth = (int)seStackDepth.Value;            
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            Clear();
        }

        private void graphOverview_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                if (e.Key == Key.C) 
                { btnCpyPic_Click(sender, null); }
            }
        }

        private void btnCpyDta3_Click(object sender, RoutedEventArgs e)
        {
            List<Point> ds = graphHisto.Data[0] as List<Point>;
            if (ds.Count == 0)
            {
                Utils.TimedMessageBox("No data to process.");
                return;
            }
            string ss = "";
            foreach (Point pnt in ds) 
            {
                ss += pnt.X.ToString("G7") + "\t" + pnt.Y.ToString("G7")+"\r";
            }
            Clipboard.SetText(ss);
            Utils.TimedMessageBox("The data is in the clipboard");
        }

        private void btnCpyDta2_Click(object sender, RoutedEventArgs e)
        {
            List<Point> ds = graphPower.Data[0] as List<Point>;
            if (ds.Count == 0)
            {
                Utils.TimedMessageBox("No data to process.");
                return;
            }
            string ss = "";
            foreach (Point pnt in ds)
            {
                ss += pnt.X.ToString("G7") + "\t" + pnt.Y.ToString("G7") + "\r";
            }
            Clipboard.SetText(ss);
            Utils.TimedMessageBox("The data is in the clipboard");
        }

        private void btnGaussFit_Click(object sender, RoutedEventArgs e)
        {
            if ((graphHisto.Data[0] as DataStack) == null) 
            {
                Utils.TimedMessageBox("No data to process");
                return;
            }
            DataStack ds = (graphHisto.Data[0] as DataStack);
            DataStack wave = Waveform.Clone();

            if (chkWindowMode.IsChecked.Value)
            {
                DataStack dt = new DataStack(ds.Depth);
                double low = Convert.ToDouble(rangeCursorHisto.ActualHorizontalRange.GetMinimum().ToString());
                double high = Convert.ToDouble(rangeCursorHisto.ActualHorizontalRange.GetMaximum().ToString());  
                foreach(Point pnt in ds)
                {
                    if (Utils.InRange(pnt.X, low, high)) dt.Add(pnt);
                }
                ds = dt.Clone();
                wave.Clear();
                foreach (Point pnt in Waveform)
                {
                    if (Utils.InRange(pnt.Y, low, high)) wave.Add(pnt);
                }
            }
            double[] wg = new double[ds.Count];
            for (int i = 0; i < wg.Length; i++) wg[i] = 1;
            double ampl,center,stdDev,res;
            double[] fitYs = CurveFit.GaussianFit(ds.pointXs(), ds.pointYs(), FitMethod.Bisquare, wg, 0, out ampl, out center, out stdDev, out res);

            lboxGaussFit.Items.Clear();
            string format = "G7";
            ListBoxItem lbi = new ListBoxItem(); lbi.Content = "Center= " + center.ToString(format); lbi.Foreground = Brushes.Navy;
            lboxGaussFit.Items.Add(lbi);
            lbi = new ListBoxItem(); lbi.Content = "stdDev= " + stdDev.ToString(format); lbi.Foreground = Brushes.Navy;
            lboxGaussFit.Items.Add(lbi);
            lbi = new ListBoxItem(); lbi.Content = "Amplitude= " + ampl.ToString(format); lbi.Foreground = Brushes.Navy;
            lboxGaussFit.Items.Add(lbi);
            lbi = new ListBoxItem(); lbi.Content = "Residuals= " + res.ToString(format); lbi.Foreground = Brushes.Navy;
            lboxGaussFit.Items.Add(lbi);
            lbi = new ListBoxItem(); lbi.Content = "Raw Mean= " + wave.pointYs().Average().ToString(format); lbi.Foreground = Brushes.DarkGreen;
            lboxGaussFit.Items.Add(lbi);
            lbi = new ListBoxItem(); lbi.Content = "Raw SDev= " + wave.pointSDev().Y.ToString(format); lbi.Foreground = Brushes.DarkGreen;
            lboxGaussFit.Items.Add(lbi);

            DataStack fit = new DataStack(DataStack.maxDepth);
            fit.importFromArrays(ds.pointXs(), fitYs);
            graphHisto.Data[1] = fit;
        }

        private void horAxisScroll_RangeChanged(object sender, ValueChangedEventArgs<Range<double>> e)
        {
            Range<double> oldVal = e.OldValue;
            Range<double> newVal = e.NewValue;            
        }

        private void btnSplit_Click(object sender, RoutedEventArgs e)
        {
            DataStack stack = new DataStack(DataStack.maxDepth); int i = 0; bool bl;
            double thre = Convert.ToDouble(tbSplitLevel.Text);
            int loose = Convert.ToInt16(tbSplitEdges.Text); ;
            int front = -1; 
            foreach (Point p in Waveform)
            {  
                if(chkUpperSplit.IsChecked.Value) bl = (p.Y > thre);
                else bl = (p.Y < thre); 
                if (bl)
                {
                    if (front == -1) front = i;
                    if ((front > -1) && ((i - front) > loose))
                    {
                        stack.Add(p);
                    }
                }
                else
                {
                    if ((front > -1) && ((i - front) > loose))
                    {
                        front = -1;
                        for (int j = 0; j < loose; j++)
                            if (stack.Count>0)
                                stack.RemoveAt(stack.Count-1);
                    }
                }
                i += 1;
            }
            Waveform = stack.Clone();
            if (stack.Count == 0) Utils.TimedMessageBox("No data left !");
            Refresh();
        }

        private void btnExtractPart_Click(object sender, RoutedEventArgs e)
        {
            int highI = Waveform.indexByX(horAxisScroll.Range.Maximum);
            if (highI > -1) Waveform.RemoveRange(highI, Waveform.Count - highI);
            int lowI = Waveform.indexByX(horAxisScroll.Range.Minimum); 
            if (lowI > -1) Waveform.RemoveRange(0, lowI);
            Refresh();
        }

        private void chkVisWindow_Checked(object sender, RoutedEventArgs e)
        {
            if (chkWindowMode.IsChecked.Value) 
            {
                rangeCursorHisto.Visibility = System.Windows.Visibility.Visible;
                double low = ((AxisDouble)graphHisto.Axes[0]).Range.Minimum;
                double high = ((AxisDouble)graphHisto.Axes[0]).Range.Maximum;
                double rng = high - low;
                rangeCursorHisto.HorizontalRange = new Range<double>(low + 0.25*rng, high - 0.25*rng);
            }               
            else rangeCursorHisto.Visibility = System.Windows.Visibility.Collapsed;
        }

        private void splitter1_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!Utils.isNull(modes))
                modes.TopOfTopFrame = rowScroll.Height.Value;
        }

        private void splitter1_LayoutUpdated(object sender, EventArgs e)
        {
            if (!Utils.isNull(modes))
                modes.TopOfTopFrame = rowScroll.Height.Value;
        }

        private void UserControl_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5) Refresh(); 
        }

        private void chkAutoScale_Checked(object sender, RoutedEventArgs e)
        {
            if (Utils.isNull(verAxisScroll)) return;
            if (chkAutoScale.IsChecked.Value)
            {
                verAxisScroll.InteractionMode = ScaleInteractionModes.None;
            }
            else
            {
                verAxisScroll.InteractionMode = ScaleInteractionModes.EditRange;
            }
            scrollPlot.AdjustVerticalScale = chkAutoScale.IsChecked.Value;
        }

        public void set2startADC24(bool down, double samplingPeriod, int InnerBufferSize)
        {
            if (!down) // user cancel
            {
                Running = false;
                Waveform.logger.Enabled = false;
                return;
            }
            if (Running) Running = false; //
            Clear();
            Waveform.StackMode = true; 
            SetWaveformDepth(InnerBufferSize);
            SamplingPeriod = samplingPeriod; //1 / axelMems.RealConvRate(1 / period);
            Running = true; Waveform.visualCountLimit = InnerBufferSize;

            SetInfo("freq: " + (1 / SamplingPeriod).ToString("G6") + ", aqcPnt: " + InnerBufferSize.ToString());
            Waveform.logger.Enabled = false;
        }
    }
}

