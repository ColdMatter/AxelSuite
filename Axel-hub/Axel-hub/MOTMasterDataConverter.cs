using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NationalInstruments.DataInfrastructure.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RemoteMessagingNS;

namespace Axel_hub
{
    public static class MOTMasterDataConverter
    {
        public static Dictionary<string, double> AverageShotSegments(MMexec data)
        {
            var avgs = new Dictionary<string, double>();
            foreach (var key in new List<string>() {"N2", "NTot", "B2", "BTot", "Bg"})
            {
                var rawData = (double[])data.prms[key];
                avgs[key] = rawData.Average();
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
            return Asymmetry(AverageShotSegments(data), subtractBackground, subtractDark);
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
