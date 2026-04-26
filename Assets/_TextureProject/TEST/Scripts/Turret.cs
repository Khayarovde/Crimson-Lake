using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class Turret : MonoBehaviour
{
    [SerializeField]
    private float _fireRate = 1f;
    [SerializeField]
    private Transform _shootTransform;
    [SerializeField]
    private float _maxShootDistance = 10;
    [SerializeField]
    private float _rotationSpeed;
    [SerializeField]
    private float _scanRotationSpeed = 45f;
    [SerializeField, Range(30f, 360f)]
    [Tooltip("Дуга сканирования вокруг стартового направления. Значение < 360 оставляет более безопасный маршрут.")]
    private float _scanArcAngle = 220f;
    [SerializeField, Range(10f, 360f)]
    [Tooltip("Угол конуса обзора турели. Игрок обнаруживается только внутри этого конуса.")]
    private float _viewAngle = 120f;
    [SerializeField, Tooltip("Сколько секунд турель должна непрерывно видеть игрока перед входом в режим агра.")]
    private float _timeToAggro = 0.4f;
    [SerializeField, Tooltip("Сколько секунд турель остается в агре после потери видимости цели.")]
    private float _loseSightGraceTime = 0.6f;
    [SerializeField]
    [Tooltip("Слои, используемые для проверки видимости между турелью и игроком.")]
    private LayerMask _lineOfSightMask = ~0;
    [SerializeField]
    [Tooltip("Объект, который должен вращаться")]
    private GameObject _turretRotationObject;
    [SerializeField]
    private LineRenderer _lineRenderer;
    [SerializeField]
    private GameObject _muzzleFlash;
    [SerializeField]
    private float _muzzleFlashActiveDuration = 0.25f;
    [SerializeField, Min(1)]
    [Tooltip("Урон, который наносится игроку за один выстрел турели.")]
    private int _damage = 1;
    [SerializeField]
    private float _delayBeforeNextTarget = 1f;
    [SerializeField]
    [Tooltip("Явная ссылка на игрока. Турель будет отслеживать и наносить урон только этой цели.")]
    private PlayerHealth _playerTarget;
    [SerializeField]
    [Tooltip("Назначьте здесь действие запуска звука (например, AudioSource.Play).")]
    private UnityEvent _onShootSound;
    [SerializeField]
    [Tooltip("Опциональное действие остановки звука при выключении muzzle flash (например, AudioSource.Stop).")]
    private UnityEvent _onShootSoundStop;
    [SerializeField]
    [Tooltip("Если назначено, звук не будет запускаться повторно, пока этот AudioSource уже играет.")]
    private AudioSource _shootAudioSource;

    private Transform _target;
    private Vector3[] _lineRendererPositions = new Vector3[2];
    private Animator _animator;
    private bool _delayingBeforeNextTarget = false;
    private float _timeSinceLastShot;
    private bool _hasLineOfSight;
    private float _aggroProgress;
    private float _lastSeenTime = -999f;
    private Vector3 _lastSeenDirection = Vector3.forward;
    private float _scanCenterYaw;
    private float _scanOffset;
    private float _scanDirection = 1f;

    private void Start()
    {
        _timeSinceLastShot = 0;
        _animator = GetComponent<Animator>();
        if (_onShootSound == null)
        {
            _onShootSound = new UnityEvent();
        }

        if (_turretRotationObject != null)
        {
            _scanCenterYaw = _turretRotationObject.transform.eulerAngles.y;
        }

        GetNextTarget();
    }

    private void Update()
    {
        ResetShootTrigger();
        RotateTowardTarget();
        SetLineRendererPoints();
        ValidateShoot();
    }

    private void RotateTowardTarget()
    {
        if (_turretRotationObject == null || _shootTransform == null)
        {
            return;
        }

        if (_target == null)
        {
            _hasLineOfSight = false;
            GetNextTarget();
            ScanArea();
            return;
        }

        if (CanSeeTarget(out Vector3 direction))
        {
            _hasLineOfSight = true;
            _lastSeenDirection = direction;
            _lastSeenTime = Time.time;
            _aggroProgress = Mathf.MoveTowards(_aggroProgress, 1f, Time.deltaTime / Mathf.Max(0.01f, _timeToAggro));

            // Получаем направление от поворотной части турели к цели.
            // Плавно поворачиваем турель в сторону цели.
            Quaternion rotation = Quaternion.Slerp(_turretRotationObject.transform.rotation, Quaternion.LookRotation(direction), _rotationSpeed * Time.deltaTime);

            _turretRotationObject.transform.rotation = rotation;
            _turretRotationObject.transform.eulerAngles = new Vector3(0, _turretRotationObject.transform.eulerAngles.y, 0);
        }
        else
        {
            _hasLineOfSight = false;
            _aggroProgress = Mathf.MoveTowards(_aggroProgress, 0f, Time.deltaTime / Mathf.Max(0.01f, _timeToAggro));

            if (IsStillAggro())
            {
                Quaternion rotation = Quaternion.Slerp(_turretRotationObject.transform.rotation, Quaternion.LookRotation(_lastSeenDirection), _rotationSpeed * Time.deltaTime);
                _turretRotationObject.transform.rotation = rotation;
                _turretRotationObject.transform.eulerAngles = new Vector3(0, _turretRotationObject.transform.eulerAngles.y, 0);
            }
            else
            {
                ScanArea();
            }
        }
    }

    private void ScanArea()
    {
        float halfArc = Mathf.Clamp(_scanArcAngle * 0.5f, 15f, 180f);
        _scanOffset += _scanDirection * _scanRotationSpeed * Time.deltaTime;

        if (_scanOffset > halfArc)
        {
            _scanOffset = halfArc;
            _scanDirection = -1f;
        }
        else if (_scanOffset < -halfArc)
        {
            _scanOffset = -halfArc;
            _scanDirection = 1f;
        }

        Vector3 euler = _turretRotationObject.transform.eulerAngles;
        euler.y = _scanCenterYaw + _scanOffset;
        _turretRotationObject.transform.eulerAngles = euler;
    }

    private void SetLineRendererPoints()
    {
        if (_lineRenderer == null || _shootTransform == null)
        {
            return;
        }

        _lineRendererPositions[0] = _shootTransform.position;

        Vector3 endPoint;

        if (Physics.Raycast(_shootTransform.position, _shootTransform.forward, out RaycastHit hitInfo, _maxShootDistance))
        {
            endPoint= hitInfo.point;
        }
        else
        {
            endPoint = _shootTransform.forward * _maxShootDistance;
            endPoint.y = _shootTransform.position.y;
        }
        
        _lineRendererPositions[1] = endPoint;

        _lineRenderer.SetPositions(_lineRendererPositions);
    }

    private void ValidateShoot()
    {
        if (!_hasLineOfSight || _aggroProgress < 1f)
        {
            _timeSinceLastShot = Mathf.Max(0, _timeSinceLastShot - Time.deltaTime);
            return;
        }

        if(_timeSinceLastShot <= 0)
        {
            Shoot();
            _timeSinceLastShot = _fireRate;
        }
        else
        {
            _timeSinceLastShot -= Time.deltaTime;
        }
    }

    private void Shoot()
    {
        if (_playerTarget == null || !_hasLineOfSight || _aggroProgress < 1f)
        {
            GetNextTarget();
            return;
        }

        SetShootTrigger();
        DoMuzzleFlash();
        _playerTarget.ApplyTurretDamage(_damage);
    }

    private bool CanSeeTarget(out Vector3 direction)
    {
        direction = Vector3.zero;

        if (_target == null || _playerTarget == null || _shootTransform == null)
        {
            return false;
        }

        Vector3 toTarget = _target.position - _shootTransform.position;
        float distance = toTarget.magnitude;

        if (distance > _maxShootDistance || distance <= 0.01f)
        {
            return false;
        }

        Vector3 viewForward = _turretRotationObject.transform.forward;
        viewForward.y = 0f;
        if (viewForward.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        Vector3 flatToTarget = toTarget;
        flatToTarget.y = 0f;
        if (flatToTarget.sqrMagnitude <= 0.0001f)
        {
            return true;
        }

        float angleToTarget = Vector3.Angle(viewForward.normalized, flatToTarget.normalized);
        if (angleToTarget > _viewAngle * 0.5f)
        {
            return false;
        }

        Vector3 rayDirection = toTarget / distance;
        if (!Physics.Raycast(_shootTransform.position, rayDirection, out RaycastHit hitInfo, distance, _lineOfSightMask, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        PlayerHealth hitPlayer = hitInfo.collider.GetComponentInParent<PlayerHealth>();
        if (hitPlayer == null || hitPlayer != _playerTarget)
        {
            return false;
        }

        direction = toTarget;
        return true;
    }

    private bool IsStillAggro()
    {
        return Time.time - _lastSeenTime <= Mathf.Max(0f, _loseSightGraceTime);
    }

    private void DoMuzzleFlash()
    {
        if (_muzzleFlash == null)
        {
            return;
        }

        _muzzleFlash.SetActive(true);
        InvokeShootSound();
        StartCoroutine(DisableAfter(_muzzleFlash, _muzzleFlashActiveDuration));
    }

    private void InvokeShootSound()
    {
        if (_shootAudioSource != null && _shootAudioSource.isPlaying)
        {
            return;
        }

        _onShootSound?.Invoke();
    }

    private IEnumerator DisableAfter(GameObject objectToDisable, float delay)
    {
        yield return new WaitForSeconds(delay);
        objectToDisable.SetActive(false);

        if (objectToDisable == _muzzleFlash)
        {
            StopShootSound();
        }
    }

    private void StopShootSound()
    {
        if (_shootAudioSource != null && _shootAudioSource.isPlaying)
        {
            _shootAudioSource.Stop();
        }

        _onShootSoundStop?.Invoke();
    }

    private void GetNextTarget()
    {
        if (!_delayingBeforeNextTarget)
        {
            _delayingBeforeNextTarget = true;
            StartCoroutine(FindTargetAfterDelay());
        }
    }

    private IEnumerator FindTargetAfterDelay()
    {
        yield return new WaitForSeconds(_delayBeforeNextTarget);

        if (_playerTarget == null)
        {
            _playerTarget = FindObjectOfType<PlayerHealth>();
        }

        _target = _playerTarget != null ? _playerTarget.transform : null;
        _delayingBeforeNextTarget = false;
    }
  
    private void SetShootTrigger()
    {
        if (_animator != null)
        {
            _animator.SetTrigger("Shoot");
        }
    }

    private void ResetShootTrigger()
    {
        if (_animator != null)
        {
            _animator.ResetTrigger("Shoot");
        }
    }
}
