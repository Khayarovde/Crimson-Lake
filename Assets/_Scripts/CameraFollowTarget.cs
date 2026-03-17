using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollowTarget : MonoBehaviour
{
    public Transform target;
    Vector3 targetPos;
    public float moveSpeed = 5;
    public float smooth = 0.2f;
    [SerializeField] private bool keepInitialOffset = true;
    [SerializeField] private bool useManualOffset;
    [SerializeField] private Vector3 manualOffset = new Vector3(0f, 1.6f, -3f);
    private Vector3 velocity = Vector3.zero;
    private bool warnedAboutTarget;
    private Vector3 followOffset;
    private bool isOffsetInitialized;

    // Start is called before the first frame update
    void Start()
    {
        if (GetComponent("CinemachineBrain") != null)
        {
            Debug.LogWarning("CameraFollowTarget отключен: на этом объекте есть CinemachineBrain, он сам управляет камерой.", this);
            enabled = false;
            return;
        }

        TryInitializeOffset();
    }

    // Update is called once per frame
    void LateUpdate()
    {
        MoveWithTarget();
    }
    void MoveWithTarget()
    {
        if (target == null)
        {
            if (!warnedAboutTarget)
            {
                Debug.LogWarning("CameraFollowTarget: не назначен target.", this);
                warnedAboutTarget = true;
            }
            return;
        }

        if (!isOffsetInitialized)
        {
            TryInitializeOffset();
        }

        warnedAboutTarget = false;
        targetPos = target.transform.position + GetActiveOffset();
        //transform.position = Vector3.Lerp(transform.position, targetPos, moveSpeed * Time.deltaTime* smooth);
        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref velocity, smooth);
    }

    private void TryInitializeOffset()
    {
        if (target == null)
        {
            return;
        }

        followOffset = keepInitialOffset ? transform.position - target.position : Vector3.zero;
        isOffsetInitialized = true;
    }

    private Vector3 GetActiveOffset()
    {
        if (useManualOffset)
        {
            return manualOffset;
        }

        return followOffset;
    }
}