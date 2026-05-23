using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Collider))]
public class SavePointInteraction : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private SaveSlotsUI saveSlotsUI;
    [SerializeField] private GameObject interactionUI;
    [SerializeField] private TMP_Text interactionTextTMP;
    [SerializeField] private Text interactionTextLegacy;
    [SerializeField] private string interactionText = "Нажмите E, чтобы сохранить";
    [SerializeField] private bool showPromptOnApproach = false;

    [Header("Interaction")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;
 
    private bool isPlayerNearby;

    private void Start()
    {
        if (interactionUI != null)
            interactionUI.SetActive(false);

        if (interactionTextTMP != null)
            interactionTextTMP.text = interactionText;
        if (interactionTextLegacy != null)
            interactionTextLegacy.text = interactionText;
    }

    private void Update()
    {
        if (!isPlayerNearby) return;

        if (Input.GetKeyDown(interactKey) || (UnityEngine.InputSystem.Gamepad.current != null && UnityEngine.InputSystem.Gamepad.current.buttonSouth.wasPressedThisFrame))
        {
            if (saveSlotsUI == null)
                saveSlotsUI = ResolveSaveSlotsUI();

            if (saveSlotsUI != null && !saveSlotsUI.IsOpen)
            {
                saveSlotsUI.Show();
                if (interactionUI != null)
                    interactionUI.SetActive(false);
            }
            else if (saveSlotsUI == null)
            {
                Debug.LogWarning("[SavePointInteraction] SaveSlotsUI не найден. Назначь его в инспекторе или добавь на Canvas.");
            }
        }
    }

    private SaveSlotsUI ResolveSaveSlotsUI()
    {
        if (SaveSlotsUI.Instance != null)
            return SaveSlotsUI.Instance;

#if UNITY_2020_1_OR_NEWER
    var all = FindObjectsByType<SaveSlotsUI>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
#else
        var all = Resources.FindObjectsOfTypeAll<SaveSlotsUI>();
#endif
        return all != null && all.Length > 0 ? all[0] : null;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        isPlayerNearby = true;
        if (interactionUI != null)
            interactionUI.SetActive(showPromptOnApproach);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        isPlayerNearby = false;
        if (interactionUI != null)
            interactionUI.SetActive(false);
    }

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }
}
