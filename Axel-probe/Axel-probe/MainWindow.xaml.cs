using System;
using System.Collections.Generic;
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
using System.Windows.Threading;
using System.Diagnostics;
using NationalInstruments.Controls;
using NationalInstruments.Analysis.Math;


namespace Axel_probe
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {   
        ChartCollection<Point> fringes, ramp, corr;
        List<double> iStack, dStack, corrList;  
        public MainWindow()
        {
            InitializeComponent();

            fringes = new ChartCollection<Point>();
            grFringes.DataSource = fringes;

            ramp = new ChartCollection<Point>();
            grRslt.Data[0] = ramp;
            corr = new ChartCollection<Point>();
            grRslt.Data[1] = corr;

            iStack = new List<double>(); dStack = new List<double>(); corrList = new List<double>(); 
         }

        public void DoEvents()
        {
            DispatcherFrame frame = new DispatcherFrame(); //
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, //
                new DispatcherOperationCallback(ExitFrame), frame);
            Dispatcher.PushFrame(frame);
        }

        public object ExitFrame(object f)
        {
            ((DispatcherFrame)f).Continue = false;
            return null;
        }

        private int getIfringe(double x)
        {
            for (int i = 0; i < fringes.Count-1; i++)
            {
                if ((fringes[i].X < x) && (x <= fringes[i+1].X)) return i;
            }
            return -1;
        } 

        private void log(string txt)
        {
            if (!chkLog.IsChecked.Value) return;
            tbLog.AppendText(txt + "\r\n");
            tbLog.Focus();
            tbLog.CaretIndex = tbLog.Text.Length;
            tbLog.ScrollToEnd();
        }

        Random rand = new Random(); 
        public double Gauss()
        {
            if ((!chkAddGauss.IsChecked.Value) || (ndGaussSigma.Value <= 0.1)) return 0;           
            double u1 = 1.0 - rand.NextDouble(); //uniform(0,1] random doubles
            double u2 = 1.0 - rand.NextDouble();
            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) *
                         Math.Sin(2.0 * Math.PI * u2); //random normal(0,1)
            return randStdNormal * ndGaussSigma.Value / 100;
        }

        public double Breathing(double x)
        {
            if (!chkVaryAmpl.IsChecked.Value) return 0;
            double per = Math.Sin(Math.PI * x / ndBreathePeriod.Value);
            double norm = ndBreatheAmpl.Value / 100;
            return per * norm;
        }

        bool stopRequest = false; 
        private void btnRun_Click(object sender, RoutedEventArgs e)
        {
            if (btnRun.Content.Equals("R U N"))
            {
                btnRun.Content = "S t o p"; btnRun.Foreground = Brushes.DarkRed;
            }
            else
            {
                btnRun.Content = "R U N"; btnRun.Foreground = Brushes.DarkGreen;
            }
            stopRequest = (btnRun.Content.Equals("R U N"));
            if (stopRequest) return;

            leftLvl = double.NaN; rightLvl = double.NaN;
            double step = ndStep.Value;
            double halfPrd = ndPeriod.Value / 2;            
            double rng = ndRange.Value;
            ((AxisDouble)grRslt.Axes[0]).Range = new Range<double>(0, ndPeriod.Value);
            ((AxisDouble)grRslt.Axes[1]).Range = new Range<double>(-rng, rng);
            ((AxisDouble)grRslt.Axes[2]).Range = new Range<double>(-0.1, 0.1);
            double slope = rng / halfPrd;
            double drift,err, pos, sp; int j;
             //tbLog.Text = ""; 
            do
            {
                ramp.Clear(); corr.Clear(); corrList.Clear();
                pos = 0; j = 0;  
                while ((pos < (ndPeriod.Value + 0.0001)) && !stopRequest)
                {
                    drift = 0;
                    switch (cbDriftType.SelectedIndex )
                    {
                        case 0: // trapeze 1323
                            if (pos < (0.8 * halfPrd) )
                            {
                                drift = slope * pos;
                            };
                            if ((pos > (0.8 * halfPrd)) && (pos < (1.2 * halfPrd))) 
                            {
                                drift = 0.8 * rng;
                            }
                            if (pos > (1.2 * halfPrd) )
                            {
                                drift = rng - slope * (pos - halfPrd);
                            }
                            break;
                        case 1: // sine
                            drift = rng * Math.Sin(2 * Math.PI * pos / ndPeriod.Value);
                            break;
                    }
                    ramp.Add(new Point(pos, drift));
                    sp = 0; //k = 0;
                    fringes.Clear();
                    while ((sp < 4 * Math.PI) && !stopRequest)
                    {
                        fringes.Add(new Point(sp, Breathing(pos) + Math.Cos(sp + drift) + Gauss()));
                        sp += Math.PI / 200;
                        //if ((k % 10) == 0) { DoEvents(); k++; }
                    }
                    if (chkFollowPID.IsChecked.Value) 
                    {
                        switch (tcStrobes.SelectedIndex)
                        {
                            case 0: err = SingleAdjust(drift);
                                    if (!double.IsNaN(err))
                                    {
                                        corr.Add(new Point(pos, err)); corrList.Add(err);
                                    }
                                break;
                            case 1: err = DoubleAdjust(j,drift);
                                    if (!double.IsNaN(err) && ((j % 2) == 1))
                                    {
                                        corr.Add(new Point(pos, err)); corrList.Add(err);
                                    }
                                break;
                        }
                    }
                    pos += step; j++;
                    do
                    {
                        DoEvents(); 
                    } while (chkPause.IsChecked.Value);
                    System.Threading.Thread.Sleep((int)ndDelay.Value);
                }                
                if (corrList.Count > 0)
                    log("Aver=" + corrList.Average().ToString("G4") + 
                        "; StDev= " + Statistics.StandardDeviation(corrList.ToArray()).ToString("G4") + "\n====================");
            } while ((cbFinite.SelectedIndex == 1) && !stopRequest);
            btnRun.Content = "R U N"; btnRun.Foreground = Brushes.DarkGreen;
        }

        private void frmMain_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            stopRequest = true;
        }

        private void grFringes_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ((Graph)sender).ResetZoomPan();
        }

        int iStDepth = 5; int dStDepth = 3;  
        public double PID(double curr)
        {
            int hillSide = 1;
            if (cbHillPos.SelectedIndex == 0) hillSide = -1;
            double pTerm = curr;
            iStack.Add(curr); while (iStack.Count > iStDepth) iStack.RemoveAt(0);
            double iTerm = iStack.Average();
            dStack.Add(curr); while (dStack.Count > dStDepth) dStack.RemoveAt(0);
            double dTerm = 0;
            for (int i = 0; i < dStack.Count - 1; i++)
            {
                dTerm += dStack[i + 1] - dStack[i];
            }
            dTerm /= Math.Max(dStack.Count - 1, 1);
            
            double cr = hillSide * (ndKP.Value * pTerm + ndKI.Value * iTerm + ndKD.Value * dTerm);
            
            int curIdx1 = getIfringe((double)crsFringes1.AxisValue+cr);

            log("PID= " + pTerm.ToString("G3") + " / " + iTerm.ToString("G3") + " / " + dTerm.ToString("G3") +
                // PID X correction and Y value after the correction
                " | " + cr.ToString("G3") + " / " + fringes[curIdx1].Y.ToString("G3"));  
            return cr;
        }
        
        private void tcStrobes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (tcStrobes.SelectedIndex == 0)
            {
                crsFringes2.Visibility = System.Windows.Visibility.Hidden;
                crsFringes1.AxisValue = 7.85;
            }
            else
            {
                crsFringes2.Visibility = System.Windows.Visibility.Visible;
                crsFringes1.AxisValue = 4.71;
                crsFringes1.AxisValue = 7.85;
                leftLvl = double.NaN; rightLvl = double.NaN;
            }
        }

        private double SingleAdjust(double drift) // return error in fringe position/phase
        {
            double curX = (double)crsFringes1.AxisValue;
            int curIdx = getIfringe(curX);
            if (curIdx == -1)
            {
                log("Error: index cannot be found"); return double.NaN;
            }
            double corr = PID(fringes[curIdx].Y); 
            crsFringes1.AxisValue = curX + corr;
            curIdx = getIfringe((double)crsFringes1.AxisValue);
            if (curIdx > -1) return drift - (2.5 * Math.PI - fringes[curIdx].X); 
            else return double.NaN;
        }

        double leftLvl = double.NaN, rightLvl = double.NaN;
        private double DoubleAdjust(int idx, double drift) // return error in fringe position/phase
        {
            int curIdx1, curIdx2; double cx, curX1, curX2;
            if ((idx % 2) == 0) // even
            {
                curX1 = (double)crsFringes1.AxisValue;
                curIdx1 = getIfringe(curX1);
                if (curIdx1 == -1)
                {
                    log("Error: index cannot be found"); return double.NaN;
                }
                leftLvl = fringes[curIdx1].Y;
                if(double.IsNaN(rightLvl)) return double.NaN;
            }
            else // odd
            {            
                curX2 = (double)crsFringes2.AxisValue;
                curIdx2 = getIfringe(curX2);
                if (curIdx2 == -1)
                {
                    log("Error: index cannot be found"); return double.NaN;
                }
                rightLvl = fringes[curIdx2].Y;
            }
            double corr = PID(leftLvl - rightLvl);
            crsFringes1.AxisValue = (double)crsFringes1.AxisValue + corr; // adjust left
            crsFringes2.AxisValue = (double)crsFringes2.AxisValue + corr; // adjust right

            cx = ((double)crsFringes2.AxisValue + (double)crsFringes1.AxisValue) / 2;
            return drift - (2 * Math.PI - cx);
        }

        private void ndKP_KeyDown(object sender, KeyEventArgs e)
        {
           if ((e.Key == Key.L) && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
           {
               log("Coeff.PID= " + ndKP.Value.ToString("G3") + " / " + ndKI.Value.ToString("G3") + " / " + ndKD.Value.ToString("G3") + " ;");
           }              
        }

        private void ndRange_KeyDown(object sender, KeyEventArgs e)
        {
            if ((e.Key == Key.L) && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
            {
                log("Accel= " + ndRange.Value.ToString("G3") + " / " + ndPeriod.Value.ToString("G3") + " / " + ndStep.Value.ToString("G3") + " ;");
            }
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            tbLog.Text = "";
        }
     }
}
