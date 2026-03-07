using UnityEngine;
using Snow2.Player;
using Snow2.Balance;

namespace Snow2.PowerUps
{
    /// <summary>
    /// 蓝药水：增强雪球威力（用于影响投射物判定/尺寸/速度等）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BluePotion : PowerUpItem
    {
        public override void ApplyEffect(PlayerController2D player)
        {
            if (player == null)
            {
                return;
            }
            player.ApplyPotion(PlayerController2D.PotionType.Blue, PotionBalance.BlueDurationSeconds, PotionBalance.BlueSnowballPowerMultiplier);
        }
    }
}
