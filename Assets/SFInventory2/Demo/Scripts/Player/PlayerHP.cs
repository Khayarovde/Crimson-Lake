using System.Collections;
using Parity.SFInventory2.Core;
using Parity.SFInventory2.Core.Commands;
using Parity.SFInventory2.Custom;
using Parity.SFInventory2.Demo.Commands;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Parity.SFInventory2.Demo.Player
{
    public class PlayerHP : MonoBehaviour
    {
        [Header("Health Settings")]
        [SerializeField] private float maxHealth = 100;
        [SerializeField] private Slider healthSlider;
        [SerializeField] private TextMeshProUGUI healthText;

        private float currentHealth;

        private Coroutine _toxicCoroutine;

        private void Start()
        {
            InitializeHealth();

            SFInventoryCommandRouter.Instance.RegisterHandler<HealCommand>(HandleHeal);
            SFInventoryCommandRouter.Instance.RegisterHandler<ToxicCommand>(HandleToxic);
        }

        private void HandleHeal(InventoryCell cell, object context)
        {
            if (cell.Item is FoodItem food)
            {
                Heal(food.healAmount);
                UpdateHealthUI();

                cell.ItemsCount--;
                if (cell.ItemsCount <= 0)
                    cell.SetInventoryItem(null);
                cell.UpdateCellUI();
            }
        }

        private void HandleToxic(InventoryCell cell, object context)
        {
            if (cell.Item is FoodItem food)
            {
                ApplyToxicEffect(food.healAmount, 5f);
                cell.ItemsCount--;
                if (cell.ItemsCount <= 0)
                    cell.SetInventoryItem(null);
                cell.UpdateCellUI();
            }
        }

        public void ApplyToxicEffect(float totalDamage, float duration)
        {
            if (_toxicCoroutine != null)
            {
                StopCoroutine(_toxicCoroutine);
            }

            _toxicCoroutine = StartCoroutine(ToxicEffectCoroutine(totalDamage, duration));
        }

        private IEnumerator ToxicEffectCoroutine(float totalDamage, float duration)
        {
            float tickInterval = 0.2f; // Every second
            float ticks = Mathf.FloorToInt(duration / tickInterval);
            float damagePerTick = totalDamage / ticks;

            for (int i = 0; i < ticks; i++)
            {
                TakeDamage(damagePerTick);
                UpdateHealthUI();

                yield return new WaitForSeconds(tickInterval);
            }
        }

        private void InitializeHealth()
        {
            currentHealth = maxHealth;
            UpdateHealthUI();
        }

        public void Heal(float healAmount)
        {
            float previousHealth = currentHealth;
            currentHealth = Mathf.Min(currentHealth + healAmount, maxHealth);

            if (currentHealth != previousHealth)
            {
                UpdateHealthUI();
            }
        }

        public void TakeDamage(float damageAmount)
        {
            float previousHealth = currentHealth;
            currentHealth = Mathf.Max(currentHealth - damageAmount, 0);

            if (currentHealth != previousHealth)
            {
                UpdateHealthUI();
            }
        }

        private void UpdateHealthUI()
        {
            if (healthSlider != null)
            {
                healthSlider.value = (float)currentHealth / maxHealth;
            }

            if (healthText != null)
            {
                healthText.text = $"{currentHealth}/{maxHealth}";
            }
        }
    }
}