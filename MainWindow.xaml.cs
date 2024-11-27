using System;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace VisualPing
{
    public partial class MainWindow : Window
    {
        private int currentX = 0;
        private int currentY = 0;
        private DispatcherTimer pingTimer;
        private Ping pinger;

        public MainWindow()
        {
            InitializeComponent();
            InitializePingTimer();
        }

        private void InitializePingTimer()
        {
            pinger = new Ping();
            pingTimer = new DispatcherTimer();
            pingTimer.Interval = TimeSpan.FromMilliseconds(1000); // Ping every second
            pingTimer.Tick += PingTimer_Tick;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            string address = Address.Text.Trim();

            // Stop any existing timer
            pingTimer.Stop();

            // Start pinging the new address
            pingTimer.Tag = address;
            pingTimer.Start();

            // Clear existing canvas
            pingGraph.Children.Clear();
            currentX = 0;
            currentY = 0;
        }

        private async void PingTimer_Tick(object sender, EventArgs e)
        {
            if (pingTimer.Tag == null) return;

            string address = pingTimer.Tag.ToString();

            try
            {
                PingReply reply = await pinger.SendPingAsync(address, 1000); // 1-second timeout

                // Dispatch UI update to main thread
                Dispatcher.Invoke(() =>
                {
                    if (reply.Status == IPStatus.Success)
                    {
                        // Create a green pixel with intensity based on ping time
                        AddPixelToCanvas(reply.RoundtripTime);
                    }
                    else
                    {
                        // Different colors for different error statuses
                        Color errorColor = GetErrorColor(reply.Status);
                        AddPixelToCanvas(errorColor, 0);
                    }
                });
            }
            catch (Exception ex)
            {
                // Dispatch error handling to main thread
                Dispatcher.Invoke(() =>
                {
                    AddPixelToCanvas(Colors.Red, 0);
                });
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
                    return Colors.AntiqueWhite;
                default:
                    return Colors.Red;
            }
        }

        private void AddPixelToCanvas(long pingTime, int errorColorIntensity = 0)
        {
            // Adjust canvas position (using 4x4 pixel size)
            if (currentX >= pingGraph.Width)
            {
                currentX = 0;
                currentY += 4;
            }

            // Reset canvas if full
            if (currentY >= pingGraph.Height)
            {
                pingGraph.Children.Clear();
                currentX = 0;
                currentY = 0;
            }

            // Determine color based on ping time
            Color pixelColor;
            if (errorColorIntensity > 0)
            {
                // Error case
                pixelColor = Color.FromRgb((byte)errorColorIntensity, 0, 0);
            }
            else if (pingTime <= 10)
            {
                // Bright green for very fast pings (under 10ms)
                pixelColor = Color.FromRgb(0, 255, 0);
            }
            else
            {
                // Gradient of green from bright to dark based on ping time
                // Max out at 250ms for darkest green
                byte greenIntensity = (byte)Math.Max(0, Math.Min(255, 255 - (pingTime * 255 / 1000)));  //1000 is a ping timeout set
                pixelColor = Color.FromRgb(0, greenIntensity, 0);
            }

            // Create 4x4 pixel rectangle
            Rectangle pixel = new Rectangle
            {
                Width = 4,
                Height = 4,
                Fill = new SolidColorBrush(pixelColor)
            };

            // Position the pixel
            Canvas.SetLeft(pixel, currentX);
            Canvas.SetTop(pixel, currentY);

            // Add pixel to canvas
            pingGraph.Children.Add(pixel);

            // Move to next position
            currentX += 4;
        }

        // Overload to handle direct color input (for error cases)
        private void AddPixelToCanvas(Color errorColor, int dummy)
        {
            // Adjust canvas position (using 4x4 pixel size)
            if (currentX >= pingGraph.Width)
            {
                currentX = 0;
                currentY += 4;
            }

            // Reset canvas if full
            if (currentY >= pingGraph.Height)
            {
                pingGraph.Children.Clear();
                currentX = 0;
                currentY = 0;
            }

            // Create 4x4 pixel rectangle
            Rectangle pixel = new Rectangle
            {
                Width = 4,
                Height = 4,
                Fill = new SolidColorBrush(errorColor)
            };

            // Position the pixel
            Canvas.SetLeft(pixel, currentX);
            Canvas.SetTop(pixel, currentY);

            // Add pixel to canvas
            pingGraph.Children.Add(pixel);

            // Move to next position
            currentX += 4;
        }

        // Add method to stop pinging when window closes
        protected override void OnClosed(EventArgs e)
        {
            pingTimer?.Stop();
            pinger?.Dispose();
            base.OnClosed(e);
        }

        private void Address_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
    }
}