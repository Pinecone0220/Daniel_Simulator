using System;
using UnityEngine;

[Serializable]
internal sealed class DanielSkillSettings
{
    public Scissors Q = new Scissors
    {
        CastRange = 7f, Reach = 3.5f, FanAngle = 65f, CenterWidth = .6f, Windup = .53f,
        HasteAttacks = 3, HasteDuration = 3f
    };
    public Mark W = new Mark { CastRange = 7f, ChargeTime = 4f, DetonationWindow = 2.5f };
    public ShadowDash E = new ShadowDash
    {
        DashDistance = 3f, DashDuration = .28f, StealthByRank = new StealthTimes(1f, 1.2f, 1.4f, 1.6f, 1.8f),
        StrikeRange = 3f, StrikeOffset = 1.1f, StrikeWindow = 3f
    };
    public Ultimate R = new Ultimate { EntryRange = 3f, ExitDistance = 3f, ExitDuration = .3f };

    [Serializable]
    internal struct Scissors
    {
        [Min(0f), InspectorName("시전 사거리 (m)")] public float CastRange;
        [Min(.01f), InspectorName("부채꼴 길이 (m)")] public float Reach;
        [Range(1f, 179f), InspectorName("부채꼴 전체 각도 (°)")] public float FanAngle;
        [Min(0f), InspectorName("중앙 전체 폭 (m)")] public float CenterWidth;
        [Min(.01f), InspectorName("선딜레이 (초)")] public float Windup;
        [Min(0), InspectorName("공격 속도 증가 적용 횟수")] public int HasteAttacks;
        [Min(0f), InspectorName("공격 속도 증가 유지 시간 (초)")] public float HasteDuration;
        public float HalfAngle => FanAngle * .5f;
        public float CenterHalfWidth => CenterWidth * .5f;
    }

    [Serializable]
    internal struct Mark
    {
        [Min(0f), InspectorName("시전 사거리 (m)")] public float CastRange;
        [Min(0f), InspectorName("표식 완성까지 (초)")] public float ChargeTime;
        [Min(.01f), InspectorName("완성 후 폭발 가능 시간 (초)")] public float DetonationWindow;
        public float TotalDuration => ChargeTime + DetonationWindow;
    }

    [Serializable]
    internal struct ShadowDash
    {
        [Min(0f), InspectorName("E1 돌진 거리 (m)")] public float DashDistance;
        [Min(.01f), InspectorName("E1 돌진 시간 (초)")] public float DashDuration;
        [InspectorName("E1 은신 시간 (초, 스킬 레벨별)")] public StealthTimes StealthByRank;
        [Min(0f), InspectorName("E2 발동 사거리 (m)")] public float StrikeRange;
        [Min(0f), InspectorName("E2 착지 시 대상과의 거리 (m)")] public float StrikeOffset;
        [Min(0f), InspectorName("E2 사용 가능 시간 (초)")] public float StrikeWindow;
    }

    [Serializable]
    internal struct StealthTimes
    {
        [Min(0f), InspectorName("레벨 1")] public float Rank1;
        [Min(0f), InspectorName("레벨 2")] public float Rank2;
        [Min(0f), InspectorName("레벨 3")] public float Rank3;
        [Min(0f), InspectorName("레벨 4")] public float Rank4;
        [Min(0f), InspectorName("레벨 5")] public float Rank5;

        public StealthTimes(float rank1, float rank2, float rank3, float rank4, float rank5)
        {
            Rank1 = rank1;
            Rank2 = rank2;
            Rank3 = rank3;
            Rank4 = rank4;
            Rank5 = rank5;
        }

        public float AtRank(int rank) => rank switch { 1 => Rank1, 2 => Rank2, 3 => Rank3, 4 => Rank4, _ => Rank5 };
    }

    [Serializable]
    internal struct Ultimate
    {
        [Min(0f), InspectorName("R1 진입 사거리 (m)")] public float EntryRange;
        [Min(0f), InspectorName("R2 탈출 거리 (m)")] public float ExitDistance;
        [Min(.01f), InspectorName("R2 탈출 시간 (초)")] public float ExitDuration;
    }
}
