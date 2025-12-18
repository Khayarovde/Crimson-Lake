using UnityEngine;
using TMPro;
using UnityEngine.Events;

public class ElevatorController : MonoBehaviour
{
    [Header("Визуальные модели лифта")]
    public GameObject closedElevator;   // GameObject с закрытым лифтом (двери закрыты, стены и т.д.)
    public GameObject openElevator;     // GameObject с открытым лифтом (двери открыты, проход свободен)

    [Header("Подсказка")]
    public TextMeshProUGUI hintText;     // Ссылка на TMP текст для подсказок (можно на отдельном Canvas)
    public string closedHint = "Закрыто";
    public float hintShowDistance = 4f;

    [Header("Игрок")]
    public Transform player;

    private bool isOpen = false;

    private void Start()
    {
        // Изначально лифт закрыт
        SetElevatorState(false);

        if (hintText != null)
            hintText.gameObject.SetActive(false);
    }

    private void Update()
    {
        // Показываем подсказку "Закрыто", только если лифт закрыт и игрок рядом
        if (!isOpen && hintText != null && player != null)
        {
            float distance = Vector3.Distance(player.position, transform.position);
            if (distance <= hintShowDistance)
            {
                hintText.gameObject.SetActive(true);
                hintText.text = closedHint;
            }
            else
            {
                hintText.gameObject.SetActive(false);
            }
        }
    }

    // Вызывается из ComputerInteraction через UnityEvent
    public void OpenElevator()
    {
        if (isOpen) return;

        isOpen = true;
        SetElevatorState(true);

        // Скрываем подсказку навсегда
        if (hintText != null)
            hintText.gameObject.SetActive(false);
    }

    private void SetElevatorState(bool open)
    {
        if (closedElevator != null)
            closedElevator.SetActive(!open);

        if (openElevator != null)
            openElevator.SetActive(open);
    }

    // Для удобства в редакторе — проверка настроек
    private void OnValidate()
    {
        if (closedElevator != null && openElevator != null)
        {
            if (closedElevator == openElevator)
                Debug.LogWarning("Closed Elevator и Open Elevator — один и тот же объект! Должны быть разные.", this);
        }
    }
}