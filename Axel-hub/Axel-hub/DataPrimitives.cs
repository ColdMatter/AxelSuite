using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
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
        public static Dictionary<string, double> AverageShotSegments(MMexec data, bool stdDev)
        {
            var avgs = new Dictionary<string, double>();
            foreach (var key in new List<string>() {"N2", "NTot", "B2", "BTot", "Bg"})
            {
                var rawData = (double[])data.prms[key];
                avgs[key] = rawData.Average();
                if (stdDev) avgs[key + "_std"] = Statistics.StandardDeviation(rawData);
            }
            avgs["N1"] = avgs["NTot"]- avgs["N2"];
            avgs["RN1"] = (avgs["NTot"] - avgs["N2"]) / avgs["NTot"];
            avgs["RN2"] = avgs["N2"] / avgs["NTot"];

            return avgs;
        }
        public static Dictionary<string, double> SignalCorrected(Dictionary<string, double> avgs, bool subtractDark = true)
        {
            var n2 = avgs["N2"];
            var ntot = avgs["NTot"];
            var b2 = avgs["B2"];
            var btot = avgs["BTot"];
            var back = avgs["Bg"];
            Dictionary<string, double> sgn = new Dictionary<string, double>(avgs);
            if (subtractDark)
            {
                sgn["N2"] = n2 - b2;
                sgn["N1"] = (ntot - btot) - (n2 - b2);
                sgn["NTot"] = ntot - btot;
                sgn["B2"] = b2;
                sgn["BTot"] = btot;
                sgn["RN1"] = ((ntot - btot) - (n2 - b2)) / (ntot - btot);
                sgn["RN2"] = (n2 - b2) / (ntot - btot);
            }
            else
            {
                sgn["N2"] = n2;
                sgn["N1"] = ntot - n2;
                sgn["NTot"] = ntot;
                sgn["B2"] = b2;
                sgn["BTot"] = btot;
                sgn["RN1"] = (ntot - n2) / ntot;
                sgn["RN2"] = n2 / ntot;
            }
            return sgn;
        }

        public static void ConvertToDoubleArray(ref MMexec data)
        {
            Dictionary<string,object> tempDict = new Dictionary<string, object>(); 
            foreach (var key in data.prms.Keys)
            {
                if (key == "runID" || key == "groupID" || key == "last" || key == "MEMS" || key.Contains("Time") || key == "samplingRate") tempDict[key] = data.prms[key];
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
            return Asymmetry(AverageShotSegments(data, false), subtractBackground, subtractDark);
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
        protected string precision = "G6";
        public string dmode { get; private set; }
        public Point3D quant; // .X - time of acquisition[s]; .Y - accel.[mg/mV]; .Z - range (duration) of acquisition[s]
        public double temperature { get; private set; } 
        private List<Point> _mems; // each point .X - time[s]; .Y - accel.[mg]; the range of mems.X supposed to be 3 times the quant measurement with the quant measuremen in the middle
        public List<Point> mems { get { return _mems; } set { _mems.Clear(); _mems.AddRange(value); } }

        /// <summary>
        /// Number of constructors
        /// </summary>
        public SingleShot()
        {
            quant = new Point3D(-1, 0, -1);
            _mems = new List<Point>();
        }
        public SingleShot(double qTime, double qSignal, double qRange)
        {
            quant = new Point3D(qTime, qSignal, qRange);
            _mems = new List<Point>();
        }
        public SingleShot(Point3D q)
        {
            quant = new Point3D(q.X, q.Y, q.Z);
            _mems = new List<Point>();
        }
        public SingleShot(Point3D q, List<Point> m, double _temperature = Double.NaN, string dMode = "")
        {
            quant = new Point3D(q.X, q.Y, q.Z);
            _mems = new List<Point>(m);
            temperature = _temperature;
            dmode = dMode;
        }
        public SingleShot(SingleShot ss)
        {
            quant = new Point3D(ss.quant.X, ss.quant.Y, ss.quant.Z);
            _mems = new List<Point>(ss.mems);
            temperature = ss.temperature;
            dmode = ss.dmode;
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
        
        public bool timeValidation(double dftRange = -1)
        {
            double z = quant.Z > 0 ? quant.Z : dftRange;
            if (z == -1) return false;
            int i1 = idxByTime(quant.X, false); int i2 = idxByTime(quant.X + z, false);
            return (i1 > -1) && (i2 > -1);
        }

        /// <summary>
        /// Find index for specific time in mems array
        /// </summary>
        /// <param name="tm"></param>
        /// <param name="smart">More direct way with some assumptions</param>
        /// <returns>index; -1 if out of range</returns>
        public int idxByTime(double tm, bool smart = true)
        {
            int idx = -1; int ml = mems.Count - 1; int j = 0;
            if (ml == -1) return idx;
            if (!Utils.InRange(tm, mems[0].X, mems[ml].X)) return idx;
            if (smart && (mems[ml].X > mems[0].X)) // assuming equidistant and increasing seq.
            {
                double prd = (mems[ml].X - mems[0].X) / ml;
                j = (int)(Math.Round((tm - mems[0].X) / prd) - 2);
                if (j < 0) j = 0;
            }
            for (int i = j; i < mems.Count-1; i++)
            {
                if (Utils.InRange(tm,mems[i].X, mems[i+1].X))
                {
                    idx = i;
                    break;
                }
            }
            return Utils.EnsureRange(idx, -1, ml);
        }

        /// <summary>
        /// Get part of mems within a range
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
        public List<Point> memsPortion(double first, double last)
        {           
            return memsPortion(new Range<double>(first, last));
        }

        public void cutMems(double first, double last) 
        {
            List<Point> tm = new List<Point>(_mems); _mems.Clear();
            foreach (Point pnt in tm)
                if (Utils.InRange(pnt.X, first, last))
                    _mems.Add(pnt);
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
            if (quant.Z < 0) quant.Z = (mems[mems.Count - 1].X - mems[0].X) / 3;
            double dur = (duration < 0) ? quant.Z: duration;
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
        public Dictionary<string, double> deconstructAccel(double fringeScale) // only for [mg], DOES NOT work for raw data !!! 
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
                double[] q = new double[3]; string timePrec = "G8";
                q[0] = Utils.formatDouble(quant.X, timePrec); q[1] = Utils.formatDouble(quant.Y, precision); q[2] = Utils.formatDouble(quant.Z, precision);
                dt["quant"] = q;
                if (!Double.IsNaN(temperature)) dt["temper"] = Utils.formatDouble(temperature, precision);
                if (dmode != "") dt["dmode"] = dmode;
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
                if (dt.ContainsKey("quant"))
                {
                    var jq = (JArray)dt["quant"]; double[] q = jq.ToObject<double[]>(); 
                    quant.X = q[0]; quant.Y = q[1]; if (q.Length < 3) quant.Z = -1; 
                }
                if (dt.ContainsKey("dmode")) dmode = Convert.ToString(dt["dmode"]);
                if (dt.ContainsKey("temper")) temperature = Convert.ToDouble(dt["temper"]);
                mems.Clear();
                if (dt.ContainsKey("mems"))
                {
                   var jm = (JArray)dt["mems"]; double[,] m = jm.ToObject<double[,]>();
                    for (int i = 0; i < m.GetLength(0); i++)
                    {
                        mems.Add(new Point(m[i, 0], m[i, 1]));
                    }
                }
             }
        }
    }

    /// <summary>
    /// List / series of single shots; file read & write 
    /// </summary>
    public class ShotList : List<SingleShot>
    {
        protected int maxSize = 100000000; // max size to be loaded as whole thing, otherwise read with archyScan
        public string filename { get; private set; }
        public bool rawData { get; private set; }
        public string prefix { get; private set; }
        private bool? _archiveMode;
        public bool? archiveMode // operate on file(true) or memory(false)
        {
            get
            {
                if (savingMode) return true; // if savingMode then archiveMode = true - count on FileLogger for efficiency
                else return _archiveMode; 
            }
            private set 
            {
                _archiveMode = value;
            } 
        } 
        
        // else if file.line.count < depth then archiveMode=true else archiveMode=false
        public bool savingMode { get; private set; } // either opening or saving data

        public Dictionary<string, double> conditions = new Dictionary<string, double>();
        public double defaultAqcTime { get; private set; } // used only for read (optionally)

        public long FileSize
        {
            get
            {
                if (!File.Exists(filename)) return -1;
                FileInfo fi = new FileInfo(filename);
                return fi.Length;
            }
        }

        StreamReader streamReader = null;
        public FileLogger streamWriter = null;

        /// <summary>
        /// Class constructor
        /// if arch -> open file if FN exists, or create FN if empty
        /// if not arch -> ignore FN and prefix 
        /// </summary>
        /// <param name="arch">operate on file - true or memory - false if possible</param>
        /// <param name="FN"></param>
        /// <param name="prefix"></param>
        public ShotList(bool save, string FN = "", string _prefix = "", bool rawDt = false): base()
        {
            archiveMode = null; // undefined (before any operations)
            savingMode = save; prefix = _prefix; rawData = rawDt; defaultAqcTime = -1;
            if (savingMode) // write
            {
                string fileExt, fileName = "";
                if (rawDt) fileExt = ".jdt";
                else fileExt = ".jlg";
                if (!FN.Equals("")) fileName = System.IO.Path.ChangeExtension(FN, fileExt);
                streamWriter = new FileLogger(prefix,fileName);
                streamWriter.defaultExt = fileExt;
                streamWriter.Enabled = true;
            }
            else // read
            {
                if (!File.Exists(FN)) throw new Exception("No such file <" + filename + ">");
                filename = FN;                
            }
            enabled = true;           
        }

        /// <summary>
        /// New Add with optional log and size limit
        /// </summary>
        /// <param name="ss"></param>
        new public void Add(SingleShot ss) 
        {
            if (!enabled) return;
            if (Count.Equals(0) && savingMode) 
            {   
                if (!conditions.Count.Equals(0))
                    streamWriter.log("#"+JsonConvert.SerializeObject(conditions));
                if (streamWriter.subheaders.Count > 0)
                    streamWriter.log("#" + streamWriter.subheaders[0]);
            }
            base.Add(ss);
            while (Count > 10000) { this.RemoveAt(0); } // keep it less than 10000 lines
            if (savingMode) 
                streamWriter.log(ss.AsString);           
        }

        private bool _enabled = false;
        /// <summary>
        /// Log ON/OFF
        /// </summary>
        public bool enabled 
        {
            get { return _enabled; }
            set 
            {
                if (savingMode)
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
        public void resetScan() // read 
        {
            if (savingMode) throw new Exception("No scanning in saving mode!");
            lastIdx = -1;
            
            if (!File.Exists(filename)) throw new Exception("No such file <"+filename+">");
            streamReader = File.OpenText(filename);            
            buffer = streamReader.ReadLine();
            if (buffer[0].Equals('#'))
            {
                buffer = buffer.Remove(0, 1);
                conditions = JsonConvert.DeserializeObject<Dictionary<string, double>>(buffer);
                buffer = streamReader.ReadLine();
            }
            if (FileSize > maxSize) archiveMode = true; // read from disk
            else // load into memory
            {
                archiveMode = true; Clear();
                bool next;
                do
                {
                    SingleShot s = archiScan(out next);
                    if (s.mems.Count == 0) continue;
                    Add(s);
                } while (next);
                archiveMode = false;
            }
            SingleShot ss; double dur;
            double dr = 0; int j = Math.Min(10, Count);
            for (int i = 3; i < j+3; i++)
            {
                ss = this[i];
                if (ss.mems.Count > 10)
                {
                    dur = ss.mems[ss.mems.Count - 1].X - ss.mems[0].X; 
                    dr += dur;
                }
            }
            defaultAqcTime = dr / (j*3);
        }

        private string buffer = String.Empty;
        /// <summary>
        /// Get the next scan from a file
        /// </summary>
        /// <param name="next">next is false on the last item</param>
        /// <returns></returns>
        public SingleShot archiScan(out bool next) // 
        {
            if (Utils.isNull(archiveMode)) throw new Exception("No file opened");
            if (archiveMode == false) // memory
            {
                lastIdx++;
                next = lastIdx < (Count - 1);
                if (next) return this[lastIdx];
                else return null;
            }
            SingleShot ss = new SingleShot();
            next = !Utils.isNull(buffer);
            if (!next) return ss;
            if (buffer.Length> 0)
                if (!buffer[0].Equals('#')) ss.AsString = buffer;

            buffer = streamReader.ReadLine(); // read next line to be deconstructed on the next call
            next = !Utils.isNull(buffer);
            return ss;
        }

        public List<SingleShot> sectionScan(int sectionSize, bool valid, out bool next) // same as archiScan but for a section (batch of shots)
        {
            if (Utils.isNull(archiveMode)) throw new Exception("No file opened");
            List<SingleShot> ls = new List<SingleShot>(); SingleShot ss;
            bool nextSS = false; int k = 0;
            do
            {
                ss = archiScan(out nextSS);
                if (nextSS)
                {
                    if (valid)
                    {
                        if (ss.timeValidation(defaultAqcTime))
                        {
                            ls.Add(ss); k++;
                        }
                    }
                    else
                    {
                        ls.Add(ss); k++;
                    }
                }
            } while (nextSS && (k < sectionSize));
            next = nextSS; // end of file
            return ls;
        }

        /*public SingleShot getSingleShot(int idx) // slow
        {
            if (Utils.isNull(archiveMode)) throw new Exception("No file opened");
        }*/

        /// <summary>
        /// Save a file with JSON of single shots per line
        /// read the format with ResetScan And ArchiScan
        /// </summary>
        /// <param name="FN"></param>
        public void Save(string FN = "")
        {
            if (FN.Equals(""))
            {
                if (Utils.isNull(filename)) filename = Utils.dataPath + Utils.timeName(prefix)+ ".jlg";
                if (filename.Equals("")) filename = Utils.dataPath + Utils.timeName(prefix) + ".jlg";
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
