using System.Collections;
using UnityEngine;

/// <summary>
/// Объект-ловушка (леска), спавнится боссом в фазе 2.
/// Висит на арене N секунд — если игрок касается, наносит урон и исчезает.
/// Если никто не задел — исчезает по таймеру сама.
/// </summary>
[DisallowMultipleComponent]
public class BossLeskaObject : MonoBehaviour
{
    [SerializeField] private Collider triggerCollider;
    [SerializeField] private ParticleSystem spawnEffect;
    [SerializeField] private ParticleSystem despawnEffect;
    [SerializeField, Min(0f)] private float particleHeightOffset = 0.2f;
    [SerializeField] private bool forceUpwardParticles = true;
    [SerializeField, Min(0f)] private float particleUpSpeed = 2f;

    private float damage;
    private float lifetime;
    private bool triggered;

    private void Awake()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<Collider>();

        if (triggerCollider != null)
            triggerCollider.isTrigger = true;

        if (spawnEffect != null)
            spawnEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (despawnEffect != null)
            despawnEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    /// <summary>
    /// Инициализация при спавне. Вызывается боссом.
    /// </summary>
    public void Init(float damageAmount, float lifetimeDuration)
    {
        damage = damageAmount;
        lifetime = lifetimeDuration;
        triggered = false;

        PlayEffect(spawnEffect, transform.position + Vector3.up * particleHeightOffset);

        StartCoroutine(LifetimeRoutine());
    }

    private IEnumerator LifetimeRoutine()
    {
        yield return new WaitForSeconds(lifetime);

        if (!triggered)
            Despawn();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggered) return;

        // Ищем PlayerHealth на корне объекта
        PlayerHealth ph = other.transform.root.GetComponent<PlayerHealth>();
        if (ph == null) return;

        triggered = true;
        ph.ApplyDamage((int)damage);
        Despawn();
    }

    private void Despawn()
    {
        PlayEffect(despawnEffect, transform.position + Vector3.up * particleHeightOffset);

        Destroy(gameObject);
    }

    private void PlayEffect(ParticleSystem effect, Vector3 worldPos)
    {
        if (effect == null)
            return;

        ParticleSystem fx = effect;
        if (fx.transform.IsChildOf(transform))
        {
            fx.transform.SetParent(null, true);
            fx.transform.position = worldPos;
        }
        else
        {
            fx = Instantiate(effect, worldPos, Quaternion.identity);
        }

        fx.transform.rotation = Quaternion.identity;

        TryNormalizeVelocityModule(fx);

        if (forceUpwardParticles)
            ForceUpwardVelocity(fx);

        ParticleSystem.MainModule main = fx.main;
        main.stopAction = ParticleSystemStopAction.Destroy;

        fx.Play();

        float lifetime = GetMaxLifetime(main.startLifetime);
        float duration = Mathf.Max(0.1f, main.duration);
        float cleanupDelay = duration + Mathf.Max(0.1f, lifetime) + 0.1f;
        Destroy(fx.gameObject, cleanupDelay);
    }

    private void ForceUpwardVelocity(ParticleSystem fx)
    {
        ParticleSystem.VelocityOverLifetimeModule vel = fx.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(0f);
        vel.y = new ParticleSystem.MinMaxCurve(particleUpSpeed);
        vel.z = new ParticleSystem.MinMaxCurve(0f);
    }

    private static float GetMaxLifetime(ParticleSystem.MinMaxCurve curve)
    {
        switch (curve.mode)
        {
            case ParticleSystemCurveMode.Constant:
                return Mathf.Max(0.1f, curve.constant);
            case ParticleSystemCurveMode.TwoConstants:
                return Mathf.Max(0.1f, curve.constantMax);
            case ParticleSystemCurveMode.Curve:
                return Mathf.Max(0.1f, curve.curveMax.Evaluate(curve.curveMax.length > 0 ? curve.curveMax.keys[curve.curveMax.length - 1].time : 0f));
            case ParticleSystemCurveMode.TwoCurves:
                return Mathf.Max(0.1f, curve.curveMax.Evaluate(curve.curveMax.length > 0 ? curve.curveMax.keys[curve.curveMax.length - 1].time : 0f));
            default:
                return 0.5f;
        }
    }

    private static void TryNormalizeVelocityModule(ParticleSystem fx)
    {
        ParticleSystem.VelocityOverLifetimeModule vel = fx.velocityOverLifetime;
        if (!vel.enabled)
            return;

        ParticleSystemCurveMode xMode = vel.x.mode;
        ParticleSystemCurveMode yMode = vel.y.mode;
        ParticleSystemCurveMode zMode = vel.z.mode;
        if (xMode != yMode || yMode != zMode)
        {
            vel.enabled = false;
        }
    }
}
