using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Timers;
using System.Net.Http;
using System.Linq;
using System.Threading;
using Windows.Devices.Bluetooth.Advertisement;
using Beacons;
using Newtonsoft.Json;
using NDesk.Options;

namespace tilt_telephone
{
    class TiltSetting
    {
        public string uuid;
        public string color;
        public string name;
        public List<TiltCaliSetting> tempCali;
        public List<TiltCaliSetting> sgCali;
        public string loggingURL;
        public int connectionTimeout;
    }
    class TiltCaliSetting
    {
        public float precal;
        public float corrected;
    }
    class Program
    {
        static private List<TiltSetting> tilts;
        static private System.Timers.Timer timer;
        static private bool mainThreadWait;
        static private iBeaconData curTilt;
        static private readonly HttpClient client = new HttpClient();

        static void Main(string[] args)
        {
            float repeat = 0.0f;
            var p = new OptionSet() {
                { "r|repeat=", "The number of mintutes to wait before logging again.",
                   (float v) => repeat = v },
            };
            List<string> extra;
            try
            {
                extra = p.Parse(args);

                do {
                    var settingsFile = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName) + @"\tilt-telephone.json";
                    var json = File.ReadAllText(settingsFile);
                    tilts = JsonConvert.DeserializeObject<List<TiltSetting>>(json);
                    foreach (TiltSetting tilt in tilts)
                    {
                        var watcher = new BluetoothLEAdvertisementWatcher();
                        watcher.Received += OnAdvertisementRecieved;
                        watcher.AdvertisementFilter.Advertisement.iBeaconSetAdvertisement(new iBeaconData() {
                            UUID = Guid.Parse(tilt.uuid)
                        });

                        Console.WriteLine("{1}: Looking for a {0} tilt", tilt.color, DateTime.Now.ToString());
                        timer = new System.Timers.Timer(tilt.connectionTimeout);
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
                            Console.WriteLine("{0}:      Precal: {1} {2}", DateTime.Now.ToString(), curTilt.Minor/ 1000.00f, curTilt.Major);
                            float gravity = GetCalibrated(curTilt.Minor / 1000.00f, tilt.sgCali);
                            float temp = GetCalibrated(curTilt.Major, tilt.tempCali);
                            mainThreadWait = true;
                            LogToCloud(tilt, temp, gravity);
                            while (mainThreadWait)
                            {
                            }
                            continue;
                        }

                        Console.WriteLine("{1}: No {0} tilt found.", tilt.color, DateTime.Now.ToString());
                    }
                    int timeout = (int)(Math.Round(repeat * 60 * 1000));
                    Thread.Sleep(timeout);
                } while (repeat != 0.0f);
            }
            catch (OptionException e)
            {
                Console.WriteLine(e.Message);
                return;
            }
        }

        private static float GetCalibrated(float input, List<TiltCaliSetting> cali)
        {
            if (cali.Count == 0)
                return input;
            else if (cali.Count == 1)
                return input + (cali[0].corrected - cali[0].precal);
            else
            {
                List<TiltCaliSetting> orderedCali = cali.OrderBy(obj => obj.precal).ToList();
                for (var x = 0; x <= (orderedCali.Count - 1); x += 1)
                {
                    var lower = orderedCali[x];
                    var upper = orderedCali[x + 1];
                    Console.WriteLine("Lower precal: {0}", lower.precal);
                    Console.WriteLine("Upper precal: {0}", upper.precal);

                    var inMin = lower.precal;
                    var inMax = upper.precal;
                    var outMin = lower.corrected - lower.precal;
                    var outMax = upper.corrected - upper.precal;
                    var correction = (input - inMin) / (inMax - inMin) * (outMax - outMin) + outMin;

                    // if these "ifs" aren't met, then try the next pair by doing nothing
                    if (input <= lower.precal)
                        return input + correction;
                    else if (input >= lower.precal && input <= upper.precal)
                        return input + correction;
                    else if (x + 1 == orderedCali.Count)
                        return input + correction;
                }
            }
            return input;
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