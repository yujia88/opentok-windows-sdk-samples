﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using OpenTok;

namespace BasicVideoChat
{
    public partial class MainWindow : Window
    {
        private const string API_KEY = "47446341";
        private const string SESSION_ID = "1_MX40NzQ0NjM0MX5-MTY2NTM5NjI0MTg2Mn5LUUg3VFRYMkNYQW5sbk84cFhPM21UMlB-fg";
        private const string TOKEN = "T1==cGFydG5lcl9pZD00NzQ0NjM0MSZzaWc9MzcyYTQ2ZWFkZjdiNDJlOGI2NjU3OGNkZTViZjcxOWViMDM5YzFmMzpzZXNzaW9uX2lkPTFfTVg0ME56UTBOak0wTVg1LU1UWTJOVE01TmpJME1UZzJNbjVMVVVnM1ZGUllNa05ZUVc1c2JrODRjRmhQTTIxVU1sQi1mZyZjcmVhdGVfdGltZT0xNjY1Mzk2MjYzJm5vbmNlPTAuODU5OTA5Nzk0MDUwODg4MiZyb2xlPXB1Ymxpc2hlciZleHBpcmVfdGltZT0xNjY3OTkxODYyJmluaXRpYWxfbGF5b3V0X2NsYXNzX2xpc3Q9";

        private Context context;
        private Session Session;
        private Publisher Publisher;

        public MainWindow()
        {
            InitializeComponent();

            context = new Context(new WPFDispatcher());

            // Uncomment following line to get debug logging
            // LogUtil.Instance.EnableLogging();

            IList<AudioDevice.InputAudioDevice> availableMics = AudioDevice.EnumerateInputAudioDevices();
            if (availableMics == null || availableMics.Count == 0)
                throw new Exception("No audio capture devices detected");
            AudioDevice.SetInputAudioDevice(availableMics[0]);

            IList<VideoCapturer.VideoDevice> capturerDevices = VideoCapturer.EnumerateDevices();
            if (capturerDevices == null || capturerDevices.Count == 0)
                throw new Exception("No video capture devices detected");

            Publisher = new Publisher.Builder(context)
            {
                Capturer = capturerDevices[0].CreateVideoCapturer(VideoCapturer.Resolution.High, VideoCapturer.FrameRate.Fps30),
                Renderer = PublisherVideo
            }.Build();

            Session = new Session.Builder(context, API_KEY, SESSION_ID).Build();
            Session.Connected += Session_Connected;
            Session.Disconnected += Session_Disconnected;
            Session.Error += Session_Error;
            Session.StreamReceived += Session_StreamReceived;
            Session.Connect(TOKEN);
        }

        private void Session_Connected(object sender, System.EventArgs e)
        {
            Session.Publish(Publisher);
        }
 
        private void Session_Disconnected(object sender, System.EventArgs e)
        {
            Trace.WriteLine("Session disconnected.");
        }

        private void Session_Error(object sender, Session.ErrorEventArgs e)
        {
            Trace.WriteLine("Session error:" + e.ErrorCode);
        }

        private void Session_StreamReceived(object sender, Session.StreamEventArgs e)
        {
            Subscriber subscriber = new Subscriber.Builder(context, e.Stream)
            {
                Renderer = SubscriberVideo
            }.Build();
            Session.Subscribe(subscriber);
        }
    }
}
