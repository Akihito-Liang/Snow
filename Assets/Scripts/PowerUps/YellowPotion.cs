using UnityEngine;
using Snow2.Player;
using Snow2.Balance;

namespace Snow2.PowerUps
{
    /// <summary>
    /// 黄药水：增加射程（影响投射物 lifetime/speed 等）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class YellowPotion : PowerUpItem
    {
        public override void ApplyEffect(PlayerController2D player)
        {
            if (player == null)
            {
                return;
            }
            player.ApplyPotion(PlayerController2D.PotionType.Yellow, PotionBalance.YellowDurationSeconds, PotionBalance.YellowRangeMultiplier);
        }
    }
}
