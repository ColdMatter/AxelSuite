using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace UtilsNS
{
    public static class Utils
    {
        public static bool isNull(System.Object o)
        {
            return object.ReferenceEquals(null, o);
        }

        public static double EnsureRange(double Value, double MinValue, double MaxValue)
        {
            return Math.Max(Math.Min(Value, MaxValue), MinValue);
        }
        public static int EnsureRange(int Value, int MinValue, int MaxValue)
        {
            return Math.Max(Math.Min(Value, MaxValue), MinValue);
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

        [DllImport("user32.dll", SetLastError=true)]
        static extern int MessageBoxTimeout(IntPtr hwnd, String text, String title, uint type, Int16 wLanguageId, Int32 milliseconds);
        public static void TimedMessageBox(string text, string title = "Information", int milliseconds = 1500)
        {
            int returnValue = MessageBoxTimeout(IntPtr.Zero, text, title, Convert.ToUInt32(0), 1, milliseconds);
            //return (MessageBoxReturnStatus)returnValue;
        }

    }

}