using System;
using System.Diagnostics;
using System.IO;
using DesktopGremlin;

internal static partial class Program
{
    private static void TestCharacterStartupIntegration()
    {
        string actions = Path.Combine(Pack("Gremlins"), "Actions");
        string idle = Path.Combine(actions, "idle.png");
        string config = Path.Combine(Pack("Gremlins"), "config.txt");
        Settings.StartingChar = "SelectedBeforeProbe";
        Settings.SpriteSize = 0.7;
        Settings.FrameWidth = 17;
        StartupCharacter.ValidateIdle(Character);
        Assert(Settings.StartingChar == "SelectedBeforeProbe" && Settings.SpriteSize == 0.7 && Settings.FrameWidth == 17,
            "Random candidate validation does not change the selected character or geometry/scale");
        File.WriteAllText(idle, "corrupt PNG");
        ExpectPackFailure(() => StartupCharacter.ValidateIdle(Character), "idle.png");
        WriteSheet(idle);
        File.WriteAllText(config, "WIDTH=2\nHEIGHT=2\nCOLUMN=2\nIDLE=5\n");
        ExpectPackFailure(() => StartupCharacter.ValidateIdle(Character), "exceeds");
        File.WriteAllText(config, "WIDTH=2\nHEIGHT=2\nCOLUMN=2\nIDLE=4\nINTRO=0\nSCALE=0.8\n");
        File.Delete(idle);
        ExpectPackFailure(() => StartupCharacter.ValidateIdle(Character), "idle.png");
        WriteSheet(idle);
        StartupCharacter.ValidateIdle(Character);
        Assert(Settings.FrameWidth == 17 && Settings.SpriteSize == 0.7, "Rejected candidates also preserve global settings");

        string rejected = Character + "_BrokenStartup";
        string rejectedPath = Path.Combine(BasePath, "SpriteSheet", "Gremlins", rejected);
        try
        {
            Directory.CreateDirectory(Path.Combine(rejectedPath, "Actions"));
            File.WriteAllText(Path.Combine(rejectedPath, "config.txt"), "WIDTH=2\nHEIGHT=2\nCOLUMN=2\nIDLE=4\n");
            File.WriteAllText(Path.Combine(rejectedPath, "Actions", "idle.png"), "corrupt PNG");
            Settings.RandomCharacter = true;
            for (int seed = 0; seed < 5; seed++)
            {
                string selected = StartupCharacter.Select(new[] { "--random-character" }, new Random(seed));
                Assert(selected != rejected, "Actual random startup skips the corrupt installed pack");
                StartupCharacter.ValidateIdle(selected);
                Assert(Settings.StartingChar == "SelectedBeforeProbe" && Settings.FrameWidth == 17 && Settings.SpriteSize == 0.7,
                    "Actual random selection preserves settings until the chosen pack is loaded");
            }
        }
        finally { if (Directory.Exists(rejectedPath)) Directory.Delete(rejectedPath, true); }

        string main = "ALLOW_ERROR_MESSAGES=false\nSTART_CHAR=Missing_Config\nRANDOM_CHARACTER=true\nSPRITE_FRAMERATE=60\n";
        File.WriteAllText(MainConfig, main);
        var window = new MainWindow(new[] { "--character", Character });
        Assert(Settings.StartingChar == Character && Settings.FrameWidth == 2 && Settings.SpriteSize == 0.8,
            "MainWindow honors explicit choice above random config and loads its real metadata");
        Assert(window.RestartArguments == "--character \"" + Character + "\"", "Tray restart retains the explicit startup selection");
        Assert(File.ReadAllText(MainConfig) == main, "Startup selection never rewrites main config");
        window.Close();

        // Launch the real WPF entry point, not just the policy/helper. If e.Args
        // are ignored, these would start the valid configured pack and hang.
        File.WriteAllText(MainConfig, "ALLOW_ERROR_MESSAGES=false\nSTART_CHAR=" + Character + "\nRANDOM_CHARACTER=false\n");
        foreach (string args in new[] { "--unknown", "--character Missing_CommandLine_" + Character })
        {
            using (var process = Process.Start(new ProcessStartInfo(Path.Combine(BasePath, "DesktopGremlin.exe"), args)
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetTempPath()
            }))
            {
                if (!process.WaitForExit(15000))
                {
                    process.Kill();
                    throw new Exception("Invalid startup arguments must exit promptly through the real WPF entry point.");
                }
                Assert(process.ExitCode == 1, "Invalid explicit/unknown argument fails cleanly without silent fallback");
            }
        }
        string diagnostics = File.ReadAllText(PackDiagnostics.LogPath);
        Assert(diagnostics.Contains("Unknown startup argument: --unknown")
            && diagnostics.Contains("Missing_CommandLine_" + Character), "Real app arguments reach selection and diagnostics outside the install working directory");
        Settings.RandomCharacter = false;
    }
}
