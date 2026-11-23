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
        [Tooltip("캐릭터에서 Q의 조준 지점까지 허용되는 최대 거리입니다. 커서가 더 멀면 사거리 끝에 조준합니다.")]
        [Min(0f), InspectorName("시전 사거리 (m)")] public float CastRange;
        [Tooltip("가위의 힌지에서 부채꼴 끝까지의 거리입니다. 공격 판정과 지면 범위 표시가 함께 바뀝니다.")]
        [Min(.01f), InspectorName("부채꼴 길이 (m)")] public float Reach;
        [Tooltip("부채꼴의 왼쪽 끝부터 오른쪽 끝까지의 전체 각도입니다. 조준 방향을 기준으로 양쪽에 절반씩 적용합니다.")]
        [Range(1f, 179f), InspectorName("부채꼴 전체 각도 (°)")] public float FanAngle;
        [Tooltip("강화 피해를 주는 중앙 띠의 전체 폭입니다. 조준축 좌우에 절반씩 적용되며, 0이면 부채꼴 전체가 외곽 피해를 줍니다.")]
        [Min(0f), InspectorName("중앙 전체 폭 (m)")] public float CenterWidth;
        [Tooltip("Q 입력부터 가위가 닫히고 피해가 들어갈 때까지의 시간입니다.")]
        [Min(.01f), InspectorName("선딜레이 (초)")] public float Windup;
        [Tooltip("Q 적중 후 공격 속도 증가를 받는 기본 공격의 최대 횟수입니다. E2도 한 번을 소비하며, 0이면 증가 효과가 꺼집니다.")]
        [Min(0), InspectorName("공격 속도 증가 적용 횟수")] public int HasteAttacks;
        [Tooltip("Q 적중 시점부터 공격 속도 증가가 유지되는 최대 시간입니다. 횟수를 먼저 소진하면 그때 종료됩니다.")]
        [Min(0f), InspectorName("공격 속도 증가 유지 시간 (초)")] public float HasteDuration;
        public float HalfAngle => FanAngle * .5f;
        public float CenterHalfWidth => CenterWidth * .5f;
    }

    [Serializable]
    internal struct Mark
    {
        [Tooltip("캐릭터가 W 표식을 붙일 수 있는 대상까지의 최대 거리입니다.")]
        [Min(0f), InspectorName("시전 사거리 (m)")] public float CastRange;
        [Tooltip("W 표식이 붙은 뒤 완성되기까지의 피해 축적 시간입니다. 0이면 표식이 즉시 완성됩니다.")]
        [Min(0f), InspectorName("표식 완성까지 (초)")] public float ChargeTime;
        [Tooltip("표식 완성 후 발동 가능한 공격으로 폭발시킬 수 있는 시간입니다. 이 시간 안에 폭발하지 않으면 사라집니다.")]
        [Min(.01f), InspectorName("완성 후 폭발 가능 시간 (초)")] public float DetonationWindow;
        public float TotalDuration => ChargeTime + DetonationWindow;
    }

    [Serializable]
    internal struct ShadowDash
    {
        [Tooltip("E1 시전 방향으로 돌진하는 목표 거리입니다. 경기장 경계에 닿으면 이동 거리가 짧아집니다.")]
        [Min(0f), InspectorName("E1 돌진 거리 (m)")] public float DashDistance;
        [Tooltip("E1 돌진을 시작한 뒤 도착하기까지의 시간입니다. 이동과 돌진 애니메이션에 함께 적용됩니다.")]
        [Min(.01f), InspectorName("E1 돌진 시간 (초)")] public float DashDuration;
        [Tooltip("E1 시전 시점부터 은신이 유지되는 시간을 스킬 레벨별로 정합니다. 돌진 중에도 시간이 흐릅니다.")]
        [InspectorName("E1 은신 시간 (초, 스킬 레벨별)")] public StealthTimes StealthByRank;
        [Tooltip("E1 후 강화 기본 공격으로 E2를 발동할 수 있는 최대 거리입니다. 캐릭터와 대상 사이에서 측정합니다.")]
        [Min(0f), InspectorName("E2 발동 사거리 (m)")] public float StrikeRange;
        [Tooltip("E2로 대상을 관통한 뒤 대상의 반대편에 얼마나 떨어져서 나타날지 정합니다. 0이면 대상 위치에 나타납니다.")]
        [Min(0f), InspectorName("E2 착지 시 대상과의 거리 (m)")] public float StrikeOffset;
        [Tooltip("E1 시전 후 강화 기본 공격으로 E2를 사용할 수 있는 시간입니다. E2를 한 번 쓰거나 시간이 끝나면 종료됩니다.")]
        [Min(0f), InspectorName("E2 사용 가능 시간 (초)")] public float StrikeWindow;
    }

    [Serializable]
    internal struct StealthTimes
    {
        [Tooltip("E 스킬 레벨 1에서 E1 시전 후 유지되는 은신 시간입니다.")]
        [Min(0f), InspectorName("레벨 1")] public float Rank1;
        [Tooltip("E 스킬 레벨 2에서 E1 시전 후 유지되는 은신 시간입니다.")]
        [Min(0f), InspectorName("레벨 2")] public float Rank2;
        [Tooltip("E 스킬 레벨 3에서 E1 시전 후 유지되는 은신 시간입니다.")]
        [Min(0f), InspectorName("레벨 3")] public float Rank3;
        [Tooltip("E 스킬 레벨 4에서 E1 시전 후 유지되는 은신 시간입니다.")]
        [Min(0f), InspectorName("레벨 4")] public float Rank4;
        [Tooltip("E 스킬 레벨 5에서 E1 시전 후 유지되는 은신 시간입니다.")]
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
        [Tooltip("최근 4초 안에 피해를 준 대상의 그림자로 진입할 수 있는 최대 거리입니다. 진입 가능 표시에도 적용됩니다.")]
        [Min(0f), InspectorName("R1 진입 사거리 (m)")] public float EntryRange;
        [Tooltip("대상 위치에서 커서 방향으로 빠져나오는 거리입니다. R1 중 탈출 방향 안내와 R2 참격 이동 거리에 함께 적용됩니다.")]
        [Min(0f), InspectorName("R2 탈출 거리 (m)")] public float ExitDistance;
        [Tooltip("R2를 시작한 뒤 탈출 지점에 도착하기까지의 시간입니다. 이동, 캐릭터 애니메이션, 참격이 이 시간에 맞춰 재생됩니다.")]
        [Min(.01f), InspectorName("R2 탈출 시간 (초)")] public float ExitDuration;
    }
}
