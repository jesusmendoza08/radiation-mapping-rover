using Microsoft.Web.WebView2.Core;
using System;
using System.Data;
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
    /// The logic behind this page makes sure to connect
    /// the Raspberry Pi and the camera
    /// to the Application that
    /// will be used to control the Rover.
    /// </summary>
    public partial class MainWindow : Window
    {
        private TcpClient client;
        private NetworkStream stream;

        //IMPORTANT! Below is the IP Address for the Pi
        private string RaspberryPiTP = "192.168.1.15"; 
        private int port = 5000;

        public MainWindow()
        {
            InitializeComponent();
            LoadCameraFeed();

        }

        // The function below makes sure to check if the Raspberry Pi is connected and the Camera is on.
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                client = new TcpClient();
                await client.ConnectAsync(RaspberryPiTP, port);
                stream = client.GetStream();
                LoadCameraFeed();

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

    // Whenever one of the WASD Keys is pressed, there will be a Command sent to the Arduino through ASCII Code
        private void SendCommand(string command)
        {
            if(stream == null)
            {
                return;
            }
            byte[] data = Encoding.ASCII.GetBytes(command + "\n");
            stream.Write(data, 0, data.Length);

        }
    //The code below is used to send these commands once the Keys for movement are pressed
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

        private async void LoadCameraFeed()
        {
            await CameraView.EnsureCoreWebView2Async();
            CameraView.Source = new Uri("http://192.168.1.15:5000/video_feed");
        }




        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            SendCommand("STOP");
        }
    }
}