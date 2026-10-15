using System;
using System.Collections.Generic;
using System.IO;

internal static partial class Program
{
    private static void TestCharacterStartup()
    {
        string root = Path.Combine(Path.GetTempPath(), "GremlinSelection_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var calls = new List<string>();
        var reports = new List<string>();
        Action<string> validate = name => { calls.Add(name); if (name != "Cafe" && name != "Rice Shower") throw new CharacterPackException("broken idle"); };
        Func<string[], bool, string> select = (args, random) => CharacterSelection.Select("Configured", random, args, root, new Random(7), validate, reports.Add);
        try
        {
            Assert(select(new string[0], false) == "Configured", "Default startup keeps START_CHAR");
            Assert(select(new[] { "--character", "Cafe" }, true) == "Cafe", "Explicit character overrides random config");
            Assert(select(new[] { "--random-character", "--character", "Rice Shower" }, false) == "Rice Shower", "Explicit choice wins over random argument");
            Assert(select(new[] { "--character", "カフェ" }, false) == "カフェ", "Unicode folder names remain supported");
            Assert(calls.Count == 0, "Nonrandom choices use the normal startup loader without probing");
            Assert(CharacterSelection.RestartArguments(new[] { "--character", "Rice Shower" }) == "--character \"Rice Shower\"", "Restart preserves quoted explicit choice");
            Assert(CharacterSelection.RestartArguments(new[] { "--random-character" }) == "--random-character", "Restart preserves random startup");
            Assert(CharacterSelection.RestartArguments(new string[0]) == "", "Default restart leaves config policy intact");
            foreach (string[] args in new[] {
                new[] { "--character" }, new[] { "--character", "" }, new[] { "--other" },
                new[] { "--character", "Cafe", "--character", "Cafe" },
                new[] { "--random-character", "--random-character" }, new[] { "--character", "--random-character" },
                new[] { "--character", "../Cafe" }, new[] { "--character", "C:\\Cafe" },
                new[] { "--character", ".." }, new[] { "--character", "Cafe\"" }, new[] { "--character", "Cafe\n" } })
                ExpectPackFailure(() => select(args, false), "Usage:");
            ExpectPackFailure(() => select(new string[0], true), "No usable character");
            Directory.CreateDirectory(Path.Combine(root, "Broken"));
            ExpectPackFailure(() => select(new[] { "--random-character" }, false), "No usable character");
            Assert(calls.Count == 1 && reports.Count == 1, "Each broken candidate is tried and reported once");
            Directory.CreateDirectory(Path.Combine(root, "Cafe"));
            Directory.CreateDirectory(Path.Combine(root, "Rice Shower"));
            var selected = new HashSet<string>();
            for (int seed = 0; seed < 100; seed++)
            {
                calls.Clear();
                string result = CharacterSelection.Select("Missing", true, new string[0], root, new Random(seed), validate, reports.Add);
                Assert(result == "Cafe" || result == "Rice Shower", "Random startup chooses only a validated installed main pack");
                Assert(new HashSet<string>(calls).Count == calls.Count, "Random selection cannot retry the same failed pack");
                selected.Add(result);
            }
            Assert(selected.Count == 2, "Random startup can reach every usable candidate");
            File.WriteAllText(MasterPath, "RANDOM_CHARACTER=true\nSTART_CHAR=Cafe\n");
            ConfigSettings.LoadMaster(MasterPath);
            Assert(Settings.RandomCharacter && Settings.StartingChar == "Cafe", "Random config does not overwrite stored character");
            File.WriteAllText(MasterPath, "RANDOM_CHARACTER=garbage\n");
            ConfigSettings.LoadMaster(MasterPath);
            Assert(Settings.RandomCharacter, "Invalid random setting preserves previous value");
            File.WriteAllText(MasterPath, "random_character=false\n");
            ConfigSettings.LoadMaster(MasterPath);
            Assert(!Settings.RandomCharacter, "Random startup is opt-in and supports invariant key casing");
            ExpectPackFailure(() => CharacterSelection.Select("Cafe", true, new string[0], root + "_missing", new Random(), validate, reports.Add), "Cannot find installed characters");
        }
        finally { Directory.Delete(root, true); Settings.RandomCharacter = false; }
    }
}
