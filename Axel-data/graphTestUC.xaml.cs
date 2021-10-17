using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using UtilsNS;

namespace Axel_data
{
    /// <summary>
    /// Interaction logic for graphTestUC.xaml
    /// </summary>
    public partial class graphTestUC : UserControl
    {
        private readonly Random _random;
        private readonly DispatcherTimer _timer;
        const int Rows = 10; // number of series
        const int Columns = 101;                      

        public graphTestUC()
        {
            InitializeComponent();
            _random = new Random();
            _timer = new DispatcherTimer(TimeSpan.FromSeconds(0.5), DispatcherPriority.Normal, OnTimerTick, Dispatcher);
            _timer.Stop();
        }
        private void OnTimerTick(object sender, EventArgs e)
        {
            if (rbMode1.IsChecked.Value)
            {   
                var data = new uint[Rows, Columns];
                for (int i = 0; i < Rows; ++i)            
                    for (int j = 0; j < Columns; ++j)
                        data[i, j] = (uint)_random.Next(0, 101);
                graph1.DataContext = data;  
            }           
        }

        private void btnTest_Click(object sender, RoutedEventArgs e)
        {
            if (rbMode1.IsChecked.Value) _timer.Start();

            if (rbMode2.IsChecked.Value)
            {
                var data = new uint[Rows, Columns];
                for (int j = 0; j < Columns; ++j)
                {
                    //var data = new uint[Rows, j+1];
                    for (int i = 0; i < Rows; ++i)                    
                        data[i, j] = (uint)_random.Next(0, 101);
                    graph1.DataContext = data; graph1.Refresh();
                    Utils.DoEvents();
                    Thread.Sleep(500);
                }
            }
        }
    }
}
