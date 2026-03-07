using UnityEngine;
using Snow2.Player;
using Snow2.Balance;

namespace Snow2.PowerUps
{
    /// <summary>
    /// 绿药水：限时无敌/巨大化。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GreenPotion : PowerUpItem
    {
        public override void ApplyEffect(PlayerController2D player)
        {
            if (player == null)
            {
                return;
            }
            player.ApplyPotion(PlayerController2D.PotionType.Green, PotionBalance.GreenDurationSeconds);
        }
    }
}
