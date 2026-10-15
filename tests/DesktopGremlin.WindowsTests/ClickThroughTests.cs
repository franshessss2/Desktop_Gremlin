using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using DesktopGremlin;

internal static partial class Program
{
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int ClickThroughStyle(IntPtr handle, int index);

    private static void TestClickThroughStartup()
    {
        File.WriteAllText(MainConfig, "ALLOW_ERROR_MESSAGES=false\nSTART_CHAR=" + Character
            + "\nSPRITE_FRAMERATE=60\nSLEEP_TIME=500\nALLOW_RANDOM_ACTIONS=false"
            + "\nSTART_BOTTOM=false\nCLICK_THROUGH=true\n");
        var main = new MainWindow { ShowActivated = false };
        try
        {
            IntPtr handle = new WindowInteropHelper(main).EnsureHandle();
            Assert((ClickThroughStyle(handle, -20) & 0x20) != 0,
                "CLICK_THROUGH=true must set WS_EX_TRANSPARENT when the HWND is created");
        }
        finally { main.Close(); }
    }
}
