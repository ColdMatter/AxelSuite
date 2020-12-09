using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UtilsNS;
using KalmanFilters;

namespace Axel_scented
{
    class KalmanD : IKalman
    {
        KalmanFilter1D filter1; KalmanFilterkD filter2; 
        public Dictionary<string, double> parameters
        {
            get
            {
                Dictionary<string, double> itms = new Dictionary<string, double>();
                /*itms["alpha"] = alpha;
                itms["beta"] = beta;
                itms["ki"] = ki;

                itms["lambda"] = lambda;
                itms["c"] = c;
                itms["q"] = q;
                itms["r"] = r;*/
                return itms;
            }

        }
        private int _mNum;
        public int mNum { get { return _mNum; } } 
        /// <summary>
        /// (re)initialization
        /// </summary>
        /// <param name="mNum">measurement number</param>
        /// <param name="prms">UFK parameters</param>
        public void Init(int __mNum = 1, Dictionary<string, double> prms = null)
        {
            _mNum = __mNum;
            switch (mNum)
            {
                case (1):
                    double measurement_sigma = 4.0;
                    double motion_sigma = 2.0;
                    double mu = 0;
                    double sigma = 10000;
                    filter1 = new KalmanFilter1D(mu, sigma, measurement_sigma, motion_sigma);                   
                    break;
                case (2):
                    filter2 = new KalmanFilterkD(1,2);                   
                    break;
                default: Utils.TimedMessageBox("No implementation for number of measurements = "+mNum.ToString()); return;
            }
            
        }
        public void Update(double[] measurements)
        {
            if (!mNum.Equals(measurements.Length)) throw new Exception("Wrong number of measurements");
            switch (mNum)
            {
                case (1):
                    filter1.Update(measurements[0]);
                    break;
                case (2):
                    filter2.Update(measurements);
                    break;
            }
        }
        public double[] getUncertainty()
        {
            double[] uncert = null;
            switch (mNum)
            {
                case (1):
                    uncert = new double[1] { filter1.Uncertainty };
                    break;
                case (2):
                    uncert = filter2.MeasurementUncertainty;
                    break;
            }
            return uncert;
        }
        public double[] getState()
        {
            double[] state = null;
            switch (mNum)
            {
                case (1):
                    state = new double[1] { filter1.State };
                    break;
                case (2):
                    state = filter2.States;
                    break;
            }
            return state;
        }
        public double getFussionState(double? defolt = null)
        {
            double fstate = 0;
            switch (mNum)
            {
                case (1):
                    fstate = getState()[0];
                    break;
                case (2):
                    fstate = (getState()[0] + getState()[1])/2;
                    break;
            }
            return fstate;
        }
    }
}
