using System;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Reflection;
using System.IO;
using System.Diagnostics;
using Newtonsoft.Json;
using ximc;
using UtilsNS;

namespace Axel_tilt
{
    class Horizontal
    {
        public double posA { get; set; } // [mm]
        public double posB { get; set; }
        public double speed { get; set; } // [mm/s]
        public double OffsetDodging { get; set; }
    }

    class Motor
    {        
        private string name {get; set;}
        public int devIdx { get; private set; }
        public double horizOffset { get; private set; }
        
        Result res;
        double step2mmSlope, step2mmIntercept; // dist = Slope * steps + intercept;

        private static void print_status_calb(status_calb_t status_calb)
        {
            Console.WriteLine("speed: {0} {3}/s position: {1} {3} flags: {2}",
                        status_calb.CurSpeed, status_calb.CurPosition, status_calb.Flags, "mm");
        }

        public Motor(string Name = "")
        {
            name = Name;
            devIdx = -1;
            if(!name.Equals("")) Open();
            step2mmSlope = 0.0049263; step2mmIntercept = 0; // the fit is very slightly better with Intercept != 0
            horizOffset = 0;
        }

        public char letter()
        {
            return (char)(64 + devIdx);
        }
        public void Open()
        {
            if(!name.Equals("")) devIdx = API.open_device(name);
            else throw new Exception("No device name assigned");
            Console.WriteLine("Device {0}", devIdx);
        }

        public bool InRange(double dist) //[mm]
        {
            return Utils.InRange(dist, -horizOffset, steps2dist(9500, 0) - horizOffset);
        }

        public bool Home(bool wait = true)
        {
            // Send "home" command to device
            res = API.command_home(devIdx);
            if (res != Result.ok)
                throw new Exception("Error " + res.ToString());
            if(wait) Wait2stop(); 
            return true;
        }

        public bool Zero(bool setHoriz = false)
        {
            // Send "zero" command to device
            if(setHoriz) horizOffset = GetPosition();
            res = API.command_zero(devIdx);
            if (res != Result.ok)
               throw new Exception("Error " + res.ToString());
            Thread.Sleep(2);
            return true;
        }

        public bool Stop()
        {
            res = API.command_stop(devIdx);
            if (res != Result.ok)
                throw new Exception("Error " + res.ToString());
            return true;
        }

        public bool Wait2stop()
        {
            res = API.command_wait_for_stop(devIdx, 100);
            if (res != Result.ok)
                throw new Exception("Error " + res.ToString());
            return true;
        }

        public status_t Status()
        {
            // Read device status
            status_t status;
            res = API.get_status(devIdx, out status);
            if (res != Result.ok)
               throw new Exception("Error " + res.ToString());
            return status;
        }
        public List<string> ListStatus()
        {
            status_t sts = Status();
            List<string> ls = new List<string>();
            ls.Add("CurPosition: "+sts.CurPosition.ToString());
            ls.Add("uCurPosition: " + sts.uCurPosition.ToString());
            ls.Add("CurSpeed: " + sts.CurSpeed); // Motor shaft speed (no torque/load)
            //ls.Add("EncPosition: "+sts.EncPosition);
            ls.Add("Flags: "+sts.Flags.ToString());
            ls.Add("MoveSts: " + sts.MoveSts.ToString());
            ls.Add("MvCmdSts: " + sts.MvCmdSts.ToString());
            return ls;
        } 
        public double GetPosition() // dist
        {
            get_position_t stPos;
            res = API.get_position(devIdx, out stPos);
            if (res != Result.ok)
                throw new Exception("Error " + res.ToString());
            return steps2dist(stPos.Position, stPos.uPosition);
        }        

        public bool SetSpeed(double speed) // [mm/s]
        {
            double spd = Utils.EnsureRange(speed, 0, 10);
            engine_settings_t engine_settings;
            res = API.get_engine_settings(devIdx, out engine_settings);
            if (res != Result.ok)
                throw new Exception("Error " + res.ToString());
            int[] speedInSteps = dist2steps(spd);
            engine_settings.NomSpeed = (uint)speedInSteps[0];
            engine_settings.uNomSpeed = (uint)speedInSteps[1];
            res = API.set_engine_settings(devIdx, ref engine_settings);
            if (res != Result.ok)
                throw new Exception("Error " + res.ToString());
            return true;
        }

        public bool SetBacklash(bool bl) 
        {           
            engine_settings_t engine_settings;
            res = API.get_engine_settings(devIdx, out engine_settings);
            if (res != Result.ok)
                throw new Exception("Error " + res.ToString());
            uint ENGINE_ANTIPLAY = 0x08;
            if (bl) engine_settings.EngineFlags |= ENGINE_ANTIPLAY;
            else engine_settings.EngineFlags &= ~ENGINE_ANTIPLAY;
            res = API.set_engine_settings(devIdx, ref engine_settings);
            if (res != Result.ok)
                throw new Exception("Error " + res.ToString());
            return true;
        }

        private calibration_t Calibration()
        {
            status_calb_t status_calb;
            engine_settings_t engine_settings;
            res = API.get_engine_settings(devIdx, out engine_settings);
            if (res != Result.ok)
                throw new Exception("Error " + res.ToString());

            calibration_t calibration = new calibration_t();
            calibration.A = 0.1;
            calibration.MicrostepMode = Math.Max(1, engine_settings.MicrostepMode);

            // Read calibrated device status
            res = API.get_status_calb(devIdx, out status_calb, ref calibration);
            if (res != Result.ok)
                throw new Exception("Error " + res.ToString());
            print_status_calb(status_calb);
            return calibration;
        }

        public double steps2dist(int steps, int usteps) // [mm]
        {
            return (steps + usteps / 256.0) * step2mmSlope + step2mmIntercept;
        }

        public int[] dist2steps(double dist) // from [mm] to [steps, usteps]
        {
            int[] rt = new int[2];
            double nat = dist / step2mmSlope - step2mmIntercept;
            bool negative = nat < 0;
            nat = Math.Abs(nat);
            rt[0] = (int)Math.Floor(nat); if (negative) rt[0] = -rt[0];
            rt[1] = (int)Math.Floor((nat % 1) * 256.0); if (negative) rt[1] = -rt[1];
            return rt;
        }

        // async moves
        private bool MoveS(int steps, int usteps, bool wait) // return success 
        {
            res = API.command_move(devIdx, steps, usteps);
            if (wait) Wait2stop();
            return (res == Result.ok);                
        }
        public bool MoveD(double dist, bool wait = true) // return success 
        {
            if (!InRange(dist)) return false;
            int[] rt = dist2steps(dist);            
            return MoveS(rt[0], rt[1], wait);
        }
    }

    class Tilt
    {
        public Motor mA, mB;
        public bool AutoBacklash = true; // backlash: move to pos -> true; anything else -> false
        public bool busy { get; private set; }
        public bool MemsCorr { get; set; }

        List<string> devs;
        Result res;
        public const double tilt_arm = 510.003; // [mm]
        public const double MemsCorr_A = 1.071;
        public const double MemsCorr_B = -8.034;
        public const double minSpeed = 0; // [mm/s]
        public Horizontal horizontal;
        public Stopwatch sw = new Stopwatch();

        public Tilt()
        {
            busy = false;
            ConfigureDriver();
            if(devs.Count == 0) throw new Exception("Error: No step-motors found");
            mA = new Motor(devs[0]); mB = new Motor(devs[1]);
            if(mA.devIdx.Equals(-1)) mA.Open();
            if(mB.devIdx.Equals(-1)) mB.Open();
            horizontal = new Horizontal();
            if (File.Exists(Utils.configPath + "horizontal.cfg"))
            {
                string fileJson = File.ReadAllText(Utils.configPath + "horizontal.cfg");
                horizontal = JsonConvert.DeserializeObject<Horizontal>(fileJson);
            }
        }

#region service routines
        private static API.LoggingCallback callback;
        private static void MyLog (API.LogLevel loglevel, string message, IntPtr user_data)
        {
            Console.WriteLine("MyLog {0}: {1}", loglevel, message);
        }

        private static void print_status(status_t status)
        {
            Console.WriteLine("rpm: {0} pos: {1} flags: {2}",
                        status.CurSpeed, status.CurPosition, status.Flags);
        }

        private void ConfigureDriver()
        {
            int device = -1;           
            try
            {
                Console.WriteLine("testapp CLR runtime version: " + Assembly.GetExecutingAssembly().ImageRuntimeVersion);
                foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
                    if (a.GetName().Name.Equals("ximcnet"))
                        Console.WriteLine("ximcnet CLR runtime version: " + a.ImageRuntimeVersion);
                Console.WriteLine("Current CLR runtime version: " + Environment.Version.ToString());

                callback = new API.LoggingCallback(MyLog);
                API.set_logging_callback(callback, IntPtr.Zero);

                // Pointer to device enumeration structure
                IntPtr device_enumeration;

                // Probe flags, used to enable various enumeration options
                const int probe_flags = (int)(Flags.ENUMERATE_PROBE | Flags.ENUMERATE_NETWORK);

                // Enumeration hint, currently used to indicate ip address for network enumeration
                String enumerate_hints = "addr=192.168.1.1,172.16.2.3";
                // String enumerate_hints = "addr="; // this hint will use broadcast enumeration, if ENUMERATE_NETWORK flag is enabled

                //  Sets bindy (network) keyfile. Must be called before any call to "enumerate_devices" or "open_device" if you
                //  wish to use network-attached controllers. Accepts both absolute and relative paths, relative paths are resolved
                //  relative to the process working directory. If you do not need network devices then "set_bindy_key" is optional.
                API.set_bindy_key("keyfile.sqlite");

                // Enumerates all devices
                device_enumeration = API.enumerate_devices(probe_flags, enumerate_hints);
                if (device_enumeration == null)
                    throw new Exception("Error enumerating devices");

                // Gets found devices count
                int device_count = API.get_device_count(device_enumeration);
                if (device_count.Equals(0))
                {
                    MessageBox.Show("No actuators found. Check power and USB cabels.", "Fatal error:", MessageBoxButton.OK);
                    Application.Current.Shutdown();
                }
                    
                // List all found devices
                devs = new List<string>();
                for (int i = 0; i < device_count; ++i)
                {
                    // Gets device name 
                    String dev = API.get_device_name(device_enumeration, i);
                    System.Console.WriteLine("Found device {0}", dev);
                    devs.Add(dev);
                }
        
                // Get first device name or command-line arg
                String deviceName;
                if (device_count > 0)
                    deviceName = API.get_device_name(device_enumeration, 0);
                else
                    throw new Exception("No devices");
                System.Console.WriteLine("Using device {0}", deviceName);

                // Open this device
                device = API.open_device(deviceName);
                Console.WriteLine("Device {0}", device);

                StringBuilder versb = new StringBuilder(256);
                API.ximc_version(versb);
                Console.WriteLine("XIMC version: {0}", versb.ToString());

                status_t status;
                // Read device status
                res = API.get_status(device, out status);
                if (res != Result.ok)
                    throw new Exception("Error " + res.ToString());
                print_status(status);

                device_information_t di;
                res = API.get_device_information(device, out di);
                if (res != Result.ok)
                    throw new Exception("Error " + res.ToString());
                Console.WriteLine("Manufacturer {0}", di.Manufacturer);
/*
                // Send "zero" command to device
                res = API.command_zero(device);
                if (res != Result.ok)
                    throw new Exception("Error " + res.ToString());
                Thread.Sleep(2);

                // Read device status
                res = API.get_status(device, out status);
                if (res != Result.ok)
                    throw new Exception("Error " + res.ToString());
                print_status(status);

                Console.WriteLine("running...");
                // Send "move to the right" command to the device
                res = API.command_right(device);
                if (res != Result.ok)
                    throw new Exception("Error " + res.ToString());

                int shift = 0;
                Thread.Sleep(3 * 1000);
                // Read device status
                res = API.get_status(device, out status);
                shift -= status.CurPosition;
                if (res != Result.ok)
                    throw new Exception("Error " + res.ToString());
                print_status(status);

                status_calb_t status_calb;
                engine_settings_t engine_settings;
                res = API.get_engine_settings(device, out engine_settings);
                if (res != Result.ok)
                    throw new Exception("Error " + res.ToString());

                calibration_t calibration = new calibration_t();
                calibration.A = 0.1;
                calibration.MicrostepMode = Math.Max(1, engine_settings.MicrostepMode);

                // Read calibrated device status
                res = API.get_status_calb(device, out status_calb, ref calibration);
                if (res != Result.ok)
                    throw new Exception("Error " + res.ToString());
                print_status_calb(status_calb);

                res = API.get_status(device, out status);
                shift += status.CurPosition;
                if (res != Result.ok)
                    throw new Exception("Error " + res.ToString());

                Console.WriteLine("waiting for stop...");
                res = API.command_move(device, shift, 0);
                if (res != Result.ok)
                    throw new Exception("Error " + res.ToString());
                // Wait for stop of the device
                res = API.command_wait_for_stop(device, 100);
                if (res != Result.ok)
                    throw new Exception("Error " + res.ToString());

                // Read device status
                res = API.get_status(device, out status);
                if (res != Result.ok)
                    throw new Exception("Error " + res.ToString());
                print_status(status);

                Console.WriteLine("Done...");*/
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception " + e.Message);
                Console.WriteLine(e.StackTrace.ToString());
            }
            finally
            {
                if (device != -1)
                    API.close_device(ref device);
            }
            //Console.ReadKey();
        }
#endregion 
        public void HomeAndZero()
        {
            SetSpeed(horizontal.speed);
            mA.Home(false); mB.Home(false);
            busy = true;
            mA.Wait2stop(); mB.Wait2stop();
            busy = false;
            mA.Zero(); mB.Zero();
        }

        public void SetHorizontal(double posA, double posB) // [mm]
        {
            mA.MoveD(posA, false); mB.MoveD(posB, false);
            busy = true; ;
            mA.Wait2stop(); mB.Wait2stop();
            busy = false;
            mA.Zero(true); mB.Zero(true);
        }

        public void Stop()
        {
            mA.Stop(); mB.Stop();
        }

        public void Close()
        {
            int iA = mA.devIdx; int iB = mB.devIdx;
            res = API.close_device(ref iA);
            if (res != Result.ok)
                throw new Exception("Error " + res.ToString());
            res = API.close_device(ref iB);
            if (res != Result.ok)
                throw new Exception("Error " + res.ToString());
        }

        public double dist2tilt(double dist) // tilt[mrad]; dist[mm] from zero pos
        {
            return 1000.0 * Math.Atan(dist / tilt_arm);
        }

        public double tilt2dist(double tilt) // tilt[mrad]; dist[mm] from zero pos
        {
            return Math.Tan(tilt / 1000.0) * tilt_arm;
        }

        public double accel2tilt(double accel) // accel[mg]; tilt[mrad]
        {
            double dodgedAccel = accel + horizontal.OffsetDodging;
            double rslt = 1000.0 * Math.Asin(dodgedAccel / 1000.0);
            if (MemsCorr) return (rslt - MemsCorr_B) / MemsCorr_A;
            else return rslt;
        }

        public double tilt2accel(double tilt) // accel[mg]; tilt[mrad]
        {
            double accel = 1000.0 * Math.Sin(tilt / 1000.0);
            double rslt = accel;
            if (MemsCorr) rslt = MemsCorr_A * accel + MemsCorr_B;
            return rslt - horizontal.OffsetDodging;
        }

        public double accelSpeed // [mg/s]
        {
            get 
            {                 
                double tlt = dist2tilt(workingSpeed);
                double ws = tilt2accel(tlt);
                double acc0 = tilt2accel(dist2tilt(0));
                return ws - acc0;
            }
            set 
            {
                double pos0 = tilt2dist(accel2tilt(0)); // pos[mm] at 0 mg accel
                double tlt = accel2tilt(value);
                double spd = tilt2dist(tlt);
                double spdr = Math.Abs(spd - pos0);
                SetSpeed(spdr);
                OnLog("accel.speed = " + accelSpeed.ToString("G5")+": "+value.ToString("G5"));
            }
        }

        public bool MoveDist(double dist, bool wait = true) // [mm]
        {
            bool bb = mA.MoveD(dist, false) && mB.MoveD(dist, false);
            if (wait)
            {
                mA.Wait2stop(); mB.Wait2stop();
            }
            return bb;
        }

        public bool MoveAccel(double accel, bool wait = true) // [mg]; wait: for manual oper -> true; for moving patterns -> false
        {
            OnLog("T> accel to " + accel.ToString("G5")); DoEvents();
            return MoveDist(tilt2dist(accel2tilt(accel)), wait);
        }

        public void Wait4Stop()
        {
            busy = true;
            try
            {
                res = API.command_wait_for_stop(mA.devIdx, 100);
                if (res != Result.ok)
                    throw new Exception("Error " + res.ToString());
                res = API.command_wait_for_stop(mB.devIdx, 100);
                if (res != Result.ok)
                    throw new Exception("Error " + res.ToString());
            }
            finally { busy = false; }
        }

        double workingSpeed;
        public bool SetSpeed(double speed = -1) // [mm/s]
        {
            double spd;
            if (speed < 0) spd = horizontal.speed;
            else spd = Utils.EnsureRange(speed, minSpeed, 10);
            OnLog("T> speed to " + spd.ToString("G5")+" [mm/s]"); DoEvents();
            workingSpeed = spd;
            return mA.SetSpeed(spd) && mB.SetSpeed(spd);
        }
        public bool SetBacklash(bool bl)
        {
            return mA.SetBacklash(bl) && mB.SetBacklash(bl);
        }

        public double GetPosition() // dist [mm]
        {
            return (mA.GetPosition() + mB.GetPosition()) / 2.0;
        }
        public double GetAccel() // accel [mg]
        {
            return tilt2accel(dist2tilt(GetPosition()));
        }

        #region pattern generator
        public void DoEvents()
        {
            DispatcherFrame frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,
                new DispatcherOperationCallback(ExitFrame), frame);
            Dispatcher.PushFrame(frame);
        }
        public object ExitFrame(object f)
        {
            ((DispatcherFrame)f).Continue = false;
            return null;
        }

        public delegate void EndHandler(bool userCancel);
        public event EndHandler OnEnd;

        protected void EndEvent(bool userCancel)
        {
            if (OnEnd != null) OnEnd(userCancel);
        }

        public delegate void LogHandler(string txt);
        public event LogHandler OnLog;

        protected void LogEvent(string txt)
        {
            if (OnLog != null) OnLog(txt);
        }

        public delegate void MoveHandler(Point target);
        public event MoveHandler OnMove;

        protected void MoveEvent(Point target)
        {
            if (OnMove != null) OnMove(target);
        }

        // start async movement to toPos, with a speed so to take time <time>
        public void SingleMove(double fromPos, double toPos, double time) // start,end [mg]; for time [s]
        {
            accelSpeed = Math.Abs((toPos - fromPos) / time)*1.05;
            MoveAccel(toPos, false);
        }

        public bool request2Stop = false;
        
        List<Point> pattern;
        int stepIdx = -1;
        DispatcherTimer dTimer;
        public void NextStep(object sender, EventArgs e)
        {
            if (stepIdx > pattern.Count - 1) { dTimer.Stop(); OnEnd(false); return; }
            double forTime = pattern[stepIdx].X - pattern[stepIdx - 1].X;
            SingleMove(pattern[stepIdx - 1].Y, pattern[stepIdx].Y, forTime);    
            stepIdx += 1;        
            if (!request2Stop)
            {
                MoveEvent(pattern[stepIdx - 1]);
                dTimer.Stop();
                dTimer.Interval = new TimeSpan((int)(forTime*1000*10000));
                dTimer.Start();
            }                              
        }

        // first pair <0, init.pos>; second - <time1, second.pos>, etc. [time,ampl] in [s,mg] units
        public void MoveInPattern(double[,] ptrn, double period, double ampl, double offset) // one pattern
        {
            pattern = new List<Point>();
            int len = ptrn.GetUpperBound(0)+1;
            for (int i = 0; i < len; i++)
                pattern.Add(new Point(period * (ptrn[i, 0] / 100.0), ampl * (ptrn[i, 1] / 100.0) + offset));
            MoveAccel(pattern[0].Y); // init position [idx = 0]            
            stepIdx = 1; // next target
            if (Utils.isNull(dTimer))
            {
                dTimer = new DispatcherTimer(DispatcherPriority.Send);
                dTimer.Tick += NextStep;
            }
            else dTimer.Stop();
            NextStep(null,null);
        }
        #endregion
    }
}
