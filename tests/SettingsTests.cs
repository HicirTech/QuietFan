using System;
using System.Collections.Generic;
using System.IO;

namespace QuietFan.Tests
{
    static class SettingsTests
    {
        public static void Register(List<KeyValuePair<string, Action>> tests)
        {
            Add(tests, "save and load round-trip every field", RoundTripsEveryField);
            Add(tests, "a missing file gives the defaults", MissingFileGivesDefaults);
            Add(tests, "invalid values keep the defaults", InvalidValuesKeepDefaults);
        }

        static void Add(List<KeyValuePair<string, Action>> tests, string name, Action test)
        {
            tests.Add(new KeyValuePair<string, Action>("Settings: " + name, test));
        }

        static void RoundTripsEveryField()
        {
            string folder = NewTempPath();
            try
            {
                Settings saved = new Settings();
                saved.Policy = Policy.Quiet;
                saved.Dirty = true;
                saved.OrigScheme = Guid.NewGuid();
                saved.OrigAc = 4;
                saved.OrigDc = 1;
                saved.Save(folder);

                Settings loaded = Settings.Load(folder);
                Check.Equal(saved.Policy, loaded.Policy, "policy");
                Check.Equal(saved.Dirty, loaded.Dirty, "dirty");
                Check.Equal(saved.OrigScheme, loaded.OrigScheme, "origScheme");
                Check.Equal(saved.OrigAc, loaded.OrigAc, "origAc");
                Check.Equal(saved.OrigDc, loaded.OrigDc, "origDc");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        static void MissingFileGivesDefaults()
        {
            AssertDefaults(Settings.Load(NewTempPath()));
        }

        static void InvalidValuesKeepDefaults()
        {
            string folder = NewTempPath();
            try
            {
                Directory.CreateDirectory(folder);
                File.WriteAllLines(Path.Combine(folder, Settings.FileName), new[]
                {
                    "policy=7",
                    "dirty=yes",
                    "origScheme=not-a-guid",
                    "origAc=99",
                    "origDc=-1",
                    "no separator",
                    "=no key",
                });
                AssertDefaults(Settings.Load(folder));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        static void AssertDefaults(Settings settings)
        {
            Check.Equal(Policy.Smart, settings.Policy, "policy");
            Check.Equal(false, settings.Dirty, "dirty");
            Check.Equal(Guid.Empty, settings.OrigScheme, "origScheme");
            Check.Equal(Boost.Aggressive, settings.OrigAc, "origAc");
            Check.Equal(Boost.Aggressive, settings.OrigDc, "origDc");
        }

        static string NewTempPath()
        {
            return Path.Combine(Path.GetTempPath(), "QuietFanTests-" + Guid.NewGuid().ToString("N"));
        }
    }
}
