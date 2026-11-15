using System;
using System.Runtime.InteropServices;

namespace QuietFan
{
    /// <summary>ASUS ATK ACPI interface (\\.\ATKACPI), the channel MyASUS uses for fan and power features.</summary>
    static class Atk
    {
        // Fan profile values, verified against MyASUS on a Vivobook S 16 (M5606WA).
        public const int ModeStandard = 0;
        public const int ModeWhisper = 1;
        public const int ModePerformance = 2;

        const uint MethodGet = 0x53545344;       // "DSTS"
        const uint MethodSet = 0x53564544;       // "DEVS"
        const uint IoctlCall = 0x0022240C;
        const uint DeviceCpuFan = 0x00110013;    // fan speed in units of 100 rpm
        const uint DeviceFanMode = 0x00110019;   // Vivobook thermal policy (fan profile)
        const long PresentFlag = 0x10000;
        const long Unsupported = 0xFFFFFFFE;
        const uint GenericReadWrite = 0xC0000000;
        const uint ShareReadWrite = 3;
        const uint OpenExisting = 3;
        const uint AttributeNormal = 0x80;

        static readonly IntPtr InvalidHandle = new IntPtr(-1);
        static IntPtr handle = InvalidHandle;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr CreateFile(string name, uint access, uint share, IntPtr security,
                                        uint disposition, uint flags, IntPtr template);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool DeviceIoControl(IntPtr device, uint code, byte[] inBuffer, uint inSize,
                                           byte[] outBuffer, uint outSize, ref uint returned, IntPtr overlapped);

        /// <summary>Opens the ATK device; false on machines without the ASUS System Control Interface.</summary>
        public static bool Open()
        {
            handle = CreateFile(@"\\.\ATKACPI", GenericReadWrite, ShareReadWrite, IntPtr.Zero, OpenExisting, AttributeNormal, IntPtr.Zero);
            return handle != InvalidHandle;
        }

        /// <summary>Fan speed in rpm (0 = stopped), or -1 when unavailable.</summary>
        public static int FanRpm()
        {
            long value = Call(MethodGet, DeviceCpuFan, 0);
            return IsPresent(value) ? (int)(value & 0xFFFF) * 100 : -1;
        }

        /// <summary>Current fan profile (ModeStandard, ModeWhisper or ModePerformance), or -1 when unavailable.</summary>
        public static int FanMode()
        {
            long value = Call(MethodGet, DeviceFanMode, 0);
            return IsPresent(value) ? (int)(value & 0xFFFF) : -1;
        }

        public static bool SetFanMode(int mode)
        {
            if (mode < ModeStandard || mode > ModePerformance) return false;
            return Call(MethodSet, DeviceFanMode, (uint)mode) == 1;
        }

        static bool IsPresent(long value)
        {
            return value >= 0 && value != Unsupported && (value & PresentFlag) != 0;
        }

        static long Call(uint method, uint device, uint argument)
        {
            if (handle == InvalidHandle) return -1;
            byte[] inBuffer = new byte[16];
            byte[] outBuffer = new byte[16];
            BitConverter.GetBytes(method).CopyTo(inBuffer, 0);
            BitConverter.GetBytes(8u).CopyTo(inBuffer, 4);   // size of the device id + argument block
            BitConverter.GetBytes(device).CopyTo(inBuffer, 8);
            BitConverter.GetBytes(argument).CopyTo(inBuffer, 12);
            uint returned = 0;
            if (!DeviceIoControl(handle, IoctlCall, inBuffer, 16, outBuffer, 16, ref returned, IntPtr.Zero)) return -1;
            return BitConverter.ToUInt32(outBuffer, 0);
        }
    }
}
