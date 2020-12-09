using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Threading;
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
using UnscentedKalmanFilter; // modify for a new implementation
using Axel_hub;
using UtilsNS;

namespace Axel_unscented
{
    public interface IKalman // everything factor specific 
    {
        void Init(int mNum = 1, Dictionary<string, double> prms = null); // number of measurements; parameters of the filter (null - defaults)
        Dictionary<string, double> parameters { get; } // init.parameters and some intermedia results
        void Update(double[] measurements); 
        double[] getState();
    }

    /// <summary>
    /// Interaction logic for kalmanUC.xaml
    /// </summary>
    public partial class kalmanUC : UserControl
    {
        DispatcherTimer dTimer;
        
        DataStack T, M1, M2, P, K, K2; int dataLen = 300;
        UKF kalman; IKalman iKalman; // 1 of 2 places to modify for a new Kalman implementation
        public kalmanUC()
        {
            InitializeComponent();
            dTimer = new DispatcherTimer(DispatcherPriority.Send);
            dTimer.Interval = new TimeSpan(300 * 10000); // 300 ms
            dTimer.Tick += new EventHandler(dTimer_Tick);
            T = new DataStack(dataLen);  graphMove.Data[0] = T;  // common trend 
            M1 = new DataStack(dataLen); graphMove.Data[1] = M1; // measurement source 1
            M2 = new DataStack(dataLen); graphMove.Data[2] = M2; // measurement source 2
            P = new DataStack(dataLen);  graphMove.Data[3] = P;  // predicted by some other method (ARIMA,RNN, ...) 
            K = new DataStack(dataLen);  graphMove.Data[4] = K;  // Kalman result for MS1 only 
            K2 = new DataStack(dataLen); graphMove.Data[5] = K2;  // Kalman result 2 for MS1 & MS2  
        }

        public void Init()
        {
            Dictionary<string, double> prms = null;
            if (Utils.isNull(kalman))  // first call
            {
                vlUFKparams.SetHeaders("Parameter", "Value");
             }    
            else // second, third...
            {
                prms = vlUFKparams.items; // take the visuals
            }    
            kalman = new UKF(); iKalman = (IKalman)kalman; // 2 of 2 places to modify
            int k = (chkEnabledM2.IsChecked.Value) ? 2 : 1;
            iKalman.Init(k,prms);      
            vlUFKparams.items = kalman.parameters;         
        }
        public delegate void LogHandler(string txt, bool detail = false, Color? clr = null);
        public event LogHandler OnLog;

        protected void LogEvent(string txt, bool detail = false, Color? clr = null)
        {
            if (OnLog != null) OnLog(txt, detail, clr);
        }

        private void chkMEMS_Checked(object sender, RoutedEventArgs e)
        {
            if (Utils.isNull(plotM1)) return;
            if (chkM1.IsChecked.Value) plotM1.Visibility = System.Windows.Visibility.Visible;
            else plotM1.Visibility = System.Windows.Visibility.Hidden;
            if (chkM2.IsChecked.Value) plotM2.Visibility = System.Windows.Visibility.Visible;
            else plotM2.Visibility = System.Windows.Visibility.Hidden;
            if (chkP.IsChecked.Value) plotP.Visibility = System.Windows.Visibility.Visible;
            else plotP.Visibility = System.Windows.Visibility.Hidden;
            if (chkK.IsChecked.Value) plotK.Visibility = System.Windows.Visibility.Visible;
            else plotK.Visibility = System.Windows.Visibility.Hidden;
            if (chkK2.IsChecked.Value) plotK2.Visibility = System.Windows.Visibility.Visible;
            else plotK2.Visibility = System.Windows.Visibility.Hidden;
        }

        public void Clear()
        {
            T.Clear(); M1.Clear(); M2.Clear(); P.Clear(); K.Clear(); K2.Clear(); 
        }
        public int steps 
        { 
            get 
            {
                if (M1.Count == 0) return 0;
                else return (int)M1.Last.X; 
            } 
        } 
        private void btnRun_Click(object sender, RoutedEventArgs e)
        {
            btnRun.Value = !btnRun.Value;
            if (btnRun.Value)
            {
                Clear(); dTimer.Start();
                Init();
            }
            else dTimer.Stop();
            chkEnabledM2.IsEnabled = !dTimer.IsEnabled;
        }

        private void dTimer_Tick(object sender, EventArgs e)
        {
            double m1 = 0; double m2 = 0; double[] measurement; 
            double trend = numAmpl.Value * Math.Sin((steps) * 3.14 * numFreq.Value / 180);
            switch (cbCommonTrend.SelectedIndex)
            {
                case (1):
                    if (trend > 0) trend = numAmpl.Value;
                    if (trend < 0) trend = -numAmpl.Value;
                    break;
            }
            
            T.AddPoint(trend); graphMove.Data[0] = T;
            if (chkGuassM1.IsChecked.Value) m1 = Utils.Gauss01()* numGaussM1.Value;
            M1.AddPoint(trend+m1); graphMove.Data[1] = M1; 
            if (chkEnabledM2.IsChecked.Value)
            {
                m2 = numAmplM2.Value * Math.Sin(steps * numFreqM2.Value * Math.PI / 180);
                if (chkGuassM2.IsChecked.Value) m2 += Utils.Gauss01() * numGaussM2.Value;
                M2.AddPoint(trend + m2); graphMove.Data[2] = M2;
                measurement = new[] { trend + m1, trend + m2 };
            }
            else measurement = new[] { (trend + m1) }; //graphMove.Data[3] = P;
            iKalman.Update(measurement);
            double[] state = iKalman.getState();             
            K.AddPoint(state[0]); graphMove.Data[4] = K;
            if (state.Length > 1)
            {
                K2.AddPoint(state[1]); graphMove.Data[5] = K2;
            }
            if ((steps % 10).Equals(0)) vlUFKparams.items = kalman.parameters;            
            // Forcing the CommandManager to raise the RequerySuggested event
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            double[,] cov = kalman.getCovariance();
            LogEvent("Cov: "+cov[0, 0].ToString("G4"));
        }
    }
    
}
