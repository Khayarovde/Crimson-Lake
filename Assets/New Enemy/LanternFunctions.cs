using UnityEngine;

public class LanternFunctions : MonoBehaviour
{
    public bool switchOn = false;

    public void ToggleSwitch()
    {
        switchOn = !switchOn;
        Debug.Log($"Фонарь {(switchOn ? "включён" : "выключён")}");
    }
}