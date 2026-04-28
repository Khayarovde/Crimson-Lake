using UnityEngine;

/// <summary>
/// Структура данных для точки телепортации
/// </summary>
[System.Serializable]
public class DebugTeleportPoint
{
    [SerializeField] public string pointName;
    [SerializeField] public Transform target;
}
