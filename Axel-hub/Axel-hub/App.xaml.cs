using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace Axel_hub
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
		private static System.Threading.Mutex _mutex = null;
		protected override void OnStartup(StartupEventArgs e)
		{
			//string mutexId = ((System.Runtime.InteropServices.GuidAttribute)System.Reflection.Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(System.Runtime.InteropServices.GuidAttribute), false).GetValue(0)).Value.ToString();
			_mutex = new System.Threading.Mutex(true, "{8F6F0AC4-B9A1-45fd-A8CF-72F04E6BDE8E}", out bool createdNew);
			if (!createdNew)
			{
				MessageBox.Show("Previous instance of Axel-hub is still running.", "Application Halted");
				Current.Shutdown();
			}
			else Exit += CloseMutexHandler;
			base.OnStartup(e);
		}
		protected virtual void CloseMutexHandler(object sender, EventArgs e)
		{
			_mutex?.Close();
		}

	}

}
