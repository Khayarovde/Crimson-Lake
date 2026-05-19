using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class ManipulatorController : MonoBehaviour
{
    // ─── Состояния каждого триггера ───────────────────────────────────────────
    public enum ZoneState { Ready, Playing, Cooldown }

    [Serializable]
    public class AnimStateConfig
    {
        [Tooltip("Точное имя состояния в Animator (например: Grab, Push, Pull)")]
        public string stateName;

        [Tooltip("Звук через Animation Event")]
        public AudioClip sound;

        [Tooltip("Коллайдер-триггер, активирующий это состояние")]
        public Collider triggerZone;

        // ── Runtime-данные (не сериализуются) ────────────────────────────────
        [NonSerialized] public ZoneState State = ZoneState.Ready;
        [NonSerialized] public float CooldownRemaining = 0f;
    }

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource audioSource;

    [Header("Idle State")]
    [Tooltip("Имя состояния, в которое возвращаемся после завершения анимации")]
    [SerializeField] private string idleStateName = "Idle";

    [Header("Animation States")]
    [SerializeField] private AnimStateConfig[] states;

    [Header("Cooldown Settings")]
    [SerializeField] private float cooldownMin = 10f;
    [SerializeField] private float cooldownMax = 120f;

    [Header("Debug")]
    [SerializeField] private bool skipCooldown = false;

    [Header("Settings")]
    [SerializeField] private string playerTag = "Player";

    private AnimStateConfig _activeConfig;
    private string _currentStateName;
    private Coroutine _activeRoutine;

    // Animator layer index — если используешь не нулевой слой, поменяй здесь
    private const int AnimatorLayer = 0;

    private void Awake()
    {
        _currentStateName = idleStateName;

        foreach (var state in states)
        {
            if (state == null || state.triggerZone == null || string.IsNullOrEmpty(state.stateName))
            {
                Debug.LogWarning("[Manipulator] Стейт не настроен полностью!", this);
                continue;
            }

            state.triggerZone.isTrigger = true;
            state.State = ZoneState.Ready;

            var proxy = state.triggerZone.gameObject.GetComponent<ManipulatorTriggerProxy>();
            if (proxy == null)
                proxy = state.triggerZone.gameObject.AddComponent<ManipulatorTriggerProxy>();

            proxy.Init(this, state, playerTag);
        }
    }

    // ─── Вход на триггер ──────────────────────────────────────────────────────
    internal void OnZoneEnter(AnimStateConfig config)
    {
        // Не Ready — триггер занят или на кулдауне, игнорируем
        if (config.State != ZoneState.Ready)
        {
            string reason = config.State == ZoneState.Playing
                ? "анимация ещё играет"
                : $"кулдаун {config.CooldownRemaining:F1}s";
            Debug.Log($"[Manipulator] '{config.stateName}' заблокирован ({reason})");
            return;
        }

        // Если сейчас играет другая анимация — не перебиваем
        // Ждём пока она закончится сама
        if (_activeConfig != null && _activeConfig.State == ZoneState.Playing)
        {
            Debug.Log($"[Manipulator] Уже играет '{_activeConfig.stateName}', '{config.stateName}' пропущен");
            return;
        }

        StartAnimation(config);
    }

    // ─── Выход с триггера — больше ничего не делаем ───────────────────────────
    internal void OnZoneExit(AnimStateConfig config)
    {
        // Анимация доигрывается сама, выход игрока не влияет
        Debug.Log($"[Manipulator] Игрок ушёл с '{config.stateName}', анимация продолжается");
    }

    // ─── Запуск анимации ──────────────────────────────────────────────────────
    private void StartAnimation(AnimStateConfig config)
    {
        // Останавливаем предыдущую корутину на всякий случай
        if (_activeRoutine != null)
            StopCoroutine(_activeRoutine);

        _activeConfig = config;
        _currentStateName = config.stateName;
        config.State = ZoneState.Playing;

        animator.Play(config.stateName, AnimatorLayer, 0f);
        Debug.Log($"[Manipulator] Играет → '{config.stateName}'");

        _activeRoutine = StartCoroutine(WaitForAnimationComplete(config));
    }

    // ─── Ждём конца анимации, потом запускаем кулдаун ────────────────────────
    private IEnumerator WaitForAnimationComplete(AnimStateConfig config)
    {
        // Ждём один кадр чтобы Animator успел переключиться на новый стейт
        yield return null;
        yield return null;

        // Ждём пока аниматор не войдёт в нужный стейт
        // (на случай если transition занимает несколько кадров)
        float waitForStateTimeout = 2f;
        float elapsed = 0f;
        while (elapsed < waitForStateTimeout)
        {
            if (animator.GetCurrentAnimatorStateInfo(AnimatorLayer).IsName(config.stateName))
                break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (elapsed >= waitForStateTimeout)
        {
            Debug.LogWarning($"[Manipulator] Не удалось войти в стейт '{config.stateName}' за {waitForStateTimeout}s. Проверь имя в Animator!");
            FinishAndStartCooldown(config);
            yield break;
        }

        // Ждём пока normalizedTime не достигнет конца (>= 1)
        // Работает только для НЕ зацикленных анимаций!
        while (true)
        {
            var info = animator.GetCurrentAnimatorStateInfo(AnimatorLayer);

            // Аниматор переключился на другой стейт раньше времени
            if (!info.IsName(config.stateName)) break;

            // Анимация завершена
            if (info.normalizedTime >= 1f) break;

            yield return null;
        }

        Debug.Log($"[Manipulator] '{config.stateName}' завершена");

        // Возвращаемся в Idle
        animator.Play(idleStateName, AnimatorLayer, 0f);
        _currentStateName = idleStateName;
        _activeConfig = null;

        FinishAndStartCooldown(config);
    }

    // ─── Кулдаун ─────────────────────────────────────────────────────────────
    private void FinishAndStartCooldown(AnimStateConfig config)
    {
        if (skipCooldown)
        {
            config.State = ZoneState.Ready;
            config.CooldownRemaining = 0f;
            Debug.Log($"[Manipulator] '{config.stateName}' кулдаун пропущен (Debug)");
            return;
        }

        float cooldown = UnityEngine.Random.Range(cooldownMin, cooldownMax);
        config.CooldownRemaining = cooldown;
        Debug.Log($"[Manipulator] '{config.stateName}' кулдаун {cooldown:F1}s");

        StartCoroutine(CooldownRoutine(config, cooldown));
    }

    private IEnumerator CooldownRoutine(AnimStateConfig config, float duration)
    {
        config.State = ZoneState.Cooldown;
        float remaining = duration;

        while (remaining > 0f)
        {
            remaining -= Time.deltaTime;
            config.CooldownRemaining = Mathf.Max(remaining, 0f);
            yield return null;
        }

        config.State = ZoneState.Ready;
        config.CooldownRemaining = 0f;
        Debug.Log($"[Manipulator] '{config.stateName}' готов снова");
    }

    // ─── Animation Event ──────────────────────────────────────────────────────
    public void PlaySound()
    {
        if (_activeConfig?.sound == null) return;
        audioSource.PlayOneShot(_activeConfig.sound);
    }

// ─── Отображение кулдаунов в Editor ──────────────────────────────────────
#if UNITY_EDITOR
    private void OnGUI()
    {
        if (!skipCooldown) return; // показываем только в debug-режиме

        GUILayout.BeginArea(new Rect(10, 10, 260, states.Length * 24 + 10));
        foreach (var state in states)
        {
            if (state == null) continue;
            string status = state.State switch
            {
                ZoneState.Ready    => "<color=green>Ready</color>",
                ZoneState.Playing  => "<color=yellow>Playing...</color>",
                ZoneState.Cooldown => $"<color=red>CD {state.CooldownRemaining:F1}s</color>",
                _                  => ""
            };
            GUILayout.Label($"<b>{state.stateName}</b>: {status}", new GUIStyle { richText = true, fontSize = 14 });
        }
        GUILayout.EndArea();
    }
#endif
}