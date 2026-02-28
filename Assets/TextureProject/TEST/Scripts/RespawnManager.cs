using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RespawnManager : MonoBehaviour
{
    bool _canStartCoroutines = true;

    private void OnDisable()
    {
        _canStartCoroutines = false;
        StopAllCoroutines();
    }

    public void Respawn(GameObject objectToReactivate, float delay)
    {
        if (isActiveAndEnabled && _canStartCoroutines) 
        {
            StartCoroutine(RespawnAfter(objectToReactivate, delay));
        }
    }

    private IEnumerator RespawnAfter(GameObject objectToReactivate, float delay)
    {
        yield return new WaitForSeconds(delay);
        objectToReactivate.SetActive(true);
    }
}
