using Aurora;
using Aurora.Devices;
using Aurora.Settings;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.IO.Ports;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using UsbHid.USB.Classes;
using UsbHid;

namespace Aurora.Devices.SDVX {
    public class SdvxController : DefaultDevice {
        public override string DeviceName => "SDVX Controller";
        public bool enabled = true;
        public List<KeyValuePair<string,UsbDescriptorStrings>> UsbSDVX; 

        private Color device_color = Color.Black;

        private static SerialPort sp = new SerialPort();
        private const int LedStates = 8;
        static byte[] LED = new byte[LedStates * 6];
        private static Thread send = new Thread(SendSerial);
        Stopwatch sw = new Stopwatch();

        public override bool Initialize() {
            if (!IsInitialized) {
                try {
                    //Perform necessary actions to initialize your device
                    string port = Global.Configuration.VarRegistry.GetVariable<string>($"{DeviceName}_port");
                    if (!SerialPort.GetPortNames().Contains(port)) {
                        LogError(string.Format("Port Name \"{0}\" is not valid, please use one of the following ports : {1}", port, string.Join(";", SerialPort.GetPortNames())));
                        return false;
                    }
                    //Global.logger.Debug("SDVX ports :" + string.Join(";", SerialPort.GetPortNames()));
                    //sp.Close();
                    //sp.PortName = port;
                    //sp.BaudRate = Global.Configuration.VarRegistry.GetVariable<int>($"{DeviceName}_baud");
                    //sp.Parity = Parity.None;
                    //sp.DataBits = 8;
                    //sp.StopBits = StopBits.One;
                    //sp.Handshake = Handshake.None;
                    //int timeout = Global.Configuration.VarRegistry.GetVariable<int>($"{DeviceName}_timeout");
                    //sp.ReadTimeout = timeout;
                    //sp.WriteTimeout = timeout;

                    //sp.Open();
                    UsbSDVX = DeviceDiscovery.FindHidDevices(new VidPidMatcher(0x16C0, 0x0482));
                    IsInitialized = true;
                    return true;
                } catch (Exception exc) {
                    IsInitialized = false;
                    LogError(exc.Message);
                    return false;
                }
            }
            return true;
        }

        public override void Reset() {
            //Perform necessary actions to reset your device
        }

        public override void Shutdown() {
            //Perform necessary actions to shutdown your device
            sp.Close();
            IsInitialized = false;
        }

        public override bool UpdateDevice(Dictionary<DeviceKeys, Color> keyColors, DoWorkEventArgs e, bool forced = false) {
            try {
                if (!sw.IsRunning) {
                    sw.Start();
                } else {
                    if (sw.ElapsedMilliseconds <= 50) {
                        return false;
                    }
                    sw.Restart();
                }
                if (send.ThreadState==System.Threading.ThreadState.Running) {
                    return false;
                }

                TryCreate(keyColors, Global.Configuration.VarRegistry.GetVariable<DeviceKeys>($"{DeviceName}_farleft"), 0);
                TryCreate(keyColors, Global.Configuration.VarRegistry.GetVariable<DeviceKeys>($"{DeviceName}_midleft"), 1);
                TryCreate(keyColors, Global.Configuration.VarRegistry.GetVariable<DeviceKeys>($"{DeviceName}_midright"), 2);
                TryCreate(keyColors, Global.Configuration.VarRegistry.GetVariable<DeviceKeys>($"{DeviceName}_farright"), 3);
                TryCreate(keyColors, Global.Configuration.VarRegistry.GetVariable<DeviceKeys>($"{DeviceName}_botleft"), 4);
                TryCreate(keyColors, Global.Configuration.VarRegistry.GetVariable<DeviceKeys>($"{DeviceName}_botright"), 5);
                LED[0] = (byte)(LED[0] | 0b10000000);
                try {
                    send = new Thread(SendSerial);
                    send.Start();
                } catch ( ThreadStateException exc){
                    LogError($"Got {exc}");
                }
                

                return true;
            } catch (Exception exc) {
                return false;
            }
        }

        private void TryCreate(Dictionary<DeviceKeys, Color> keyColors, DeviceKeys targetKey, int led) {
            if(keyColors.TryGetValue(targetKey, out Color targetCol)) {
                createRGB(led,  Math.Sqrt((float)targetCol.R)/16, Math.Sqrt((float)targetCol.G) / 16,  Math.Sqrt((float) targetCol.B) / 16);
            }
        }

        private void createRGB(int led, double r, double g, double b) {
            r *= LedStates;
            g *= LedStates;
            b *= LedStates;
            for(int state = 0; state < LedStates; state++) {
                LED[6 * state + led] = (byte) ((state >= r) ? 0b100 : 0);
                LED[6 * state + led] = (byte) (LED[6 * state + led] | (state >= g ? 0b010 : 0));
                LED[6 * state + led] = (byte) (LED[6 * state + led] | (state >= b ? 0b001 : 0));
            }
        }

        public static void SendSerial() {
            Global.logger.Debug("starting writing Leds");
            try
            {
                if (!sp.IsOpen) return;
                sp.Write(LED, 0, 6 * LedStates);
                Global.logger.Debug("finished writing Leds");
            }catch(Exception exc) {
                Global.logger.Error($"SDVX : {exc}");
            }
        }

        protected override void RegisterVariables(VariableRegistry variableRegistry) {
            var devKeysEnumAsEnumerable = Enum.GetValues(typeof(DeviceKeys)).Cast<DeviceKeys>();

            variableRegistry.Register($"{DeviceName}_farleft", DeviceKeys.D, "Most left key", devKeysEnumAsEnumerable.Max(), devKeysEnumAsEnumerable.Min());
            variableRegistry.Register($"{DeviceName}_midleft", DeviceKeys.F, "Middle left key", devKeysEnumAsEnumerable.Max(), devKeysEnumAsEnumerable.Min());
            variableRegistry.Register($"{DeviceName}_midright", DeviceKeys.J, "Middle right key", devKeysEnumAsEnumerable.Max(), devKeysEnumAsEnumerable.Min());
            variableRegistry.Register($"{DeviceName}_farright", DeviceKeys.K, "Most right key", devKeysEnumAsEnumerable.Max(), devKeysEnumAsEnumerable.Min());
            variableRegistry.Register($"{DeviceName}_botleft", DeviceKeys.V, "Bottom left key", devKeysEnumAsEnumerable.Max(), devKeysEnumAsEnumerable.Min());
            variableRegistry.Register($"{DeviceName}_botright", DeviceKeys.N, "Bottom right key", devKeysEnumAsEnumerable.Max(), devKeysEnumAsEnumerable.Min());
            variableRegistry.Register($"{DeviceName}_send_delay", 17, "Send delay (ms)");
            variableRegistry.Register($"{DeviceName}_port", "", "COM Port");
            variableRegistry.Register($"{DeviceName}_baud", 500_000, "Serial bitrate");
            variableRegistry.Register($"{DeviceName}_timeout", 500, "Timeout (ms)");
        }
    }
}