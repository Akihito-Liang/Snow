using UnityEngine;

namespace Snow2.Balance
{
    public static class EnemyDropBalance
    {
        // Enemy death drop
        public static readonly bool EnableDeathDrop = true;

        // 调高：敌人死后掉落“药水/蛋糕/点心”的整体概率
        public const float DeathDropChance = 0.80f;

        // Weighted selection (Potion/Cake/Snack)
        public const float PotionWeight = 2.0f;
        public const float CakeWeight = 0.7f;
        public const float SnackWeight = 1.2f;

        // Scores
        public const int CakeScore = 30;
        public const int SnackScore = 15;

        // Spawn offset
        public const float DropYOffset = 0.6f;

        // Pickup layer naming
        public const string PickupLayerName1 = "Pickup";
        public const string PickupLayerName2 = "Pickups";
        public const string PickupLayerName3 = "PowerUp";
        public const int FallbackPickupLayer = 2; // Ignore Raycast

        // Visuals
        public static readonly Color CakeColor = new Color(1f, 0.55f, 0.75f, 1f);
        public static readonly Color SnackColor = new Color(1f, 0.75f, 0.2f, 1f);
        public const float PotionScale = 0.55f;
        public const float CakeSnackScale = 0.5f;
        public const int PotionSortingOrder = 25;
        public const int RewardSortingOrder = 22;
    }
}
