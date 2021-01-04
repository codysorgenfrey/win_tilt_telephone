using System;
using Windows.Devices.Bluetooth.Advertisement;
using Beacons;
using System.IO;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Timers;
using System.Net.Http;

namespace tilt_telephone
{
    class TiltSetting
    {
        public string uuid;
        public string color;
        public string name;
        public float tempCali;
        public float sgCali;
        public string loggingURL;
    }
    class Program
    {
        static private List<TiltSetting> tilts;
        static private Timer timer;
        static private int bluetoothTimeout = 10000;
        static private bool mainThreadWait;
        static private iBeaconData curTilt;
        static private readonly HttpClient client = new HttpClient();

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
                timer = new Timer(bluetoothTimeout);
                timer.Elapsed += OnTimerElapsed;
                mainThreadWait = true;
                curTilt = null;

                timer.Start();
                watcher.Start();
                while (mainThreadWait)
                {
                }
                watcher.Stop();
                timer.Stop();

                Console.Write("\n");
                if (curTilt != null)
                {
                    Console.WriteLine("    Found a {0} tilt.", tilt.color);
                    float gravity = (curTilt.Minor / 1000.00f) + tilt.sgCali;
                    float temp = curTilt.Major + tilt.tempCali;
                    LogToCloud(tilt, temp, gravity);
                    mainThreadWait = true;
                    while (mainThreadWait)
                    {
                    }
                    continue;
                }

                Console.WriteLine("No {0} tilt found.", tilt.color);
            }
        }

        private static async void LogToCloud(TiltSetting tilt, float temp, float gravity)
        {
            Console.WriteLine("    Logging to cloud...");
            Console.WriteLine("        Gravity: {0}", gravity);
            Console.WriteLine("        Temp: {0}", temp);
            var values = new Dictionary<string, string>
                    {
                        { "Beer", tilt.name },
                        { "Temp", temp.ToString() },
                        { "SG", gravity.ToString() },
                        { "Color", tilt.color.ToUpper() },
                        { "Comment", "" },
                        { "Timepoint", DateTime.Now.ToString() }
                    };
            var content = new FormUrlEncodedContent(values);
            var response = await client.PostAsync(tilt.loggingURL, content);
            var responseString = await response.Content.ReadAsStringAsync();
            Console.WriteLine("    {0}", responseString);
            mainThreadWait = false;
        }

        private static void OnTimerElapsed(Object source, ElapsedEventArgs e)
        {
            mainThreadWait = false;
        }

        private static void OnAdvertisementRecieved(BluetoothLEAdvertisementWatcher watcher, BluetoothLEAdvertisementReceivedEventArgs eventArgs)
        {
            var beaconData = eventArgs.Advertisement.iBeaconParseAdvertisement(eventArgs.RawSignalStrengthInDBm);

            if (beaconData == null)
                return;

            mainThreadWait = false;
            curTilt = beaconData; 
        }
    }
}