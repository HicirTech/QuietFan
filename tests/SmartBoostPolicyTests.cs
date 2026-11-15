using System;
using System.Collections.Generic;

namespace QuietFan.Tests
{
    static class SmartBoostPolicyTests
    {
        public static void Register(List<KeyValuePair<string, Action>> tests)
        {
            Add(tests, "starts with boost off", StartsWithBoostOff);
            Add(tests, "a burst that already ended keeps boost off", EndedBurstKeepsBoostOff);
            Add(tests, "20 s of load turns boost on", SustainedLoadTurnsBoostOn);
            Add(tests, "an average of exactly 50% turns boost on", ExactUpThresholdTurnsBoostOn);
            Add(tests, "moderate load keeps boost on", ModerateLoadKeepsBoostOn);
            Add(tests, "an average of exactly 20% over 30 s turns boost off", ExactDownThresholdTurnsBoostOff);
            Add(tests, "missing samples are ignored", MissingSamplesAreIgnored);
            Add(tests, "reset clears state and history", ResetClearsStateAndHistory);
        }

        static void Add(List<KeyValuePair<string, Action>> tests, string name, Action test)
        {
            tests.Add(new KeyValuePair<string, Action>("SmartBoostPolicy: " + name, test));
        }

        static void StartsWithBoostOff()
        {
            Check.True(!new SmartBoostPolicy().BoostOn, "boost starts off");
        }

        static void EndedBurstKeepsBoostOff()
        {
            SmartBoostPolicy policy = new SmartBoostPolicy();
            Feed(policy, 100f, SmartBoostPolicy.UpSamples / 2);
            Feed(policy, 5f, SmartBoostPolicy.DownSamples);
            Check.True(!policy.BoostOn, "a 10 s burst followed by idle does not turn boost on");
        }

        static void SustainedLoadTurnsBoostOn()
        {
            SmartBoostPolicy policy = new SmartBoostPolicy();
            Feed(policy, 90f, SmartBoostPolicy.UpSamples - 1);
            Check.True(!policy.BoostOn, "boost stays off before 20 s of load");
            Check.True(policy.Add(90f), "the sample that completes 20 s reports a change");
            Check.True(policy.BoostOn, "boost is on after 20 s of load");
        }

        static void ExactUpThresholdTurnsBoostOn()
        {
            SmartBoostPolicy policy = new SmartBoostPolicy();
            Feed(policy, SmartBoostPolicy.UpPercent, SmartBoostPolicy.UpSamples);
            Check.True(policy.BoostOn, "boost is on at exactly the up threshold");
        }

        static void ModerateLoadKeepsBoostOn()
        {
            SmartBoostPolicy policy = BoostedPolicy();
            Feed(policy, 30f, SmartBoostPolicy.DownSamples * 2);
            Check.True(policy.BoostOn, "30% load keeps boost on");
        }

        static void ExactDownThresholdTurnsBoostOff()
        {
            SmartBoostPolicy policy = BoostedPolicy();
            Feed(policy, SmartBoostPolicy.DownPercent, SmartBoostPolicy.DownSamples - 1);
            Check.True(policy.BoostOn, "boost stays on while a busy sample is still in the 30 s window");
            Check.True(policy.Add(SmartBoostPolicy.DownPercent), "the sample that completes 30 s reports a change");
            Check.True(!policy.BoostOn, "boost is off at exactly the down threshold");
        }

        static void MissingSamplesAreIgnored()
        {
            SmartBoostPolicy policy = new SmartBoostPolicy();
            Feed(policy, 90f, SmartBoostPolicy.UpSamples - 1);
            Check.True(!policy.Add(float.NaN), "a missing sample reports no change");
            Check.True(!policy.BoostOn, "a missing sample does not count as load");
            policy.Add(90f);
            Check.True(policy.BoostOn, "the next real sample completes the 20 s window");
        }

        static void ResetClearsStateAndHistory()
        {
            SmartBoostPolicy policy = BoostedPolicy();
            policy.Reset();
            Check.True(!policy.BoostOn, "reset turns boost off");
            Check.Equal(0f, policy.Average(SmartBoostPolicy.DownSamples), "average after reset");
        }

        static SmartBoostPolicy BoostedPolicy()
        {
            SmartBoostPolicy policy = new SmartBoostPolicy();
            Feed(policy, 100f, SmartBoostPolicy.UpSamples);
            Check.True(policy.BoostOn, "precondition: boost on");
            return policy;
        }

        static void Feed(SmartBoostPolicy policy, float percent, int samples)
        {
            for (int i = 0; i < samples; i++) policy.Add(percent);
        }
    }
}
