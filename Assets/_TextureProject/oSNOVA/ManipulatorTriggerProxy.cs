using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ManipulatorTriggerProxy : MonoBehaviour
{
    private ManipulatorController _controller;
    private ManipulatorController.AnimStateConfig _config;
    private string _playerTag;

    internal void Init(
        ManipulatorController controller,
        ManipulatorController.AnimStateConfig config,
        string playerTag)
    {
        _controller = controller;
        _config     = config;
        _playerTag  = playerTag;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(_playerTag))
            _controller.OnZoneEnter(_config);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(_playerTag))
            _controller.OnZoneExit(_config);
    }
}