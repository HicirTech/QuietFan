using System;
using System.Collections.Generic;

namespace QuietFan.Tests
{
    /// <summary>Runs every registered test; the exit code is the number of failures.</summary>
    static class TestRunner
    {
        static int Main()
        {
            List<KeyValuePair<string, Action>> tests = new List<KeyValuePair<string, Action>>();
            SmartBoostPolicyTests.Register(tests);
            SettingsTests.Register(tests);

            int failed = 0;
            foreach (KeyValuePair<string, Action> test in tests)
            {
                try
                {
                    test.Value();
                    Console.WriteLine("ok   " + test.Key);
                }
                catch (Exception ex)
                {
                    failed++;
                    Console.WriteLine("FAIL " + test.Key + ": " + ex.Message);
                }
            }
            Console.WriteLine("{0} passed, {1} failed", tests.Count - failed, failed);
            return failed;
        }
    }
}
