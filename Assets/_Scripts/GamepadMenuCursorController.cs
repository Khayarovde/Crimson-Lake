using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class GamepadMenuCursorController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject menuRoot;

    [Header("Input")]
    [SerializeField, Range(200f, 2500f)] private float cursorSpeed = 1100f;
    [SerializeField, Range(0.05f, 0.5f)] private float moveDeadzone = 0.2f;
    [SerializeField, Range(0.05f, 0.5f)] private float clickThreshold = 0.2f;
    [SerializeField, Range(0f, 80f)] private float edgePadding = 18f;

    private Vector2 cursorPosition;
    private bool cursorInitialized;
    private bool cursorStateCaptured;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockState;
    private readonly List<RaycastResult> raycastResults = new List<RaycastResult>(16);

    private void Update()
    {
        if (!IsMenuActive())
        {
            RestoreCursorState();
            return;
        }

        Gamepad gamepad = Gamepad.current;
        if (gamepad == null)
        {
            RestoreCursorState();
            return;
        }

        CaptureCursorState();
        UpdateCursorFromGamepad(gamepad.leftStick.ReadValue());

        if (gamepad.buttonSouth.wasPressedThisFrame)
            ClickButtonUnderCursor();
    }

    private bool IsMenuActive()
    {
        if (menuRoot != null)
            return menuRoot.activeInHierarchy;

        return isActiveAndEnabled && gameObject.activeInHierarchy;
    }

    private void UpdateCursorFromGamepad(Vector2 stickInput)
    {
        if (!cursorInitialized)
        {
            cursorPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            cursorInitialized = true;
            WarpCursor(cursorPosition);
        }

        if (stickInput.sqrMagnitude < moveDeadzone * moveDeadzone)
            return;

        float deltaTime = Mathf.Max(0f, Time.unscaledDeltaTime);
        cursorPosition += stickInput * cursorSpeed * deltaTime;

        float maxX = Mathf.Max(edgePadding, Screen.width - edgePadding);
        float maxY = Mathf.Max(edgePadding, Screen.height - edgePadding);
        cursorPosition.x = Mathf.Clamp(cursorPosition.x, edgePadding, maxX);
        cursorPosition.y = Mathf.Clamp(cursorPosition.y, edgePadding, maxY);

        WarpCursor(cursorPosition);
    }

    private void WarpCursor(Vector2 position)
    {
        if (Mouse.current != null)
            Mouse.current.WarpCursorPosition(position);
    }

    private void ClickButtonUnderCursor()
    {
        if (EventSystem.current == null)
            return;

        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = Mouse.current != null ? Mouse.current.position.ReadValue() : cursorPosition
        };

        raycastResults.Clear();
        EventSystem.current.RaycastAll(pointerData, raycastResults);

        for (int i = 0; i < raycastResults.Count; i++)
        {
            RaycastResult result = raycastResults[i];
            if (result.gameObject == null)
                continue;

            Button button = result.gameObject.GetComponentInParent<Button>();
            if (button == null || !button.interactable)
                continue;

            button.onClick.Invoke();
            return;
        }

        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null)
            return;

        Button selectedButton = selected.GetComponent<Button>();
        if (selectedButton == null || !selectedButton.interactable)
            selectedButton = selected.GetComponentInParent<Button>();

        if (selectedButton != null && selectedButton.interactable)
            selectedButton.onClick.Invoke();
    }

    private void CaptureCursorState()
    {
        if (cursorStateCaptured)
            return;

        previousCursorVisible = Cursor.visible;
        previousCursorLockState = Cursor.lockState;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        cursorStateCaptured = true;
    }

    private void RestoreCursorState()
    {
        if (!cursorStateCaptured)
            return;

        Cursor.visible = previousCursorVisible;
        Cursor.lockState = previousCursorLockState;
        cursorStateCaptured = false;
        cursorInitialized = false;
    }

    private void OnDisable()
    {
        RestoreCursorState();
    }
}