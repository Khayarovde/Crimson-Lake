using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class DeadBodyMusic : MonoBehaviour
{
    [Tooltip("Объект, относительно которого считается расстояние (обычно игрок или камера). Если пусто, скрипт найдет главную камеру.")]
    public Transform listener;

    [Tooltip("Дистанция, на которой звук имеет максимальную громкость.")]
    public float minDistance = 2f;

    [Tooltip("Дистанция, за пределами которой звук полностью исчезает и отключается.")]
    public float maxDistance = 15f;

    [Tooltip("Максимальная громкость звука.")]
    [Range(0f, 1f)]
    public float maxVolume = 1f;

    [Tooltip("Время плавного появления/затухания громкости (секунды). Если 0 — мгновенно).")]
    public float fadeTime = 0.15f;

    [Tooltip("Показывать gizmos в сцене.")]
    public bool showGizmos = true;

    [Tooltip("Цвет gizmo для области минимума (minDistance).")]
    public Color minGizmoColor = new Color(0f, 1f, 0f, 0.25f);

    [Tooltip("Цвет gizmo для области максимальной слышимости (maxDistance).")]
    public Color maxGizmoColor = new Color(1f, 0f, 0f, 0.15f);

    private AudioSource _audioSource;
    private float _maxDistSqr;
    private bool _isPlayingActive;
    private Transform _myTransform;
    private float _targetVolume;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _myTransform = transform; // Кешируем transform для оптимизации
        // Переводим в 2D, так как мы симулируем 3D и управляем громкостью вручную для оптимизации
        _audioSource.spatialBlend = 0f;
        _audioSource.playOnAwake = false;
        _audioSource.loop = true;

        CalculateSqrDistances();

        // Предварительная загрузка/декомпрессия аудио: воспроизводим и сразу ставим на паузу.
        // Это переносит потенциальный «подвис» на загрузку сцены, а не на момент входа игрока в зону.
        if (_audioSource.clip != null)
        {
            float prevVol = _audioSource.volume;
            _audioSource.volume = 0f;
            _audioSource.Play();
            _audioSource.Pause();
            _audioSource.volume = prevVol;
        }
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;
        Vector3 pos = transform.position;
        Color prev = Gizmos.color;

        Gizmos.color = maxGizmoColor;
        Gizmos.DrawWireSphere(pos, maxDistance);

        Gizmos.color = minGizmoColor;
        Gizmos.DrawWireSphere(pos, minDistance);

        Gizmos.color = prev;
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;
        Vector3 pos = transform.position;
        Color prev = Gizmos.color;

        Gizmos.color = new Color(maxGizmoColor.r, maxGizmoColor.g, maxGizmoColor.b, maxGizmoColor.a);
        Gizmos.DrawSphere(pos, maxDistance);

        Gizmos.color = new Color(minGizmoColor.r, minGizmoColor.g, minGizmoColor.b, minGizmoColor.a);
        Gizmos.DrawSphere(pos, minDistance);

        Gizmos.color = prev;
    }

    private void Start()
    {
        // Если слушатель не назначен руками в инспекторе, берем главную камеру
        if (listener == null && Camera.main != null)
        {
            listener = Camera.main.transform;
        }
    }

    private void OnValidate()
    {
        CalculateSqrDistances();
    }

    private void CalculateSqrDistances()
    {
        // Заранее вычисляем квадрат дистанции для оптимизации
        _maxDistSqr = maxDistance * maxDistance;
    }

    private void Update()
    {
        if (listener == null) return;

        // Вычисляем квадрат расстояния (sqrMagnitude работает намного быстрее Vector3.Distance, т.к. не использует Sqrt)
        float sqrDist = (_myTransform.position - listener.position).sqrMagnitude;
        // Если игрок вне максимальной зоны — цель громкости 0
        float minDistSqr = minDistance * minDistance;
        if (sqrDist > _maxDistSqr)
        {
            _targetVolume = 0f;
        }
        else
        {
            if (sqrDist <= minDistSqr)
            {
                _targetVolume = maxVolume;
            }
            else
            {
                float dist = Mathf.Sqrt(sqrDist);
                float t = (dist - minDistance) / (maxDistance - minDistance);
                _targetVolume = Mathf.Lerp(maxVolume, 0f, t);
            }
        }

        // Если нужна слышимость — включаем аудиоплеер (Play вызывается редко благодаря флагу)
        if (_targetVolume > 0f)
        {
            if (!_isPlayingActive)
            {
                if (!_audioSource.isPlaying)
                    _audioSource.Play();
                _isPlayingActive = true;
            }
        }

        // Плавно изменяем громкость к целевой
        if (fadeTime <= 0f)
        {
            _audioSource.volume = _targetVolume;
        }
        else
        {
            float maxDelta = (maxVolume / Mathf.Max(0.0001f, fadeTime)) * Time.deltaTime;
            _audioSource.volume = Mathf.MoveTowards(_audioSource.volume, _targetVolume, maxDelta);
        }

        // Когда громкость достигла 0 — можно поставить на паузу, чтобы снизить нагрузку
        if (_isPlayingActive && _audioSource.volume <= 0f && _targetVolume <= 0f)
        {
            _audioSource.Pause();
            _isPlayingActive = false;
        }
    }
}
