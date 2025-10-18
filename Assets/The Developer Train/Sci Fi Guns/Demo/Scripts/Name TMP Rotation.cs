using UnityEngine;

namespace TheDeveloperTrain.SciFiGuns
{
    public class NameTMPRotation : MonoBehaviour
    {
        private Transform cameraTransform;
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            cameraTransform = UnityEngine.Camera.main != null ? UnityEngine.Camera.main.transform : null;
        }

        // Update is called once per frame
        void Update()
        {
            transform.LookAt(cameraTransform);
        }
    }
}