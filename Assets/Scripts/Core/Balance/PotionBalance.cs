using UnityEngine;

namespace Snow2.Balance
{
    public static class PotionBalance
    {
        // Duration
        public const float RedDurationSeconds = 8f;
        public const float BlueDurationSeconds = 8f;
        public const float YellowDurationSeconds = 8f;
        public const float GreenDurationSeconds = 10f;

        // Multipliers
        public const float RedSpeedMultiplier = 1.5f;
        public const float BlueSnowballPowerMultiplier = 1.35f;
        public const float YellowRangeMultiplier = 1.5f;

        // Tint colors (used for player color blending & potion visuals)
        public static readonly Color RedColor = new Color(1f, 0.25f, 0.25f);
        public static readonly Color BlueColor = new Color(0.25f, 0.45f, 1f);
        public static readonly Color YellowColor = new Color(1f, 0.9f, 0.15f);
        public static readonly Color GreenColor = new Color(0.2f, 0.9f, 0.35f);

        // Player tint blending weight per active potion
        public const float PlayerTintWeightEach = 0.35f;
    }
}

