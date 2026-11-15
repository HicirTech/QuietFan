using System;
using System.Collections.Generic;

namespace QuietFan
{
    /// <summary>
    /// Turns boost on only while the CPU is still busy after a busy stretch, so short bursts never spin up the
    /// fan, and off again once the load has settled.
    /// </summary>
    sealed class SmartBoostPolicy
    {
        public const int SampleIntervalMs = 2000;
        public const int UpSamples = 10;       // 20 s
        public const int DownSamples = 15;     // 30 s
        public const float UpPercent = 50f;
        public const float DownPercent = 20f;

        readonly List<float> history = new List<float>();

        public bool BoostOn { get; private set; }

        /// <summary>Adds one CPU usage sample in percent; returns true when BoostOn changed.</summary>
        public bool Add(float cpuPercent)
        {
            if (float.IsNaN(cpuPercent)) return false;
            history.Add(cpuPercent);
            if (history.Count > DownSamples) history.RemoveAt(0);

            // The latest sample must still be busy: a burst that already ended can lift the average on its own.
            if (!BoostOn && history.Count >= UpSamples && cpuPercent >= UpPercent && Average(UpSamples) >= UpPercent)
            {
                BoostOn = true;
                return true;
            }
            if (BoostOn && history.Count >= DownSamples && Average(DownSamples) <= DownPercent)
            {
                BoostOn = false;
                return true;
            }
            return false;
        }

        /// <summary>Average of the newest samples, or of all stored samples when there are fewer.</summary>
        public float Average(int samples)
        {
            int count = Math.Min(samples, history.Count);
            if (count == 0) return 0f;
            float sum = 0f;
            for (int i = history.Count - count; i < history.Count; i++) sum += history[i];
            return sum / count;
        }

        public void Reset()
        {
            history.Clear();
            BoostOn = false;
        }
    }
}
