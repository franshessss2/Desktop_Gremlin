using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DesktopGremlin;
using Forms = System.Windows.Forms;

internal static partial class Program
{
    private static readonly Color[] HotspotPalette = { Colors.Red, Colors.Blue, Colors.Purple, Colors.Yellow, Colors.Orange };

    private static void TestHotspotDisableRegression()
    {
        bool oldDisabled = Settings.DisableHotspots;
        bool oldColored = Settings.AllowColoredHotSpot;
        try
        {
            Settings.DisableHotspots = false;
            Settings.AllowColoredHotSpot = false;
            var regions = NewHotspotRegions();
            var controller = NewHotspotController(regions);
            controller.ToggleHotspots();
            for (int i = 0; i < regions.Length; i++)
                Assert(!regions[i].IsEnabled, "Disable Hotspots must disable all five regions; still active: region " + i);
            TestHotspotCombinations();
            TestHotspotTrayIntegration();
            Console.WriteLine("PASS hotspot configuration, WPF hit testing and tray synchronization");
        }
        finally
        {
            Settings.DisableHotspots = oldDisabled;
            Settings.AllowColoredHotSpot = oldColored;
        }
    }

    private static Border[] NewHotspotRegions()
    {
        return Enumerable.Range(0, 5).Select(i => new Border
        {
            Name = "Region" + i, Width = 40, Height = 40, Margin = new Thickness(i * 50, 0, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top
        }).ToArray();
    }

    private static HotspotController NewHotspotController(Border[] regions)
    {
        return new HotspotController(null, regions[0], regions[1], regions[2], regions[3], regions[4]);
    }

    private static void TestHotspotCombinations()
    {
        foreach (bool disabled in new[] { false, true })
        foreach (bool colored in new[] { false, true })
        {
            Settings.DisableHotspots = disabled;
            Settings.AllowColoredHotSpot = colored;
            Border[] regions = NewHotspotRegions();
            var surface = new Border { Name = "SpriteSurface", Background = Brushes.White };
            var grid = new Grid { Width = 250, Height = 50 };
            grid.Children.Add(surface);
            foreach (Border region in regions) grid.Children.Add(region);
            var controller = NewHotspotController(regions);
            var host = new Window
            {
                Content = grid, Width = 250, Height = 50, WindowStyle = WindowStyle.None,
                ShowInTaskbar = false, ShowActivated = false, Opacity = 0
            };
            try
            {
                host.Show();
                AssertHotspotState(grid, surface, regions, disabled, colored);
                controller.ToggleHotspots();
                AssertHotspotState(grid, surface, regions, !disabled, colored);
                controller.ShowHotspots();
                AssertHotspotState(grid, surface, regions, !disabled, !colored);
                controller.ToggleHotspots();
                AssertHotspotState(grid, surface, regions, disabled, !colored);
                controller.ShowHotspots();
                AssertHotspotState(grid, surface, regions, disabled, colored);
            }
            finally { host.Close(); }
        }
    }

    private static void AssertHotspotState(Grid grid, Border surface, Border[] regions, bool disabled, bool colored)
    {
        // Background changes invalidate WPF rendering/layout. Complete the same
        // layout pass a displayed window performs before querying its visual hit tree.
        grid.UpdateLayout();
        for (int i = 0; i < regions.Length; i++)
        {
            Assert(regions[i].IsEnabled == !disabled, "Every region has the intended enabled state");
            Assert(regions[i].IsHitTestVisible == !disabled, "Disabled regions cannot intercept underlying sprite input");
            Color expected = disabled ? Colors.Transparent : colored ? HotspotPalette[i] : Color.FromArgb(1, 0, 0, 0);
            Assert(((SolidColorBrush)regions[i].Background).Color == expected, "Configured and toggled colors use one palette; disabled overlays remain clear");
            var hit = grid.InputHitTest(new Point(i * 50 + 20, 20)) as FrameworkElement;
            Assert(ReferenceEquals(hit, disabled ? surface : regions[i]),
                "WPF input target: disabled=" + disabled + ", colored=" + colored + ", region=" + i
                + ", hit=" + (hit == null ? "null" : hit.Name));
        }
        Assert(Settings.DisableHotspots == disabled && Settings.AllowColoredHotSpot == colored,
            "Runtime flags match the actual controller state and saved color preference");
    }

    private static void TestHotspotTrayIntegration()
    {
        foreach (bool disabled in new[] { false, true })
        foreach (bool colored in new[] { false, true })
        {
            string config = "ALLOW_ERROR_MESSAGES=false\nSTART_CHAR=" + Character
                + "\nSPRITE_FRAMERATE=60\nSLEEP_TIME=500\nSTART_BOTTOM=false\nALLOW_RANDOM_ACTIONS=false\nENABLE_GRAVITY=false\n"
                + "DISABLE_HOTSPOTS=" + disabled + "\nALLOW_COLOR_HOTSPOT=" + colored + "\n";
            File.WriteAllText(MainConfig, config);
            var main = new MainWindow();
            // Inspect the real native tray menu without exposing test-only app APIs.
            var appConfig = typeof(MainWindow).GetField("_config", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(main);
            var tray = (Forms.NotifyIcon)typeof(ConfigManager.AppConfig).GetField("_trayIcon", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(appConfig);
            try
            {
                var disableItem = tray.ContextMenuStrip.Items.OfType<Forms.ToolStripMenuItem>().Single(item => item.Text == "Disable Hotspots");
                var showItem = tray.ContextMenuStrip.Items.OfType<Forms.ToolStripMenuItem>().Single(item => item.Text == "Show Hotspots");
                AssertTrayState(main, disableItem, showItem, disabled, colored);
                main.HotSpot(); // The same entry point used by the R keyboard binding.
                AssertTrayState(main, disableItem, showItem, !disabled, colored);
                disableItem.PerformClick();
                AssertTrayState(main, disableItem, showItem, disabled, colored);
                if (disabled)
                {
                    showItem.PerformClick();
                    AssertTrayState(main, disableItem, showItem, true, colored);
                    main.HotSpot();
                }
                showItem.PerformClick();
                AssertTrayState(main, disableItem, showItem, false, !colored);
                Assert(File.ReadAllText(MainConfig) == config, "Tray/keyboard hotspot changes never rewrite saved configuration");
            }
            finally { main.Close(); tray.Dispose(); }
        }
    }

    private static void AssertTrayState(MainWindow main, Forms.ToolStripMenuItem disableItem,
        Forms.ToolStripMenuItem showItem, bool disabled, bool colored)
    {
        Assert(main.HotspotsDisabled == disabled && main.HotspotColorsRequested == colored,
            "Main-window state tracks the requested toggle");
        Assert(disableItem.Checked == disabled && showItem.Checked == colored && showItem.Enabled == !disabled,
            "Tray checkmarks and color-command availability match config, keyboard and menu changes");
        foreach (string name in new[] { "LeftHotspot", "RightHotspot", "TopHotspot", "LeftDownHotspot", "RightDownHotspot" })
        {
            var region = (Border)main.FindName(name);
            Assert(region.IsEnabled == !disabled && region.IsHitTestVisible == !disabled,
                "Actual main-window region honors the shared state: " + name);
        }
    }
}
