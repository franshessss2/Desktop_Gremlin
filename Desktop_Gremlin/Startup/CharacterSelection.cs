using System;
using System.Collections.Generic;
using System.IO;

// Portable startup policy. Actual PNG validation is supplied by the WPF host.
public static class CharacterSelection
{
    public static string Select(string configured, bool randomize, string[] arguments,
        string packRoot, Random random, Action<string> validate, Action<string> report)
    {
        Parse(arguments, out string requested, out bool randomArgument);
        if (requested != null) return requested;
        if (!randomize && !randomArgument) return RequireFolderName(configured);

        string[] directories;
        try { directories = Directory.GetDirectories(packRoot); }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            throw new CharacterPackException("Cannot find installed characters: " + packRoot + "\n" + exception.Message);
        }
        var candidates = new List<string>();
        foreach (string directory in directories) candidates.Add(Path.GetFileName(directory));
        // Shuffle once, then try each candidate at most once. Broken packs cannot
        // cause a retry loop or permanently bias selection toward sorted names.
        candidates.Sort(StringComparer.Ordinal);
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            string temporary = candidates[i];
            candidates[i] = candidates[j];
            candidates[j] = temporary;
        }
        foreach (string candidate in candidates)
        {
            try
            {
                RequireFolderName(candidate);
                validate(candidate);
                return candidate;
            }
            catch (CharacterPackException exception)
            {
                report("Skipped random character '" + candidate + "': " + exception.Message);
            }
        }
        throw new CharacterPackException("No usable character packs found in " + packRoot
            + ". Install a complete pack containing config.txt and Actions/idle.png."
            + " Random startup does not select companion packs.");
    }

    public static string RestartArguments(string[] arguments)
    {
        Parse(arguments, out string requested, out bool random);
        if (requested != null) return "--character \"" + requested + "\"";
        return random ? "--random-character" : "";
    }

    private static void Parse(string[] arguments, out string character, out bool random)
    {
        character = null;
        random = false;
        for (int i = 0; i < arguments.Length; i++)
        {
            if (arguments[i] == "--character")
            {
                if (character != null || i + 1 >= arguments.Length || arguments[i + 1].StartsWith("--", StringComparison.Ordinal))
                    throw Usage("--character requires one installed folder name and may appear only once.");
                character = RequireFolderName(arguments[++i]);
            }
            else if (arguments[i] == "--random-character")
            {
                if (random) throw Usage("--random-character may appear only once.");
                random = true;
            }
            else throw Usage("Unknown startup argument: " + arguments[i]);
        }
    }

    private static string RequireFolderName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "." || value == ".."
            || value != value.Trim() || value.EndsWith(".", StringComparison.Ordinal)
            || value.IndexOfAny("<>:\"/\\|?*".ToCharArray()) >= 0)
            throw Usage("Character must be one installed folder name under SpriteSheet/Gremlins.");
        foreach (char c in value) if (char.IsControl(c)) throw Usage("Character contains a control character.");
        return value;
    }

    private static CharacterPackException Usage(string reason)
    {
        return new CharacterPackException(reason
            + "\nUsage: DesktopGremlin.exe [--character \"Cafe\"] [--random-character]"
            + "\nAn explicit --character overrides random startup and START_CHAR. Config files are not changed.");
    }
}
