using UnityEngine;

namespace Snow2.Enemies
{
    /// <summary>
    /// 敌人数据（ScriptableObject）。
    ///
    /// 配置方式：
    /// 1) 在 Project 窗口：右键 -> Create -> Snow2 -> Enemies -> Enemy Data
    /// 2) 为不同敌人各建一个数据资产（如 RedDemonData、BlueMonkeyData…）
    /// 3) 在敌人 Prefab/场景对象上挂对应的 EnemyBase 派生脚本，并把 Data 字段拖上去
    /// 4) 运行时 EnemyBase.Start() 会根据 tintColor 自动给 SpriteRenderer 上色
    /// </summary>
    [CreateAssetMenu(menuName = "Snow2/Enemies/Enemy Data", fileName = "EnemyData")]
    public sealed class EnemyDataSO : ScriptableObject
    {
        [Header("Movement")]
        [Min(0f)]
        public float moveSpeed = 1.6f;

        [Header("Attack")]
        /// <summary>
        /// 攻击频率（次/秒）。
        /// 例如 0.33 表示平均约 3 秒一次；1 表示 1 秒一次。
        /// </summary>
        [Min(0f)]
        public float attackRate = 0.33f;

        [Header("Jump")]
        /// <summary>
        /// 跳跃力度（向上的瞬时速度/冲量基准）。
        /// 子类可根据需要用 SetVelocity 或 AddForce 解释。
        /// </summary>
        [Min(0f)]
        public float jumpForce = 7.5f;

        [Header("Visual")]
        public Color tintColor = Color.white;
    }
}

