using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using DesktopGremlin;

internal static partial class Program
{
    private static object LifecycleField(object target, string name)
    {
        return target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }

    private static void TestWindowResourceCleanup(Application app)
    {
        File.WriteAllText(MainConfig, "ALLOW_ERROR_MESSAGES=false\nSTART_CHAR=" + Character
            + "\nSPRITE_FRAMERATE=60\nSLEEP_TIME=500\nENABLE_GRAVITY=false\nALLOW_RANDOM_ACTIONS=false\nSTART_BOTTOM=false\n");
        var main = new MainWindow();
        var animation = (DispatcherTimer)LifecycleField(LifecycleField(main, "_animationController"), "_masterTimer");
        var idle = (DispatcherTimer)LifecycleField(LifecycleField(main, "_timerController"), "_idleTimer");
        object config = LifecycleField(main, "_config");
        object tray = LifecycleField(config, "_trayIcon");
        try
        {
            Assert(animation.IsEnabled && idle.IsEnabled, "Precondition: main-window timers are running");
            main.Close();
            Assert(!animation.IsEnabled && !idle.IsEnabled, "Closing the main window must stop animation and idle timers");
        }
        finally
        {
            animation.Stop();
            idle.Stop();
            main.Close();
            ((IDisposable)tray).Dispose();
        }
    }
}
