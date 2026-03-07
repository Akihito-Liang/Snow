using UnityEngine;
using Snow2.Player;
using Snow2.Balance;

namespace Snow2.PowerUps
{
    /// <summary>
    /// 红药水：移动速度提升。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RedPotion : PowerUpItem
    {
        public override void ApplyEffect(PlayerController2D player)
        {
            if (player == null)
            {
                return;
            }
            player.ApplyPotion(PlayerController2D.PotionType.Red, PotionBalance.RedDurationSeconds, PotionBalance.RedSpeedMultiplier);
        }
    }
}
