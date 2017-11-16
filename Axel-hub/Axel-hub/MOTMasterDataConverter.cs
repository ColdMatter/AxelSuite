﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NationalInstruments.DataInfrastructure.Primitives;
using NationalInstruments.Analysis.Math;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RemoteMessagingNS;
using OptionsNS;

namespace Axel_hub
{
    public static class MOTMasterDataConverter
    {
        public static Dictionary<string, double> AverageShotSegments(MMexec data, bool initN2)
        {
            var avgs = new Dictionary<string, double>();
            double std = 0.0;
            double mean = 0.0;
            foreach (var key in new List<string>() {"N2", "NTot", "B2", "BTot", "Bg"})
            {
                var rawData = (double[])data.prms[key];
                mean = rawData.Average();
                avgs[key] = mean;
                std = 0.0;
                for (int i = 0; i< rawData.Length; i++)
                {
                    std += (rawData[i]-mean)*(rawData[i]-mean);
                }
                std = Math.Sqrt(std/(rawData.Length-1));
                avgs[key + "_std"] = std;
                if (key.Equals("N2") && initN2)
                {
                    double[] seq = new double[rawData.Length];
                    for (int i = 0; i < rawData.Length; i++) { seq[i] = i; }
                    double[] fit = CurveFit.LinearFit(seq, rawData);
                    avgs["initN2"] = fit[0];
                }               
            }
            return avgs;
        }

        public static void ConvertToDoubleArray(ref MMexec data)
        {
            Dictionary<string,object>tempDict = new Dictionary<string, object>(); 
            foreach (var key in data.prms.Keys)
            {
                if (key == "runID" || key == "groupID" || key == "last") tempDict[key] = data.prms[key];
                else
                {
                    var rawData = (JArray) data.prms[key];
                    tempDict[key] = rawData.ToObject<double[]>();
                }
            }
            data.prms = tempDict;
        }
        public static double Asymmetry(MMexec data, bool subtractBackground = true,
            bool subtractDark = true)
        {
            return Asymmetry(AverageShotSegments(data, false), subtractBackground, subtractDark);
        }

        public static double Asymmetry(Dictionary<string, double> avgs, bool subtractBackground = true,
            bool subtractDark = true)
        {
            var n2 = avgs["N2"];
            var ntot = avgs["NTot"];
            var b2 = avgs["B2"];
            var btot = avgs["BTot"];
            var back = avgs["Bg"];
            if (subtractBackground && subtractDark)
            {
                return ((ntot - btot) - 2 * (n2 - b2) - back) / (ntot - btot - back);
            }
            if (!subtractDark)
            {
                return ((ntot - btot) - 2 * (n2 - b2)) / (ntot - btot);
            }
            else
            {
                return (ntot - 2 * n2) / ntot;
            }
        }

        public static double IntegrateAcceleration(MMexec data)
        {
            throw new NotImplementedException();
        }
    }
}
