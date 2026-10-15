using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using DesktopGremlin;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

internal static partial class Program
{
    private const int ClickThroughExStyle = -20;
    private const int TransparentStyle = 0x20;
    private const int LayeredStyle = 0x80000;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int ClickThroughStyle(IntPtr handle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetClickThroughStyle(IntPtr handle, int index, int style);

    private static void TestClickThroughStartup()
    {
        bool original = Settings.ClickThrough;
        try
        {
            foreach (bool configured in new[] { true, false })
            for (int earlyToggles = 0; earlyToggles <= 2; earlyToggles++)
                TestClickThroughWindow(configured, earlyToggles);
        }
        finally { Settings.ClickThrough = original; }
    }

    private static void TestClickThroughWindow(bool configured, int earlyToggles)
    {
        string config = "ALLOW_ERROR_MESSAGES=false\nSTART_CHAR=" + Character
            + "\nSPRITE_FRAMERATE=60\nSLEEP_TIME=500\nALLOW_RANDOM_ACTIONS=false"
            + "\nSTART_BOTTOM=false\nENABLE_GRAVITY=false\nRANDOMIZE_SPAWN=false"
            + "\nENABLE_KEYBOARD=true\nSHOW_TASKBAR=false\nCLICK_THROUGH=" + configured + "\n";
        File.WriteAllText(MainConfig, config);
        var main = new MainWindow { Left = 100, Top = 100, ShowActivated = false };
        var appConfig = (ConfigManager.AppConfig)ClickThroughField(main, "_config");
        var tray = (NotifyIcon)ClickThroughField(appConfig, "_trayIcon");
        var menu = tray.ContextMenuStrip;
        var item = menu.Items.OfType<ToolStripMenuItem>().Single(i => i.Text == "Toggle Click Through");
        bool enabled = configured;
        try
        {
            Assert(Settings.ClickThrough == configured, "Startup config loads its click-through flag");
            Assert(main.IsClickThroughEnabled == enabled && item.Checked == enabled,
                "Window state and tray checkmark start from the configured flag");
            Assert(new WindowInteropHelper(main).Handle == IntPtr.Zero,
                "Construction leaves native window creation to WPF");
            for (int i = 0; i < earlyToggles; i++)
            {
                main.ToggleClickThrough();
                enabled = !enabled;
                Assert(main.IsClickThroughEnabled == enabled && item.Checked == enabled,
                    "A pre-handle toggle updates requested state and tray checkmark");
                Assert(new WindowInteropHelper(main).Handle == IntPtr.Zero,
                    "A pre-handle toggle does not force window creation");
            }

            IntPtr handle = new WindowInteropHelper(main).EnsureHandle();
            AssertClickThrough(main, handle, item, enabled, "SourceInitialized");
            main.Show();
            Pump();
            AssertClickThrough(main, handle, item, enabled, "Shown window");

            // Keep a separate extended style bit while changing mouse pass-through.
            int before = ClickThroughStyle(handle, ClickThroughExStyle) | 0x08000000; // WS_EX_NOACTIVATE
            SetClickThroughStyle(handle, ClickThroughExStyle, before);
            main.ToggleClickThrough();
            enabled = !enabled;
            AssertClickThrough(main, handle, item, enabled, "Direct toggle");
            Assert((ClickThroughStyle(handle, ClickThroughExStyle) & ~TransparentStyle)
                == (before & ~TransparentStyle), "Toggling preserves unrelated native style bits");

            main.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(main),
                Environment.TickCount, Key.D0) { RoutedEvent = Keyboard.KeyDownEvent });
            enabled = !enabled;
            AssertClickThrough(main, handle, item, enabled, "Keyboard 0 toggle");
            item.PerformClick();
            enabled = !enabled;
            AssertClickThrough(main, handle, item, enabled, "Tray toggle");
            Pump();
            AssertClickThrough(main, handle, item, enabled, "After dispatcher processing");
            Assert(Settings.ClickThrough == configured && File.ReadAllText(MainConfig) == config,
                "Session toggles preserve the startup preference and config file");
        }
        finally
        {
            main.Close();
            tray.Dispose();
            menu.Dispose();
        }
    }

    private static object ClickThroughField(object instance, string name)
    {
        return instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(instance);
    }

    private static void AssertClickThrough(MainWindow main, IntPtr handle, ToolStripMenuItem item,
        bool enabled, string context)
    {
        int style = ClickThroughStyle(handle, ClickThroughExStyle);
        Assert(((style & TransparentStyle) != 0) == enabled,
            context + ": native WS_EX_TRANSPARENT matches the requested click-through state");
        Assert((style & LayeredStyle) != 0, context + ": window retains WS_EX_LAYERED");
        Assert(main.IsClickThroughEnabled == enabled && item.Checked == enabled,
            context + ": window state and tray checkmark stay synchronized");
    }
}
