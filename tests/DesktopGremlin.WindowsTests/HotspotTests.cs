using System.Windows.Controls;
using DesktopGremlin;

internal static partial class Program
{
    private static void TestHotspotDisableRegression()
    {
        Settings.DisableHotspots = false;
        Settings.AllowColoredHotSpot = false;
        var regions = new[] { new Border(), new Border(), new Border(), new Border(), new Border() };
        var controller = new HotspotController(null, regions[0], regions[1], regions[2], regions[3], regions[4]);
        controller.ToggleHotspots();
        for (int i = 0; i < regions.Length; i++)
            Assert(!regions[i].IsEnabled, "Disable Hotspots must disable all five regions; still active: region " + i);
    }
}
