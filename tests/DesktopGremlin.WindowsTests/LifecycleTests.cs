using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Input;
using DesktopGremlin.Quirks.Companion;
using Forms = System.Windows.Forms;
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
        TestActiveWindowCleanup(app, false);
        TestActiveWindowCleanup(app, true);
        Console.WriteLine("PASS main-window timer, input, helper-window and tray cleanup");
    }

    private static void TestActiveWindowCleanup(Application app, bool closeHelpersFirst)
    {
        string previousFood = Settings.FoodSpawn;
        bool previousGravity = Settings.EnableGravity;
        bool previousKeyboard = Settings.AllowKeyboard;
        string characterConfig = Path.Combine(Pack("Gremlins"), "config.txt");
        byte[] original = File.ReadAllBytes(characterConfig);
        string foodName = "Lifecycle_" + Guid.NewGuid().ToString("N") + ".png";
        string foodPath = Path.Combine(BasePath, "SpriteSheet", "Misc", foodName);
        Directory.CreateDirectory(Path.GetDirectoryName(foodPath));
        WriteSheet(foodPath);
        File.WriteAllText(characterConfig, "WIDTH=4\nHEIGHT=4\nCOLUMN=1\nIDLE=1\nINTRO=0\n");
        string config = "ALLOW_ERROR_MESSAGES=false\nSTART_CHAR=" + Character + "\nCOMPANION_CHAR=" + Character
            + "\nSPRITE_FRAMERATE=60\nSLEEP_TIME=500\nENABLE_GRAVITY=true\nENABLE_KEYBOARD=true\n"
            + "ALLOW_RANDOM_ACTIONS=false\nSTART_BOTTOM=false\nRANDOM_MOVE_DISTANCE=80\nWALK_DISTANCE=200\nFOOD_SPAWN=" + foodName + "\n";
        File.WriteAllText(MainConfig, config);
        MainWindow main = null;
        Companion companion = null;
        Target food = null;
        Forms.NotifyIcon tray = null;
        var timers = new List<DispatcherTimer>();
        try
        {
            main = new MainWindow { Left = 100, Top = 100, Opacity = 0, ShowActivated = false };
            app.MainWindow = main;
            main.Show();
            var source = PresentationSource.FromVisual(main);
            var animation = LifecycleField(main, "_animationController");
            var timing = LifecycleField(main, "_timerController");
            var keyboard = (KeyboardController)LifecycleField(main, "_keyboardController");
            var follower = LifecycleField(main, "_foodFollower");
            var appConfig = LifecycleField(main, "_config");
            tray = (Forms.NotifyIcon)LifecycleField(appConfig, "_trayIcon");
            var menu = tray.ContextMenuStrip;
            int trayDisposals = 0;
            tray.Disposed += (sender, args) => trayDisposals++;
            timers.Add((DispatcherTimer)LifecycleField(animation, "_masterTimer"));
            timers.Add((DispatcherTimer)LifecycleField(timing, "_idleTimer"));
            timers.Add((DispatcherTimer)LifecycleField(timing, "_gravityTimer"));
            main.ToggleCompanion();
            companion = (Companion)LifecycleField(main, "_companionInstance");
            timers.Add((DispatcherTimer)LifecycleField(LifecycleField(companion, "_animationController"), "_masterTimer"));
            timers.Add((DispatcherTimer)LifecycleField(LifecycleField(companion, "_followController"), "_gravityTimer"));
            main.TriggerRandomMove();
            timers.Add((DispatcherTimer)LifecycleField(LifecycleField(main, "_movementController"), "_activeRandomMoveTimer"));
            keyboard.EnableKeyboardMovement();
            typeof(KeyboardController).GetMethod("HandleMovementKeyDown", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(keyboard, new object[] { Key.Right });
            timers.Add((DispatcherTimer)LifecycleField(keyboard, "_keyboardMoveTimer"));
            main.TriggerFood();
            food = (Target)LifecycleField(main, "_currentFood");
            timers.Add((DispatcherTimer)LifecycleField(follower, "_followTimer"));
            foreach (DispatcherTimer timer in timers) Assert(timer.IsEnabled, "Precondition: every exercised resource timer is running");
            Assert(companion.IsVisible && food.IsVisible && tray.Visible && keyboard.IsKeyboardMoving,
                "Precondition: helper windows, tray and keyboard movement are active");
            if (closeHelpersFirst) { companion.Close(); food.Close(); }
            main.Close();
            foreach (DispatcherTimer timer in timers) Assert(!timer.IsEnabled, "Closing the main window stops every controller timer");
            Assert(!app.Windows.Cast<Window>().Contains(companion) && !app.Windows.Cast<Window>().Contains(food),
                "Companion and food windows do not outlive the closed main window");
            Assert(LifecycleField(main, "_companionInstance") == null && LifecycleField(main, "_currentFood") == null
                && LifecycleField(follower, "_currentFood") == null, "Helper references are released");
            Assert(!keyboard.IsKeyboardMoving && !tray.Visible && trayDisposals == 1 && menu.IsDisposed,
                "Keyboard movement resets and native tray/menu resources are disposed once");
            ((IDisposable)appConfig).Dispose();
            keyboard.Dispose();
            Assert(trayDisposals == 1, "Repeated cleanup does not dispose the tray twice");
            main.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Space) { RoutedEvent = Keyboard.KeyDownEvent });
            Assert(!((DispatcherTimer)LifecycleField(timing, "_idleTimer")).IsEnabled,
                "Detached keyboard handlers cannot restart the idle timer after close");
            double left = main.Left, top = main.Top;
            Pump();
            Assert(main.Left == left && main.Top == top && timers.All(timer => !timer.IsEnabled),
                "Dispatcher pumping after close does not continue moving or animating the old window");
            Assert(!app.Dispatcher.HasShutdownStarted, "Cleanup respects the host application's explicit shutdown policy");
            Assert(File.ReadAllText(MainConfig) == config, "Cleanup never rewrites saved settings");
        }
        finally
        {
            foreach (DispatcherTimer timer in timers) timer.Stop();
            if (food != null && food.IsVisible) food.Close();
            if (companion != null && companion.IsVisible) companion.Close();
            main?.Close();
            tray?.Dispose();
            File.WriteAllBytes(characterConfig, original);
            // WPF's URI image cache can retain this fixture until process exit.
            try { File.Delete(foodPath); } catch (IOException) { }
            Settings.FoodSpawn = previousFood;
            Settings.EnableGravity = previousGravity;
            Settings.AllowKeyboard = previousKeyboard;
        }
    }

}
