using System.Windows.Controls;
using System.Windows.Media;

namespace DesktopGremlin
{
    public class HotspotController
    {
        private readonly Border[] _regions;
        private static readonly Brush[] Palette =
        {
            Brushes.Red, Brushes.Blue, Brushes.Purple, Brushes.Yellow, Brushes.Orange
        };
        private static readonly Brush Uncolored = CreateUncoloredBrush();

        public bool IsDisabled { get; private set; }
        public bool ColorsRequested { get; private set; }

        public HotspotController(MainWindow window, Border leftHotspot, Border rightHotspot,
            Border topHotspot, Border leftDownHotspot, Border rightDownHotspot)
        {
            _regions = new[] { leftHotspot, rightHotspot, topHotspot, leftDownHotspot, rightDownHotspot };
            IsDisabled = Settings.DisableHotspots;
            ColorsRequested = Settings.AllowColoredHotSpot;
            ApplyState();
        }

        public void ToggleHotspots()
        {
            IsDisabled = !IsDisabled;
            Settings.DisableHotspots = IsDisabled;
            ApplyState();
        }

        public void ShowHotspots()
        {
            ColorsRequested = !ColorsRequested;
            Settings.AllowColoredHotSpot = ColorsRequested;
            ApplyState();
        }

        private void ApplyState()
        {
            for (int i = 0; i < _regions.Length; i++)
            {
                Border region = _regions[i];
                if (region == null) continue;
                region.IsEnabled = !IsDisabled;
                // Disabled overlays must let the underlying sprite receive input.
                region.IsHitTestVisible = !IsDisabled;
                region.Background = IsDisabled ? Brushes.Transparent
                    : ColorsRequested ? Palette[i] : Uncolored;
            }
        }

        private static Brush CreateUncoloredBrush()
        {
            // Retain the existing almost-transparent hit surface while enabled.
            var brush = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
            brush.Freeze();
            return brush;
        }
    }
}
