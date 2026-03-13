using UnityEngine;

public class FinalDoor : MonoBehaviour
{
    public bool isOpen = false;

    public void OpenFinalDoor()
    {
        isOpen = true;
        Debug.Log("Финальная дверь открыта!");
    }
}