using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media;
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

namespace UtilsNS
{
    public static class Utils
    {
        public static void DoEvents()
        {
            DispatcherFrame frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,
                new DispatcherOperationCallback(ExitFrame), frame);
            Dispatcher.PushFrame(frame);
        }

        public static object ExitFrame(object f)
        {
            ((DispatcherFrame)f).Continue = false;
            return null;
        }
        
        public static bool TheosComputer()
        {
            return (string)System.Environment.GetEnvironmentVariables()["COMPUTERNAME"] == "DESKTOP-U334RMA";
        }

        public static string getRunningVersion()
        {
            return System.Diagnostics.FileVersionInfo.GetVersionInfo(System.Environment.GetCommandLineArgs()[0]).ToString();
            //System.Reflection.Assembly.GetExecutingAssembly().Location; //Assembly.GetExecutingAssembly().Location
        }

        public static bool isNull(System.Object o)
        {
            return object.ReferenceEquals(null, o);
        }

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

        public static void writeList(string filename, List<string> ls)
        {
            File.WriteAllLines(filename, ls.ToArray());
        }

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

        public static void writeDict(string filename, Dictionary<string, string> dict)
        {
            List<string> ls = new List<string>();
            foreach (var pair in dict)
            {
                ls.Add(pair.Key + "=" + pair.Value);
            }
            File.WriteAllLines(filename, ls.ToArray());
        }

        public static double EnsureRange(double Value, double MinValue, double MaxValue)
        {
            if (Value < MinValue) return MinValue;
            if (Value > MaxValue) return MaxValue;
            return Value;
        }
        public static int EnsureRange(int Value, int MinValue, int MaxValue)
        {
            if (Value < MinValue) return MinValue;
            if (Value > MaxValue) return MaxValue;
            return Value;
        }
        public static bool InRange(double Value, double MinValue, double MaxValue)
        {
            return ((MinValue <= Value) && (Value <= MaxValue));
        }
        public static bool InRange(int Value, int MinValue, int MaxValue)
        {
            return ((MinValue <= Value) && (Value <= MaxValue));
        }
        public static void errorMessage(string errorMsg)
        {
            Console.WriteLine("Error: " + errorMsg);
        }

        public static double formatDouble(double d, string format)
        {
            return Convert.ToDouble(d.ToString(format));
        }
        public static double[] formatDouble(double[] d, string format)
        {
            double[] da = new double[d.Length];
            for (int i = 0; i < d.Length; i++) da[i] = Convert.ToDouble(d[i].ToString(format));
            return da;
        }

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

        public static Dictionary<string, string> readINI(string filename)
        {
            Dictionary<string, string> dict = new Dictionary<string, string>();
            if (!File.Exists(filename)) throw new Exception("File not found: " + filename);           
            List<string> ls = new List<string>();
            string line;
            foreach (string wline in File.ReadLines(filename))
            {
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
        public static void TimedMessageBox(string text, string title = "Information", int milliseconds = 1500)
        {
            int returnValue = MessageBoxTimeout(IntPtr.Zero, text, title, Convert.ToUInt32(0), 1, milliseconds);
            //return (MessageBoxReturnStatus)returnValue;
        }

       // public static string basePath = Directory.GetParent(Directory.GetParent(Environment.GetCommandLineArgs()[0]).FullName).FullName;
        public static string basePath = Directory.GetParent(Directory.GetParent(Environment.GetCommandLineArgs()[0]).Parent.FullName).FullName; 
        public static string configPath { get { return basePath + "\\Config\\"; } }
        public static string dataPath { get { return basePath + "\\Data\\"; } }


    }

    #region async file logger
    /// <summary>
    /// Async data storage device 
    /// first you set the full path of the file, otherwise it will save in data dir under date-time file name
    /// when you want the logging to start you set Enabled to true
    /// at the end you set Enabled to false (that will flush the buffer to HD)
    /// </summary>
    public class AutoFileLogger
    {
        private const bool traceMode = true;
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
                        string prenote;
                        if (prefix.Equals("")) prenote = "";
                        else prenote = prefix+"_";
                        _AutoSaveFileName = Utils.dataPath + prenote + DateTime.Now.ToString("yy-MM-dd_H-mm-ss") + defaultExt;
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
    // new (dec.2018) optimized for speed (7x faster) logger
    public class FileLogger
    {
        private const bool traceMode = true;
        public string header = ""; // that will be put as a file first line with # in front of it
        public string defaultExt = ".ahf";
        private ActionBlock<string> block;
        public string prefix { get; private set; }
        public string reqFilename { get; private set; }
        public bool writing { get; private set; }
        public bool missingData { get; private set; }
        public Stopwatch stw;
 
        public FileLogger(string _prefix = "", string _reqFilename = "") // if reqFilename is something it should contain the prefix; 
            // the usual use is only prefix and no reqFilename
        {
            reqFilename = _reqFilename;
            prefix = _prefix;
            stw = new Stopwatch();
        }

        public void log(List<string> newItems)
        {
            if (!Enabled) return;
            foreach (string newItem in newItems) log(newItem);
            return;
        }

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

        private void ConsoleLine(string txt)
        {
            Console.WriteLine(txt);
        }

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
                    string prenote;
                    if (prefix.Equals("")) prenote = "";
                    else prenote = prefix + "_";
                    if (reqFilename.Equals("")) LogFilename = Utils.dataPath + prenote + DateTime.Now.ToString("yy-MM-dd_H-mm-ss") + defaultExt;
                    else
                    {
                        dir = Directory.GetParent(reqFilename).FullName;
                        if (!Directory.Exists(dir)) dir = Utils.dataPath;
                        LogFilename = dir + Path.GetFileName(reqFilename);
                    }                        
                    CreateLogger(LogFilename);
                    string hdr = "";
                    if (!header.Equals("")) hdr = "# " + header + "\n";
                        
                    writing = false;
                    missingData = false;
                    stw.Restart();
                    _Enabled = true;
                    if (!header.Equals("")) log(hdr);
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

    #endregion

    public class WaitCursor : IDisposable
    {
        private Cursor _previousCursor;

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
    /*
    using(new WaitCursor())
    {
        // very long task to be marked
    }
     */
}