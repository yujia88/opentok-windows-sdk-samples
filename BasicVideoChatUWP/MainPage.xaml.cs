using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Xml;
using System.Xml.Serialization;
using Windows.System;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using OpenTok;
using Windows.Media.Capture;
using Windows.Devices.Enumeration;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace BasicVideoChatUWP
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private static string API_KEY = "45995012";
        private static string SESSION_ID = "1_MX40NTk5NTAxMn5-MTY3NjUzNjIxMDEyNX5Ma0ZZZktyTzFLZVlyWVMxaXI1c2pNQTJ-fn4";
        private static string TOKEN = "T1==cGFydG5lcl9pZD00NTk5NTAxMiZzaWc9YjcwMjYwOTViYjE3NTk5MTQ0ZTA1OTA4NTg5NmNjZTA1NjU1ODIyODpub25jZT01MDA2MjkmY29ubmVjdGlvbl9kYXRhPSU3QiUyMnBhcnRpY2lwYW50X2lkJTIyJTNBJTIyNTYyMzglM0E0ZTczOThjYy04MjgyLTQ0MGYtYTgzNS05NTdjMTNiMmY1MzMlMjIlN0QmY3JlYXRlX3RpbWU9MTY3NjUzNjIxMSZyb2xlPXB1Ymxpc2hlciZleHBpcmVfdGltZT0xNjc2NjIyNjExJnNlc3Npb25faWQ9MV9NWDQwTlRrNU5UQXhNbjUtTVRZM05qVXpOakl4TURFeU5YNU1hMFpaWmt0eVR6RkxaVmx5V1ZNeGFYSTFjMnBOUVRKLWZuNA==";

        private readonly ConcurrentDictionary<Stream, Subscriber> subscriberByStream = new ConcurrentDictionary<Stream, Subscriber>();
        private readonly Context context;
        private Session session;
        private Publisher publisher;
        private bool isConnected = false;

        public MainPage()
        {
            InitializeComponent();

            context = new Context(new UWPDispatcher(this));

            Loaded += MainPage_Loaded;
            Unloaded += MainPage_Unloaded;
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(API_KEY) || string.IsNullOrWhiteSpace(SESSION_ID))
            {
                throw new Exception("ApiKey, SessionId and Token parameters must be provided inside .config file");
            }
            session = new Session.Builder(context, API_KEY, SESSION_ID).Build();

            session.Connected += Session_Connected;
            session.Disconnected += Session_Disconnected;
            session.Error += Session_Error;
            session.ConnectionCreated += Session_ConnectionCreated;
            session.StreamReceived += Session_StreamReceived;
            session.StreamDropped += Session_StreamDropped;

            InitAsync();
        }

        private void MainPage_Unloaded(object sender, RoutedEventArgs e)
        {
            foreach (Subscriber subscriber in subscriberByStream.Values)
            {
                subscriber.Dispose();
            }
            if (publisher != null)
            {
                publisher.VideoCapturer.Destroy();
                publisher.Dispose();
                session?.Dispose();
            }
        }

        private async void InitAsync()
        {
            _ = await Launcher.LaunchUriAsync(new Uri("ms-settings:appsfeatures-app"));
            MessageDialog requestPermissionDialog = new MessageDialog("Before proceeding, verify Cam and Mic permissions are granted to the application and then click OK to continue");
            UICommand okCommand = new UICommand("OK");
            requestPermissionDialog.Commands.Add(okCommand);
            requestPermissionDialog.DefaultCommandIndex = 0;
            _ = await requestPermissionDialog.ShowAsync();

            //ConfigureAudioDevice();
            RebuildPublisher();
        }

        private void Session_ConnectionCreated(object sender, Session.ConnectionEventArgs e)
        {
            Console.WriteLine("Session connection created:" + e.Connection.Id);
        }

        private void Session_Error(object sender, Session.ErrorEventArgs e)
        {
            Console.WriteLine("Session error:" + e.ErrorCode);
        }

        private void Session_Disconnected(object sender, EventArgs e)
        {
            Console.WriteLine("Session disconnected");
            subscriberByStream.Clear();

            subscriberGrid.Children.Clear();
        }

        private void Session_Connected(object sender, EventArgs e)
        {
            Console.WriteLine("Session connected connection id:" + session.Connection.Id);
            try
            {
                session.Publish(publisher);
            }
            catch (OpenTokException ex)
            {
                Console.WriteLine("OpenTokException " + ex.ToString());
            }
        }

        private void Session_StreamReceived(object sender, Session.StreamEventArgs e)
        {
            Console.WriteLine("Session stream received");
            Subscriber subscriber = new Subscriber.Builder(context, e.Stream)
            {
                //Renderer = renderer
                Renderer = subscriberVideo
            }.Build();
            subscriber.SubscribeToAudio = false;
            _ = subscriberByStream.TryAdd(e.Stream, subscriber);

            try
            {
                session.Subscribe(subscriber);
            }
            catch (OpenTokException ex)
            {
                Console.WriteLine("OpenTokException " + ex.ToString());
            }
        }

        private void Session_StreamDropped(object sender, Session.StreamEventArgs e)
        {
            Console.WriteLine("Session stream dropped");
            Subscriber subscriber = subscriberByStream[e.Stream];
            if (subscriber != null)
            {
                _ = subscriberByStream.TryRemove(e.Stream, out _);

                try
                {
                    session.Unsubscribe(subscriber);
                }
                catch (OpenTokException ex)
                {
                    Console.WriteLine("OpenTokException " + ex.ToString());
                }
            }

        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (isConnected)
            {
                Console.WriteLine("Disconnecting session");

                try
                {
                    session.Unpublish(publisher);
                    session.Disconnect();
                }
                catch (OpenTokException ex)
                {
                    Console.WriteLine("OpenTokException " + ex.ToString());
                }
            }
            else
            {
                Console.WriteLine("Connecting session");
                try
                {
                    if (string.IsNullOrWhiteSpace(TOKEN))
                    {
                        throw new Exception("ApiKey, SessionId and Token parameters must be provided inside .config file");
                    }
                    session.Connect(TOKEN);
                }
                catch (OpenTokException ex)
                {
                    Console.WriteLine("OpenTokException " + ex.ToString());
                }
            }
            isConnected = !isConnected;
            ConnectButton.Content = isConnected ? "Disconnect" : "Connect";
        }

        private async void RebuildPublisher()
        {
            if (publisher != null)
            {
                publisher.Dispose();
            }

            try
            {
                //IReadOnlyList<UWPVideoCapturer.Device> availableCameras = await UWPVideoCapturer.EnumerateDevicesAsync();
                //if (availableCameras == null || availableCameras.Count == 0)
                //{
                //    throw new Exception("No cameras detected");
                //}

                //UWPVideoCapturer.Device currentCamera = availableCameras[0];

                var devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
                IReadOnlyList<MediaCaptureVideoProfile> profiles = MediaCapture.FindAllVideoProfiles(devices[0].Id);

                UWPVideoCapturer capturer = await UWPVideoCapturer.CreateAsync(devices[0].Id, "", 1280, 720, 30);
                publisher = new Publisher.Builder(context)
                {
                    Renderer = publisherVideo,
                    Capturer = capturer
                }.Build();
                publisher.PublishAudio = false;
            }
            catch (OpenTokException ex)
            {
                Console.WriteLine("OpenTokException " + ex.ToString());
            }

            if (isConnected)
            {
                session.Publish(publisher);
            }
        }

        private void ConfigureAudioDevice()
        {
            IList<AudioDevice.InputAudioDevice> availableMics = AudioDevice.EnumerateInputAudioDevices();
            if (availableMics == null || availableMics.Count == 0)
            {
                Console.WriteLine("No mics detected");
                return;
            }
            AudioDevice.SetInputAudioDevice(availableMics[0]);
        }
    }
}