using UnityEngine;

public static class PlayerAmmoData
{
    public static int gunReserve = 35;
    public static int gunInMag = 7;

    public static int pistolReserve = 120;
    public static int pistolInMag = 12;

    public static bool initialized;

    public static void InitializeIfNeeded(int startGunReserve, int startGunInMag, int startPistolReserve, int startPistolInMag)
    {
        if (initialized)
            return;

        gunReserve = Mathf.Max(0, startGunReserve);
        gunInMag = Mathf.Max(0, startGunInMag);
        pistolReserve = Mathf.Max(0, startPistolReserve);
        pistolInMag = Mathf.Max(0, startPistolInMag);
        initialized = true;
    }

    // Можно сбросить при смерти и т.п.
    public static void Reset()
    {
        gunReserve = 35;
        gunInMag = 7;
        pistolReserve = 120;
        pistolInMag = 12;
        initialized = false;
    }
}