using UnityEngine;

namespace MinePrototype
{
    public abstract class BreakableDebris : MonoBehaviour
    {
        protected abstract int MaxHealth { get; } // 각 잔해물 종류가 정하는 최대 체력
        public int Health { get; private set; } // 파괴되기까지 남은 체력

        protected virtual void Awake() => Health = MaxHealth;

        public virtual void TakeHit(int damage, RaycastHit hit, Camera camera, float range)
        {
            if (Health == 0) return;

            Health = Mathf.Max(0, Health - damage);
            if (Health == 0) Destroy(gameObject);
        }
    }
}
