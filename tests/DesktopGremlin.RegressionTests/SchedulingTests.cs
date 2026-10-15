using System;
using System.IO;

internal static partial class Program
{
    private static readonly int InitialMinInterval = Settings.RandomMinInterval;
    private static readonly int InitialMaxInterval = Settings.RandomMaxInterval;
    private static readonly int InitialSleepTime = Settings.SleepTime;

    private static void TestSchedulingConfig()
    {
        Settings.RandomMinInterval = 30;
        Settings.RandomMaxInterval = 40;
        foreach (string content in new[] { "MIN_INTERVAL=90\nMAX_INTERVAL=30\n", "MAX_INTERVAL=30\nMIN_INTERVAL=90\n" })
        {
            File.WriteAllText(MasterPath, content);
            for (int load = 0; load < 2; load++)
            {
                ConfigSettings.LoadMaster(MasterPath);
                // This exact call previously threw in the production action scheduler.
                Equal(90, new Random(1).Next(Settings.RandomMinInterval, Settings.RandomMaxInterval),
                    "Reversed action bounds resolve to a stable fixed interval in either file order");
            }
            Assert(File.ReadAllText(MasterPath) == content, "Range recovery never rewrites the user's file");
        }
        Assert(InitialMinInterval == 30 && InitialMaxInterval == 40 && InitialSleepTime == 500,
            "Omitted scheduling settings start with safe defaults matching the bundled config");
        Settings.RandomMinInterval = 7;
        Settings.RandomMaxInterval = 11;
        Settings.SleepTime = 99;
        Settings.FrameRate = 60;
        Settings.SpawnDistance = 20;
        Settings.MoveDistance = 120;
        foreach (string bad in new[] { "-1", "0", "2147483648", "not-a-number" })
        {
            File.WriteAllText(MasterPath, "MIN_INTERVAL=" + bad + "\nMAX_INTERVAL=" + bad
                + "\nSLEEP_TIME=" + bad + "\nSPRITE_FRAMERATE=" + bad + "\n");
            ConfigSettings.LoadMaster(MasterPath);
            Assert(Settings.RandomMinInterval == 7 && Settings.RandomMaxInterval == 11,
                "Invalid action intervals retain the previous safe range");
            Assert(Settings.SleepTime == 99 && Settings.FrameRate == 60, "Invalid timer inputs retain the previous safe values");
        }
        File.WriteAllText(MasterPath, "SLEEP_TIME=" + (Settings.MaximumSleepSeconds + 1)
            + "\nSPRITE_FRAMERATE=" + (Settings.MaximumFrameRate + 1)
            + "\nSPAWN_DISTANCE=-1\nRANDOM_MOVE_DISTANCE=-1\n");
        ConfigSettings.LoadMaster(MasterPath);
        Assert(Settings.SleepTime == 99 && Settings.FrameRate == 60, "Reject values beyond WPF timer limits");
        Assert(Settings.SpawnDistance == 20 && Settings.MoveDistance == 120, "Reject negative movement/spawn distances");
        File.WriteAllText(MasterPath, "SLEEP_TIME=" + Settings.MaximumSleepSeconds + "\nSPRITE_FRAMERATE=1000\n"
            + "MIN_INTERVAL=2147483647\nMAX_INTERVAL=2147483647\nSPAWN_DISTANCE=2147483647\nRANDOM_MOVE_DISTANCE=0\n");
        ConfigSettings.LoadMaster(MasterPath);
        Assert(Settings.SleepTime == Settings.MaximumSleepSeconds && Settings.FrameRate == 1000, "Accept the safe WPF timer boundaries");
        Assert(TimeSpan.FromSeconds(Settings.SleepTime).TotalMilliseconds <= int.MaxValue
            && TimeSpan.FromMilliseconds(1000.0 / Settings.FrameRate).TotalMilliseconds >= 1, "Timer intervals remain representable and nonzero");
        int interval = new Random(2).Next(Settings.RandomMinInterval, Settings.RandomMaxInterval);
        Assert(interval == int.MaxValue && DateTime.Now.AddSeconds(interval) > DateTime.Now, "Largest fixed random delay remains a valid deadline");
        Assert(Settings.MoveDistance == 0, "Zero movement distance remains supported");
        foreach (int radius in new[] { 0, 1, 20, int.MaxValue - 1, int.MaxValue })
        {
            var random = new Random(3);
            for (int sample = 0; sample < 10; sample++)
            {
                int offset = RandomRanges.NextOffset(random, radius);
                Assert(offset >= -radius && offset <= radius, "Spawn offsets remain in range without bound overflow");
            }
        }
        Equal(-int.MaxValue, RandomRanges.NextOffset(new EdgeRandom(0), int.MaxValue), "Largest radius can reach its negative endpoint");
        Equal(int.MaxValue, RandomRanges.NextOffset(new EdgeRandom(1.0 - 1.0 / 9007199254740992.0), int.MaxValue), "Largest radius can reach its positive endpoint");
        File.WriteAllText(MasterPath, "MIN_INTERVAL=7\nMAX_INTERVAL=11\nSLEEP_TIME=500\nSPRITE_FRAMERATE=60\nSPAWN_DISTANCE=0\n");
        ConfigSettings.LoadMaster(MasterPath);
        for (int seed = 0; seed < 10; seed++)
        {
            int result = new Random(seed).Next(Settings.RandomMinInterval, Settings.RandomMaxInterval);
            Assert(result >= 7 && result < 11, "Valid random intervals retain the existing exclusive maximum semantics");
        }
        Assert(Settings.SpawnDistance == 0 && RandomRanges.NextOffset(new Random(), 0) == 0,
            "Zero spawn radius still places the character at center");
    }

    private sealed class EdgeRandom : Random
    {
        private readonly double _sample;
        public EdgeRandom(double sample) { _sample = sample; }
        public override double NextDouble() { return _sample; }
    }
}
