using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace AstonMartin
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>

    public partial class MainWindow : Window
    {
        const string ADDR = "http://www.worldweatheronline.com/Riga-weather/Riga/LV.aspx";

        public MainWindow()
        {
            InitializeComponent();

            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException += new UnhandledExceptionEventHandler(GlobalErrorHandler);
        }

        static private void GlobalErrorHandler(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = (Exception)e.ExceptionObject;
            MessageBox.Show("Wohoho! " + ex.Message + Environment.NewLine +
                            ex.InnerException + Environment.NewLine + ex.StackTrace);
        }

        private BackgroundWorker bw = new BackgroundWorker();

        Timer visualTimer = new Timer(100);

        string temperature = "ielādēju...";
        DateTime lastTempLoaded = DateTime.Now.AddSeconds(-60);

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            visualTimer.Elapsed += visualTimer_Elapsed;
            visualTimer.Start();

            bw.DoWork += new DoWorkEventHandler(bw_DoWork);
            bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bw_RunWorkerCompleted);
        }

        private void bw_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            temperature = (string)e.Result;
        }

        private void bw_DoWork(object sender, DoWorkEventArgs e)
        {
           
            e.Result = GetTemperature();
        }

        private void visualTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            visualTimer.Stop();
            this.Dispatcher.Invoke((Action)(() =>
            {
                tb.Inlines.Clear();
                tb.Inlines.Add(new Run(DateTime.Now.ToShortDateString() + " "));
                tb.Inlines.Add(new Run(DateTime.Now.ToShortTimeString()) { Foreground = Brushes.GreenYellow, FontSize = 90 });
                tb.Inlines.Add(new Run(":" + DateTime.Now.ToString("ss")));
                tb.Inlines.Add(new Run(Environment.NewLine + "               " + temperature));

                if (DateTime.Now - lastTempLoaded > new TimeSpan(0, 0, 40))
                {
                    lastTempLoaded = DateTime.Now;
                    if (bw.IsBusy)
                        bw.CancelAsync();
                    else
                        bw.RunWorkerAsync();
                }
            }));
            visualTimer.Start();
        }

        private string GetTemperature()
        {
            

            string temperatureLocal;

            try
            {
            HtmlAgilityPack.HtmlWeb web = new HtmlWeb();
            HtmlAgilityPack.HtmlDocument doc =
                web.Load(ADDR);

            HtmlNodeCollection nodes = doc.DocumentNode.SelectNodes("//*[contains(@class, 'outlook_box1')]");

            
                temperatureLocal = "error loading temperature";

                foreach (HtmlNode row in nodes)
                {
                    temperatureLocal = HttpUtility.HtmlDecode(row.ChildNodes[1].InnerText);
                }
                
            }
            catch
            {
                temperatureLocal = "neizdevās!";
            }

            

            return temperatureLocal;
        }


    }
}
