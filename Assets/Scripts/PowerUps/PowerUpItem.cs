using UnityEngine;
using Snow2.Player;

namespace Snow2.PowerUps
{
    /// <summary>
    /// 强化道具基类：
    /// - 通过 Trigger 与玩家接触时自动拾取
    /// - 统一播放拾取音效并销毁自身
    /// - 具体效果由子类重写 ApplyEffect
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public abstract class PowerUpItem : MonoBehaviour
    {
        [Header("Pickup")]
        [SerializeField] private AudioClip pickupSfx;
        [SerializeField, Range(0f, 1f)] private float pickupSfxVolume = 1f;
        [SerializeField] private bool destroyOnPickup = true;

        protected virtual void Awake()
        {
            // 通用兜底：确保“至少一个 Trigger Collider”存在，避免把实体落地 Collider 误改成 Trigger。
            var cols = GetComponents<Collider2D>();
            if (cols == null || cols.Length == 0)
            {
                return;
            }

            for (var i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null && cols[i].isTrigger)
                {
                    return;
                }
            }

            // 若没有任何 Trigger，则把第一个 Collider 改成 Trigger 作为拾取判定。
            cols[0].isTrigger = true;
        }

        /// <summary>
        /// 子类实现具体强化逻辑。
        /// </summary>
        public virtual void ApplyEffect(PlayerController2D player)
        {
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null)
            {
                return;
            }

            var player = other.GetComponentInParent<PlayerController2D>();
            if (player == null)
            {
                return;
            }

            ApplyEffect(player);

            if (pickupSfx != null)
            {
                AudioSource.PlayClipAtPoint(pickupSfx, transform.position, pickupSfxVolume);
            }

            if (destroyOnPickup)
            {
                Destroy(gameObject);
            }
        }
    }
}
