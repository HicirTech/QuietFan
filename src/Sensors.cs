using System;
using System.Diagnostics;

namespace QuietFan
{
    /// <summary>Board temperature (ACPI thermal zone) and total CPU usage from Windows performance counters.</summary>
    sealed class Sensors : IDisposable
    {
        const float KelvinOffset = 273.15f;
        readonly PerformanceCounter temperature;
        readonly PerformanceCounter cpu;

        public Sensors()
        {
            string zone = PickThermalZone();
            if (zone != null) temperature = new PerformanceCounter("Thermal Zone Information", "Temperature", zone, true);
            cpu = new PerformanceCounter("Processor Information", "% Processor Time", "_Total", true);
            cpu.NextValue();   // the first read of a rate counter is always 0
        }

        /// <summary>Degrees Celsius, or NaN when the machine reports no thermal zone.</summary>
        public float TempC()
        {
            return temperature == null ? float.NaN : temperature.NextValue() - KelvinOffset;
        }

        public float CpuPercent()
        {
            return cpu.NextValue();
        }

        public void Dispose()
        {
            if (temperature != null) temperature.Dispose();
            cpu.Dispose();
        }

        // Prefer the main ACPI zone (\_TZ.THRM on ASUS laptops), otherwise the first one listed.
        static string PickThermalZone()
        {
            if (!PerformanceCounterCategory.Exists("Thermal Zone Information")) return null;
            string pick = null;
            foreach (string zone in new PerformanceCounterCategory("Thermal Zone Information").GetInstanceNames())
            {
                if (pick == null || zone.ToUpperInvariant().Contains("THRM")) pick = zone;
            }
            return pick;
        }
    }
}
