using System;

public static class RandomRanges
{
    public static int NextOffset(Random random, int radius)
    {
        if (radius < 0) throw new ArgumentOutOfRangeException(nameof(radius));
        // Keep the existing inclusive integer distribution and RNG behavior for
        // ordinary values. At Int32.MaxValue, radius + 1 would overflow.
        if (radius < int.MaxValue) return random.Next(-radius, radius + 1);
        return (int)(Math.Floor(random.NextDouble() * (2.0 * radius + 1.0)) - radius);
    }
}
