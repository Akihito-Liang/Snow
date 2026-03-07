using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Snow2
{
    /// <summary>
    /// 通用刷怪器：支持在 Inspector 中配置 Wave 与 SpawnPoints。
    ///
    /// 注意：
    /// - EnemyCount 的统计建议复用 GameManager（EnemyController 体系会自动注册/注销）。
    /// - LevelManager 会把“所有 spawner 已完成 + 场上敌人清空”作为通关门控。
    /// </summary>
    public sealed class EnemySpawner : MonoBehaviour
    {
        public enum SpawnPointMode
        {
            Random,
            RoundRobin
        }

        [Serializable]
        public sealed class SpawnEntry
        {
            public GameObject prefab;
            [Min(1)] public int count = 1;
            [Min(0f)] public float intervalSeconds = 0.2f;
        }

        [Serializable]
        public sealed class Wave
        {
            public string name;
            [Min(0f)] public float startDelaySeconds = 0.0f;
            public SpawnEntry[] spawns;
        }

        [Header("Spawn Points")]
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private SpawnPointMode spawnPointMode = SpawnPointMode.Random;

        [Header("Waves")]
        [SerializeField] private Wave[] waves;
        [SerializeField] private bool playOnStart = true;

        [Header("Events")]
        public UnityEvent<int> OnWaveStarted;
        public UnityEvent<int> OnWaveCompleted;
        public UnityEvent OnAllWavesCompleted;

        public bool IsCompleted { get; private set; }

        private Coroutine _routine;
        private int _rrIndex;

        private void Start()
        {
            if (playOnStart)
            {
                StartSpawning();
            }
        }

        public void StartSpawning()
        {
            if (_routine != null)
            {
                return;
            }

            IsCompleted = false;
            _rrIndex = 0;
            _routine = StartCoroutine(SpawnRoutine());
        }

        public void StopSpawning()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
        }

        private IEnumerator SpawnRoutine()
        {
            if (waves == null || waves.Length == 0)
            {
                IsCompleted = true;
                OnAllWavesCompleted?.Invoke();
                _routine = null;
                yield break;
            }

            for (var wi = 0; wi < waves.Length; wi++)
            {
                var w = waves[wi];
                OnWaveStarted?.Invoke(wi);

                if (w != null && w.startDelaySeconds > 0f)
                {
                    yield return new WaitForSeconds(w.startDelaySeconds);
                }

                if (w != null && w.spawns != null)
                {
                    for (var si = 0; si < w.spawns.Length; si++)
                    {
                        var s = w.spawns[si];
                        if (s == null || s.prefab == null)
                        {
                            continue;
                        }

                        var c = Mathf.Max(1, s.count);
                        var interval = Mathf.Max(0f, s.intervalSeconds);
                        for (var k = 0; k < c; k++)
                        {
                            SpawnOne(s.prefab);
                            if (interval > 0f)
                            {
                                yield return new WaitForSeconds(interval);
                            }
                        }
                    }
                }

                OnWaveCompleted?.Invoke(wi);
            }

            IsCompleted = true;
            OnAllWavesCompleted?.Invoke();
            _routine = null;
        }

        private void SpawnOne(GameObject prefab)
        {
            var p = PickSpawnPoint();
            var pos = p != null ? p.position : transform.position;
            Instantiate(prefab, pos, Quaternion.identity);
        }

        private Transform PickSpawnPoint()
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                return null;
            }

            if (spawnPointMode == SpawnPointMode.RoundRobin)
            {
                var idx = _rrIndex % spawnPoints.Length;
                _rrIndex++;
                return spawnPoints[idx];
            }

            return spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
        }
    }
}

