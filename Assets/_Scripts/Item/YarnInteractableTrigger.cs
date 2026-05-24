using UnityEngine;
using Yarn.Unity;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

[RequireComponent(typeof(Collider))]
public class YarnInteractableTrigger : MonoBehaviour
{
    [Header("Yarn Spinner")]
    [SerializeField] private string yarnNodeName = "item";

    [Header("Объекты")]
    [SerializeField] private GameObject objectToHide;
    [SerializeField] private GameObject replacementObject;
    [SerializeField] private Vector3 replacementTargetScale = Vector3.one;

    [Header("Анимация")]
    [SerializeField] private float fadeDuration = 1.5f;
    [SerializeField] private float replacementDelay = 0.3f;

    [Header("UI подсказка")]
    [SerializeField] private Image interactHintImage;

    private DialogueRunner _dialogueRunner;
    private bool _playerInRange;
    private bool _used;

    private void Awake()
    {
        _dialogueRunner = Object.FindFirstObjectByType<DialogueRunner>();
        GetComponent<Collider>().isTrigger = true;

        if (replacementObject != null)
            replacementObject.SetActive(false);

        if (_dialogueRunner != null)
            _dialogueRunner.AddCommandHandler("confirm_hide_object", HandleConfirmHide);

        SetHintVisible(false);
    }

    private void Update()
    {
        if (!_playerInRange || _used) return;
        if (Input.GetKeyDown(KeyCode.E))
            StartDialogue();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player") || _used) return;
        _playerInRange = true;
        SetHintVisible(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _playerInRange = false;
        SetHintVisible(false);
    }

    private void StartDialogue()
    {
        if (_dialogueRunner == null || _dialogueRunner.IsDialogueRunning) return;
        SetHintVisible(false);
        _dialogueRunner.StartDialogue(yarnNodeName);
    }

    private void HandleConfirmHide()
    {
        _used = true;
        StartCoroutine(FadeOut());
    }

    private IEnumerator FadeOut()
    {
        if (objectToHide == null) yield break;

        var renderers = objectToHide.GetComponentsInChildren<Renderer>();
        var materials = new List<Material>();

        foreach (var rend in renderers)
            foreach (var mat in rend.materials)
                materials.Add(mat);

        foreach (var mat in materials)
        {
            // URP
            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            // Built-in
            else
            {
                mat.SetFloat("_Mode", 2f);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
        }

        var seq = DOTween.Sequence();
        foreach (var mat in materials)
            seq.Join(mat.DOFade(0f, fadeDuration).SetEase(Ease.InOutSine));

        yield return seq.WaitForCompletion();

        objectToHide.SetActive(false);

        yield return new WaitForSeconds(replacementDelay);

        if (replacementObject != null)
        {
            replacementObject.SetActive(true);
            replacementObject.transform.localScale = replacementTargetScale;
        }
    }

    private void SetHintVisible(bool visible)
    {
        if (interactHintImage != null)
            interactHintImage.enabled = visible;
    }

    private void OnDestroy()
    {
        if (_dialogueRunner != null)
            _dialogueRunner.RemoveCommandHandler("confirm_hide_object");
    }
}