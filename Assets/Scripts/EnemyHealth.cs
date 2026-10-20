using System.Collections;
using UnityEngine;

namespace TapTapGameJam.Combat
{
    [DisallowMultipleComponent]
    public sealed class EnemyHealth : MonoBehaviour
    {
        [SerializeField, Min(1)] int maxHealth = 12;
        [SerializeField] EnemyResistance resistance;
        [SerializeField, Range(0, .5f)] float simultaneousHitWindow = .12f;
        [SerializeField] SpriteRenderer healthBarFill;
        readonly EnemyDamageRules damageRules = new EnemyDamageRules();
        Vector3 fullFillScale, fullFillPosition;
        float fillLeft;
        bool fillCaptured;
        public int MaxHealth => Mathf.Max(1, maxHealth);
        public int CurrentHealth { get; private set; }
        public bool IsAlive => CurrentHealth > 0 && isActiveAndEnabled;
        SpriteRenderer[] sprites;
        Color[] colors;
        Coroutine flash;

        void Awake()
        {
            if (healthBarFill == null)
            {
                var fill = transform.Find("HealthBarRoot/HealthBarFill");
                if (fill != null) healthBarFill = fill.GetComponent<SpriteRenderer>();
            }
            if (healthBarFill != null && !fillCaptured)
            {
                fullFillScale = healthBarFill.transform.localScale;
                fullFillPosition = healthBarFill.transform.localPosition;
                fillLeft = healthBarFill.sprite != null ? healthBarFill.sprite.bounds.min.x : 0;
                fillCaptured = true;
            }
            sprites = GetComponentsInChildren<SpriteRenderer>(true);
            colors = new Color[sprites.Length];
            for (int i = 0; i < sprites.Length; i++) colors[i] = sprites[i].color;
        }
        void OnEnable()
        {
            damageRules.Reset();
            CurrentHealth = MaxHealth;
            UpdateHealthBar();
        }

        public bool CanBeTargetedBy(TowerInstrument instrument) => IsAlive && EnemyDamageRules.CanHit(resistance, instrument);

        // True means the hit was accepted, even when the boss is waiting for a
        // second instrument. Towers must still show feedback for these hits.
        public bool ReceiveHit(int amount, TowerInstrument instrument, double hitTime)
        {
            if (amount <= 0 || !CanBeTargetedBy(instrument)) return false;
            int resolved = damageRules.Resolve(resistance, instrument, amount, hitTime, simultaneousHitWindow);
            if (resolved > 0) ApplyDamage(resolved);
            return true;
        }

        public bool TakeDamage(int amount)
        {
            // Legacy untyped callers cannot bypass a variant's resistance.
            return resistance == EnemyResistance.Any && ApplyDamage(amount);
        }

        bool ApplyDamage(int amount)
        {
            if (amount <= 0 || !IsAlive) return false;
            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            UpdateHealthBar();
            if (CurrentHealth == 0)
            {
                // Remove from all range queries now, before deferred destruction.
                gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(gameObject);
            }
            else if (Application.isPlaying)
            {
                if (flash != null) StopCoroutine(flash);
                flash = StartCoroutine(Flash());
            }
            return true;
        }

        void UpdateHealthBar()
        {
            if (healthBarFill == null || !fillCaptured) return;
            float ratio = Mathf.Clamp01((float)CurrentHealth / MaxHealth);
            var scale = fullFillScale;
            scale.x *= ratio;
            healthBarFill.transform.localScale = scale;
            healthBarFill.transform.localPosition = fullFillPosition +
                healthBarFill.transform.localRotation * Vector3.right * (fillLeft * (fullFillScale.x - scale.x));
        }

        IEnumerator Flash()
        {
            foreach (var sprite in sprites) if (sprite != null) sprite.color = new Color(1, .45f, .4f);
            yield return new WaitForSecondsRealtime(.1f);
            RestoreColors(); flash = null;
        }
        void RestoreColors()
        {
            if (sprites == null) return;
            for (int i = 0; i < sprites.Length; i++) if (sprites[i] != null) sprites[i].color = colors[i];
        }
        void OnDisable()
        {
            damageRules.Reset();
            if (flash != null) StopCoroutine(flash);
            flash = null; RestoreColors();
        }
    }
}
