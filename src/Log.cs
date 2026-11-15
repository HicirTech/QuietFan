using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace QuietFan
{
    /// <summary>Best-effort log next to the settings; a failed log write must never stop the app.</summary>
    static class Log
    {
        const string FileName = "quietfan.log";
        const long MaxBytes = 512 * 1024;

        public static void Write(string message)
        {
            try
            {
                string folder = Settings.DefaultFolder;
                Directory.CreateDirectory(folder);
                FileInfo file = new FileInfo(Path.Combine(folder, FileName));
                if (file.Exists && file.Length > MaxBytes) file.Delete();
                string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                File.AppendAllText(file.FullName, stamp + "  " + message + Environment.NewLine, Encoding.UTF8);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
