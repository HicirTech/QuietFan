using System;
using System.Drawing;
using System.Drawing.Text;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace QuietFan
{
    /// <summary>Tray icon, menu and the control loop that applies the boost policy every two seconds.</summary>
    sealed class TrayApp : ApplicationContext
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunValueName = "QuietFan";
        const string ProjectUrl = "https://github.com/HicirTech/QuietFan";
        const int TooltipMaxLength = 63;   // NotifyIcon.Text limit on .NET Framework
        const int BalloonMs = 4000;
        const float WarmCelsius = 60f;
        const float HotCelsius = 75f;

        static bool chinese;

        readonly NotifyIcon tray = new NotifyIcon();
        readonly ContextMenuStrip menu = new ContextMenuStrip();
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        readonly Sensors sensors = new Sensors();
        readonly SmartBoostPolicy smart = new SmartBoostPolicy();
        readonly EventWaitHandle exitEvent = new EventWaitHandle(false, EventResetMode.ManualReset, Program.ExitEventName);
        readonly Settings cfg;
        readonly bool atkOk;
        readonly bool fanModeSupported;

        ToolStripMenuItem statusItem;
        ToolStripMenuItem smartItem;
        ToolStripMenuItem quietItem;
        ToolStripMenuItem fullItem;
        ToolStripMenuItem whisperItem;
        ToolStripMenuItem standardItem;
        ToolStripMenuItem performanceItem;
        ToolStripMenuItem autoStartItem;
        uint fullAc = Boost.Aggressive;
        uint fullDc = Boost.Aggressive;
        long lastWritten = -1;   // last AC boost value QuietFan wrote; -1 = untouched
        bool writeErrorShown;
        bool stopped;
        string iconKey;
        Icon currentIcon;

        [DllImport("user32.dll")]
        static extern bool DestroyIcon(IntPtr handle);

        public TrayApp()
        {
            cfg = Settings.Load(Settings.DefaultFolder);
            chinese = UsesChinese(cfg.Language);
            atkOk = Atk.Open();
            fanModeSupported = atkOk && Atk.FanMode() >= 0;
            CaptureOriginalBoost();
            BuildMenu();

            tray.ContextMenuStrip = menu;
            tray.MouseUp += OnTrayMouseUp;
            SetIcon("--", Color.DimGray);
            tray.Visible = true;

            timer.Interval = SmartBoostPolicy.SampleIntervalMs;
            timer.Tick += OnTimerTick;
            timer.Start();
            SystemEvents.SessionEnding += OnSessionEnding;

            Log.Write("start " + Application.ProductVersion + " policy=" + cfg.Policy +
                      " origAc=" + cfg.OrigAc + " origDc=" + cfg.OrigDc + " atk=" + atkOk + " fanMode=" + fanModeSupported);
            Tick();
            tray.ShowBalloonTip(BalloonMs, T("QuietFan 已启动", "QuietFan is running"), PolicyDescription(cfg.Policy), ToolTipIcon.Info);
        }

        /// <summary>Restores the saved original boost values; used by --restore when QuietFan is not running.</summary>
        public static void RestoreFromSettings()
        {
            Settings saved = Settings.Load(Settings.DefaultFolder);
            if (!saved.Dirty) return;
            uint result = Boost.Write(saved.OrigScheme, saved.OrigAc, saved.OrigDc);
            if (result == 0)
            {
                saved.Dirty = false;
                saved.Save(Settings.DefaultFolder);
            }
            Log.Write("--restore ac=" + saved.OrigAc + " dc=" + saved.OrigDc + " rc=" + result);
        }

        static string T(string chineseText, string englishText)
        {
            return chinese ? chineseText : englishText;
        }

        static bool UsesChinese(UiLanguage language)
        {
            if (language == UiLanguage.Auto) return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh";
            return language == UiLanguage.Chinese;
        }

        // ---------- original boost values ----------

        void CaptureOriginalBoost()
        {
            Guid scheme = Boost.ActiveScheme();
            uint ac, dc;
            if (!Boost.Read(scheme, out ac, out dc))
            {
                Log.Write("cannot read the boost mode of power plan " + scheme);
                return;
            }
            // A previous run that crashed or was killed left boost off: keep the originals it saved.
            bool recover = cfg.Dirty && cfg.OrigScheme == scheme && ac == Boost.Disabled;
            if (recover)
            {
                Log.Write("recovering originals from the previous run ac=" + cfg.OrigAc + " dc=" + cfg.OrigDc);
            }
            else
            {
                cfg.OrigScheme = scheme;
                cfg.OrigAc = ac;
                cfg.OrigDc = dc;
                cfg.Dirty = false;
                cfg.Save(Settings.DefaultFolder);
            }
            // Full uses the originals; if boost was already off there, it uses the Windows default instead.
            fullAc = cfg.OrigAc == Boost.Disabled ? Boost.Aggressive : cfg.OrigAc;
            fullDc = cfg.OrigDc == Boost.Disabled ? Boost.Aggressive : cfg.OrigDc;
            lastWritten = recover ? (long)Boost.Disabled : -1L;
        }

        void RestoreBoost()
        {
            if (!cfg.Dirty) return;
            uint result = Boost.Write(cfg.OrigScheme, cfg.OrigAc, cfg.OrigDc);
            if (result != 0)
            {
                Log.Write("boost restore failed rc=" + result);
                return;
            }
            cfg.Dirty = false;
            cfg.Save(Settings.DefaultFolder);
            lastWritten = -1;
            Log.Write("boost restored ac=" + cfg.OrigAc + " dc=" + cfg.OrigDc);
        }

        void CheckPlanChange()
        {
            Guid active = Boost.ActiveScheme();
            if (active == Guid.Empty || active == cfg.OrigScheme) return;
            Log.Write("power plan changed to " + active);
            RestoreBoost();
            CaptureOriginalBoost();
        }

        // ---------- control loop ----------

        void OnTimerTick(object sender, EventArgs e)
        {
            Tick();
        }

        void Tick()
        {
            if (stopped) return;
            if (exitEvent.WaitOne(0))
            {
                ExitThread();
                return;
            }
            float celsius = sensors.TempC();
            float cpuPercent = sensors.CpuPercent();
            int rpm = atkOk ? Atk.FanRpm() : -1;
            int mode = fanModeSupported ? Atk.FanMode() : -1;
            CheckPlanChange();
            ApplyPolicy(cpuPercent);
            UpdateUi(celsius, cpuPercent, rpm, mode);
        }

        void ApplyPolicy(float cpuPercent)
        {
            if (smart.Add(cpuPercent))
            {
                float load = smart.Average(smart.BoostOn ? SmartBoostPolicy.UpSamples : SmartBoostPolicy.DownSamples);
                Log.Write(string.Format(CultureInfo.InvariantCulture, "smart: load {0:F0}% -> boost {1}", load, smart.BoostOn ? "on" : "off"));
            }

            bool wantBoost = cfg.Policy == Policy.Full || (cfg.Policy == Policy.Smart && smart.BoostOn);
            uint ac = wantBoost ? fullAc : Boost.Disabled;
            uint dc = wantBoost ? fullDc : Boost.Disabled;
            if (lastWritten == ac) return;

            uint result = Boost.Write(cfg.OrigScheme, ac, dc);
            if (result == 0)
            {
                lastWritten = ac;
                cfg.Dirty = ac != cfg.OrigAc || dc != cfg.OrigDc;
                cfg.Save(Settings.DefaultFolder);
                Log.Write("boost -> " + (wantBoost ? "on (" + ac + "/" + dc + ")" : "off"));
            }
            else if (!writeErrorShown)
            {
                writeErrorShown = true;
                Log.Write("boost write failed rc=" + result);
                tray.ShowBalloonTip(BalloonMs, "QuietFan",
                    string.Format(T("无法修改睿频设置(错误 {0})。", "Could not change the boost setting (error {0})."), result),
                    ToolTipIcon.Warning);
            }
        }

        // ---------- UI ----------

        void BuildMenu()
        {
            statusItem = AddHeader(T("读取中…", "Reading sensors…"));
            menu.Items.Add(new ToolStripSeparator());

            AddHeader(T("安静策略(控制 CPU 睿频)", "Quiet policy (CPU boost)"));
            smartItem = AddItem(T("智能:平时关睿频,持续高负载才开(推荐)", "Smart: boost only under sustained load (recommended)"),
                                delegate { SetPolicy(Policy.Smart); });
            quietItem = AddItem(T("静音:始终关闭睿频", "Quiet: boost always off"), delegate { SetPolicy(Policy.Quiet); });
            fullItem = AddItem(T("性能:系统原本的睿频设置", "Full: original boost setting"), delegate { SetPolicy(Policy.Full); });
            menu.Items.Add(new ToolStripSeparator());

            AddHeader(T("ASUS 风扇模式(同 MyASUS)", "ASUS fan profile (same as MyASUS)"));
            whisperItem = AddItem(T("静音模式 Whisper", "Whisper"), delegate { SetFanMode(Atk.ModeWhisper); });
            standardItem = AddItem(T("标准模式 Standard", "Standard"), delegate { SetFanMode(Atk.ModeStandard); });
            performanceItem = AddItem(T("性能模式 Performance", "Performance"), delegate { SetFanMode(Atk.ModePerformance); });
            whisperItem.Enabled = standardItem.Enabled = performanceItem.Enabled = fanModeSupported;
            menu.Items.Add(new ToolStripSeparator());

            autoStartItem = AddItem(T("开机自动启动", "Start with Windows"), delegate { ToggleAutoStart(); });
            autoStartItem.Checked = AutoStartEnabled();
            ToolStripMenuItem language = new ToolStripMenuItem("Language / 语言");
            AddLanguageItem(language, T("自动(跟随 Windows 显示语言)", "Auto (Windows display language)"), UiLanguage.Auto);
            AddLanguageItem(language, "中文", UiLanguage.Chinese);
            AddLanguageItem(language, "English", UiLanguage.English);
            menu.Items.Add(language);
            AddItem(T("说明", "About"), delegate { ShowHelp(); });
            AddItem(T("退出(恢复原来的睿频设置)", "Exit (restore boost setting)"), delegate { ExitThread(); });
        }

        ToolStripMenuItem AddHeader(string text)
        {
            ToolStripMenuItem header = new ToolStripMenuItem(text);
            header.Enabled = false;
            menu.Items.Add(header);
            return header;
        }

        ToolStripMenuItem AddItem(string text, EventHandler onClick)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.Click += onClick;
            menu.Items.Add(item);
            return item;
        }

        void AddLanguageItem(ToolStripMenuItem parent, string text, UiLanguage language)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.Checked = cfg.Language == language;
            item.Click += delegate { SetLanguage(language); };
            parent.DropDownItems.Add(item);
        }

        void OnTrayMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            // NotifyIcon opens its menu only on right click; its private ShowContextMenu does the same for left click.
            MethodInfo show = typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
            if (show != null) show.Invoke(tray, null);
        }

        void UpdateUi(float celsius, float cpuPercent, int rpm, int mode)
        {
            string temp = float.IsNaN(celsius) ? "--" : ((int)Math.Round(celsius)).ToString(CultureInfo.InvariantCulture);
            string cpu = float.IsNaN(cpuPercent) ? "-" : ((int)Math.Round(cpuPercent)).ToString(CultureInfo.InvariantCulture);
            string fan = rpm < 0 ? "?" : rpm == 0 ? T("停转", "stopped") : rpm.ToString(CultureInfo.InvariantCulture) + " rpm";
            string boost = lastWritten == Boost.Disabled ? T("关", "off") : T("开", "on");

            statusItem.Text = string.Format(T("温度 {0}°C    风扇 {1}    CPU {2}%    睿频 {3}", "Temp {0}°C    Fan {1}    CPU {2}%    Boost {3}"),
                                            temp, fan, cpu, boost);
            smartItem.Checked = cfg.Policy == Policy.Smart;
            quietItem.Checked = cfg.Policy == Policy.Quiet;
            fullItem.Checked = cfg.Policy == Policy.Full;
            whisperItem.Checked = mode == Atk.ModeWhisper;
            standardItem.Checked = mode == Atk.ModeStandard;
            performanceItem.Checked = mode == Atk.ModePerformance;

            string tip = string.Format(T("{0}°C · 风扇 {1}\n{2} · 睿频{3} · {4}", "{0}°C · Fan {1}\n{2} · Boost {3} · {4}"),
                                       temp, fan, ModeName(mode), boost, PolicyName(cfg.Policy));
            tray.Text = tip.Length > TooltipMaxLength ? tip.Substring(0, TooltipMaxLength) : tip;

            Color background = float.IsNaN(celsius) ? Color.DimGray
                             : celsius < WarmCelsius ? Color.FromArgb(46, 125, 50)
                             : celsius < HotCelsius ? Color.FromArgb(230, 120, 0)
                             : Color.FromArgb(198, 40, 40);
            SetIcon(temp, background);
        }

        void SetIcon(string text, Color background)
        {
            string key = text + "|" + background.ToArgb();
            if (key == iconKey) return;
            iconKey = key;
            Size size = SystemInformation.SmallIconSize;
            using (Bitmap bitmap = new Bitmap(size.Width, size.Height))
            {
                using (Graphics graphics = Graphics.FromImage(bitmap))
                using (SolidBrush fill = new SolidBrush(background))
                using (Font font = new Font("Segoe UI", size.Height * (text.Length >= 3 ? 0.5f : 0.68f), FontStyle.Bold, GraphicsUnit.Pixel))
                using (StringFormat format = new StringFormat())
                {
                    graphics.Clear(Color.Transparent);
                    graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    graphics.FillRectangle(fill, 0, 0, size.Width, size.Height);
                    format.Alignment = StringAlignment.Center;
                    format.LineAlignment = StringAlignment.Center;
                    graphics.DrawString(text, font, Brushes.White, new RectangleF(-2, 0, size.Width + 4, size.Height + 1), format);
                }
                IntPtr handle = bitmap.GetHicon();
                Icon icon = (Icon)Icon.FromHandle(handle).Clone();
                DestroyIcon(handle);
                tray.Icon = icon;
                if (currentIcon != null) currentIcon.Dispose();
                currentIcon = icon;
            }
        }

        void SetPolicy(Policy policy)
        {
            cfg.Policy = policy;
            cfg.Save(Settings.DefaultFolder);
            smart.Reset();
            Log.Write("policy -> " + policy);
            Tick();
            tray.ShowBalloonTip(BalloonMs, T("安静策略:", "Quiet policy: ") + PolicyName(policy), PolicyDescription(policy), ToolTipIcon.Info);
        }

        void SetLanguage(UiLanguage language)
        {
            cfg.Language = language;
            cfg.Save(Settings.DefaultFolder);
            chinese = UsesChinese(language);
            Log.Write("language -> " + language);
            while (menu.Items.Count > 0) menu.Items[0].Dispose();
            BuildMenu();
            Tick();
        }

        void SetFanMode(int mode)
        {
            bool ok = Atk.SetFanMode(mode);
            Log.Write("fan mode -> " + mode + (ok ? " ok" : " failed"));
            if (!ok) tray.ShowBalloonTip(BalloonMs, "QuietFan", T("切换风扇模式失败。", "Could not change the fan profile."), ToolTipIcon.Warning);
            Tick();
        }

        static string ModeName(int mode)
        {
            switch (mode)
            {
                case Atk.ModeWhisper: return T("静音模式", "Whisper");
                case Atk.ModeStandard: return T("标准模式", "Standard");
                case Atk.ModePerformance: return T("性能模式", "Performance");
                default: return T("风扇模式未知", "Fan profile n/a");
            }
        }

        static string PolicyName(Policy policy)
        {
            switch (policy)
            {
                case Policy.Quiet: return T("静音", "Quiet");
                case Policy.Full: return T("性能", "Full");
                default: return T("智能", "Smart");
            }
        }

        static string PolicyDescription(Policy policy)
        {
            switch (policy)
            {
                case Policy.Quiet: return T("始终关闭 CPU 睿频:风扇最安静,性能会下降。", "CPU boost is always off: quietest fans, lower performance.");
                case Policy.Full: return T("使用系统原本的睿频设置。", "Uses the power plan's original boost setting.");
                default: return T("平时关闭睿频保持安静;CPU 持续 20 秒高负载才打开睿频。", "Boost stays off for quiet fans and turns on after 20 s of sustained load.");
            }
        }

        static bool AutoStartEnabled()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey))
                return key != null && key.GetValue(RunValueName) != null;
        }

        void ToggleAutoStart()
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (key.GetValue(RunValueName) != null) key.DeleteValue(RunValueName, false);
                else key.SetValue(RunValueName, "\"" + Application.ExecutablePath + "\"");
            }
            autoStartItem.Checked = AutoStartEnabled();
        }

        void ShowHelp()
        {
            string text = T(
                "这台 Vivobook 的 BIOS 不开放自定义风扇曲线:CPU 一睿频,EC 就让风扇转起来,停下后还要转很久。\n\n" +
                "QuietFan 通过限制睿频减少发热,让风扇晚转、少转:\n" +
                "• 智能(推荐):平时关闭睿频;CPU 持续 20 秒平均高于 50% 才打开,30 秒平均低于 20% 再关闭。\n" +
                "• 静音:始终关闭睿频。\n" +
                "• 性能:使用电源计划原本的睿频设置。\n\n" +
                "ASUS 风扇模式和 MyASUS 里的“风扇模式”是同一个设置。退出时会恢复原来的睿频设置。\n\n" +
                "版本 {0}\n设置和日志:{1}\n{2}",
                "The BIOS of these Vivobooks has no custom fan curves: the embedded controller spins the fan up as soon as " +
                "the CPU boosts and keeps it running long after.\n\n" +
                "QuietFan limits CPU boost so the fan starts later and less often:\n" +
                "• Smart (recommended): boost off; on when the CPU averages 50% or more for 20 s, off again at 20% or less for 30 s.\n" +
                "• Quiet: boost always off.\n" +
                "• Full: the power plan's original boost setting.\n\n" +
                "The ASUS fan profile is the same setting as in MyASUS. QuietFan restores the original boost setting when it exits.\n\n" +
                "Version {0}\nSettings and log: {1}\n{2}");
            MessageBox.Show(string.Format(text, Application.ProductVersion, Settings.DefaultFolder, ProjectUrl),
                            "QuietFan", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        void OnSessionEnding(object sender, SessionEndingEventArgs e)
        {
            timer.Stop();
            RestoreBoost();
        }

        protected override void ExitThreadCore()
        {
            if (!stopped)
            {
                stopped = true;
                timer.Stop();
                SystemEvents.SessionEnding -= OnSessionEnding;
                RestoreBoost();
                tray.Visible = false;
                tray.Dispose();
                sensors.Dispose();
                exitEvent.Dispose();
                if (currentIcon != null) currentIcon.Dispose();
                Log.Write("exit");
            }
            base.ExitThreadCore();
        }
    }
}
