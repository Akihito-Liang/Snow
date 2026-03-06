using UnityEngine;
using Snow2.Player;

namespace Snow2.Rewards
{
    public enum PickupType
    {
        Sushi,
        Potion
    }

    public sealed class PickupItem : MonoBehaviour
    {
        public PickupType Type;
        public int SushiScore = 10;
        public float DespawnY = -12f;

        private void Update()
        {
            if (transform.position.y < DespawnY)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null)
            {
                return;
            }
            if (other.GetComponent<PlayerController2D>() == null)
            {
                return;
            }

            if (GameManager.Instance != null)
            {
                if (Type == PickupType.Sushi)
                {
                    GameManager.Instance.AddScore(SushiScore);
                }
                else if (Type == PickupType.Potion)
                {
                    GameManager.Instance.AddPotion(1);
                }
            }

            Destroy(gameObject);
        }
    }
}

