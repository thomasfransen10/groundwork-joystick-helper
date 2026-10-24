using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Windows.Gaming.Input;

namespace GroundworkJoystickHelper
{
    // Use Microsoft's supported C#/WinRT projection. This program does not
    // allocate unmanaged buffers or construct COM vtable function pointers.
    internal static class Wgi
    {
        private static bool initialized;
        private static void InventoryChanged(object sender, RawGameController controller) { }
        internal static void Init()
        {
            if (initialized) return;
            RawGameController.RawGameControllerAdded += InventoryChanged;
            RawGameController.RawGameControllerRemoved += InventoryChanged;
            initialized = true;
        }
        internal static List<Controller> Enumerate()
        {
            Init();
            var devices = RawGameController.RawGameControllers.ToArray();
            if (devices.Length > 64) throw new InvalidOperationException("Unexpected controller count.");
            return devices.Select(device => new Controller(device)).ToList();
        }
        internal static void Shutdown()
        {
            if (!initialized) return;
            RawGameController.RawGameControllerAdded -= InventoryChanged;
            RawGameController.RawGameControllerRemoved -= InventoryChanged;
            initialized = false;
        }
    }

    internal sealed class Controller : IDisposable
    {
        private RawGameController device;
        private readonly bool[] nativeButtons;
        private readonly GameControllerSwitchPosition[] nativeSwitches;
        internal string Name, WindowsName, Id;
        internal ushort Vendor, Product;
        internal int AxisCount, ButtonCount, SwitchCount;
        internal bool IsGamepad;
        internal double[] Axes, Minimum, Maximum;
        internal byte[] Buttons;
        internal int[] Switches;
        internal ulong Timestamp;
        internal long Changes;
        internal string Error = "";
        private bool seen;

        internal Controller(RawGameController controller)
        {
            device = controller;
            AxisCount = controller.AxisCount;
            ButtonCount = controller.ButtonCount;
            SwitchCount = controller.SwitchCount;
            if (AxisCount < 0 || AxisCount > 128 || ButtonCount < 0 || ButtonCount > 1024 || SwitchCount < 0 || SwitchCount > 32)
                throw new InvalidOperationException("Unsupported controller layout size.");
            Vendor = controller.HardwareVendorId;
            Product = controller.HardwareProductId;
            WindowsName = (controller.DisplayName ?? "").TrimEnd('\0');
            Id = controller.NonRoamableId;
            IsGamepad = Gamepad.FromGameController(controller) != null;
            Name = OemName(Vendor, Product) ?? WindowsName;
            if (String.IsNullOrWhiteSpace(Name)) Name = "Unnamed controller";
            Axes = new double[AxisCount];
            Buttons = new byte[ButtonCount];
            Switches = new int[SwitchCount];
            nativeButtons = new bool[ButtonCount];
            nativeSwitches = new GameControllerSwitchPosition[SwitchCount];
            Minimum = Enumerable.Repeat(Double.PositiveInfinity, AxisCount).ToArray();
            Maximum = Enumerable.Repeat(Double.NegativeInfinity, AxisCount).ToArray();
        }
        internal static string OemName(ushort vendor, ushort product)
        {
            string key = @"System\CurrentControlSet\Control\MediaProperties\PrivateProperties\Joystick\OEM\VID_" +
                vendor.ToString("X4") + "&PID_" + product.ToString("X4");
            foreach (RegistryKey root in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                try
                {
                    using (RegistryKey node = root.OpenSubKey(key, false))
                    {
                        if (node == null) continue;
                        string value = node.GetValue("OEMName") as string;
                        if (!String.IsNullOrWhiteSpace(value)) return value.Trim();
                    }
                }
                catch (System.Security.SecurityException) { }
                catch (UnauthorizedAccessException) { }
            }
            return null;
        }
        internal bool Read()
        {
            if (device == null) { Error = "Controller has been released."; return false; }
            try
            {
                ulong time = device.GetCurrentReading(nativeButtons, nativeSwitches, Axes);
                if (seen && Timestamp != time) Changes++;
                Timestamp = time; seen = true; Error = "";
                for (int i = 0; i < Buttons.Length; i++) Buttons[i] = nativeButtons[i] ? (byte)1 : (byte)0;
                for (int i = 0; i < Switches.Length; i++) Switches[i] = (int)nativeSwitches[i];
                for (int i = 0; i < Axes.Length; i++)
                {
                    Minimum[i] = Math.Min(Minimum[i], Axes[i]);
                    Maximum[i] = Math.Max(Maximum[i], Axes[i]);
                }
                return true;
            }
            catch (COMException e) { Error = "Input unavailable (0x" + e.ErrorCode.ToString("X8") + ")"; return false; }
            catch (InvalidOperationException e) { Error = "Input unavailable: " + e.Message; return false; }
        }
        public void Dispose() { device = null; }
    }
}
