using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class PlayerTeleportTrigger : MonoBehaviour
{
    [Header("Телепорт")]
    [SerializeField] private Transform teleportTarget;

    [Header("Игрок")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("Текст подсказки")]
    [SerializeField] private TMP_Text hintText;
    [SerializeField] private GameObject hintRoot;
    [SerializeField] private string destinationText = "Новая точка";
    [SerializeField] private string hintFormat = "Нажми E, чтобы телепортироваться в {0}";

    private Transform currentPlayer;
    private Rigidbody currentPlayerRigidbody;
    private bool playerInZone;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        SetHintVisible(false);
    }

    private void Update()
    {
        if (!playerInZone || teleportTarget == null || currentPlayer == null)
            return;

        if (Input.GetKeyDown(interactKey))
            TeleportPlayer();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other))
            return;

        currentPlayer = GetPlayerRoot(other);
        currentPlayerRigidbody = currentPlayer != null ? currentPlayer.GetComponentInParent<Rigidbody>() : null;
        playerInZone = currentPlayer != null;

        if (playerInZone)
            ShowHint();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other))
            return;

        currentPlayer = null;
        currentPlayerRigidbody = null;
        playerInZone = false;
        SetHintVisible(false);
    }

    private void TeleportPlayer()
    {
        if (currentPlayer == null || teleportTarget == null)
            return;

        if (currentPlayerRigidbody != null)
        {
            currentPlayerRigidbody.linearVelocity = Vector3.zero;
            currentPlayerRigidbody.angularVelocity = Vector3.zero;
            currentPlayerRigidbody.position = teleportTarget.position;
            currentPlayerRigidbody.rotation = teleportTarget.rotation;
        }
        else
        {
            currentPlayer.SetPositionAndRotation(teleportTarget.position, teleportTarget.rotation);
        }
    }

    private void ShowHint()
    {
        if (hintText != null)
            hintText.text = string.Format(hintFormat, destinationText);

        SetHintVisible(true);
    }

    private void SetHintVisible(bool visible)
    {
        if (hintRoot != null)
            hintRoot.SetActive(visible);

        if (hintText != null)
            hintText.gameObject.SetActive(visible);
    }

    private bool IsPlayer(Collider other)
    {
        return other.CompareTag(playerTag)
               || (other.transform.root != null && other.transform.root.CompareTag(playerTag));
    }

    private Transform GetPlayerRoot(Collider other)
    {
        if (other.transform.root != null && other.transform.root.CompareTag(playerTag))
            return other.transform.root;

        return other.CompareTag(playerTag) ? other.transform : null;
    }

    private void Reset()
    {
        Collider collider = GetComponent<Collider>();
        if (collider != null)
            collider.isTrigger = true;
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(destinationText))
            destinationText = "Новая точка";

        if (string.IsNullOrWhiteSpace(hintFormat))
            hintFormat = "Нажми E, чтобы телепортироваться в {0}";
    }
}