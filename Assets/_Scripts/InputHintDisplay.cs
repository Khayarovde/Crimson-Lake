using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

[Serializable]
public class InputHintSlot
{
    [Header("Visuals")]
    public Image targetImage;
    public Sprite keyboardSprite;
    public Sprite gamepadSprite;

    [Header("Text (optional)")]
    public bool showLabel;
    public TMP_Text targetText;
    public string keyboardLabel;
    public string gamepadLabel;
}

public class InputHintDisplay : MonoBehaviour
{
    [Header("Hint Slots")]
    [SerializeField] private InputHintSlot[] slots;

    [Header("Auto Show/Hide")]
    [SerializeField] private GameObject watchTarget;

    [Header("Start State")]
    [SerializeField] private bool showOnStart = false;

    [Header("Device Override (debug)")]
    [SerializeField] private bool forceKeyboard;
    [SerializeField] private bool forceGamepad;

    private GamepadController cachedGamepadController;
    private InputHintWatcher watchTargetHelper;
    private Coroutine devicePollRoutine;
    private bool currentIsGamepad;
    private bool isVisible = true;

    private void Awake()
    {
        cachedGamepadController = FindFirstObjectByType<GamepadController>();
        currentIsGamepad = ResolveIsGamepad();
        RefreshSlots();
        BindWatchTargetHelper();

        if (!showOnStart)
        {
            isVisible = false;
            SetSlotsVisible(false);
        }
        else
        {
            isVisible = true;
            SetSlotsVisible(true);
        }
    }

    private void OnEnable()
    {
        if (devicePollRoutine == null)
        {
            devicePollRoutine = StartCoroutine(DevicePollLoop());
        }
    }

    private void OnDisable()
    {
        if (devicePollRoutine != null)
        {
            StopCoroutine(devicePollRoutine);
            devicePollRoutine = null;
        }
    }

    private IEnumerator DevicePollLoop()
    {
        var wait = new WaitForSecondsRealtime(0.2f);

        while (true)
        {
            bool nextIsGamepad = ResolveIsGamepad();
            if (nextIsGamepad != currentIsGamepad)
            {
                currentIsGamepad = nextIsGamepad;
                RefreshSlots();
            }

            yield return wait;
        }
    }

    private void BindWatchTargetHelper()
    {
        if (watchTarget == null)
        {
            watchTargetHelper = null;
            return;
        }

        watchTargetHelper = watchTarget.GetComponent<InputHintWatcher>();
        if (watchTargetHelper == null)
        {
            watchTargetHelper = watchTarget.AddComponent<InputHintWatcher>();
        }

        watchTargetHelper.Init(this);
    }

    private bool ResolveIsGamepad()
    {
        if (forceKeyboard)
        {
            return false;
        }

        if (forceGamepad)
        {
            return true;
        }

        if (cachedGamepadController != null)
        {
            return cachedGamepadController.IsGamepadModeActive;
        }

        return Gamepad.current != null;
    }

    public void RefreshSlots()
    {
        bool isGamepad = ResolveIsGamepad();

        if (slots == null || slots.Length == 0)
        {
            return;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            InputHintSlot slot = slots[i];
            if (slot == null)
            {
                continue;
            }

            Sprite targetSprite = isGamepad ? slot.gamepadSprite : slot.keyboardSprite;
            if (slot.targetImage != null)
            {
                if (targetSprite == null)
                {
                    slot.targetImage.gameObject.SetActive(false);
                }
                else
                {
                    slot.targetImage.gameObject.SetActive(isVisible);
                    slot.targetImage.sprite = targetSprite;
                }
            }

            if (slot.targetText != null)
            {
                if (slot.showLabel)
                {
                    slot.targetText.gameObject.SetActive(isVisible);
                    slot.targetText.text = isGamepad ? slot.gamepadLabel : slot.keyboardLabel;
                }
                else
                {
                    slot.targetText.gameObject.SetActive(false);
                }
            }
        }
    }

    private void SetSlotsVisible(bool visible)
    {
        if (slots == null)
        {
            return;
        }

        foreach (InputHintSlot slot in slots)
        {
            if (slot == null)
            {
                continue;
            }

            if (slot.targetImage != null && slot.targetImage.sprite != null)
            {
                slot.targetImage.gameObject.SetActive(visible);
            }

            if (slot.targetText != null && slot.showLabel)
            {
                slot.targetText.gameObject.SetActive(visible);
            }
        }
    }

    public void Show()
    {
        isVisible = true;
        RefreshSlots();
        SetSlotsVisible(true);
    }

    public void Hide()
    {
        isVisible = false;
        SetSlotsVisible(false);
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            return;
        }
    }

    private void OnDestroy()
    {
        if (devicePollRoutine != null)
        {
            StopCoroutine(devicePollRoutine);
            devicePollRoutine = null;
        }
    }

}

[DisallowMultipleComponent]
public class InputHintWatcher : MonoBehaviour
{
    private InputHintDisplay _owner;
    private bool _ready;

    public void Init(InputHintDisplay owner)
    {
        _owner = owner;
        _ready = true;
    }

    private void OnEnable()
    {
        if (!_ready) return;
        _owner?.Show();
    }

    private void OnDisable()
    {
        if (!_ready) return;
        _owner?.Hide();
    }
}

