using System.Collections;
using UnityEngine;

namespace TapTapGameJam.Combat
{
    [DisallowMultipleComponent]
    public sealed class EnemyHealth : MonoBehaviour
    {
        [SerializeField, Min(1)] int maxHealth = 3;
        public int MaxHealth => Mathf.Max(1, maxHealth);
        public int CurrentHealth { get; private set; }
        public bool IsAlive => CurrentHealth > 0 && isActiveAndEnabled;
        SpriteRenderer[] sprites;
        Color[] colors;
        Coroutine flash;

        void Awake()
        {
            sprites = GetComponentsInChildren<SpriteRenderer>(true);
            colors = new Color[sprites.Length];
            for (int i = 0; i < sprites.Length; i++) colors[i] = sprites[i].color;
        }
        void OnEnable() => CurrentHealth = MaxHealth;

        public bool TakeDamage(int amount)
        {
            if (amount <= 0 || !IsAlive) return false;
            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
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
            if (flash != null) StopCoroutine(flash);
            flash = null; RestoreColors();
        }
    }
}
