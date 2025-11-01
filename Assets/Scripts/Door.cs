using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class Door : MonoBehaviour
{
    public Transform playerSpawnPoint;     // Точка появления игрока
    public TankController tankCtrl;        // Контроллер танка
    public GameObject darkeningPanel;      // Элемент затемнения
    public float fadeDuration = 1f;        // Время затухания затемнения
    public float delayBetweenPhases = 1f;  // Пауза между фазами
    public string playerTag = "Player";    // Тег игрока
    
    public Image hudIndicator;             // Индикация на экране (UI)
    public Sprite indicatorSprite;         // Единственный используемый спрайт

    private bool isFading = false;
    private bool playerInside = false;     // Внутри ли игрок зоны двери
    private bool doorUsed = false;         // Была ли дверь открыта хотя бы однажды

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            playerInside = true;
            ShowIndicator();               // Отображаем индикатор
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            playerInside = false;
            HideIndicator();               // Скрываем индикатор
        }
    }

    private void Update()
    {
        if (playerInside && !doorUsed && Input.GetKeyDown(KeyCode.E))
        {
            TriggerTransition();
            doorUsed = true;              // Запоминаем факт использования двери
        }
    }

    public void TriggerTransition()
    {
        if (!isFading)
        {
            isFading = true;
            StartCoroutine(FadeToDarkness());
        }
    }

    IEnumerator FadeToDarkness()
    {
        darkeningPanel.SetActive(true);

        float elapsedTime = 0f;
        while (elapsedTime < fadeDuration)
        {
            float normalizedTime = elapsedTime / fadeDuration;
            float currentAlpha = Mathf.Lerp(0f, 1f, normalizedTime);
            Image imageComponent = darkeningPanel.GetComponentInChildren<Image>();
            imageComponent.color = new Color(0, 0, 0, currentAlpha);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        TeleportPlayer();
        yield return new WaitForSeconds(delayBetweenPhases);

        darkeningPanel.SetActive(false);
        tankCtrl.enabled = true;
        isFading = false;
    }

    void TeleportPlayer()
    {
        GameObject.FindGameObjectWithTag(playerTag).transform.position = playerSpawnPoint.position;
    }

    void ShowIndicator()
    {
        if (hudIndicator != null)
        {
            hudIndicator.sprite = indicatorSprite;
            hudIndicator.gameObject.SetActive(true); // Только здесь мы включаем отображение иконки
        }
    }

    void HideIndicator()
    {
        if (hudIndicator != null)
        {
            hudIndicator.gameObject.SetActive(false); // Здесь скрываем иконку
        }
    }
}