using System.Collections;
using UnityEngine;

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
    [Tooltip("This is the object that should rotate")]
    private GameObject _turretRotationObject;
    [SerializeField]
    private LineRenderer _lineRenderer;
    [SerializeField]
    private GameObject _muzzleFlash;
    [SerializeField]
    private float _muzzleFlashActiveDuration = 0.25f;
    [SerializeField]
    private float _delayBeforeNextTarget = 1f;
    [SerializeField]
    [Min(1)]
    private int _bulletsToKillPlayer = 2;

    private GameObject _target;
    private Vector3[] _lineRendererPositions = new Vector3[2];
    private Animator _animator;
    private bool _delayingBeforeNextTarget = false;
    private float _timeSinceLastShot;
    private int _playerHitCount;

    private void Start()
    {
        _timeSinceLastShot = 0;
        _animator = GetComponent<Animator>();
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
        if(_target != null)
        {
            //Get direction from turret rotation object to the target.
            Vector3 direction = _target.transform.position - _turretRotationObject.transform.position;
            //Get the interpolated rotation between the turret rotation object and the target.
            Quaternion rotation = Quaternion.Slerp(_turretRotationObject.transform.rotation, Quaternion.LookRotation(direction), _rotationSpeed * Time.deltaTime);

            _turretRotationObject.transform.rotation = rotation;
            _turretRotationObject.transform.eulerAngles = new Vector3(0, _turretRotationObject.transform.eulerAngles.y, 0);
        }
        else
        {
            GetNextTarget();
        }
    }

    private void SetLineRendererPoints()
    {
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
        SetShootTrigger();
        DoMuzzleFlash();

        if (Physics.Raycast(_shootTransform.position, _shootTransform.forward, out RaycastHit hitInfo, _maxShootDistance))
        {
            if (hitInfo.collider.CompareTag("Player"))
            {
                PlayerHealth playerHealth = hitInfo.collider.GetComponentInParent<PlayerHealth>();
                if (playerHealth != null)
                {
                    _playerHitCount++;

                    if (_playerHitCount >= _bulletsToKillPlayer)
                    {
                        while (!playerHealth.IsDead)
                        {
                            playerHealth.TakeEnemyHit();
                        }

                        _playerHitCount = 0;
                    }
                }
            }
        }
    }

    private void DoMuzzleFlash()
    {
        _muzzleFlash.SetActive(true);
        StartCoroutine(DisableAfter(_muzzleFlash, _muzzleFlashActiveDuration));
    }

    private IEnumerator DisableAfter(GameObject objectToDisable, float delay)
    {
        yield return new WaitForSeconds(delay);
        objectToDisable.SetActive(false);
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
        _target = GameObject.FindGameObjectWithTag("Player");
        _delayingBeforeNextTarget = false;
    }
  
    private void SetShootTrigger()
    {
        _animator.SetTrigger("Shoot");
    }

    private void ResetShootTrigger()
    {
        _animator.ResetTrigger("Shoot");
    }
}
