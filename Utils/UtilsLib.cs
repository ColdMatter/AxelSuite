using System;
using System.Windows;
using System.Windows.Input;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading.Tasks.Dataflow;

namespace UtilsNS
{
    public static class Utils
    {
        public static bool TheosComputer()
        {
            return (string)System.Environment.GetEnvironmentVariables()["COMPUTERNAME"] == "DESKTOP-U334RMA";
        }

        public static bool isNull(System.Object o)
        {
            return object.ReferenceEquals(null, o);
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

        public static void writeList(List<string> ls, string filename)
        {
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
        public bool writing { get; private set; }
        public bool missingData { get; private set; }
        public Stopwatch stw;

        public AutoFileLogger(string Filename = "")
        {
            _AutoSaveFileName = Filename;
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
                        _AutoSaveFileName = Utils.dataPath + DateTime.Now.ToString("yy-MM-dd_H-mm-ss") + defaultExt; 

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
                if (_Enabled) throw new Exception("Logger.Enabled must be Off when you set AutoSaveFileName.");
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
        public bool writing { get; private set; }
        public bool missingData { get; private set; }
        public Stopwatch stw;

        public FileLogger(string Filename = "")
        {
            _LogFilename = Filename;
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
                    if (!LogFilename.Equals("")) dir = Directory.GetParent(LogFilename).FullName;
                    if (!Directory.Exists(dir))
                        _LogFilename = Utils.dataPath + DateTime.Now.ToString("yy-MM-dd_H-mm-ss") + defaultExt;
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

        private string _LogFilename = "";
        public string LogFilename
        {
            get
            {
                return _LogFilename;
            }
            set
            {
                if (_Enabled) throw new Exception("Logger.Enabled must be Off when you set AutoSaveFileName.");
                _LogFilename = value;
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