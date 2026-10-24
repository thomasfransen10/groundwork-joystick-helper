using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace GroundworkJoystickHelper
{
    internal static class Wgi
    {
        [ThreadStatic] private static bool initialized;
        [ThreadStatic] private static IntPtr rawFactory;
        [ThreadStatic] private static IntPtr gamepadFactory;
        [DllImport("combase.dll")] private static extern int RoInitialize(uint type);
        [DllImport("combase.dll", CharSet=CharSet.Unicode)] private static extern int WindowsCreateString(string value, uint length, out IntPtr text);
        [DllImport("combase.dll")] private static extern int WindowsDeleteString(IntPtr text);
        [DllImport("combase.dll")] private static extern IntPtr WindowsGetStringRawBuffer(IntPtr text, out uint length);
        [DllImport("combase.dll")] private static extern int RoGetActivationFactory(IntPtr name, ref Guid iid, out IntPtr factory);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] internal delegate int GetPointer(IntPtr self, out IntPtr value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] internal delegate int GetInt(IntPtr self, out int value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] internal delegate int GetUShort(IntPtr self, out ushort value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] internal delegate int GetAt(IntPtr self, uint index, out IntPtr value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] internal delegate int FromController(IntPtr self, IntPtr controller, out IntPtr value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] internal delegate int Reading(IntPtr self, uint buttons, IntPtr buttonValues,
            uint switches, IntPtr switchValues, uint axes, IntPtr axisValues, out ulong time);
        internal static T Function<T>(IntPtr self, int index) where T:class
        {
            return (T)(object)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(self), index*IntPtr.Size), typeof(T));
        }
        internal static void Check(int result) { if (result < 0) Marshal.ThrowExceptionForHR(result); }
        internal static IntPtr Query(IntPtr self, string iid)
        {
            Guid guid = new Guid(iid); IntPtr result;
            Check(Marshal.QueryInterface(self, ref guid, out result)); return result;
        }
        internal static void Release(IntPtr value) { if (value != IntPtr.Zero) Marshal.Release(value); }
        internal static int Number(IntPtr self, int slot)
        { int value; Check(Function<GetInt>(self,slot)(self,out value)); return value; }
        internal static ushort Number16(IntPtr self, int slot)
        { ushort value; Check(Function<GetUShort>(self,slot)(self,out value)); return value; }
        internal static string Text(IntPtr self, int slot)
        {
            IntPtr value; Check(Function<GetPointer>(self,slot)(self,out value));
            try { uint length; IntPtr text=WindowsGetStringRawBuffer(value,out length); return Marshal.PtrToStringUni(text,(int)length) ?? ""; }
            finally { WindowsDeleteString(value); }
        }
        internal static IntPtr Factory(string name, string iid)
        {
            IntPtr text; Check(WindowsCreateString(name,(uint)name.Length,out text));
            try { Guid guid=new Guid(iid); IntPtr result; Check(RoGetActivationFactory(text,ref guid,out result)); return result; }
            finally { WindowsDeleteString(text); }
        }
        internal static void Init()
        {
            if(initialized)return;
            int hr=RoInitialize(0);
            if (hr != unchecked((int)0x80010106)) Check(hr);
            initialized=true;
        }
        internal static List<Controller> Enumerate()
        {
            Init();
            List<Controller> list=new List<Controller>();
            IntPtr vector=IntPtr.Zero;
            try
            {
                if(rawFactory==IntPtr.Zero)rawFactory=Factory("Windows.Gaming.Input.RawGameController","eb8d0792-e95a-4b19-afc7-0a59f8bf759e");
                if(gamepadFactory==IntPtr.Zero)gamepadFactory=Factory("Windows.Gaming.Input.Gamepad","42676dc5-0856-47c4-9213-b395504c3a3c");
                Check(Function<GetPointer>(rawFactory,10)(rawFactory,out vector));
                int count=Number(vector,7);
                if (count < 0 || count > 64) throw new InvalidOperationException("Unexpected controller count.");
                for (uint i=0;i<count;i++)
                {
                    IntPtr raw; Check(Function<GetAt>(vector,6)(vector,i,out raw));
                    try { list.Add(new Controller(raw,gamepadFactory)); }
                    catch { Release(raw); throw; }
                }
                return list;
            }
            catch { foreach (Controller d in list) d.Dispose(); throw; }
            finally { Release(vector); }
        }
        internal static void Shutdown(){Release(gamepadFactory);Release(rawFactory);gamepadFactory=rawFactory=IntPtr.Zero;}
    }

    internal sealed class Controller:IDisposable
    {
        private IntPtr raw, buttonBuffer, switchBuffer, axisBuffer;
        private Wgi.Reading reader;
        internal string Name, WindowsName, Id;
        internal ushort Vendor, Product;
        internal int AxisCount, ButtonCount, SwitchCount;
        internal bool IsGamepad;
        internal double[] Axes;
        internal byte[] Buttons;
        internal int[] Switches;
        internal double[] Minimum, Maximum;
        internal ulong Timestamp;
        internal long Changes;
        internal string Error="";
        private bool seen;
        internal Controller(IntPtr device, IntPtr gamepadFactory)
        {
            // Ownership transfers only after initialization succeeds.
            AxisCount=Wgi.Number(device,6); ButtonCount=Wgi.Number(device,7); SwitchCount=Wgi.Number(device,11);
            if (AxisCount<0||AxisCount>128||ButtonCount<0||ButtonCount>1024||SwitchCount<0||SwitchCount>32)
                throw new InvalidOperationException("Unsupported controller layout size.");
            Vendor=Wgi.Number16(device,10);Product=Wgi.Number16(device,9);
            IntPtr extra=Wgi.Query(device,"43c0c035-bb73-4756-a787-3ed6bea617bd");
            try { WindowsName=Wgi.Text(extra,8).TrimEnd('\0');Id=Wgi.Text(extra,7); }
            finally { Wgi.Release(extra); }
            IntPtr common=Wgi.Query(device,"1baf6522-5f64-42c5-8267-b9fe2215bfbd");
            IntPtr pad=IntPtr.Zero;
            try { IsGamepad=Wgi.Function<Wgi.FromController>(gamepadFactory,6)(gamepadFactory,common,out pad)>=0 && pad!=IntPtr.Zero; }
            finally { Wgi.Release(pad); Wgi.Release(common); }
            Name=OemName(Vendor,Product) ?? WindowsName;
            if (String.IsNullOrWhiteSpace(Name)) Name="Unnamed controller";
            Axes=new double[AxisCount];Buttons=new byte[ButtonCount];Switches=new int[SwitchCount];
            Minimum=new double[AxisCount];Maximum=new double[AxisCount];
            for(int i=0;i<AxisCount;i++){Minimum[i]=Double.PositiveInfinity;Maximum[i]=Double.NegativeInfinity;}
            reader=Wgi.Function<Wgi.Reading>(device,13);
            try
            {
                buttonBuffer=Marshal.AllocHGlobal(Math.Max(1,ButtonCount));
                switchBuffer=Marshal.AllocHGlobal(Math.Max(1,SwitchCount*4));
                axisBuffer=Marshal.AllocHGlobal(Math.Max(1,AxisCount*8));
            }
            catch { FreeBuffers();throw; }
            raw=device;
        }
        internal static string OemName(ushort vendor, ushort product)
        {
            string key=@"System\CurrentControlSet\Control\MediaProperties\PrivateProperties\Joystick\OEM\VID_"+
                vendor.ToString("X4")+"&PID_"+product.ToString("X4");
            foreach(RegistryKey root in new[]{Registry.CurrentUser,Registry.LocalMachine})
            {
                try { using(RegistryKey node=root.OpenSubKey(key))
                    { if(node!=null){string name=node.GetValue("OEMName") as string;if(!String.IsNullOrWhiteSpace(name))return name.Trim();} } }
                catch(System.Security.SecurityException){}
            }
            return null;
        }
        internal bool Read()
        {
            try
            {
                ulong time;
                Wgi.Check(reader(raw,(uint)ButtonCount,buttonBuffer,(uint)SwitchCount,switchBuffer,(uint)AxisCount,axisBuffer,out time));
                if(seen && Timestamp!=time)Changes++;
                Timestamp=time;seen=true;Error="";
                if(AxisCount>0)Marshal.Copy(axisBuffer,Axes,0,AxisCount);
                if(ButtonCount>0)Marshal.Copy(buttonBuffer,Buttons,0,ButtonCount);
                if(SwitchCount>0)Marshal.Copy(switchBuffer,Switches,0,SwitchCount);
                for(int i=0;i<AxisCount;i++){Minimum[i]=Math.Min(Minimum[i],Axes[i]);Maximum[i]=Math.Max(Maximum[i],Axes[i]);}
                return true;
            }
            catch(COMException e){Error="Input unavailable (0x"+e.ErrorCode.ToString("X8")+")";return false;}
        }
        private void FreeBuffers()
        {
            if(buttonBuffer!=IntPtr.Zero)Marshal.FreeHGlobal(buttonBuffer);
            if(switchBuffer!=IntPtr.Zero)Marshal.FreeHGlobal(switchBuffer);
            if(axisBuffer!=IntPtr.Zero)Marshal.FreeHGlobal(axisBuffer);
            buttonBuffer=switchBuffer=axisBuffer=IntPtr.Zero;
        }
        public void Dispose(){FreeBuffers();Wgi.Release(raw);raw=IntPtr.Zero;}
    }
}
