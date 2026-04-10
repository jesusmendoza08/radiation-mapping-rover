using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using System;
using System.Data;
using System.Data.Entity.Infrastructure.MappingViews;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using System.Windows.Media.Imaging;

using System.Windows.Navigation;

using System.Windows.Shapes;

using static System.Net.WebRequestMethods;



namespace RadiationRoverControlApp

{

    /// <summary>

    /// Interaction logic for MainWindow.xaml

    /// </summary>

    public partial class MainWindow : Window

    {
        private TcpClient client;
        private NetworkStream stream;
        private bool mapInitialized = false;


        private string RaspberryPiIP = "192.168.1.15";
        private int Cameraport = 5000;
        private int GPSport = 5050;
        private TcpClient gpsClient;
        public MainWindow()

        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await LoadCameraFeed();

                await InitializeMap();
                await StartGpsListener();

                ConnectionStatus.Text = "Connnected";
                ConnectionStatus.Foreground = System.Windows.Media.Brushes.Green;

            }
            catch
            {
                ConnectionStatus.Text = "Not Connected";
                ConnectionStatus.Foreground = System.Windows.Media.Brushes.Red;

            }

            Keyboard.Focus(this);
        }

        private void SendCommand(string command)
        {
            if (stream == null)
            {
                return;
            }
            byte[] data = Encoding.ASCII.GetBytes(command + "\n");
            stream.Write(data, 0, data.Length);

        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.W:
                    SendCommand("FORWARD");
                    break;

                case Key.S:
                    SendCommand("BACKWARD");
                    break;

                case Key.A:
                    SendCommand("LEFT");
                    break;

                case Key.D:
                    SendCommand("RIGHT");
                    break;
            }

        }
        private async Task LoadCameraFeed()
        {
            await CameraView.EnsureCoreWebView2Async();
            CameraView.Source = new Uri("http://192.168.1.15:5000/video_feed");
        }


        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            SendCommand("STOP");
        }
        private async Task InitializeMap()
        {

            await MapView.EnsureCoreWebView2Async();
            string html = @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'/>
    <link rel='stylesheet' href='https://unpkg.com/leaflet/dist/leaflet.css'/>
    <script src='https://unpkg.com/leaflet/dist/leaflet.js'></script>
    <style>
        html, body, #map { margin: 0; padding: 0; width: 100vw; height: 100vh; }
    </style>
</head>
<body>
    <div id='map'></div>
    <script>
        var map, marker, roverPath;

        function initMap() {
            map = L.map('map').setView([0,0], 2);
            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                maxZoom: 19,
            }).addTo(map);

            marker = L.marker([0,0]).addTo(map);
            roverPath = L.polyline([], {color:'lime', weight:3}).addTo(map);
        }

        function updateRover(lat, lon) {
            if(map && marker) {
                var newPos = [lat, lon];
                marker.setLatLng(newPos);
                map.setView(newPos, 15);
                roverPath.addLatLng(newPos);
            }
        }

        initMap(); 
    </script>
</body>
</html>";
            MapView.NavigateToString(html);
            MapView.NavigationCompleted += (s, e) => mapInitialized = true;

        }



        private async Task StartGpsListener()
        {

            gpsClient = new TcpClient();
            await gpsClient.ConnectAsync(RaspberryPiIP, GPSport);

            StreamReader reader = new StreamReader(gpsClient.GetStream());

            while (true)
            {

                string line = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(line)) continue;

                string[] parts = line.Split(',');
                if (parts.Length != 2) continue;


                if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double lat)) continue;
                if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double lon)) continue;


                Dispatcher.Invoke(async () =>
                {
                    await UpdateMap(lat, lon);
                });

            }

        }

        private async Task UpdateMap(double lat, double lon)
        {

            if (!mapInitialized) 
                return;

           string js = $"updateRover({lat.ToString(CultureInfo.InvariantCulture)}, {lon.ToString(CultureInfo.InvariantCulture)});";
           await MapView.ExecuteScriptAsync(js);

        }

    }



}