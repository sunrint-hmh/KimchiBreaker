using System;

public static class KimchiTowerRules
{
    public static long JarCount(long completedKimchi)
    {
        if (completedKimchi < 0) throw new ArgumentOutOfRangeException(nameof(completedKimchi));
        return completedKimchi / 2; // Integer division: discard an unpaired kimchi.
    }
}
