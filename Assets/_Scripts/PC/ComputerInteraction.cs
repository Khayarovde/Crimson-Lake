using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events; // Добавляем для UnityEvent

public class ComputerInteraction : MonoBehaviour
{
    [Header("References")]
    public GameObject player;
    public float interactionDistance = 3f;
    public Canvas computerCanvas;
    public Image pcImage;
    public Sprite pcOffSprite;
    public Sprite pcOnSprite;
    public Sprite pcInsertedSprite;
    public Button powerButton;
    public Button insertButton;
    public Image disketteIcon;
    public Slider progressBar;
    public TextMeshProUGUI statusText;
    
    [Header("Sounds")]
    public AudioClip openInterfaceSound;    // Тихий хум/вентиляторы при подходе
    public AudioClip powerOnSound;          // Beep или клик включения
    public AudioClip buzzingSound;          // Жужжание привода при удержании (уже был)
    public AudioClip failureSound;          // Звук сбоя/заедания дискеты
    public AudioClip successSound;          // Звук успешной вставки/загрузки

    public InventoryData inventoryData;
    public InventoryUI inventoryUI;

    [Header("Settings")]
    public float fillSpeed = 30f;
    public float dropSpeed = 15f;
    public float randomFailureChance = 0.15f;
    public float randomFailureDrop = 20f;

    [Header("Лифт")]
    public UnityEvent onDisketteInsertedSuccess; // Событие, которое вызовется при успехе

    private AudioSource audioSource;
    private bool interacting = false;
    private bool isOn = false;
    private bool hasDiskette = false;
    private InventoryItem disketteItem = null;
    private bool inserting = false;
    private bool holding = false;
    private float progress = 0f;
    private float failureTimer = 0f;
    private bool insertSuccess = false;

    [HideInInspector] public bool mouseOverButton = false;

    void Start()
    {
        if (computerCanvas != null) computerCanvas.gameObject.SetActive(false);
        if (progressBar != null) progressBar.gameObject.SetActive(false);
        if (insertButton != null) insertButton.gameObject.SetActive(false);
        if (disketteIcon != null) disketteIcon.gameObject.SetActive(false);
        if (statusText != null) statusText.text = "";

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        powerButton.onClick.AddListener(TurnOnPC);
        insertButton.onClick.AddListener(StartInserting);
    }

    void Update()
    {
        if (!interacting && Vector3.Distance(player.transform.position, transform.position) < interactionDistance && Input.GetKeyDown(KeyCode.E))
        {
            StartInteraction();
        }
        else if (interacting && Input.GetKeyDown(KeyCode.E))
        {
            CloseInteraction();
        }

        if (inserting && isOn && !insertSuccess)
        {
            if (mouseOverButton && Input.GetMouseButton(0))
            {
                if (!holding)
                {
                    holding = true;
                    if (buzzingSound != null) audioSource.PlayOneShot(buzzingSound);
                    statusText.text = "Запуск мотора привода...";
                }

                progress += Time.deltaTime * fillSpeed;

                failureTimer += Time.deltaTime;
                if (failureTimer >= 1f)
                {
                    if (Random.value < randomFailureChance)
                    {
                        progress -= randomFailureDrop;
                        if (failureSound != null) audioSource.PlayOneShot(failureSound); // Звук сбоя
                    }
                    failureTimer = 0f;
                }
            }
            else
            {
                if (holding)
                {
                    holding = false;
                    audioSource.Stop();
                    if (progress < 100f) statusText.text = "Вставка приостановлена...";
                }

                progress -= Time.deltaTime * dropSpeed;
            }

            progress = Mathf.Clamp(progress, 0f, 100f);
            progressBar.value = progress / 100f;

            if (progress >= 100f)
            {
                InsertSuccess();
            }
        }
    }

    private void StartInteraction()
    {
        interacting = true;
        computerCanvas.gameObject.SetActive(true);
        InventoryData activeInventoryData = ResolveInventoryData();

        // Звук открытия интерфейса (подход к ПК)
        if (openInterfaceSound != null) audioSource.PlayOneShot(openInterfaceSound);

        // Сброс...
        isOn = false;
        inserting = false;
        insertSuccess = false;
        holding = false;
        progress = 0f;
        progressBar.value = 0f;
        progressBar.gameObject.SetActive(false);
        insertButton.gameObject.SetActive(false);
        statusText.text = "";
        pcImage.sprite = pcOffSprite;
        powerButton.gameObject.SetActive(true);

        hasDiskette = false;
        disketteItem = null;
        if (activeInventoryData != null)
        {
            foreach (var item in activeInventoryData.items)
            {
                if (item != null && (item.type == InventoryItem.ItemType.Cassette || item.type == InventoryItem.ItemType.Disketa))
                {
                    hasDiskette = true;
                    disketteItem = item;
                    break;
                }
            }
        }

        if (disketteIcon != null)
        {
            disketteIcon.gameObject.SetActive(hasDiskette);
        }
    }

    private void CloseInteraction()
    {
        interacting = false;
        computerCanvas.gameObject.SetActive(false);
        audioSource.Stop();

        if (inserting && !insertSuccess)
        {
            inserting = false;
            progressBar.gameObject.SetActive(false);
            statusText.text = "";
        }
    }

    private void TurnOnPC()
    {
        if (!isOn)
        {
            isOn = true;
            pcImage.sprite = pcOnSprite;
            powerButton.gameObject.SetActive(false);

            // Звук включения ПК
            if (powerOnSound != null) audioSource.PlayOneShot(powerOnSound);

            if (hasDiskette)
            {
                insertButton.gameObject.SetActive(true);
            }
        }
    }

    private void StartInserting()
    {
        if (isOn && hasDiskette && !inserting)
        {
            inserting = true;
            progressBar.gameObject.SetActive(true);
            statusText.text = "Нажмите и удерживайте (Вставить)";
        }
    }

    private void InsertSuccess()
    {
        insertSuccess = true;
        inserting = false;
        holding = false;
        audioSource.Stop();

        if (successSound != null) audioSource.PlayOneShot(successSound);

        statusText.text = "Дискета успешно вставлена! Лифт разблокирован.";

        if (disketteItem != null)
        {
            InventoryData activeInventoryData = ResolveInventoryData();
            if (activeInventoryData != null)
                activeInventoryData.RemoveItem(disketteItem);
            if (inventoryUI != null) inventoryUI.UpdateInventoryUI();
        }

        if (disketteIcon != null)
        {
            disketteIcon.gameObject.SetActive(false);
        }

        if (pcInsertedSprite != null)
        {
            pcImage.sprite = pcInsertedSprite;
        }

        insertButton.gameObject.SetActive(false);
        progressBar.gameObject.SetActive(false);

        // <<< ВАЖНО: Сообщаем, что лифт теперь можно открыть >>>
        onDisketteInsertedSuccess?.Invoke();
    }

    private InventoryData ResolveInventoryData()
    {
        if (player != null)
        {
            PlayerInventory playerInventory = player.GetComponent<PlayerInventory>();
            if (playerInventory != null && playerInventory.inventoryData != null)
                return playerInventory.inventoryData;
        }

        PlayerInventory fallbackPlayerInventory = FindFirstObjectByType<PlayerInventory>();
        if (fallbackPlayerInventory != null && fallbackPlayerInventory.inventoryData != null)
            return fallbackPlayerInventory.inventoryData;

        return inventoryData;
    }
}