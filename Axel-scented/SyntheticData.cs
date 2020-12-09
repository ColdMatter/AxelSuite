using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.UI;
using UtilsNS;

namespace Kalman_Filter
{
    public class SyntheticData
    {
        public Matrix<float> state;
        public Matrix<float> transitionMatrix;
        public Matrix<float> measurementMatrix;
        public Matrix<float> processNoise;
        public Matrix<float> measurementNoise;
        public Matrix<float> errorCovariancePost;
        public double procNoise { get; protected set; }
        public double measureNoise { get; protected set; }
        public SyntheticData(Dictionary<string, double> prms = null)
        {
            state = new Matrix<float>(4, 1);
            state[0, 0] = 0f; // x-pos
            state[1, 0] = 0f; // y-pos
            state[2, 0] = 0f; // x-velocity
            state[3, 0] = 0f; // y-velocity
            transitionMatrix = new Matrix<float>(new float[,]
                    {
                        {1, 0, 1, 0},  // x-pos, y-pos, x-velocity, y-velocity
                        {0, 1, 0, 1},
                        {0, 0, 1, 0},
                        {0, 0, 0, 1}
                    }); 
            measurementMatrix = new Matrix<float>(new float[,]
                    {
                        { 1, 0, 0, 0 },
                        { 0, 1, 0, 0 }
                    });
            // defaults
            procNoise = 1.0e-4;
            measureNoise = 1.0e-1;
            if (!Utils.isNull(prms))
            {
                if (prms.ContainsKey("procNoise")) procNoise = prms["procNoise"];
                if (prms.ContainsKey("measureNoise")) measureNoise = prms["measureNoise"];
            }
            measurementMatrix.SetIdentity();
            processNoise = new Matrix<float>(4, 4); //Linked to the size of the transition matrix
            processNoise.SetIdentity(new MCvScalar(procNoise)); //The smaller the value the more resistance to noise 
            measurementNoise = new Matrix<float>(2, 2); //Fixed accordiong to input data 
            measurementNoise.SetIdentity(new MCvScalar(measureNoise));
            errorCovariancePost = new Matrix<float>(4, 4); //Linked to the size of the transition matrix
            errorCovariancePost.SetIdentity();
        }

        public Matrix<float> GetMeasurement()
        {
            Matrix<float> measurementNoise = new Matrix<float>(2, 1);
            measurementNoise.SetRandNormal(new MCvScalar(), new MCvScalar(Math.Sqrt(measurementNoise[0, 0])));
            return measurementMatrix * state + measurementNoise;
        }

        public void GoToNextState()
        {
            Matrix<float> processNoise = new Matrix<float>(4, 1);
            processNoise.SetRandNormal(new MCvScalar(), new MCvScalar(processNoise[0, 0]));
            state = transitionMatrix * state + processNoise;
        }
    }
}
