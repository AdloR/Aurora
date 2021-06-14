using Aurora.Settings;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Aurora.Devices.WledWarls {
    public class WledWarls : DefaultDevice {
        public override string DeviceName => "WLED";
        private List<IPAddress> Addresses = new List<IPAddress>();
        private Stopwatch UpdateDelayStopWatch = new Stopwatch();
        private HttpClient httpClient = new HttpClient();

        private Color LastUpdateC;

        public override bool Initialize() {
            if (!IsInitialized) {
                try {
                    var IPListString = Global.Configuration.VarRegistry.GetVariable<string>($"{DeviceName}_IP");

                    String[] IPStringList = IPListString.Split(new[] { ',' });

                    foreach (String s in IPStringList) {
                        s.Replace(" ", "");
                        IPAddress address = IPAddress.Parse(s);
                        Addresses.Add(address);
                    }

                    if (Addresses.Count == 0) {
                        IsInitialized = false;
                        throw new Exception("Device IP list is empty.");
                    }

                    foreach (IPAddress addr in Addresses) {
                        Task task = sendRequest(httpClient, addr, $"TT={Global.Configuration.VarRegistry.GetVariable<int>($"{DeviceName}_TransiAurora")}");
                    }

                    IsInitialized = true;
                    return true;
                } catch (Exception exc) {
                    LogError($"Encountered an error while initializing. Exception: {exc}");
                    IsInitialized = false;
                    return false;
                }
            }
            return true;
        }

        public override void Shutdown() {
            if (IsInitialized) { 
                foreach(IPAddress addr in Addresses) {
                    Task task = sendRequest(httpClient, addr, $"TT={Global.Configuration.VarRegistry.GetVariable<int>($"{DeviceName}_TransiDef")}");
                }
                IsInitialized = false;
            }
        }

        public override bool UpdateDevice(Dictionary<DeviceKeys, Color> keyColors, DoWorkEventArgs e, bool forced = false) {
            // Reduce sending based on user config
            if (!UpdateDelayStopWatch.IsRunning) {
                UpdateDelayStopWatch.Start();
            }

            if (UpdateDelayStopWatch.ElapsedMilliseconds <= Global.Configuration.VarRegistry.GetVariable<int>($"{DeviceName}_send_delay"))
                return false;

            var targetKey = Global.Configuration.VarRegistry.GetVariable<DeviceKeys>($"{DeviceName}_devicekey");
            if (!keyColors.TryGetValue(targetKey, out var targetColor))
                return false;

            if (targetColor.Equals(LastUpdateC)) {
                return false;
            }
            LastUpdateC = targetColor;

            //addr.ToString() + $"/win&FX=0&R={targetColor.R}&G={targetColor.G}&B={targetColor.G}"
            foreach (IPAddress addr in Addresses) {
                Task task = sendRequest(httpClient, addr, targetColor.R, targetColor.G, targetColor.B);
                try {
                    task.Wait();
                } catch (Exception exc) {
                    LogError($"Encountered an error while drawing. Exception: {exc}");
                }
            }
            UpdateDelayStopWatch.Restart();

            return true;
        }

        static async Task sendRequest(HttpClient client, IPAddress address, byte R, byte G, byte B){
            HttpResponseMessage message = await client.GetAsync("http://" + address.ToString() + $"/win&FX=0&R={R}&G={G}&B={B}");
        }
        static async Task sendRequest(HttpClient client, IPAddress address, string requestEnd) {
            HttpResponseMessage message = await client.GetAsync("http://" + address.ToString() + $"/win&{requestEnd}");
        }

        protected override void RegisterVariables(VariableRegistry variableRegistry) {
            var devKeysEnumAsEnumerable = Enum.GetValues(typeof(DeviceKeys)).Cast<DeviceKeys>();

            variableRegistry.Register($"{DeviceName}_devicekey", DeviceKeys.Peripheral_Logo, "Key to Use", devKeysEnumAsEnumerable.Max(), devKeysEnumAsEnumerable.Min());
            variableRegistry.Register($"{DeviceName}_send_delay", 17, "Send delay (ms)");
            variableRegistry.Register($"{DeviceName}_timeout_delay", 1, "Timeout (s)", null, null, "(1-254, 255 for no timeout)");
            variableRegistry.Register($"{DeviceName}_IP", "", "WLED IP(s)", null, null, "Comma separated IPv4 or IPv6 addresses");
            variableRegistry.Register($"{DeviceName}_TransiAurora", 50, "Controlled transition time", null, null, "Transition time while WLED is controlled by Aurora");
            variableRegistry.Register($"{DeviceName}_TransiDef", 300, "Default transition time", null, null, "Transition time after this module is stopped");
        }
    }
}
