using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollowTarget : MonoBehaviour
{
    public Transform target;
    Vector3 targetPos;
    public Vector3 offsetPos;
    public float moveSpeed = 5;
    public float smooth = 0.2f;
    private Vector3 velocity = Vector3.zero;
    private bool warnedAboutTarget;

    // Start is called before the first frame update
    void Start()
    {
        if (GetComponent("CinemachineBrain") != null)
        {
            Debug.LogWarning("CameraFollowTarget отключен: на этом объекте есть CinemachineBrain, он сам управляет камерой.", this);
            enabled = false;
            return;
        }
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

        warnedAboutTarget = false;
        targetPos = target.transform.position + offsetPos;
        //transform.position = Vector3.Lerp(transform.position, targetPos, moveSpeed * Time.deltaTime* smooth);
        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref velocity, smooth);
    }
}