using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows;
using System.Threading.Tasks;
using NationalInstruments.DataInfrastructure.Primitives;
using NationalInstruments.Controls;
using NationalInstruments.Analysis.Math;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OptionsNS;
using UtilsNS;

namespace Axel_hub
{
#region DataConvertion
    /// <summary>
    /// Averaging the photo diode signals {"N2", "NTot", "B2", "BTot", "Bg"}
    /// </summary>
    public static class MMDataConverter
    {
        public static Dictionary<string, double> AverageShotSegments(MMexec data, bool initN2, bool stdDev)
        {
            var avgs = new Dictionary<string, double>();
            double std = 0.0;
            double mean = 0.0;
            foreach (var key in new List<string>() {"N2", "NTot", "B2", "BTot", "Bg"})
            {
                var rawData = (double[])data.prms[key];
                mean = rawData.Average();
                avgs[key] = mean;
                if (stdDev)
                {
                    std = 0.0;
                    for (int i = 0; i< rawData.Length; i++)
                    {
                        std += (rawData[i]-mean)*(rawData[i]-mean);
                    }
                    std = Math.Sqrt(std/(rawData.Length-1));
                    avgs[key + "_std"] = std;
                }
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
            Dictionary<string,object> tempDict = new Dictionary<string, object>(); 
            foreach (var key in data.prms.Keys)
            {
                if (key == "runID" || key == "groupID" || key == "last" || key == "MEMS" || key == "rTime") tempDict[key] = data.prms[key];
                else
                {
                    var rawData = (JArray) data.prms[key];
                    tempDict[key] = rawData.ToObject<double[]>();
                }
            }
            data.prms = tempDict;
        }
        public static double Asymmetry(MMexec data, bool subtractBackground = true, bool subtractDark = true)
        {
            return Asymmetry(AverageShotSegments(data, false,false), subtractBackground, subtractDark);
        }
        /// <summary>
        /// Calculating the result signal from {"N2", "NTot", "B2", "BTot", "Bg"} means
        /// </summary>
        /// <param name="avgs"></param>
        /// <param name="subtractBackground"></param>
        /// <param name="subtractDark"></param>
        /// <returns></returns>
        public static double Asymmetry(Dictionary<string, double> avgs, bool subtractBackground = true, bool subtractDark = true)
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

        /// <summary>
        /// Restrict phase to 2Pi
        /// </summary>
        /// <param name="phi"></param>
        /// <returns></returns>
        public static double Restrict2twoPI(double phi) 
        {            
            double ph = phi;
            if (ph <= 0) ph += 2 * Math.PI;
            if (ph <= 0) ph += 2 * Math.PI;
            if (Utils.InRange(ph, 0, 2 * Math.PI)) return ph;
            return ph % (2 * Math.PI);
        }
    }
#endregion DataConvertion

#region shots collection
    /// <summary>
    /// Class representing single shot with both components quant (MOT) and MEMS (ADC24)
    /// </summary>
    public class SingleShot
    {
        protected string precision = "G5";

        public Point quant; // .X - time of acquisition[s]; .Y - accel.[mg]
        private List<Point> _mems; // each point .X - time[s]; .Y - accel.[mg]; the range of mems.X supposed to be 3 times the quant measurement with the quant measuremen in the middle
        public List<Point> mems { get { return _mems; } set { _mems.Clear(); _mems.AddRange(value); } }

        /// <summary>
        /// Number of constructors
        /// </summary>
        public SingleShot()
        {
            quant = new Point(-1, 0);
            _mems = new List<Point>();
        }
        public SingleShot(double qTime, double qSignal)
        {
            quant = new Point(qTime, qSignal);
            _mems = new List<Point>();
        }
        public SingleShot(Point q)
        {
            quant = new Point(q.X, q.Y);
            _mems = new List<Point>();
        }
        public SingleShot(Point q, List<Point> m)
        {
            quant = new Point(q.X, q.Y);
            _mems = new List<Point>(m);
        }

        /// <summary>
        /// Check if empty
        /// </summary>
        /// <returns></returns>
        public bool IsEmpty()
        {
            if(Utils.isNull(quant) || Utils.isNull(_mems)) return true;
            return (quant.X < 0) || (_mems.Count < 3);
        }

        /// <summary>
        /// Find index for specific time
        /// </summary>
        /// <param name="tm"></param>
        /// <param name="smart">More direct way with some assumptions</param>
        /// <returns></returns>
        public int idxByTime(double tm, bool smart = true)
        {
            int idx = -1; int ml = mems.Count - 1; int j = 0;
            if (ml == -1) return idx;
            if (!Utils.InRange(tm, mems[0].X, mems[ml].X)) return idx;
            if (smart && (mems[ml].X > mems[0].X))// assuming equidistant and increasing seq.
            {
                double prd = (mems[ml].X - mems[0].X) / ml;
                j = (int)(Math.Round((tm - mems[0].X) / prd) - 2);
                if (j < 0) j = 0;
            }
            for (int i = j; i < mems.Count; i++)
            {
                if (mems[i].X >= tm)
                {
                    idx = i;
                    break;
                }
            }
            return Utils.EnsureRange(idx, -1, ml);
        }

        /// <summary>
        /// Get part of mems within a reange
        /// </summary>
        /// <param name="rng"></param>
        /// <returns></returns>
        public List<Point> memsPortion(Range<double> rng)
        {
            List<Point> ls = new List<Point>();
            for (int i=0; i<mems.Count; i++) 
                if (Utils.InRange(mems[i].X, rng.Minimum,rng.Maximum)) ls.Add(mems[i]);
            return ls;
        }

        /// <summary>
        /// Calculating mems acceleration time-related to quant point
        /// </summary>
        /// <param name="delay">delay reference to quant.X</param>
        /// <param name="duration">the range length</param>
        /// <param name="triangle">alternative to triangle is uniform</param>
        /// <returns></returns>
        public double memsWeightAccel(double delay, double duration = -1, bool triangle = true) 
        {
            if (IsEmpty()) return Double.NaN;
            double dur = (duration < 0) ? (mems[mems.Count-1].X - mems[0].X)/3: duration;
            List<Point> lp = memsPortion(new Range<double>(quant.X + delay, quant.X + delay + dur));
            int len = lp.Count; if (len.Equals(0)) return Double.NaN;
            double a, b, sum = 0;
            switch (triangle)
            {
                case false:
                    {
                        foreach (Point pnt in lp) sum += pnt.Y;
                        break;
                    }
                case true:
                    {
                        for (int i = 0; i < len; i++)
                        {
                            if (i < (len / 2))
                            {
                                a = 4 / len; b = 0;
                            }
                            else
                            {
                                a = -4 / len; b = 4;
                            }
                            sum += (a * i + b) * lp[i].Y;
                        }
                        break;
                    }
            }
            return sum / len;
        }

        /// <summary>
        /// Accelerations components in a dictinary with order, resid, etc.
        /// </summary>
        /// <param name="fringeScale"></param>
        /// <returns></returns>
        public Dictionary<string, double> deconstructAccel(double fringeScale)
        {
            Dictionary<string, double> da = new Dictionary<string, double>(); double resid;
            if (IsEmpty()) return da;
            double mms = memsWeightAccel(0.0, -1, false);
            if (Double.IsNaN(mms)) return da;
            da["MEMS"] = mms;

            // M for measure
            da["PhiMg"] = quant.Y; // [mg]
            da["PhiRad"] = quant.Y / fringeScale; // [rad]

            da["Order"] = calcAccel.accelOrder(da["MEMS"] - da["PhiMg"], fringeScale, out resid); // order from mems; "resid" [rad]
            da["OrdRes"] = resid * fringeScale;
            
            calcAccel.accelOrder(quant.Y, fringeScale, out resid); // quant 
            
            double orderAccel, residAccel;
            da["Accel"] = calcAccel.resultAccel(da["Order"], resid, fringeScale, out orderAccel, out residAccel); // [mg]
            
            return da;
        }

        /// <summary>
        /// A single shot in JSON format for file import/export
        /// </summary>
        public string AsString
        {
            get
            {
                Dictionary<string, object> dt = new Dictionary<string, object>();
                double[] q = new double[2]; string timePrec = "G8";
                q[0] = Utils.formatDouble(quant.X, timePrec); ; q[1] = Utils.formatDouble(quant.Y, precision);
                dt["quant"] = q;
                double[,] m = new double[mems.Count, 2];
                for (int i = 0; i < mems.Count; i++)
                {
                    m[i, 0] = Utils.formatDouble(mems[i].X, timePrec); m[i, 1] = Utils.formatDouble(mems[i].Y, precision);
                }
                dt["mems"] = m;
                return JsonConvert.SerializeObject(dt);
            }
            set
            {
                Dictionary<string, object> dt = JsonConvert.DeserializeObject<Dictionary<string, object>>(value);
                var jq = (JArray)dt["quant"]; double[] q = jq.ToObject<double[]>(); quant.X = q[0]; quant.Y = q[1];
                mems.Clear();
                var jm = (JArray)dt["mems"]; double[,] m = jm.ToObject<double[,]>();
                for (int i = 0; i < m.GetLength(0); i++)
                {
                    mems.Add(new Point(m[i, 0], m[i, 1]));
                }
            }
        }
    }

    /// <summary>
    /// List / series of single shots 
    /// </summary>
    public class ShotList : List<SingleShot>
    {
        protected int depth = 10000; // max number of last items with direct access; for the whole thing - archyScan
        public string filename { get; private set; }
        private string _prefix;
        public bool archiveMode { get; private set; } // reading or writing file
        public bool savingMode { get; private set; } // either opening or saving data

        public Dictionary<string, double> conditions = new Dictionary<string, double>();

        public int FileCount { get; private set; } // num of shots in the file
        StreamReader streamReader = null;
        FileLogger streamWriter = null;

        /// <summary>
        /// Class constructor
        /// if arch -> open file if FN not empty, or create FN if empty
        /// if not arch -> ignore FN and prefix 
        /// </summary>
        /// <param name="arch">log the incomming data</param>
        /// <param name="FN"></param>
        /// <param name="prefix"></param>
        public ShotList(bool arch = true, string FN = "", string prefix = ""): base()
        {
            archiveMode = arch; 
            if (arch)
            {
                savingMode = FN.Equals("");
                if (savingMode) 
                { 
                    streamWriter = new FileLogger(prefix); 
                    streamWriter.defaultExt = ".jlg";
                    streamWriter.Enabled = true;
                }
                else 
                {
                    if (!File.Exists(FN)) throw new Exception("No such file <" + filename + ">");
                    filename = FN;
                    FileCount = Utils.readList(filename).Count;
                }
                enabled = true;
            }
        }

        /// <summary>
        /// New Add with optional log and size limit
        /// </summary>
        /// <param name="ss"></param>
        new public void Add(SingleShot ss) 
        {
            if (!enabled) return;
            if (Count.Equals(0) && !conditions.Count.Equals(0) && savingMode && archiveMode) 
            { 
                streamWriter.log("#"+JsonConvert.SerializeObject(conditions));
            }
            base.Add(ss);
            while (Count > depth) { this.RemoveAt(0); }
            if (archiveMode && savingMode) 
                streamWriter.log(ss.AsString);           
        }

        private bool _enabled = true;
        /// <summary>
        /// Log ON/OFF
        /// </summary>
        public bool enabled 
        {
            get { return _enabled; }
            set 
            {
                if (archiveMode && savingMode)
                {
                    streamWriter.Enabled = value;
                }                   
                _enabled = value;
            }
        }
 
        public int lastIdx { get; private set; }
        /// <summary>
        /// Reset scan of archive
        /// </summary>
        public void resetScan()
        {
            if(savingMode) throw new Exception("No scanning in saving mode!");
            lastIdx = -1;
            if (!archiveMode) return;
            if (!File.Exists(filename)) throw new Exception("No such file <"+filename+">");
            streamReader = File.OpenText(filename);            
            buffer = streamReader.ReadLine();
            if (buffer[0].Equals('#'))
            {
                buffer = buffer.Remove(0, 1);
                conditions = JsonConvert.DeserializeObject<Dictionary<string, double>>(buffer);
                buffer = streamReader.ReadLine();
            }           
            if (FileCount <= depth) // loading file in memory 
            {
                bool next;
                do
                {
                    this.Add(archiScan(out next));
                } while (next);
                archiveMode = false;
            }
        }

        private string buffer = String.Empty;
        /// <summary>
        /// Get the next scan from a file
        /// </summary>
        /// <param name="next">next is false on the last item</param>
        /// <returns></returns>
        public SingleShot archiScan(out bool next) // 
        {            
            if (!archiveMode)
            {
                lastIdx++;
                next = lastIdx < (Count - 1);
                return this[lastIdx];
            }
            SingleShot ss = new SingleShot();
            next = !Utils.isNull(buffer);
            if (!next) return ss;
            ss.AsString = buffer;

            buffer = streamReader.ReadLine();
            next = !Utils.isNull(buffer);
            return ss;
        }

        /// <summary>
        /// Save a file with JSON of single shots per line
        /// read the format with ResetScan And ArchiScan
        /// </summary>
        /// <param name="FN"></param>
        public void Save(string FN = "")
        {
            if (FN.Equals(""))
            {
                if (Utils.isNull(filename)) filename = Utils.dataPath + Utils.timeName(_prefix)+ ".jlg";
                if (filename.Equals("")) filename = Utils.dataPath + Utils.timeName(_prefix) + ".jlg";
            }
            else filename = FN;
            StreamWriter sWriter = new StreamWriter(new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None, 65536, true));
            if (!conditions.Count.Equals(0))
                sWriter.WriteLine("#" + JsonConvert.SerializeObject(conditions));
            int j = 0;
            foreach (SingleShot ss in this)
            {
                sWriter.WriteLine(ss.AsString); j++;
            }
            sWriter.Close();
            Utils.TimedMessageBox("Saved "+j.ToString()+" shots in join file: " + filename);
        } 
    }
#endregion shots collection
}
