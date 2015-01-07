using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
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

        readonly Dictionary<char,string> Compass = new Dictionary<char,string>() {
                {'N', "ziemeļu"},
                {'S', "dienvidu"},
                {'W', "rietumu"},
                {'E', "austrumu"},
        };

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
        Dictionary<ExtraWeatherInfo, string> extraInfo;
        DateTime lastTempLoaded = DateTime.Now.AddSeconds(-60);

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            visualTimer.Elapsed += visualTimer_Elapsed;
            visualTimer.Start();
            bw.WorkerSupportsCancellation = true;

            bw.DoWork += new DoWorkEventHandler(bw_DoWork);
            bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bw_RunWorkerCompleted);
        }

        private void bw_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            dynamic dyn = e.Result;
            temperature = dyn.temper;
            extraInfo = dyn.dict;
        }

        private void bw_DoWork(object sender, DoWorkEventArgs e)
        {
            Dictionary<ExtraWeatherInfo,string> dict;
            string temper = GetWeather(out dict);
            e.Result = new { temper, dict };
        }

        private void visualTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            visualTimer.Stop();
            this.Dispatcher.Invoke((Action)(() =>
            {
                tb.Inlines.Clear();

                string monthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(DateTime.Now.Month);
                string dayName = CultureInfo.CurrentCulture.DateTimeFormat.GetDayName(DateTime.Now.DayOfWeek);

                tb.Inlines.Add(new Run(dayName + ", " + DateTime.Now.Day + ". " + monthName)
                {
                    FontSize = 60
                });
                tb.Inlines.Add(new Run(" " + DateTime.Now.ToShortTimeString())
                {
                    Foreground = Brushes.GreenYellow,
                    FontSize = 90
                });
                tb.Inlines.Add(new Run(":" + DateTime.Now.ToString("ss")));
                tb.Inlines.Add(new Run(Environment.NewLine + "               " + temperature));

                if (extraInfo != null)
                {
                    tb.Inlines.Add(new Run(Environment.NewLine +
                        "               " + extraInfo[ExtraWeatherInfo.WindSpeed] + Environment.NewLine +
                        "           " + extraInfo[ExtraWeatherInfo.Humidity] + " mitrums" +
                        " " + extraInfo[ExtraWeatherInfo.Pressure]
                        )
                    {
                        Foreground = Brushes.LightBlue,
                        FontSize = 50
                    });
                }

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

        private string GetWeather(out Dictionary<ExtraWeatherInfo, string> extraInfo)
        {
            string temper;

            try
            {
                HtmlAgilityPack.HtmlWeb web = new HtmlWeb();
                HtmlAgilityPack.HtmlDocument doc = web.Load(ADDR);

                temper = ExtractTemperature(doc);
                extraInfo = ExtractExtraInfo(doc);
            }
            catch
            {
                temper = "neizdevās!";
                extraInfo = null;
            }

            return temper;
        }

        enum ExtraWeatherInfo { WindSpeed = 0, Humidity = 1, Pressure = 2}

        private Dictionary<ExtraWeatherInfo, string> ExtractExtraInfo(HtmlDocument doc)
        {
            HtmlNodeCollection nodes = doc.DocumentNode.SelectNodes("//*[contains(@class, 'outlook_right')]");
            
            string windSpeed = "", humidity = "", pressure = "";
            int count = 0;

            foreach (HtmlNode row in nodes)
            {
                string item = HttpUtility.HtmlDecode(row.InnerText);

                switch (count)
                {
                    case (int)ExtraWeatherInfo.WindSpeed:
                        windSpeed = ParseWindspeed(item);
                        break;
                    case (int)ExtraWeatherInfo.Humidity:
                        humidity = item;
                        break;
                    case (int)ExtraWeatherInfo.Pressure:
                        pressure = item;
                        break;
                }
                count++;
            }

            return new Dictionary<ExtraWeatherInfo, string>() {
                    {ExtraWeatherInfo.WindSpeed, windSpeed},
                    {ExtraWeatherInfo.Humidity, humidity},
                    {ExtraWeatherInfo.Pressure, pressure},
                };
        }

        private string ParseWindspeed(string item)
        {
            string windSpeed = item.Trim();

            windSpeed = MphtoMs(windSpeed);

            windSpeed = windSpeed.Replace("mph from the", "m/s");
            
            int pos = windSpeed.Length - 1;
            string postfix = "";
            int count = 0;
            do
            {
                char lastChar = windSpeed[pos];
                string fixedStr = Compass[lastChar];
                
                postfix = fixedStr + " " + postfix;

                pos--;
                count++;
            } while (Compass.Keys.Contains(windSpeed[pos]));

            if(count > 1)
                postfix = postfix.ReplaceFirstOccurrance(" ", ", ");

            windSpeed = windSpeed.Remove(pos + 1, windSpeed.Length - pos - 1);
            windSpeed += postfix;

            return windSpeed;
        }

        private static string MphtoMs(string windSpeed)
        {
            string firstNums = String.Concat(windSpeed.TakeWhile(c => char.IsDigit(c)).ToList<char>());
            int windSpeedMph = int.Parse(firstNums);
            double windSpeedMs = Math.Round(0.44704 * windSpeedMph, 1);
            windSpeed = windSpeed.Remove(0, windSpeedMph.ToString().Length);
            return windSpeedMs + windSpeed;
        }      

        private static string ExtractTemperature(HtmlAgilityPack.HtmlDocument doc)
        {
            string temper = "gļuks";
            HtmlNodeCollection nodes = doc.DocumentNode.SelectNodes("//*[contains(@class, 'outlook_box1')]");

            foreach (HtmlNode row in nodes)
            {
                temper = HttpUtility.HtmlDecode(row.ChildNodes[1].InnerText);
            }
            return temper;
        }

    }

    public static class MyExtensions
    {

        public static string ReplaceFirstOccurrance(this string original, string oldValue, string newValue)
        {
            if (String.IsNullOrEmpty(original))
                return String.Empty;
            if (String.IsNullOrEmpty(oldValue))
                return original;
            if (String.IsNullOrEmpty(newValue))
                newValue = String.Empty;
            int loc = original.IndexOf(oldValue);
            return original.Remove(loc, oldValue.Length).Insert(loc, newValue);
        }
    }
}
