using System;
using Windows.Devices.Bluetooth.Advertisement;
using Beacons;
using System.IO;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Timers;

namespace tilt_telephone
{
    class TiltSetting
    {
        public string uuid;
        public string color;
        public float tempCali;
        public float sgCali;
        public string logginURL;
    }
    class Program
    {
        static private List<TiltSetting> tilts;
        static private Timer timer;
        static private int timeout = 10000;
        static private bool watching;
        static private iBeaconData curTilt;

        static void Main(string[] args)
        {
            var json = File.ReadAllText("tilt-telephone.json");
            tilts = JsonConvert.DeserializeObject<List<TiltSetting>>(json);
            foreach (TiltSetting tilt in tilts)
            {
                var watcher = new BluetoothLEAdvertisementWatcher();
                watcher.Received += OnAdvertisementRecieved;
                watcher.AdvertisementFilter.Advertisement.iBeaconSetAdvertisement(new iBeaconData(){
                    UUID = Guid.Parse(tilt.uuid)
                });

                Console.Write("Looking for a {0} tilt", tilt.color);
                timer = new Timer(timeout);
                timer.Elapsed += OnTimerElapsed;
                watching = true;
                curTilt = null;

                timer.Start();
                watcher.Start();
                while (watching)
                {       
                }
                watcher.Stop();
                timer.Stop();

                Console.Write("\n");
                if (curTilt != null)
                {
                    float gravity = (curTilt.Minor / 1000.00f) + tilt.sgCali;
                    float temp = curTilt.Major + tilt.tempCali;
                    Console.WriteLine("    Found a {0} tilt.", tilt.color);
                    Console.WriteLine("    Logging to cloud...");
                    Console.WriteLine("        Gravity: {0}", gravity);
                    Console.WriteLine("        Temp: {0}", temp);
                }
                   
            }
        }

        private static void OnTimerElapsed(Object source, ElapsedEventArgs e)
        {
            watching = false;
        }

        private static void OnAdvertisementRecieved(BluetoothLEAdvertisementWatcher watcher, BluetoothLEAdvertisementReceivedEventArgs eventArgs)
        {
            var beaconData = eventArgs.Advertisement.iBeaconParseAdvertisement(eventArgs.RawSignalStrengthInDBm);

            if (beaconData == null)
                return;

            watching = false;
            curTilt = beaconData;
        }
    }
}