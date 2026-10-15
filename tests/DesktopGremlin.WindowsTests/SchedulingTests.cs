using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DesktopGremlin;

internal static partial class Program
{
    private static object PrivateField(object target, string name)
    {
        return target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }

    private static void TestSchedulingIntegration()
    {
        Settings.SleepTime = 500;
        Settings.FrameRate = 60;
        Settings.RandomMinInterval = 30;
        Settings.RandomMaxInterval = 40;
        string content = "ALLOW_ERROR_MESSAGES=false\nSTART_CHAR=" + Character
            + "\nSTART_BOTTOM=false\nRANDOMIZE_SPAWN=true\nSPAWN_DISTANCE=2147483647\n"
            + "MIN_INTERVAL=90\nMAX_INTERVAL=30\nSLEEP_TIME=-1\nSPRITE_FRAMERATE=2147483647\n"
            + "ENABLE_KEYBOARD=false\nENABLE_GRAVITY=false\nALLOW_RANDOM_ACTIONS=false\n";
        File.WriteAllText(MainConfig, content);
        var main = new MainWindow();
        var timerController = (TimerController)PrivateField(main, "_timerController");
        var animation = (AnimationController)PrivateField(main, "_animationController");
        try
        {
            Assert(Settings.SleepTime == 500 && Settings.FrameRate == 60, "Real startup retains safe sleep/frame rate after invalid input");
            Assert(((DispatcherTimer)PrivateField(timerController, "_idleTimer")).Interval == TimeSpan.FromSeconds(500),
                "Real .NET Framework idle timer accepts the recovered interval");
            Assert(((DispatcherTimer)PrivateField(animation, "_masterTimer")).Interval.TotalMilliseconds >= 1,
                "Real animation timer cannot become a zero-delay dispatcher loop");
            Rect area = SystemParameters.WorkArea;
            Assert(main.Left >= area.Left && main.Left <= area.Right - main.Width
                && main.Top >= area.Top && main.Top <= area.Bottom - main.Height,
                "Largest configured spawn radius does not overflow and still clamps to the actual work area");

            var states = (AnimationStates)PrivateField(main, "_gremlinState");
            states.UnlockState();
            states.ResetAllExceptIdle();
            Settings.AllowRandomness = true;
            typeof(AnimationController).GetField("_rng", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(animation, new ClickOnlyRandom());
            var handle = typeof(AnimationController).GetMethod("HandleRandomActions", BindingFlags.Instance | BindingFlags.NonPublic);
            handle.Invoke(animation, null);
            AssertDeadline(animation, "Entering idle safely schedules a reversed configured interval");
            typeof(AnimationController).GetField("_nextRandomActionTime", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(animation, DateTime.Now.AddSeconds(-1));
            handle.Invoke(animation, null);
            Assert(states.GetState("Click"), "Production scheduler executes the controlled random action");
            AssertDeadline(animation, "After-action scheduling uses the recovered bounds too");
            Assert(File.ReadAllText(MainConfig) == content, "Recovery does not rewrite the saved config");
        }
        finally
        {
            Settings.AllowRandomness = false;
            animation.Stop();
            ((DispatcherTimer)PrivateField(timerController, "_idleTimer")).Stop();
            ((DispatcherTimer)PrivateField(timerController, "_gravityTimer")).Stop();
            main.Close();
            var appConfig = PrivateField(main, "_config");
            ((IDisposable)PrivateField(appConfig, "_trayIcon")).Dispose();
        }

        Settings.SleepTime = Settings.MaximumSleepSeconds;
        Settings.FrameRate = Settings.MaximumFrameRate;
        var maxTimer = new TimerController(null, new AnimationStates());
        Assert(((DispatcherTimer)PrivateField(maxTimer, "_idleTimer")).Interval.TotalMilliseconds <= int.MaxValue,
            "Largest allowed sleep delay is accepted by the actual .NET Framework WPF timer");
        var fastestAnimation = new AnimationController(null, new AnimationStates(), new CurrentFrames(),
            new FrameCounts(), new Image(), new Random());
        Assert(((DispatcherTimer)PrivateField(fastestAnimation, "_masterTimer")).Interval == TimeSpan.FromMilliseconds(1),
            "Highest allowed frame rate stays at one millisecond on .NET Framework");
        Settings.SleepTime = 500;
        Settings.FrameRate = 60;
        Settings.RandomMinInterval = 30;
        Settings.RandomMaxInterval = 40;
        Settings.SpawnDistance = 0;
        Settings.RandomizeSpawn = false;
        Console.WriteLine("PASS real WPF scheduling limits and spawn recovery");
    }

    private static void AssertDeadline(AnimationController controller, string message)
    {
        double seconds = ((DateTime)PrivateField(controller, "_nextRandomActionTime") - DateTime.Now).TotalSeconds;
        Assert(seconds > 85 && seconds <= 90, message);
    }

    private sealed class ClickOnlyRandom : Random
    {
        public override int Next(int minValue, int maxValue)
        {
            return minValue == 0 && maxValue == 4 ? 0 : base.Next(minValue, maxValue);
        }
    }
}
