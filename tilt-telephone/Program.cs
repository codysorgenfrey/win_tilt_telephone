using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Timers;
using System.Net.Http;
using Windows.Devices.Bluetooth.Advertisement;
using Beacons;
using Newtonsoft.Json;

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
        public int connectionTimeout;
    }
    class Program
    {
        static private List<TiltSetting> tilts;
        static private Timer timer;
        static private bool mainThreadWait;
        static private iBeaconData curTilt;
        static private readonly HttpClient client = new HttpClient();

        static void Main(string[] args)
        {
            var settingsFile = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName) + @"\tilt-telephone.json";
            var json = File.ReadAllText(settingsFile);
            tilts = JsonConvert.DeserializeObject<List<TiltSetting>>(json);
            foreach (TiltSetting tilt in tilts)
            {
                var watcher = new BluetoothLEAdvertisementWatcher();
                watcher.Received += OnAdvertisementRecieved;
                watcher.AdvertisementFilter.Advertisement.iBeaconSetAdvertisement(new iBeaconData(){
                    UUID = Guid.Parse(tilt.uuid)
                });

                Console.WriteLine("{1}: Looking for a {0} tilt", tilt.color, DateTime.Now.ToString());
                timer = new Timer(tilt.connectionTimeout);
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

                if (curTilt != null)
                {
                    Console.WriteLine("{1}:    Found a {0} tilt.", tilt.color, DateTime.Now.ToString());
                    float gravity = (curTilt.Minor / 1000.00f) + tilt.sgCali;
                    float temp = curTilt.Major + tilt.tempCali;
                    LogToCloud(tilt, temp, gravity);
                    mainThreadWait = true;
                    while (mainThreadWait)
                    {
                    }
                    continue;
                }

                Console.WriteLine("{1}: No {0} tilt found.", tilt.color, DateTime.Now.ToString());
            }
        }

        private static async void LogToCloud(TiltSetting tilt, float temp, float gravity)
        {
            Console.WriteLine("{0}:    Logging to cloud...", DateTime.Now.ToString());
            Console.WriteLine("{1}:        Gravity: {0}", gravity, DateTime.Now.ToString());
            Console.WriteLine("{1}:        Temp: {0}", temp, DateTime.Now.ToString());
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
            Console.WriteLine("{1}:    {0}", responseString, DateTime.Now.ToString());
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