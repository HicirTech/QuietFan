using System;
using System.Globalization;
using System.IO;

namespace QuietFan
{
    /// <summary>The user's policy plus the original boost values needed to undo QuietFan's changes.</summary>
    sealed class Settings
    {
        public const string FileName = "settings.ini";

        public Policy Policy = Policy.Smart;
        public UiLanguage Language = UiLanguage.Auto;
        public bool Dirty;                    // boost changed by QuietFan and not restored yet
        public Guid OrigScheme = Guid.Empty;  // power plan the original values belong to
        public uint OrigAc = Boost.Aggressive;
        public uint OrigDc = Boost.Aggressive;

        public static string DefaultFolder
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QuietFan"); }
        }

        /// <summary>Reads the settings file; missing files and invalid values fall back to the defaults.</summary>
        public static Settings Load(string folder)
        {
            Settings settings = new Settings();
            string path = Path.Combine(folder, FileName);
            if (!File.Exists(path)) return settings;
            foreach (string line in File.ReadAllLines(path))
            {
                int separator = line.IndexOf('=');
                if (separator > 0) settings.Apply(line.Substring(0, separator).Trim(), line.Substring(separator + 1).Trim());
            }
            return settings;
        }

        public void Save(string folder)
        {
            Directory.CreateDirectory(folder);
            File.WriteAllLines(Path.Combine(folder, FileName), new[]
            {
                "policy=" + ((int)Policy).ToString(CultureInfo.InvariantCulture),
                "language=" + ((int)Language).ToString(CultureInfo.InvariantCulture),
                "dirty=" + (Dirty ? "1" : "0"),
                "origScheme=" + OrigScheme.ToString(),
                "origAc=" + OrigAc.ToString(CultureInfo.InvariantCulture),
                "origDc=" + OrigDc.ToString(CultureInfo.InvariantCulture),
            });
        }

        void Apply(string key, string value)
        {
            int number;
            bool isNumber = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number);
            bool isBoostValue = isNumber && number >= 0 && number <= Boost.MaxValue;
            Guid guid;
            switch (key)
            {
                case "policy":
                    if (isNumber && Enum.IsDefined(typeof(Policy), number)) Policy = (Policy)number;
                    break;
                case "language":
                    if (isNumber && Enum.IsDefined(typeof(UiLanguage), number)) Language = (UiLanguage)number;
                    break;
                case "dirty":
                    Dirty = value == "1";
                    break;
                case "origScheme":
                    if (Guid.TryParse(value, out guid)) OrigScheme = guid;
                    break;
                case "origAc":
                    if (isBoostValue) OrigAc = (uint)number;
                    break;
                case "origDc":
                    if (isBoostValue) OrigDc = (uint)number;
                    break;
            }
        }
    }
}
