using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.UI;
using Emgu.CV.Util;

namespace Axel_unscented
{
    class linearKF : IKalman
    {
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
        /// <summary>
        /// initialization
        /// </summary>
        /// <param name="mNum">measurement number</param>
        /// <param name="prms">UFK parameters</param>
        public void Init(int mNum = 1, Dictionary<string, double> prms = null)
        {

        }
        public void Update(double[] measurements)
        {
    
        }
        public double[] getState()
        {
            return new double[1];
        }

    }
}
