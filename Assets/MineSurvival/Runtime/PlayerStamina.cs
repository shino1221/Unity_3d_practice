using System;
using UnityEngine;

namespace MineSurvival
{
    // [역할] 달리기와 도구가 함께 쓰는 스태미나 잔량을 한 곳에서 관리합니다.
    // 플레이어 몸에 하나만 붙입니다. 이동/장비마다 별도 잔량을 만들면 서로의 소모를 알 수 없습니다.
    [DisallowMultipleComponent]
    public sealed class PlayerStamina : MonoBehaviour
    {
        [SerializeField, Min(1)] float maximum = 100;
        // 2026-11-13 회의 기준: 마지막 소모 뒤 3초를 기다리고, 이후 초당 10씩 회복합니다.
        [SerializeField, Min(0)] float recoveryDelay = 3;
        [SerializeField, Min(0)] float recoveryPerSecond = 10;
        [SerializeField, Min(0)] float baseMaximumRecoveryPerSecond = 1;
        public const float PickaxeCost = 12;
        public const float ShovelCost = 6;
        public const float RockfallDamage = 20;

        // 현재 잔량과 최대치 손실은 별개입니다. 최대치가 변해도 현재 잔량 비율을 재계산하지 않습니다.
        float current = 100;
        float maximumLoss;
        float recoveryWait;
        public float Maximum => Mathf.Max(0, maximum - maximumLoss);
        public float Current => Mathf.Min(current, Maximum);
        // UI 게이지에는 0~1 값인 Normalized를, 숫자 표시에는 Current와 Maximum을 사용합니다.
        public float Normalized => Maximum > 0 ? Current / Maximum : 0;
        public bool IsExhausted => Current <= 0;
        public bool IsDead => Maximum <= 0;
        public bool IsInBase { get; private set; }
        // 게임 종료 담당자는 이 알림에 연결합니다. 현재 잔량이 0일 때는 발생하지 않습니다.
        public event Action Died;

        void Awake() => current = maximum;

        // 저수준 잔량 차감입니다. 비용 부족 시 행동을 막아야 하는 달리기/도구는 TryConsume을 사용합니다.
        // 실제 사용량이 있을 때만 회복 대기 시간을 다시 시작합니다.
        public void Consume(float amount)
        {
            RequireNonNegativeFinite(amount, nameof(amount));
            if (amount == 0 || Current == 0 || IsDead) return;
            current = Mathf.Max(0, Current - amount);
            recoveryWait = recoveryDelay;
        }

        // [장비 담당 연결] 곡괭이/삽 한 번 사용처럼 정해진 비용을 전부 낼 수 있을 때만 true입니다.
        // 곡괭이는 PickaxeCost(12), 삽은 ShovelCost(6). 헛스윙/잘못된 도구도 실제 사용이면 비용을 냅니다.
        // 홀드 도구는 실패 시 자체 입력을 잠그고, 버튼을 놓은 뒤 새 입력에서 다시 시도해야 합니다.
        // 잔량 부족으로 실패한 요청은 잔량이나 회복 시간을 바꾸지 않습니다.
        public bool TryConsume(float amount)
        {
            RequireNonNegativeFinite(amount, nameof(amount));
            if (IsDead || amount > Current) return false;
            Consume(amount);
            return true;
        }

        // CapsuleMovement.Move가 매 프레임 한 번 시간을 진행합니다. 장비/UI는 중복 호출하지 않습니다.
        // 걷기·가방·손전등 등 무료 행동은 이 대기를 초기화하지 않습니다. 소진 후 별도 재개 잔량도 없습니다.
        public void Recover(float seconds)
        {
            RequireNonNegativeFinite(seconds, nameof(seconds));
            if (IsDead) return;
            // 거점은 줄어든 최대치만 복구합니다. 현재 잔량의 대기/회복 속도는 그대로입니다.
            if (IsInBase) maximumLoss = Mathf.Max(0, maximumLoss - baseMaximumRecoveryPerSecond * seconds);
            float waiting = Mathf.Min(seconds, recoveryWait);
            recoveryWait -= waiting;
            // 한 프레임이 대기 종료 시점을 지나도, 대기 이후에 남은 시간만 회복에 사용합니다.
            current = Mathf.Min(Maximum, Current + (seconds - waiting) * recoveryPerSecond);
        }

        // [맵 담당 연결] 낙석 피격 때 ReduceMaximum(RockfallDamage)를 한 번 호출합니다.
        // 미정인 잔량 처리에는 최소한의 범위 제한만 적용: 현재 잔량이 새 최대치를 넘으면 그만큼만 줄입니다.
        public void ReduceMaximum(float amount)
        {
            RequireNonNegativeFinite(amount, nameof(amount));
            if (IsDead || amount == 0) return;
            maximumLoss = Mathf.Min(maximum, maximumLoss + amount);
            current = Mathf.Min(current, Maximum);
            if (IsDead) Died?.Invoke();
        }

        // [거점 담당 연결] 회복 가능한 거점에 들어가면 true, 모든 거점에서 벗어나면 false입니다.
        // 거점 복구 여부/범위는 거점 담당자가 판정합니다. 최대치 0 이후에는 이 함수로 부활하지 않습니다.
        public void SetInBase(bool inBase) => IsInBase = inBase;

        static void RequireNonNegativeFinite(float value, string name)
        {
            if (value < 0 || float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(name);
        }
    }
}
