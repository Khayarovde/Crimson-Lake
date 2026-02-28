using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Health : MonoBehaviour
{
    [SerializeField]
    private float _healthAmount = 100f;
    [SerializeField]
    private bool _shouldRespawn = true;
    [SerializeField]
    private float _respawnDelay = 2f;

    private RespawnManager _respawnManager;
    private float _currentHealth;

    public float CurrentHealth
    {
        get
        {
            return _currentHealth;
        }
    }

    private void Start()
    {
        _respawnManager = FindObjectOfType<RespawnManager>();
        _currentHealth = _healthAmount;
    }

    public void DealDamage(float damage)
    {
        _currentHealth -= damage;
        if(_currentHealth <= 0)
        {
            Die();
        }
    }

    private void OnEnable()
    {
        _currentHealth = _healthAmount;
    }

    private void Die()
    {
        if (_shouldRespawn)
        {
            _respawnManager.Respawn(gameObject, _respawnDelay);
        }
        
        gameObject.SetActive(false);
    }
}
