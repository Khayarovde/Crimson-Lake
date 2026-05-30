using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

public class ButtonStateController : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler,  IPointerUpHandler,
    ISelectHandler, IDeselectHandler
{
    enum BtnState { Normal = 0, Hover = 1, Pressed = 2, Selected = 3, Disabled = 4 }

    [SerializeField] float transitionDuration = 0.12f;
    [SerializeField] bool  interactable        = true;

    Material  _mat;
    BtnState  _current = BtnState.Normal;
    Coroutine _tween;
    bool      _isPointerInside = false;
    bool      _isPressed       = false;

    void Awake() {
        _mat = GetComponent<Image>().material =
               new Material(GetComponent<Image>().material);
        SetStateImmediate(interactable ? BtnState.Normal : BtnState.Disabled);
    }

    // Вызывается когда панель закрылась / фокус вернулся на кнопку
    void OnEnable() => ForceNormal();
    void OnDisable() => ForceNormal();

    // Сброс при потере фокуса приложения (Alt+Tab, открытие панели)
    void OnApplicationFocus(bool focus) {
        if (!focus) ForceNormal();
    }

    public void OnPointerEnter(PointerEventData _) {
        if (!interactable) return;
        _isPointerInside = true;
        TransitionTo(BtnState.Hover);
    }

    public void OnPointerExit(PointerEventData _) {
        if (!interactable) return;
        _isPointerInside = false;
        // Если зажата — не сбрасываем сразу, ждём OnPointerUp
        if (!_isPressed) TransitionTo(BtnState.Normal);
    }

    public void OnPointerDown(PointerEventData _) {
        if (!interactable) return;
        _isPressed = true;
        TransitionTo(BtnState.Pressed);
    }

    public void OnPointerUp(PointerEventData _) {
        if (!interactable) return;
        _isPressed = false;
        // Возвращаемся в Hover если курсор ещё на кнопке, иначе в Normal
        TransitionTo(_isPointerInside ? BtnState.Hover : BtnState.Normal);
    }

    public void OnSelect(BaseEventData _) {
        if (!interactable) return;
        if (_current == BtnState.Selected) return;
        TransitionTo(BtnState.Selected);
    }

    public void OnDeselect(BaseEventData _) {
        if (!interactable) return;
        if (_current == BtnState.Selected) return;
        if (_isPressed) return;
        TransitionTo(BtnState.Normal);
    }

    // Вызови это когда панель открылась — кнопка сразу сбросится в Normal
    public void ForceNormal() {
        _isPressed       = false;
        _isPointerInside = false;
        if (_current == BtnState.Selected) return; // Selected не трогаем
        BtnState target = interactable ? BtnState.Normal : BtnState.Disabled;
        if (!isActiveAndEnabled) {
            SetStateImmediate(target);
            return;
        }
        TransitionTo(target);
    }

    public void SetInteractable(bool value) {
        interactable = value;
        TransitionTo(value ? BtnState.Normal : BtnState.Disabled);
    }

    public void SetSelected(bool value) =>
        TransitionTo(value ? BtnState.Selected : BtnState.Normal);

    void TransitionTo(BtnState next) {
        if (!isActiveAndEnabled) {
            SetStateImmediate(next);
            return;
        }

        if (_tween != null) StopCoroutine(_tween);
        _tween = StartCoroutine(Tween(_current, next));
        _current = next;
    }

    void SetStateImmediate(BtnState s) {
        _current = s;
        _mat.SetFloat("_State", (float)s);
        _mat.SetFloat("_Blend", 0f);
    }

    IEnumerator Tween(BtnState from, BtnState to) {
        _mat.SetFloat("_State", (float)from);
        _mat.SetFloat("_Blend", 0f);
        float t = 0f;
        while (t < 1f) {
            t += Time.unscaledDeltaTime / transitionDuration;
            _mat.SetFloat("_Blend", Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        SetStateImmediate(to);
    }
}