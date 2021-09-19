using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using AForge.Imaging.Filters;
using AForge.Video;
using AForge.Video.DirectShow;
using ZXing;
using System.Runtime.InteropServices;

namespace WpfImageProcessing
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        static BitmapImage bi;

        #region public vlastnosti

        public ObservableCollection<FilterInfo> VideoDevices { get; set; }

        public FilterInfo CurrentDevice
        {
            get { return _currentDevice; }
            set { _currentDevice = value; this.OnPropertyChanged("CurrentDevice"); }
        }
        private FilterInfo _currentDevice;

        public bool Original
        {
            get { return _original; }
            set { _original = value; this.OnPropertyChanged("Original"); }
        }
        private bool _original;

        int correctTime = 30; // 35
        int errorTime = 10; // 20
        int maxSec = 60;
        int currentSec = 0;
        #endregion

        enum Netopyri {
            NetopyrVecerni,
            NetopyrRezavy,
            NetopyrVodni,
            VrapenecMaly,
            ZadnyNetopyr
        }

        bool QRLocked = false;
        Dictionary<Netopyri, System.Windows.Controls.Image> batPics;
        Dictionary<Netopyri, System.Windows.Controls.Label> batLabels;
        Menu mainMenu;
        #region private

        private IVideoSource _videoSource;

        #endregion
 
        public MainWindow(Menu mainMenu)
        {
           
            InitializeComponent();
            this.DataContext = this;
            GetVideoDevices();

            this.mainMenu = mainMenu;
            
            Original = true;
            this.Closing += Window_Closing;


            vecerni.Tag = Netopyri.NetopyrVecerni;
            rezavy.Tag = Netopyri.NetopyrRezavy;
            vodni.Tag = Netopyri.NetopyrVodni;
            maly.Tag = Netopyri.VrapenecMaly;

            batPics = new Dictionary<Netopyri, System.Windows.Controls.Image>
            {
                { Netopyri.NetopyrVecerni, vecerni1 },
                { Netopyri.NetopyrRezavy, rezavy1 },
                { Netopyri.NetopyrVodni, vodni1 },
                { Netopyri.VrapenecMaly, maly1}
            };
            foreach (KeyValuePair<Netopyri, System.Windows.Controls.Image> aPic in batPics)
            {
                aPic.Value.Visibility = Visibility.Hidden;
            }

            System.Windows.Controls.Label l = vodni2;

            batLabels = new Dictionary<Netopyri, System.Windows.Controls.Label>
            {
                { Netopyri.NetopyrVecerni, vecerni2 },
                { Netopyri.NetopyrRezavy, rezavy2 },
                { Netopyri.NetopyrVodni, vodni2 },
                { Netopyri.VrapenecMaly, maly2}
            };

            timer.Tick += TimerTick;
        }
        DispatcherTimer timer = new DispatcherTimer();

        private Netopyri stav = Netopyri.ZadnyNetopyr;

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            StopCamera();
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            StartCamera();
        }
        
        private void Load_Msgbox(string msg) {

            if (msg.Contains("večerní") && stav == Netopyri.NetopyrVecerni)
            {
               
                AutoClosingMessageBox.Show("Máš štěstí! Ulovil jsi signál netopýra večerního. Víš, že netopýr večerní nemá rád hory, ale zato rád bydlí blízko lidí? \n \nStiskni OK a pokračuj dál v lovu", "Info", correctTime, MessageBoxButton.OK, MessageBoxImage.Information);

            }
            else if (msg.Contains("vodní") && stav == Netopyri.NetopyrVodni)
            {
               
                AutoClosingMessageBox.Show("Skvělé! Máš signál netopýra rezavého. Víš, že vytrvalý lezec a na své zimoviště uletí i přes 2000 km? \n \nStiskni OK a pokračuj dál v lovu", "Info", correctTime, MessageBoxButton.OK, MessageBoxImage.Information);

            }
            else if (msg.Contains("rezavý") && stav == Netopyri.NetopyrRezavy)
            {
                
                AutoClosingMessageBox.Show("Skvělé! Máš signál netopýra rezavého. Víš, že vytrvalý lezec a na své zimoviště uletí i přes 2000 km? \n \nStiskni OK a pokračuj dál v lovu", "Info", correctTime,MessageBoxButton.OK, MessageBoxImage.Information);

            }
            else if (msg.Contains("malý") && stav == Netopyri.VrapenecMaly)
            {
                
                AutoClosingMessageBox.Show("Trefa! Signály, které jsi zaměřil patří vrapenci malému. Víš, že také zimuje v koloniích, ale odděleně, aby se ostátních vrápenců nedotýkal? \n \nStiskni OK a pokračuj dál v lovu", "Info", correctTime, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                
                AutoClosingMessageBox.Show("Špatně jsi to naladil, zkus to znovu! ", "Info", errorTime, MessageBoxButton.OK, MessageBoxImage.Error);
                //MessageBox.Show(, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
       
            batLabels[stav].Foreground = System.Windows.Media.Brushes.LightGreen;
            batPics[stav].Visibility = Visibility.Visible;
            stav = Netopyri.ZadnyNetopyr;
        }
        bool keepMenuAlive = false;
        private void video_NewFrame(object sender, AForge.Video.NewFrameEventArgs eventArgs)
        {
            eventArgs.Frame.RotateFlip(RotateFlipType.RotateNoneFlipX);


            Bitmap bmp = (Bitmap)eventArgs.Frame.Clone();

            if (QRLocked == false)
            {
                BarcodeReader reader = new BarcodeReader();
                var result = reader.Decode(bmp);

                if (result != null)
                {
                    this.txtBarcode.Dispatcher.BeginInvoke(new Action(delegate ()
                    {
                            //txtBarcode.Text = result.ToString();
                            QRLocked = false;
                            Load_Msgbox(result.ToString());
                            QRLocked = true;
                            RefreshTimer();
                    }));
                }
            }
            
            try
            {
                
                using (var bitmap = (Bitmap)eventArgs.Frame.Clone())
                {
                    if (Original)
                    {
                        bi = bitmap.ToBitmapImage();
                    }
                }
                bi.Freeze(); // avoid cross thread operations and prevents leaks
                Dispatcher.BeginInvoke(new ThreadStart(delegate { videoPlayer.Source = bi; }));
                
            }
            catch (Exception exc)
            {
                MessageBox.Show("Error on _videoSource_NewFrame:\n" + exc.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StopCamera();
            }
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            StopCamera();
        }

        private void GetVideoDevices()
        {
            VideoDevices = new ObservableCollection<FilterInfo>();
            foreach (FilterInfo filterInfo in new FilterInfoCollection(FilterCategory.VideoInputDevice))
            {
                VideoDevices.Add(filterInfo);
            }
            if (VideoDevices.Any())
            {
                CurrentDevice = VideoDevices[0];
            }
            else
            {
                MessageBox.Show("No video sources found", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StartCamera()
        {
            if (CurrentDevice != null)
            {
                _videoSource = new VideoCaptureDevice(CurrentDevice.MonikerString);
                _videoSource.NewFrame += video_NewFrame;
            }
            _videoSource.Start();
            videoPlayer.Visibility = Visibility.Visible;
        }


        private void StopCamera()
        {
            RefreshTimer();
            if (_videoSource != null && _videoSource.IsRunning)
            {
                _videoSource.Stop();

                videoPlayer.Visibility = Visibility.Hidden;
            }
        }

        #region INotifyPropertyChanged members

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = this.PropertyChanged;
            if (handler != null)
            {
                var e = new PropertyChangedEventArgs(propertyName);
                handler(this, e);
            }
        }

        #endregion
       

        /*private void Stop_Stream(object sender, EventArgs e)
        {
            StopCamera();
        }*/
        
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            
            if (keepMenuAlive == false)
            {
                mainMenu.Close();
                StopCamera();
            }
            
        }
        
        // naladit

        private void netopyr_Click(object sender, RoutedEventArgs e)
        {
            RefreshTimer();
            if (stav != Netopyri.ZadnyNetopyr)
                batLabels[stav].Foreground = System.Windows.Media.Brushes.White;
            Button b = sender as Button;
            stav = (Netopyri)b.Tag;
            batLabels[stav].Foreground = System.Windows.Media.Brushes.Yellow;
            if (_videoSource.IsRunning == false)
                StartCamera();
        }

        // go to menu  // CLOSE
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            SwitchToMenu();
        }
        

        private void SwitchToMenu()
        {
           
            keepMenuAlive = true;
            mainMenu.Show();
            
            Close();
        }


        private void TimerTick(object sender, EventArgs e)
        {
            
            if (currentSec < maxSec)
            {
                currentSec++;
            }
            else
            {
                DispatcherTimer timer = (DispatcherTimer)sender;
                
                RefreshLayout();
                RefreshTimer();
            }
        }

        private void StartCloseTimer(object sender, RoutedEventArgs e)
        {
            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1d);
            timer.Tick += TimerTick;
            timer.Start();
            StartCamera();



        }
        private void RefreshTimer()
        {
            currentSec = 0;
        }

        private void RefreshLayout()
        {
            StopCamera();
            foreach (KeyValuePair<Netopyri, System.Windows.Controls.Image> aPic in batPics)
            {
                aPic.Value.Visibility = Visibility.Hidden;
            }
            foreach (var label in batLabels)
            {
                label.Value.Foreground = System.Windows.Media.Brushes.White;
            }

        }

        private void Window_Activated(object sender, EventArgs e)
        {
            //StartCamera();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            StopCamera();
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            RefreshTimer();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            RefreshTimer();
        }

        public class AutoClosingMessageBox
        {
            DispatcherTimer _timeoutTimer;
            string _caption;
            AutoClosingMessageBox(string text, string caption, int timeout, MessageBoxButton button,MessageBoxImage image = MessageBoxImage.Information)
            {
                _caption = caption;
                _timeoutTimer = new DispatcherTimer();
                _timeoutTimer.Tick += OnTimerElapsed;
                _timeoutTimer.Interval = new TimeSpan(0, 0, timeout);
                _timeoutTimer.Start();
                MessageBox.Show(text, caption, button, image);
            }
            public static void Show(string text, string caption, int timeout, MessageBoxButton button, MessageBoxImage image = MessageBoxImage.Information)
            {
                new AutoClosingMessageBox(text, caption, timeout, button, image);
            }
            void OnTimerElapsed(object sender, EventArgs e)
            {
                IntPtr mbWnd = FindWindow(null, _caption);
                if (mbWnd != IntPtr.Zero)
                    SendMessage(mbWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                _timeoutTimer.Stop();
                _timeoutTimer = null;
            }
            const int WM_CLOSE = 0x0010;
            [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
            static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
            [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
            static extern IntPtr SendMessage(IntPtr hWnd, UInt32 Msg, IntPtr wParam, IntPtr lParam);
        }
    }
}

