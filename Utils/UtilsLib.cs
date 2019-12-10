using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading.Tasks.Dataflow;
using System.Deployment.Application;
using System.Reflection;
using NationalInstruments.Controls;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;


namespace UtilsNS
{
    public static class Utils
    {
        static Random rand = new Random();
        /// <summary>
        /// ProcessMessages of the visual components 
        /// </summary>
        /// <param name="dp"></param>
        public static void DoEvents(DispatcherPriority dp = DispatcherPriority.Background)
        {
            DispatcherFrame frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(dp,
                new DispatcherOperationCallback(ExitFrame), frame);
             Dispatcher.PushFrame(frame);
        }
        public static object ExitFrame(object f)
        {
            ((DispatcherFrame)f).Continue = false;
            return null;
        }
        
        /// <summary>
        /// The developer computer. It shouldn't matter, but still..
        /// </summary>
        /// <returns></returns>
        public static bool TheosComputer()
        {
            return (string)System.Environment.GetEnvironmentVariables()["COMPUTERNAME"] == "DESKTOP-U334RMA";
        }

        /// <summary>
        /// Get the app version from Project properties -> Assembly Information
        /// </summary>
        /// <returns></returns>
        public static string getRunningVersion()
        {
            return System.Diagnostics.FileVersionInfo.GetVersionInfo(System.Environment.GetCommandLineArgs()[0]).ToString();
            //System.Reflection.Assembly.GetExecutingAssembly().Location; //Assembly.GetExecutingAssembly().Location
        }

        /// <summary>
        /// The proper way to check if object is null
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static bool isNull(System.Object o)
        {
            return object.ReferenceEquals(null, o);
        }

        /// <summary>
        /// Write in log to rich-text-box
        /// </summary>
        /// <param name="richText">The target rich-text-box</param>
        /// <param name="txt">the actual text to log</param>
        /// <param name="clr">color</param>
        public static void log(RichTextBox richText, string txt, Color? clr = null)
        {
            Color ForeColor = clr.GetValueOrDefault(Brushes.Black.Color);
            Application.Current.Dispatcher.BeginInvoke( DispatcherPriority.Background,
              new Action(() =>
              {
                  TextRange rangeOfText1 = new TextRange(richText.Document.ContentStart, richText.Document.ContentEnd);
                  string tx = rangeOfText1.Text;
                  int len = tx.Length; int maxLen = 10000; // the number of chars kept
                  if (len > (2 * maxLen)) // when it exceeds twice the maxLen
                  {
                      tx = tx.Substring(maxLen);
                      var paragraph = new Paragraph();
                      paragraph.Inlines.Add(new Run(tx));
                      richText.Document.Blocks.Clear();
                      richText.Document.Blocks.Add(paragraph);
                  }
                  rangeOfText1 = new TextRange(richText.Document.ContentEnd, richText.Document.ContentEnd);
                  rangeOfText1.Text = Utils.RemoveLineEndings(txt) + "\r";
                  rangeOfText1.ApplyPropertyValue(TextElement.ForegroundProperty, new System.Windows.Media.SolidColorBrush(ForeColor));
                  richText.ScrollToEnd();
              }));
        }

        /// <summary>
        /// Write in log to text-box
        /// </summary>
        /// <param name="tbLog">The target text-box</param>
        /// <param name="txt">the actual text to log</param>
        public static void log(TextBox tbLog, string txt)
        {
            tbLog.AppendText(txt + "\r\n");
            string text = tbLog.Text;
            int maxLen = 10000;
            if (text.Length > 2 * maxLen) tbLog.Text = text.Substring(maxLen);
            tbLog.Focus();
            tbLog.CaretIndex = tbLog.Text.Length;
            tbLog.ScrollToEnd();
        }

        /// <summary>
        /// If the name for file to save is unknown, make-up one as date-time stamp
        /// </summary>
        /// <param name="prefix"></param>
        /// <returns></returns>
        public static string timeName(string prefix = "")
        {
            if (prefix.Equals("")) return DateTime.Now.ToString("yy-MM-dd_H-mm-ss");
            else return DateTime.Now.ToString("yy-MM-dd_H-mm-ss") + "_" + prefix;
        }

        public static void copyGraphToClipboard(Graph gr)
        {
            Rect bounds; RenderTargetBitmap bitmap;
            bounds = System.Windows.Controls.Primitives.LayoutInformation.GetLayoutSlot(gr);
            bitmap = new RenderTargetBitmap((int)bounds.Width, (int)bounds.Height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(gr);
            Clipboard.SetImage(bitmap);
            Utils.TimedMessageBox("The image is in the clipboard");
        }

        /// <summary>
        /// Read text file in List of string
        /// </summary>
        /// <param name="filename">The text file</param>
        /// <param name="skipRem">If to skip # and empty lines</param>
        /// <returns></returns>
        public static List<string> readList(string filename, bool skipRem = true)
        {
            List<string> ls = new List<string>();
            foreach (string line in File.ReadLines(filename))            
            {
                if(skipRem)
                {
                    if (line.Equals("")) continue;
                    if (line[0].Equals('#')) continue;
                }
                ls.Add(line);
            }
            return ls;
        }

        /// <summary>
        /// Write down in a text file a List of string
        /// </summary>
        /// <param name="filename">yes, you guessed right...</param>
        /// <param name="ls">The list in question</param>
        public static void writeList(string filename, List<string> ls)
        {
            File.WriteAllLines(filename, ls.ToArray());
        }

        /// <summary>
        /// Read text file in Dictionary of string, string
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="skipRem">If to skip # and empty lines</param>
        /// <returns></returns>
        public static Dictionary<string, string> readDict(string filename, bool skipRem = true)
        {
            Dictionary<string, string> dict = new Dictionary<string, string>();
            if (!File.Exists(filename))
            {
                Utils.TimedMessageBox("File not found: " + filename); return dict;
            }
            List<string> ls = new List<string>();
            foreach (string line in File.ReadLines(filename))
            {
                if (skipRem)
                {
                    if (line.Equals("")) continue;
                    if (line[0].Equals('#')) continue;
                }
                string[] sb = line.Split('=');
                if (sb.Length != 2) break;
                dict[sb[0]] = sb[1];
            }
            return dict;
        }

        /// <summary>
        /// Write dictionary(string,string) in key=value format
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="dict"></param>
        public static void writeDict(string filename, Dictionary<string, string> dict)
        {
            List<string> ls = new List<string>();
            foreach (var pair in dict)
            {
                ls.Add(pair.Key + "=" + pair.Value);
            }
            File.WriteAllLines(filename, ls.ToArray());
        }

        /// <summary>
        /// Restrict Value to MinValue and MaxValue (double)
        /// </summary>
        /// <param name="Value"></param>
        /// <param name="MinValue"></param>
        /// <param name="MaxValue"></param>
        /// <returns></returns>
        public static double EnsureRange(double Value, double MinValue, double MaxValue)
        {
            if (Value < MinValue) return MinValue;
            if (Value > MaxValue) return MaxValue;
            return Value;
        }
        /// <summary>
        /// Restrict Value to MinValue and MaxValue (int)
        /// </summary>
        /// <param name="Value"></param>
        /// <param name="MinValue"></param>
        /// <param name="MaxValue"></param>
        /// <returns></returns>
        public static int EnsureRange(int Value, int MinValue, int MaxValue)
        {
            if (Value < MinValue) return MinValue;
            if (Value > MaxValue) return MaxValue;
            return Value;
        }
        /// <summary>
        /// Check if Value is in range[MinValue..MaxValue] (double)
        /// </summary>
        /// <param name="Value"></param>
        /// <param name="MinValue"></param>
        /// <param name="MaxValue"></param>
        /// <returns></returns>
        public static bool InRange(double Value, double MinValue, double MaxValue)
        {
            return ((MinValue <= Value) && (Value <= MaxValue));
        }
        /// <summary>
        /// Check if Value is in range[MinValue..MaxValue] (int)
        /// </summary>
        /// <param name="Value"></param>
        /// <param name="MinValue"></param>
        /// <param name="MaxValue"></param>
        public static bool InRange(int Value, int MinValue, int MaxValue)
        {
            return ((MinValue <= Value) && (Value <= MaxValue));
        }

        /// <summary>
        /// Convert string to bool with default value if cannot
        /// </summary>
        /// <param name="value"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public static bool Convert2BoolDef(string value, bool defaultValue = false)
        {
            bool result;
            return bool.TryParse(value, out result) ? result : defaultValue;
        }
        /// <summary>
        /// Convert string to int with default value if cannot
        /// </summary>
        /// <param name="value"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public static int Convert2IntDef(string value, int defaultValue = 0)
        {
            int result;
            return int.TryParse(value, out result) ? result : defaultValue;
        }
        /// <summary>
        /// Convert string to double with default value if cannot
        /// </summary>
        /// <param name="value"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public static double Convert2DoubleDef(string value, double defaultValue = 0)
        {
            double result;
            return double.TryParse(value, out result) ? result : defaultValue;
        }

        /// <summary>
        /// Random normaly distributed (mean:0 stDev:1) value
        /// </summary>
        /// <returns>random value</returns>
        public static double Gauss01()  
        {
            double u1 = 1.0 - rand.NextDouble(); //uniform(0,1] random doubles
            double u2 = 1.0 - rand.NextDouble();
            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) *
                         Math.Sin(2.0 * Math.PI * u2);
            return randStdNormal;
        }

        /// <summary>
        /// Error outlet if no visual logs available
        /// </summary>
        /// <param name="errorMsg"></param>
        public static void errorMessage(string errorMsg)
        {
            Console.WriteLine("Error: " + errorMsg);
        }

        /// <summary>
        /// Format double to another double with required format (e.g.precision)
        /// </summary>
        /// <param name="d"></param>
        /// <param name="format"></param>
        /// <returns></returns>
        public static double formatDouble(double d, string format)
        {
            return Convert.ToDouble(d.ToString(format));
        }
        /// <summary>
        /// Format double array to another double array with required format (e.g.precision)
        /// </summary>
        /// <param name="d"></param>
        /// <param name="format"></param>
        /// <returns></returns>
        public static double[] formatDouble(double[] d, string format)
        {
            double[] da = new double[d.Length];
            for (int i = 0; i < d.Length; i++) da[i] = Convert.ToDouble(d[i].ToString(format));
            return da;
        }

        /// <summary>
        /// Strip line endings
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string RemoveLineEndings(string value)
        {
            if (String.IsNullOrEmpty(value))
            {
                return value;
            }
            string lineSeparator = ((char)0x2028).ToString();
            string paragraphSeparator = ((char)0x2029).ToString();

            return value.Replace("\r\n", string.Empty).Replace("\n", string.Empty).Replace("\r", string.Empty).Replace(lineSeparator, string.Empty).Replace(paragraphSeparator, string.Empty);
        }

        /// <summary>
        /// Read dictionary(string,string) from file format key=value
        /// skip empty line, starting with [ or ;
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public static Dictionary<string, string> readINI(string filename)
        {
            Dictionary<string, string> dict = new Dictionary<string, string>();
            if (!File.Exists(filename)) throw new Exception("File not found: " + filename);           
            List<string> ls = new List<string>();
            string line;
            foreach (string wline in File.ReadLines(filename))
            {
                if (wline.Equals("")) continue;
                char ch = wline[0];
                if (ch.Equals('[')) continue;
                int sc = wline.IndexOf(';');
                if (sc > -1) line = wline.Remove(sc);
                else line = wline;
                if (line.Equals("")) continue;
                
                string[] sb = line.Split('=');
                if (sb.Length != 2) break;
                dict[sb[0]] = sb[1];
            }
            return dict;
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern int MessageBoxTimeout(IntPtr hwnd, String text, String title, uint type, Int16 wLanguageId, Int32 milliseconds);
        /// <summary>
        /// Temporary message not to bother click OK
        /// </summary>
        /// <param name="text"></param>
        /// <param name="title"></param>
        /// <param name="milliseconds"></param>
        public static void TimedMessageBox(string text, string title = "Information", int milliseconds = 1500)
        {
            int returnValue = MessageBoxTimeout(IntPtr.Zero, text, title, Convert.ToUInt32(0), 1, milliseconds);
            //return (MessageBoxReturnStatus)returnValue;
        }

        /// <summary>
        /// Main directory of current app
        /// </summary>
        public static string basePath = Directory.GetParent(Directory.GetParent(Environment.GetCommandLineArgs()[0]).Parent.FullName).FullName; 
        public static string configPath { get { return basePath + "\\Config\\"; } }
        public static string dataPath { get { return basePath + "\\Data\\"; } }

        /// <summary>
        /// Random string (for testing purposes)
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        public static string randomString(int length)
        {
            int l8 = 1 + length / 8; string path, ss = "";
            for (int i = 0; i < l8; i++)
            {
                path = Path.GetRandomFileName();
                path = path.Replace(".", ""); // Remove period.
                ss += path.Substring(0, 8);  // Return 8 character string
            }
            return ss.Remove(length);
        } 
    }

    #region async file logger
    /// <summary> This is an obsolete version, please use FileLogger below instead !!!
    /// Async data storage device 
    /// first you set the full path of the file, otherwise it will save in data dir under date-time file name
    /// when you want the logging to start you set Enabled to true
    /// at the end you set Enabled to false (that will flush the buffer to HD)
    /// </summary>
    public class AutoFileLogger 
    {
        private const bool traceMode = false;
        public string header = ""; // that will be put as a file first line with # in front of it
        public string defaultExt = ".ahf";
        List<string> buffer;
        public int bufferLimit = 256; // number of items
        private int bufferCharLimit = 256000; // the whole byte/char size
        public int bufferSize { get { return buffer.Count; } }
        public int bufferCharSize { get; private set; }
        public string prefix { get; private set; } 
        public bool writing { get; private set; }
        public bool missingData { get; private set; }
        public Stopwatch stw;

        public AutoFileLogger(string _prefix = "", string Filename = "")
        {
            _AutoSaveFileName = Filename;
            prefix = _prefix;
            bufferCharSize = 0;
            buffer = new List<string>();
            stw = new Stopwatch();
        }

        public int log(List<string> newItems)
        {
            if (!Enabled) return buffer.Count;
            foreach (string newItem in newItems) log(newItem);
            return buffer.Count;
        }

        public int log(string newItem)
        {
            if (!Enabled) return buffer.Count;
            buffer.Add(newItem); bufferCharSize += newItem.Length;
            if ((buffer.Count > bufferLimit) || (bufferCharSize > bufferCharLimit)) 
                Flush();
            return buffer.Count;
        }
        public void DropLastChar()
        {
            if (buffer.Count == 0) return;
            string lastItem = buffer[buffer.Count - 1];
            buffer[buffer.Count - 1] = lastItem.Substring(0, lastItem.Length - 1);
        }

        private void ConsoleLine(string txt)
        {
            Console.WriteLine(txt);
        }

        public Task Flush() // do not forget to flush when exit (OR switch Enabled Off)
        {
            if (buffer.Count == 0) return null;
            if(traceMode) ConsoleLine("0h: " + stw.ElapsedMilliseconds.ToString());
            string strBuffer = "";
            for (int i = 0; i < buffer.Count; i++)
            {
                strBuffer += buffer[i] + "\n";
            }
            buffer.Clear(); bufferCharSize = 0;           
            var task = Task.Run(() => FileWriteAsync(AutoSaveFileName, strBuffer, true));
            return task;
        }

        private async Task FileWriteAsync(string filePath, string messaage, bool append = true)
        {
            FileStream stream = null;
            try
            {
                stream = new FileStream(filePath, append ? FileMode.Append : FileMode.Create, FileAccess.Write,
                                                                                               FileShare.None, 65536, true);
                using (StreamWriter sw = new StreamWriter(new BufferedStream(stream)))
                {
                    writing = true;
                    string msg = "1k: " + stw.ElapsedMilliseconds.ToString();                    
                    await sw.WriteAsync(messaage);
                    if(traceMode) ConsoleLine(msg);
                    if(traceMode) ConsoleLine("2p: " + stw.ElapsedMilliseconds.ToString());
                    writing = false;
                }
            }
            catch (IOException e)
            {
                ConsoleLine(">> IOException - " + e.Message);
                missingData = true;
            }
            finally
            {
                if (stream != null) stream.Dispose();
            }
        }

        private bool _Enabled = false;
        public bool Enabled
        {
            get { return _Enabled; }
            set
            {
                if (value == _Enabled) return;
                if (value && !_Enabled) // when it goes from false to true
                {
                    string dir = "";
                    if (!_AutoSaveFileName.Equals("")) dir = Directory.GetParent(_AutoSaveFileName).FullName;
                    if (!Directory.Exists(dir))
                    {
                        _AutoSaveFileName = Utils.dataPath  + Utils.timeName(prefix) + defaultExt;
                    }                        

                    string hdr = "";
                    if (header != "") hdr = "# " + header + "\n";
                    var task = Task.Run(() => FileWriteAsync(AutoSaveFileName, hdr, false));

                    task.Wait();
                    writing = false;
                    missingData = false;
                    stw.Start();
                    _Enabled = true;
                }
                if (!value && _Enabled) // when it goes from true to false
                {
                    while (writing)
                    {
                        Thread.Sleep(100);
                    }
                    Task task = Flush();
                    if (task != null) task.Wait();
                    if (missingData) Console.WriteLine("Some data maybe missing from the log");
                    stw.Reset();
                    header = "";
                    _Enabled = false;
                }
            }
        }

        private string _AutoSaveFileName = "";
        public string AutoSaveFileName
        {
            get
            {
                return _AutoSaveFileName;
            }
            set
            {
                if (Enabled) throw new Exception("Logger.Enabled must be Off when you set AutoSaveFileName.");
                _AutoSaveFileName = value;
            }
        }
    }

    /// <summary>
    /// Async data storage device - new (dec.2018) optimized for speed (7x faster to AutoFilelogger) logger
    /// first you set the full path of the file, otherwise it will save in data dir under date-time file name
    /// when you want the logging to start you set Enabled to true
    /// at the end you set Enabled to false (that will flush the buffer to HD)
    /// </summary>
    public class FileLogger
    {
        private const bool traceMode = false;
        public string header = ""; // that will be put as a file first line with # in front of it
        public List<string> subheaders; 
        public string defaultExt = ".ahf";
        private ActionBlock<string> block;
        public string prefix { get; private set; }
        public string reqFilename { get; private set; }
        public bool writing { get; private set; }
        public bool missingData { get; private set; }
        public Stopwatch stw;

        /// <summary>
        /// Class constructor
        /// </summary>
        /// <param name="_prefix"></param>
        /// <param name="_reqFilename"></param>
        public FileLogger(string _prefix = "", string _reqFilename = "") // if reqFilename is something it should contain the prefix; 
        // the usual use is only prefix and no reqFilename
        {
            subheaders = new List<string>();
            reqFilename = _reqFilename;
            prefix = _prefix;
            stw = new Stopwatch();
        }

        /// <summary>
        /// The main call if you have List of strings
        /// </summary>
        /// <param name="newItems"></param>
        public void log(List<string> newItems)
        {
            if (!Enabled) return;
            foreach (string newItem in newItems) log(newItem);
            return;
        }

        /// <summary>
        /// That's the main method 
        /// </summary>
        /// <param name="newItem"></param>
        public void log(string newItem)
        {
            if (!Enabled) return;
            if (writing)
            {
                missingData = true; return;
            }
            writing = true;
            block.Post(newItem);
            writing = false;
            return;
        }

        /// <summary>
        /// Optional (traceMode) console output - only for debug
        /// </summary>
        /// <param name="txt"></param>
        private void ConsoleLine(string txt)
        {
            if(traceMode) Console.WriteLine(txt);
        }

        /// <summary>
        /// Create actual asynchronious logger 
        /// </summary>
        /// <param name="filePath"></param>
        public void CreateLogger(string filePath)
        {
            block = new ActionBlock<string>(async message =>
            {
                using (var f = File.Open(filePath, FileMode.OpenOrCreate, FileAccess.Write))
                {
                    f.Position = f.Length;
                    using (var sw = new StreamWriter(f))
                    {
                        await sw.WriteLineAsync(message);
                    }
                }
            }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 1 });
        }

        private string _LogFilename = "";
        public string LogFilename
        {
            get { return _LogFilename; }
            private set
            {
                if (Enabled) throw new Exception("Logger.Enabled must be Off when you set LogFileName.");
                _LogFilename = value;
            }
        }

        /// <summary>
        /// Write a header and subheaders with # 
        /// </summary>
        public virtual void writeHeader()
        {
            if (!header.Equals("")) log("#" + header);
            for (int i=0; i<subheaders.Count; i++)
                log("#" + subheaders[i]);
            if(!header.Equals("") || (subheaders.Count > 0)) log("\n");
        }

        private bool _Enabled = false;
        /// <summary>
        /// Switch this on to create the file and start to accept logs
        /// switch it off to flash the buffer and close the file
        /// </summary>
        public bool Enabled
        {
            get { return _Enabled; }
            set
            {
                if (value == _Enabled) return;
                if (value && !_Enabled) // when it goes from false to true
                {
                    string dir = "";

                    if (reqFilename.Equals("")) LogFilename = Utils.dataPath + Utils.timeName(prefix) + defaultExt;
                    else
                    {
                        dir = Directory.GetParent(reqFilename).FullName;
                        if (!Directory.Exists(dir)) dir = Utils.dataPath;
                        LogFilename = dir + "\\" + Path.GetFileName(reqFilename);
                    }
                    if (File.Exists(LogFilename)) File.Delete(LogFilename);
                    CreateLogger(LogFilename);

                    writing = false;
                    missingData = false;
                    stw.Restart();
                    _Enabled = true;
                    writeHeader();
                }
                if (!value && _Enabled) // when it goes from true to false
                {
                    if (missingData) Console.WriteLine("Some data maybe missing from the log");
                    stw.Reset();
                    header = "";
                    _Enabled = false;
                }
            }
        }
    }

    /// <summary>
    /// creates and logs in multicollumn table; record structure is defined in record List of string
    /// the column names must be set when created
    /// dictLog will extract only the keys with these names in that order
    /// </summary>
    public class DictFileLogger : FileLogger 
    {
        List<string> record;
        /// <summary>
        /// Class constructor
        /// </summary>
        /// <param name="_record">Required and fixed column names</param>
        /// <param name="_prefix">Ending in case of timestamp name</param>
        /// <param name="_reqFilename">If empty timestamp name is generated</param>
        public DictFileLogger(string[] _record, string _prefix = "", string _reqFilename = ""): base(_prefix, _reqFilename)
            // if reqFilename is something it should contain the prefix; 
            // the usual use is only prefix and no reqFilename
        {
            record = new List<string>(_record);
        }
        private readonly string[] titles = { "params", "steps" };
        /// <summary>
        /// When the group MMexec is known
        /// </summary>
        /// <param name="mme"></param>
        public void setMMexecAsHeader(MMexec mme)
        {
            header = ""; subheaders.Clear(); char q = '"';
            if (Utils.isNull(mme)) return;
            foreach (string ss in titles)
            {
                if (mme.prms.ContainsKey(ss))
                {
                    string sub = JsonConvert.SerializeObject(mme.prms[ss]);
                    mme.prms.Remove(ss);
                    subheaders.Add("{"+q+ss+q+":"+sub+"}");
                }
            }
            header = JsonConvert.SerializeObject(mme);
        }
        /// <summary>
        /// Write the header & subheaders and colunm names line
        /// </summary>
        public override void writeHeader()
        {
            base.writeHeader();
            if(Utils.isNull(record)) throw new Exception("The record list not set");
            if (record.Count.Equals(0)) throw new Exception("The record list is empty");
            string ss = "";
            foreach (string item in record) 
                ss += item + '\t';
            ss = ss.Remove(ss.Length - 1); 
            log(ss);
        }
       
        /// <summary>
        /// This main methods in three variations, but that is the basic one
        /// </summary>
        /// <param name="dict"></param>
        public void dictLog(Dictionary<string, string> dict)
        {
            string ss = "";
            foreach (string item in record)
            {
                if (dict.ContainsKey(item)) ss += dict[item];
                else ss += "<none>";
                ss += '\t';
            }
            ss = ss.Remove(ss.Length - 1);
            log(ss);
        }

        /// <summary>
        /// log that way with undefined Values type
        /// </summary>
        /// <param name="dict"></param>
        public void dictLog(Dictionary<string, object> dict)
        {
            Dictionary<string, string> dictS = new Dictionary<string, string>();
            foreach (var pair in dict)
            {
                dictS[pair.Key] = Convert.ToString(pair.Value);                
            }
            dictLog(dictS); 
        }

        /// <summary>
        /// log that way with double Values type with format
        /// </summary>
        /// <param name="dict"></param>
        /// <param name="format"></param>
        public void dictLog(Dictionary<string, double> dict, string format = "")
        {
            Dictionary<string, string> dictS = new Dictionary<string, string>();
            foreach (var pair in dict)
            {
                if (Double.IsNaN(pair.Value)) dictS[pair.Key] = "NaN";
                else dictS[pair.Key] = pair.Value.ToString(format);
            }
            dictLog(dictS); 
        }
    }

    /// <summary>
    /// Read dictionary from multi-column text file (tab separated)
    /// format first line #header (for conditions) - optional
    /// next line column names;
    /// header, subheaders & col names are read when instance is created 
    /// if _record = null then read this row in record
    /// if _record has items the record will be the cross-section of _record and column names list (fileRecord)
    /// </summary>
    public class DictFileReader
    {
        public string header;
        public List<string> subheaders; 
        StreamReader fileReader; public int counter = 0;
        public List<string> record, fileRecord;
        /// <summary>
        /// Class constructor
        /// </summary>
        /// <param name="Filename">File must exits</param>
        /// <param name="strArr">Array of column names</param>
        public DictFileReader(string Filename, string[] strArr = null)
        {            
            if (!File.Exists(Filename)) throw new Exception("no such file: "+Filename);
            header = ""; subheaders = new List<string>();
            // Read file using StreamReader. Reads file line by line  
            fileReader = new StreamReader(Filename);  
            counter = 0;
            string ln = fileReader.ReadLine();
            while(ln.StartsWith("#"))
            {
                if (header.Equals("")) header = ln.Remove(0, 1);
                else subheaders.Add(ln.Remove(0, 1));
                ln = fileReader.ReadLine();
            }
            while (ln.Equals(""))
                ln = fileReader.ReadLine(); // read the next if empty, and again...

            string[] ns = ln.Split('\t');
            fileRecord = new List<string>(ns);
            if (Utils.isNull(strArr)) record = new List<string>(fileRecord);
            else
            {
                List<string> _record = new List<string>(strArr);
                if (_record.Count.Equals(0)) record = new List<string>(fileRecord);
                else
                {
                    record = new List<string>(_record);
                    for (int i = _record.Count-1; i > -1; i--)
                    {
                        int j = fileRecord.IndexOf(_record[i]);
                        if (j.Equals(-1)) record.RemoveAt(i); 
                    }
                }                    
            }
        }
        
        /// <summary>
        /// returns one line (row) as (column.name , cell.value) dictionary
        /// </summary>
        /// <param name="rslt">one table row</param>
        /// <returns>if we can go again</returns>
        public bool stringIterator(ref Dictionary<string,string> rslt) // 
        {
            if (Utils.isNull(rslt)) rslt = new Dictionary<string, string>();
            else rslt.Clear();
            bool next = false; 
            string ln = fileReader.ReadLine();           
            if (Utils.isNull(ln))
            {
                fileReader.Close();
                return next;
            }
            while (ln.Equals("")) 
                ln = fileReader.ReadLine(); // read the next if empty, and again...
            string[] ns = ln.Split('\t');
            if(ns.Length != fileRecord.Count) throw new Exception("wrong number of columns");
            foreach (string ss in record)
            {
                int j = fileRecord.IndexOf(ss);
                if (j.Equals(-1)) throw new Exception("wrong column name: "+ss);
                rslt[ss] = ns[j];
            }
            counter++;
            next = true; return next;
        }

        /// <summary>
        /// same as above but Values are double
        /// </summary>
        /// <param name="rslt"></param>
        /// <returns></returns>
        public bool doubleIterator(ref Dictionary<string, double> rslt) // same as above but values in double (if possible)
        {
            rslt = new Dictionary<string, double>();
            Dictionary<string, string> strRslt = new Dictionary<string, string>();
            bool next = stringIterator(ref strRslt);
            foreach (var pair in strRslt)
            {
                try
                {
                    double dbl = Convert.ToDouble(pair.Value);
                    rslt[pair.Key] = dbl;
                }
                catch (FormatException)
                {
                    rslt[pair.Key] = Double.NaN;
                }               
            }
            return next;
        }
    }
    
    #endregion
    /// <summary>
    /// Hour-glass cursor while waiting for Godot
    /// </summary>
    public class WaitCursor : IDisposable
    {
        private System.Windows.Input.Cursor _previousCursor;
        public WaitCursor()
        {
            _previousCursor = Mouse.OverrideCursor;

            Mouse.OverrideCursor = Cursors.Wait;
        }

        #region IDisposable Members
        public void Dispose()
        {
            Mouse.OverrideCursor = _previousCursor;
        }
        #endregion
    }
}