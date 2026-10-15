using System;
using System.Collections.Generic;
using System.IO;

internal static class StartupCharacter
{
    public static string Select(string[] arguments, Random random)
    {
        string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SpriteSheet", "Gremlins");
        return CharacterSelection.Select(Settings.StartingChar, Settings.RandomCharacter, arguments,
            root, random, ValidateIdle, message => PackDiagnostics.ReportOnce(message));
    }

    internal static void ValidateIdle(string character)
    {
        string path = Path.Combine(CharacterPack.GetDirectory("Gremlins", character), "config.txt");
        string[] lines = CharacterPack.ReadConfig(path, false);
        var values = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (string line in lines)
        {
            string[] parts = line.Split('=');
            if (parts.Length == 2 && ConfigNumbers.TryParseInt(parts[1].Trim(), out int value))
                values[parts[0].Trim()] = value;
        }
        // Use a private store so probing a broken candidate neither changes the
        // selected character/scales nor contaminates the active renderer's cache.
        var store = new SpriteSheetStore("Gremlins", character, values["WIDTH"], values["HEIGHT"], values["COLUMN"]);
        store.InitializeIdle(values["IDLE"]);
    }
}
