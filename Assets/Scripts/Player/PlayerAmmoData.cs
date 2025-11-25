public static class PlayerAmmoData
{
    public static int gunReserve = 35;
    public static int gunInMag = 7;

    public static int pistolReserve = 120;
    public static int pistolInMag = 12;

    // Можно сбросить при смерти и т.п.
    public static void Reset()
    {
        gunReserve = 35;
        gunInMag = 7;
        pistolReserve = 120;
        pistolInMag = 12;
    }
}