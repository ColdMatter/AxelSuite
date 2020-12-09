using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.UI;
using Emgu.CV.Util;
using Kalman_Filter;
using UtilsNS;

namespace Axel_scented
{
    class LinearKF2 : IKalman
    {
        float px, py, cx, cy;
        
        #region Kalman Filter and Poins Lists
        PointF[] oup = new PointF[2];
        private Kalman kal;
        private SyntheticData syntheticData;
        #endregion

        public Dictionary<string, double> parameters
        {
            get
            {
                Dictionary<string, double> itms = new Dictionary<string, double>();
                if (Utils.isNull(syntheticData)) return itms;
                itms["procNoise"] = syntheticData.procNoise;
                itms["measureNoise"] = syntheticData.measureNoise;               
                return itms;
            }
        }
        public int mNum { get; protected set; }
        /// <summary>
        /// initialization
        /// </summary>
        /// <param name="mNum">measurement number</param>
        /// <param name="prms">UFK parameters</param>
        public void Init(int _mNum = 1, Dictionary<string, double> prms = null)
        {
            mNum = _mNum;
            kal = new Kalman(4, 2, 0);
            syntheticData = new SyntheticData(prms);
            Matrix<float> state = new Matrix<float>(new float[]
            {
                    0.0f, 0.0f, 0.0f, 0.0f
            });
            kal.CorrectedState = state;
            kal.TransitionMatrix = syntheticData.transitionMatrix;
            kal.MeasurementNoiseCovariance = syntheticData.measurementNoise;
            kal.ProcessNoiseCovariance = syntheticData.processNoise;
            kal.ErrorCovariancePost = syntheticData.errorCovariancePost;
            kal.MeasurementMatrix = syntheticData.measurementMatrix;
        }
        public void Update(double[] measurements)
        {
            int l = measurements.Length;
            if (!Utils.InRange(l,1,2)) throw new Exception("No implementaion for <>"+l.ToString()+" number of measurements");
            if (!mNum.Equals(l)) throw new Exception("Wrong measurement dimension"); 
            syntheticData.state[0, 0] = (float)measurements[0];           
            if (l.Equals(1)) syntheticData.state[1, 0] = 0;
            else syntheticData.state[1, 0] = (float)measurements[1];

            Matrix<float> prediction = kal.Predict();           
            Matrix<float> estimated = kal.Correct(syntheticData.GetMeasurement());           
            syntheticData.GoToNextState();

            px = syntheticData.GetMeasurement()[0, 0]; // predicted
            py = syntheticData.GetMeasurement()[1, 0];
            cx = estimated[0, 0]; // corrected
            cy = estimated[1, 0];
        }
        public double[] getUncertainty()
        {
            if (mNum.Equals(1)) return new double[1] { px - cx };
            else return new double[2] { px - cx, py - cy };
        }        
        public double[] getState()
        {
            if (mNum.Equals(1)) return new double[1] { cx };
            else return new double[2] { cx, cy };
        }
        public double getFussionState(double? defolt = null)
        {
            double[] unc = getUncertainty();
            if (!unc.Length.Equals(2))
            {
                if (Utils.isNull(defolt)) return Double.NaN;
                else return (double)defolt;
            }
            if (unc[0].Equals(0) || unc[1].Equals(0))
            {
                if (Utils.isNull(defolt)) return Double.NaN;
                else return (double)defolt;
            }
            double s0 = 1/ (unc[0] * unc[0]); double s1 = 1 / (unc[1] * unc[1]);
            return (s0 * getState()[0] + s1 * getState()[1]) / (s0 + s1);
        }
    }
}
