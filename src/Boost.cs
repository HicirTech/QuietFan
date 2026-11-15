using System;
using System.Runtime.InteropServices;

namespace QuietFan
{
    /// <summary>"Processor performance boost mode" (powercfg alias PERFBOOSTMODE) of a power plan.</summary>
    static class Boost
    {
        public const uint Disabled = 0;
        public const uint Aggressive = 2;   // Windows default
        public const uint MaxValue = 6;
        const uint InvalidParameter = 87;

        static Guid subProcessor = new Guid("54533251-82be-4824-96c1-47b60b740d00");
        static Guid perfBoostMode = new Guid("be337238-0d82-4146-a960-4f3749d470c7");

        [DllImport("powrprof.dll")]
        static extern uint PowerGetActiveScheme(IntPtr root, out IntPtr scheme);

        [DllImport("powrprof.dll")]
        static extern uint PowerReadACValueIndex(IntPtr root, ref Guid scheme, ref Guid sub, ref Guid setting, out uint value);

        [DllImport("powrprof.dll")]
        static extern uint PowerReadDCValueIndex(IntPtr root, ref Guid scheme, ref Guid sub, ref Guid setting, out uint value);

        [DllImport("powrprof.dll")]
        static extern uint PowerWriteACValueIndex(IntPtr root, ref Guid scheme, ref Guid sub, ref Guid setting, uint value);

        [DllImport("powrprof.dll")]
        static extern uint PowerWriteDCValueIndex(IntPtr root, ref Guid scheme, ref Guid sub, ref Guid setting, uint value);

        [DllImport("powrprof.dll")]
        static extern uint PowerSetActiveScheme(IntPtr root, ref Guid scheme);

        [DllImport("kernel32.dll")]
        static extern IntPtr LocalFree(IntPtr memory);

        public static Guid ActiveScheme()
        {
            IntPtr pointer;
            if (PowerGetActiveScheme(IntPtr.Zero, out pointer) != 0 || pointer == IntPtr.Zero) return Guid.Empty;
            Guid scheme = (Guid)Marshal.PtrToStructure(pointer, typeof(Guid));
            LocalFree(pointer);
            return scheme;
        }

        public static bool Read(Guid scheme, out uint ac, out uint dc)
        {
            ac = 0;
            dc = 0;
            if (scheme == Guid.Empty) return false;
            return PowerReadACValueIndex(IntPtr.Zero, ref scheme, ref subProcessor, ref perfBoostMode, out ac) == 0
                && PowerReadDCValueIndex(IntPtr.Zero, ref scheme, ref subProcessor, ref perfBoostMode, out dc) == 0;
        }

        /// <summary>Writes both values and applies them if the plan is active; returns 0 or a Win32 error code.</summary>
        public static uint Write(Guid scheme, uint ac, uint dc)
        {
            if (scheme == Guid.Empty) return InvalidParameter;
            uint result = PowerWriteACValueIndex(IntPtr.Zero, ref scheme, ref subProcessor, ref perfBoostMode, ac);
            if (result != 0) return result;
            result = PowerWriteDCValueIndex(IntPtr.Zero, ref scheme, ref subProcessor, ref perfBoostMode, dc);
            if (result != 0) return result;
            return scheme == ActiveScheme() ? PowerSetActiveScheme(IntPtr.Zero, ref scheme) : 0;
        }
    }
}
