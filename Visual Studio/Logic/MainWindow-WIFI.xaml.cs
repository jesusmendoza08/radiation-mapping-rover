using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.ComponentModel;

namespace RadiationRoverControlApp
{
    public partial class MainWindow : Window
    {
        //Variables to create a direct connection to the Raspberry Pi
        private TcpClient motorClient;
        private NetworkStream motorStream;
        private TcpClient telemetryClient;
        private bool mapInitialized = false;

        // Port Configuration
        private string RaspberryPiIP = "10.42.0.1";
        private int Cameraport = 5000;
        private int Telemetryport = 5060;
        private int MotorPort = 5070;

        public TelemetryData Telemetry { get; set; } = new TelemetryData();

        public MainWindow()
        {
            //Loading XAML Layout
            InitializeComponent();

            //Telling the UI to update the labels on the Telemetry
            this.DataContext = this.Telemetry;
            this.Focus();
        }

        //Loading the Window. Async let's it load several taskts without freezing the window.
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                //Waiting for the JPEG Frames from Flask Application
                await LoadCameraFeed();

                //Loads Leaflet and it's components
                await InitializeMap();

                // Connect to Motor Server
                try
                {
                    motorClient = new TcpClient();

                    //Creating physical connection to the Motor Server
                    await motorClient.ConnectAsync(RaspberryPiIP, MotorPort);
                    motorStream = motorClient.GetStream();
                    System.Diagnostics.Debug.WriteLine("Motor Control Connected");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Motor Connection Failed: {ex.Message}");
                }

                // Start Telemetry Listener (GPS + Radiation)
                _ = Task.Run(async () =>
                {
                    try { await StartTelemetryListener(); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Telemetry Task Error: {ex.Message}"); }
                });

                //Update the connection label on the UI if connected.
                ConnectionStatus.Text = "Connected";
                ConnectionStatus.Foreground = Brushes.Green;
            }
            catch (Exception ex)
            {
                //If anything fails during connection, change the text to Partial Connection.
                System.Diagnostics.Debug.WriteLine($"Startup Error: {ex.Message}");
                ConnectionStatus.Text = "Partial Connection";
                ConnectionStatus.Foreground = Brushes.Orange;
            }

            this.Focus();
            Keyboard.Focus(this);
        }

        private async Task LoadCameraFeed()
        {
            try
            {
                //Preparing Browser Engine
                await CameraView.EnsureCoreWebView2Async();

                //Points Browser to URL with Raspberry Pi IP.
                CameraView.Source = new Uri($"http://{RaspberryPiIP}:{Cameraport}/video_feed");
            }
            catch { /* Camera offline */ }
        }

        private async Task InitializeMap()
        {
            //Preparing Browser Engine
            await MapView.EnsureCoreWebView2Async();

            //Looking for the folder "MapResources" In the base directory
            string rootDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MapResources");
            
            //If the folder is not found
            if (!Directory.Exists(rootDirectory))
            {
                //Look up the folder tree to find it in the project source folder
                rootDirectory = Path.Combine(Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).Parent.Parent.Parent.FullName, "MapResources");
            }
            
            //Telling the browser that the local folder on the Desktop shuld be treated like a Website.
            MapView.CoreWebView2.SetVirtualHostNameToFolderMapping("rover.local", rootDirectory, CoreWebView2HostResourceAccessKind.Allow);

            string html = @"
            <!DOCTYPE html>
            <html>
            <head>
                <link rel='stylesheet' href='http://rover.local/leaflet.css'/>
                <script src='http://rover.local/leaflet.js'></script>
                <style>
                    html, body, #map { margin: 0; padding: 0; width: 100%; height: 100%; background: #222; }
                </style>
            </head>
            <body>
                <div id='map'></div>
                <script>
                    var map, marker, roverPath;
                    var hasCentered = false;
                    
                    //Initializing map
                    function initMap() {

                        //Setting position to Brownsville, TX. Zoom at lvl 15.
                        map = L.map('map').setView([25.9017, -97.4975], 15);

                        //Setting up Tile Files path. Indicating to the Software, that the Tiles will be in the local folder, not online.
                        L.tileLayer('http://rover.local/tiles/{z}/{x}/{y}.png', { maxZoom: 19 }).addTo(map);
                        
                        //Initializing the marker
                        marker = L.marker([0,0]).addTo(map);

                        //Initializing the Path with a lime color
                        roverPath = L.polyline([], {color:'lime', weight:3}).addTo(map);
                    }

                    function updateRover(lat, lon,usv) {

                        //Creating a Variable to hold the usv value
                        var usvNum = parseFloat(usv) || 0;

                        if(lat === 0) return;

                        //Create a variable holding the new Position to update
                        var newPos = L.latLng(lat, lon);

                        //Set the Marker to the new updated Position
                        marker.setLatLng(newPos);

                        //Add the new position to the rover path, thus creating a line
                        roverPath.addLatLng(newPos);

                        //Zooming in on the Robot position when starting.
                        if (!hasCentered) {
                         map.setView(newPos, 18);
                          hasCentered = true; 
                        }
                        
                        //Draw a Circle if radiation is high
                        if (usvNum > 0.20) {
                            var circleColor = 'yellow';
                            var circleRadius = 5; // meters

                            if (usvNum > 0.30) { 
                                circleColor = 'orange'; 
                                circleRadius = 10; 
                            }
                            if (usvNum > 0.50) { 
                                circleColor = 'red'; 
                                circleRadius = 15; 
                            }

                            //Create the Circle on the map
                            L.circle(newPos, {
                                color: circleColor,
                                fillColor: circleColor,
                                fillOpacity: 0.4,
                                radius: circleRadius,
                                weight: 1,
                                interactive: false
                            }).addTo(map).bringToBack(); // Put circles behind the marker/line
                        }

                        //Keep a thin, consistent path line
                        var points = roverPath.getLatLngs();

                        if(points.length > 0){
                            var lastPos = points[points.length - 1];
                            L.polyline([lastPos, newPos], {
                            color: '#00FF00', // Constant Lime Green
                            weight: 2,     
                            opacity: 0.7
                        }).addTo(map);
                        }

                        //Update marker
                        marker.setLatLng(newPos);
                        roverPath.addLatLng(newPos);
                    }
                    initMap();
                </script>
            </body>
            </html>";

            //Take all of the HTML String and inject it into the browser
            MapView.NavigateToString(html);

            //Set a flag to reset the code once the map is ready to receive data
            MapView.NavigationCompleted += (s, ev) => mapInitialized = true;
        }

        //Sending String commands to Raspberry Pi
        private void SendCommand(string command)
        {
            //Checknig if the cnnection exists
            if (motorStream != null && motorStream.CanWrite)
            {
                try
                {
                    //Send String command as ASCII Bytes followed by a New Line 
                    byte[] data = Encoding.ASCII.GetBytes(command + "\n");
                    motorStream.Write(data, 0, data.Length);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Send Error: {ex.Message}");
                }
            }
        }

        //Send Command Strings after the press of the corresponding Key
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            //If the key is being held, do not spam the signal.
            if (e.IsRepeat) return;

            switch (e.Key)
            {
                case Key.W: SendCommand("FORWARD"); break;
                case Key.S: SendCommand("BACKWARD"); break;
                case Key.A: SendCommand("LEFT"); break;
                case Key.D: SendCommand("RIGHT"); break;
            }
        }

        //Send the stopping command as soon as a Key is no longer being pressed
        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            SendCommand("STOP");
        }

        private async Task StartTelemetryListener()
        {
            try
            {
                //Creating new Network Connection
                telemetryClient = new TcpClient();

                //Creating the new connection directed to Specified Data Port
                await telemetryClient.ConnectAsync(RaspberryPiIP, Telemetryport);

                //Creating a reader that reads Network data line by line
                var reader = new StreamReader(telemetryClient.GetStream());

                //Setting Options for incoming data
                var options = new JsonSerializerOptions
                {
                    //Ignoring Case Sensitivity for data
                    PropertyNameCaseInsensitive = true,

                    //If numbers are strings, convert into respective data
                    NumberHandling = JsonNumberHandling.AllowReadingFromString
                };

                while (true)
                {
                    //Wait for a full line ending in \n and store it
                    string line = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(line)) continue;

                    // Parse nested JSON: {"gps": {...}, "rad": {...}}
                    var packet = JsonSerializer.Deserialize<TelemetryPacket>(line, options);

                    if (packet != null)
                    {
                        //Dispatcher will act as a bridge for the background threads to update UI
                        Dispatcher.Invoke(() => {

                            if (packet.Gps != null)
                            {
                                //Updating Telemetry Values
                                Telemetry.Latitude = packet.Gps.Latitude;
                                Telemetry.Longitude = packet.Gps.Longitude;
                                Telemetry.Altitude = packet.Gps.Altitude;

                                //Trigger UpdateMap Function
                                _ = UpdateMap(Telemetry.Latitude, Telemetry.Longitude, Telemetry.uSv);
                            }

                            if (packet.Rad != null)
                            {
                                //Updating Telemetry Values
                                Telemetry.CPM = packet.Rad.Cpm;
                                Telemetry.TotalClicks = packet.Rad.Total;
                                Telemetry.uSv = packet.Rad.uSv;
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Telemetry Error: {ex.Message}");
                await Task.Delay(3000); // Reconnection delay
                _ = StartTelemetryListener();
            }
        }

        //Updating the map
        private async Task UpdateMap(double lat, double lon, double usv = 0)
        {
            //Do not start if map is not initialized
            if (!mapInitialized) return;
            
           
            /*
             ~~ Building a line of JavaScript as a string ~~
            For the bits containing Culture info, here's a brief explanation.
            In some countries with different language settings, 
            commas are used instead of periods.
            This will make it viable to run the program regardless of language setting.
             */
            string js =
        $"updateRover(" +
        $"{lat.ToString(CultureInfo.InvariantCulture)}, " +
        $"{lon.ToString(CultureInfo.InvariantCulture)}, " +
        $"{Telemetry.uSv.ToString(CultureInfo.InvariantCulture)});";
            //Send the String to the Browser
            await MapView.ExecuteScriptAsync(js);
        }
    }

    // JSON Mapping Classes
    //These classes define the structure of the Data Packets
    public class TelemetryPacket
    {
        [JsonPropertyName("gps")] public GpsData Gps { get; set; }
        [JsonPropertyName("rad")] public RadiationData Rad { get; set; }
    }

    public class GpsData
    {
        [JsonPropertyName("lat")] public double Latitude { get; set; }
        [JsonPropertyName("lon")] public double Longitude { get; set; }
        [JsonPropertyName("alt")] public double Altitude { get; set; }
    }

    public class RadiationData
    {
        [JsonPropertyName("total")] public int Total { get; set; }
        [JsonPropertyName("cpm")] public double Cpm { get; set; }
        [JsonPropertyName("usv")] public double uSv { get; set; }
    } 


    // View Model for UI Binding
    //Using INotifyPropertyChanged let's a class let the UI a value changed.
    public class TelemetryData : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private double lat, lon, alt, cpm, usv;
        private int total;
        private string safetyStatus = "UNKNOWN";
        private Brush safetyColor = Brushes.LightGray;

        public double Latitude 
        {
            get => lat;
            set
            { 
                lat = value;
                OnChanged(nameof(Latitude)); 
            } 
        }
        public double Longitude 
        { 
            get => lon;
            set 
            {
                lon = value;
                OnChanged(nameof(Longitude));
            }
        }
        public double Altitude 
        {
            get => alt;
            set 
            {
                alt = value;
                OnChanged(nameof(Altitude));
            }
        }
        public double CPM
        {
            get => cpm;
            set
            {
                cpm = value;
                OnChanged(nameof(CPM));
            }
        }
        public double uSv
        {
            get => usv;
            set
            {
                usv = value;
                UpdateSafety(value);   //auto-update safety when radiation changes
                OnChanged(nameof(uSv));
            }
        }

        public int TotalClicks 
        {
            get => total;
            set
            {
                total = value;
                OnChanged(nameof(TotalClicks));
            }
        }

        public string SafetyStatus
        {
            get => safetyStatus;
            set 
            { 
                safetyStatus = value;
                OnChanged(nameof(SafetyStatus));
            }
        }

        public Brush SafetyColor
        {
            get => safetyColor;
            set { safetyColor = value; OnChanged(nameof(SafetyColor)); }
        }

        //Function to update Safety label
        private void UpdateSafety(double usv)
        {
            if (usv < 0.15)
            {
                SafetyStatus = "SAFE";
                SafetyColor = Brushes.LimeGreen;
            }
            else if (usv < 0.30)
            {
                SafetyStatus = "CAUTION";
                SafetyColor = Brushes.Yellow;
            }
            else
            {
                SafetyStatus = "UNSAFE";
                SafetyColor = Brushes.Red;
            }
        }

        //The OnChanged Property will tell the UI to Refresh if a value is Updated.
        private void OnChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}