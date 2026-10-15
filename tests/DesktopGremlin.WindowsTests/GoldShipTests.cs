using System;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static partial class Program
{
    private static void TestGoldShipRelease()
    {
        string fixture = Environment.GetEnvironmentVariable("GREMLIN_GOLDSHIP_RELEASE_DIR");
        if (string.IsNullOrEmpty(fixture))
        {
            Console.WriteLine("Gold Ship release playback skipped: set GREMLIN_GOLDSHIP_RELEASE_DIR to the repaired fixture.");
            return;
        }
        string destination = Path.Combine(BasePath, "SpriteSheet", "Gremlins", "GoldShip");
        if (Directory.Exists(destination)) throw new Exception("Refusing to overwrite an existing GoldShip test pack.");
        try
        {
            string source = Path.Combine(fixture, "SpriteSheet", "Gremlins", "GoldShip");
            foreach (string path in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string target = Path.Combine(destination, path.Substring(source.Length + 1));
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(path, target);
            }
            Settings.StartingChar = "GoldShip";
            Settings.AllowCache = true;
            var frames = new FrameCounts();
            frames.LoadConfigChar("GoldShip");
            Assert(frames.Hover == 25 && frames.Sleep == 50 && frames.Outro == 35
                && frames.Grab == 60 && frames.WalkIdle == 138, "Real Gold Ship config loads repaired counts");
            var image = new Image { Source = SpriteManager.Initialize(frames) };
            foreach (var animation in new[] {
                new { Name = "hover", Count = frames.Hover },
                new { Name = "sleep", Count = frames.Sleep },
                new { Name = "outro", Count = frames.Outro },
                new { Name = "grab", Count = frames.Grab },
                new { Name = "runIdle", Count = frames.WalkIdle } })
            {
                int current = 0;
                for (int i = 0; i < animation.Count * 2; i++)
                {
                    int next = SpriteManager.PlayAnimation(animation.Name, "Actions", current, animation.Count, image);
                    Assert(next == (i + 1) % animation.Count, animation.Name + " advances and wraps across two complete cycles");
                    var bitmap = (BitmapSource)image.Source;
                    Assert(bitmap.PixelWidth == 350 && bitmap.PixelHeight == 350, "Real frame crop dimensions");
                    if (animation.Name == "hover")
                    {
                        var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
                        byte[] pixels = new byte[350 * 350 * 4];
                        converted.CopyPixels(pixels, 350 * 4, 0);
                        bool visible = false;
                        for (int p = 3; p < pixels.Length; p += 4) visible |= pixels[p] != 0;
                        Assert(visible, "Every repaired hover frame contains visible artwork");
                    }
                    current = next;
                }
            }
            for (int i = 0; i < frames.Emote4; i++)
                Assert(SpriteManager.PlayAnimation("emote4", "Emotes", i, frames.Emote4, image)
                    == (i + 1) % frames.Emote4, "Matching fourth-emote artwork plays completely");
            string sound = Path.Combine(fixture, "Sounds", "GoldShip", "emote4.wav");
            using (var sha = SHA256.Create())
                Assert(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(sound))).Replace("-", "").ToLowerInvariant()
                    == "865dc3a9df56c7c095709b2645cc1e35e59840bfb3076912d7287c68e69a985f", "Restored sound matches the original first-emote audio");
            using (var player = new System.Media.SoundPlayer(sound))
            {
                player.Load();
                Assert(player.IsLoadCompleted, "Restored WAV loads through the Windows audio decoder");
            }
            using (var reader = new BinaryReader(File.OpenRead(sound)))
            {
                Assert(new string(reader.ReadChars(4)) == "RIFF", "Recovered sound has a RIFF header");
                reader.ReadInt32();
                Assert(new string(reader.ReadChars(4)) == "WAVE", "Recovered sound is WAV audio");
            }
        }
        finally { if (Directory.Exists(destination)) Directory.Delete(destination, true); }
    }
}
