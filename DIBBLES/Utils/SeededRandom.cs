namespace DIBBLES.Utils;

using System;

public class SeededRandom
{
    private long seed;

    // Constructor: Initialize with a seed
    public SeededRandom(long seed)
    {
        this.seed = (seed ^ 0x5DEECE66DL) & ((1L << 48) - 1);
    }

    // Set seed manually (e.g., for chunk-based seeding)
    public void SetSeed(long seed)
    {
        this.seed = (seed ^ 0x5DEECE66DL) & ((1L << 48) - 1);
    }

    // Generate the next random integer (mimics Java's Random.next(int bits))
    private int Next(int bits)
    {
        seed = (seed * 0x5DEECE66DL + 0xBL) & ((1L << 48) - 1);
        return (int)(seed >> (48 - bits));
    }

    // Generate a random int (0 to 2^31-1)
    public int NextInt()
    {
        return Next(32);
    }

    // Generate a random int between 0 and bound-1
    public int NextInt(int bound)
    {
        if (bound <= 0) return 0;

        int r = Next(31);
        int m = bound - 1;
        if ((bound & m) == 0) // If bound is a power of 2
            r = (int)((bound * (long)r) >> 31);
        else
        {
            int u;
            do
            {
                u = Next(31);
                r = u % bound;
            } while (u - r + m < 0);
        }
        return r;
    }

    // Generate a random float between 0.0 and 1.0
    public float NextFloat()
    {
        return Next(24) / (float)(1 << 24);
    }

    // Generate a random long
    public long NextLong()
    {
        return ((long)Next(32) << 32) + Next(32);
    }
    
    public bool NextChance(float percent)
    {
        // Clamp percent between 0 and 100
        percent = Math.Clamp(percent, 0f, 100f);
        return NextFloat() < (percent / 100f);
    }
}