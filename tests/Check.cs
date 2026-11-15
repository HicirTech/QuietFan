using System;

namespace QuietFan.Tests
{
    /// <summary>Assertions for the built-in test runner.</summary>
    static class Check
    {
        public static void True(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        public static void Equal<T>(T expected, T actual, string what)
        {
            if (!Equals(expected, actual)) throw new InvalidOperationException(what + ": expected " + expected + ", got " + actual);
        }
    }
}
