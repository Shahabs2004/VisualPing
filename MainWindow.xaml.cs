using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.IO;
using Microsoft.Win32;
using System.Linq;
using System.Xml.Serialization;

namespace VisualPing
{
    public partial class MainWindow : Window
    {
        private int currentX = 0;
        private int currentY = 0;
        private DispatcherTimer pingTimer;
        private Ping pinger;
        private List<long> pingTimes = new List<long>();
        private int packetsSent = 0;
        private int packetsLost = 0;

        public MainWindow()
        {
            InitializeComponent();
            InitializePingTimer();
            InitializeDefaultSettings();
        }

        private void InitializeDefaultSettings()
        {
            cbInterval.SelectedIndex = 1; // Default to 1000ms
            cbTimeout.SelectedIndex = 1;  // Default to 1000ms
            UpdateTimerInterval();
        }

        private void InitializePingTimer()
        {
            pinger = new Ping();
            pingTimer = new DispatcherTimer();
            pingTimer.Interval = TimeSpan.FromMilliseconds(1000);
            pingTimer.Tick += PingTimer_Tick;
        }

        private void UpdateTimerInterval()
        {
            if (cbInterval.SelectedItem is ComboBoxItem selectedItem)
            {
                if (int.TryParse(selectedItem.Content.ToString(), out int interval))
                {
                    pingTimer.Interval = TimeSpan.FromMilliseconds(interval);
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (pingTimer.IsEnabled)
            {
                pingTimer.Stop();
                btnPing.Content = "Start Ping";
            }
            else
            {
                string address = Address.Text.Trim();
                pingTimer.Tag = address;
                pingTimer.Start();
                btnPing.Content = "Stop Ping";
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            pingGraph.Children.Clear();
            currentX = 0;
            currentY = 0;
            pingTimes.Clear();
            packetsSent = 0;
            packetsLost = 0;
            UpdateStatistics();
        }

        private async void PingTimer_Tick(object sender, EventArgs e)
        {
            if (pingTimer.Tag == null) return;

            string address = pingTimer.Tag.ToString();
            int timeout = 1000;

            if (cbTimeout.SelectedItem is ComboBoxItem selectedItem)
            {
                int.TryParse(selectedItem.Content.ToString(), out timeout);
            }

            try
            {
                packetsSent++;
                PingReply reply = await pinger.SendPingAsync(address, timeout);

                Dispatcher.Invoke(() =>
                {
                    if (reply.Status == IPStatus.Success)
                    {
                        pingTimes.Add(reply.RoundtripTime);
                        AddPixelToCanvas(reply.RoundtripTime);
                        txtCurrentPing.Text = $"{reply.RoundtripTime} ms";
                    }
                    else
                    {
                        packetsLost++;
                        Color errorColor = GetErrorColor(reply.Status);
                        AddPixelToCanvas(errorColor, 0);
                        txtCurrentPing.Text = "Failed";
                    }
                    UpdateStatistics();
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    packetsLost++;
                    AddPixelToCanvas(Colors.Red, 0);
                    txtCurrentPing.Text = "Error";
                    UpdateStatistics();
                });
            }
        }

        private void UpdateStatistics()
        {
            if (pingTimes.Count > 0)
            {
                double average = pingTimes.Average();
                txtAveragePing.Text = $"{average:F1} ms";
            }
            else
            {
                txtAveragePing.Text = "-- ms";
            }

            if (packetsSent > 0)
            {
                double lossRate = (double)packetsLost / packetsSent * 100;
                txtPacketLoss.Text = $"{lossRate:F1}%";
            }
            else
            {
                txtPacketLoss.Text = "0%";
            }
        }

        private Color GetErrorColor(IPStatus status)
        {
            switch (status)
            {
                case IPStatus.DestinationNetworkUnreachable:
                    return Colors.CadetBlue;
                case IPStatus.DestinationHostUnreachable:
                    return Colors.Magenta;
                case IPStatus.TimedOut:
                    return Colors.Red;
                default:
                    return Colors.Black;
            }
        }

        private void AddPixelToCanvas(long pingTime, int errorColorIntensity = 0)
        {
            if (currentX >= pingGraph.ActualWidth)
            {
                currentX = 0;
                currentY += 4;
            }

            if (currentY >= pingGraph.ActualHeight)
            {
                pingGraph.Children.Clear();
                currentX = 0;
                currentY = 0;
            }

            Color pixelColor;
            if (errorColorIntensity > 0)
            {
                pixelColor = Color.FromRgb((byte)errorColorIntensity, 0, 0);
            }
            else if (pingTime <= 10)
            {
                pixelColor = Color.FromRgb(0, 255, 0);
            }
            else
            {
                byte greenIntensity = (byte)Math.Max(0, Math.Min(255, 255 - (pingTime * 255 / 1000)));
                pixelColor = Color.FromRgb(0, greenIntensity, 0);
            }

            Rectangle pixel = new Rectangle
            {
                Width = 4,
                Height = 4,
                Fill = new SolidColorBrush(pixelColor)
            };

            Canvas.SetLeft(pixel, currentX);
            Canvas.SetTop(pixel, currentY);
            pingGraph.Children.Add(pixel);
            currentX += 4;
        }

        private void AddPixelToCanvas(Color errorColor, int dummy)
        {
            if (currentX >= pingGraph.ActualWidth)
            {
                currentX = 0;
                currentY += 4;
            }

            if (currentY >= pingGraph.ActualHeight)
            {
                pingGraph.Children.Clear();
                currentX = 0;
                currentY = 0;
            }

            Rectangle pixel = new Rectangle
            {
                Width = 4,
                Height = 4,
                Fill = new SolidColorBrush(errorColor)
            };

            Canvas.SetLeft(pixel, currentX);
            Canvas.SetTop(pixel, currentY);
            pingGraph.Children.Add(pixel);
            currentX += 4;
        }

        private void Interval_Changed(object sender, SelectionChangedEventArgs e)
        {
            UpdateTimerInterval();
        }

        private void Timeout_Changed(object sender, SelectionChangedEventArgs e)
        {
            // Timeout value is used directly in the PingTimer_Tick method
        }

        // Menu item click handlers
        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            // Implement settings save logic
        }

        private void LoadSettings_Click(object sender, RoutedEventArgs e)
        {
            // Implement settings load logic
        }
        private void Export_Click(object sender, RoutedEventArgs e)
        
        {
            // Implement log export logic
        }
        private void ExportLogs_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ShowStats_Click(object sender, RoutedEventArgs e)
        {
            // Implement statistics window
        }

        private void ShowSettings_Click(object sender, RoutedEventArgs e)
        {
            // Implement settings window
        }

        protected override void OnClosed(EventArgs e)
        {
            pingTimer?.Stop();
            pinger?.Dispose();
            base.OnClosed(e);
        }
    }
}
