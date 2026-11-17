using System.Collections.Generic;

namespace KimchiBreaker.Core
{
    /// <summary>
    /// 스킬 식별자. 값(숫자)은 네트워크로 전송되므로 바꾸지 말고, 새 스킬은 뒤에 추가한다.
    /// </summary>
    public enum SkillId : byte
    {
        None = 0,

        // 패시브 (라운드 내내 적용)
        FlameGloves = 1,       // 불꽃 고무장갑
        AutoHands = 2,         // 자동 손놀림
        SeasoningSplash = 3,   // 양념 튀기기
        FishSauceMagnet = 4,   // 젓갈 자석
        WarriorsBlessing = 5,  // 김치전사의 가호

        // 액티브 (라운드당 1회)
        PepperPowder = 6,      // 후춧가루 / 고춧가루 뿌리기
        HomeRunSmash = 7,      // 홈런 스매시
        GoldenCabbage = 8,     // 황금 배추 소환
        SeasoningBomb = 9,     // 양념 폭탄
    }

    /// <summary>스킬 목록과 분류 정보.</summary>
    public static class SkillCatalog
    {
        static readonly SkillId[] s_All =
        {
            SkillId.FlameGloves,
            SkillId.AutoHands,
            SkillId.SeasoningSplash,
            SkillId.FishSauceMagnet,
            SkillId.WarriorsBlessing,
            SkillId.PepperPowder,
            SkillId.HomeRunSmash,
            SkillId.GoldenCabbage,
            SkillId.SeasoningBomb,
        };

        /// <summary>추첨 대상이 되는 모든 스킬(None 제외).</summary>
        public static IReadOnlyList<SkillId> All => s_All;

        public static bool IsDefined(SkillId id) => id >= SkillId.FlameGloves && id <= SkillId.SeasoningBomb;

        public static bool IsPassive(SkillId id) => id >= SkillId.FlameGloves && id <= SkillId.WarriorsBlessing;

        public static bool IsActive(SkillId id) => id >= SkillId.PepperPowder && id <= SkillId.SeasoningBomb;

        /// <summary>화면에 표시할 한글 이름.</summary>
        public static string DisplayName(SkillId id)
        {
            switch (id)
            {
                case SkillId.FlameGloves: return "불꽃 고무장갑";
                case SkillId.AutoHands: return "자동 손놀림";
                case SkillId.SeasoningSplash: return "양념 튀기기";
                case SkillId.FishSauceMagnet: return "젓갈 자석";
                case SkillId.WarriorsBlessing: return "김치전사의 가호";
                case SkillId.PepperPowder: return "고춧가루 뿌리기";
                case SkillId.HomeRunSmash: return "홈런 스매시";
                case SkillId.GoldenCabbage: return "황금 배추 소환";
                case SkillId.SeasoningBomb: return "양념 폭탄";
                default: return "없음";
            }
        }

        /// <summary>화면에 표시할 한 줄 설명.</summary>
        public static string Description(SkillId id)
        {
            switch (id)
            {
                case SkillId.FlameGloves: return "클릭 한 번에 묻는 양념이 늘어난다.";
                case SkillId.AutoHands: return "마우스를 누르고만 있어도 자동으로 연타한다.";
                case SkillId.SeasoningSplash: return "주변 재료에도 양념이 함께 묻는다.";
                case SkillId.FishSauceMagnet: return "주변 재료가 내 앞으로 서서히 끌려온다.";
                case SkillId.WarriorsBlessing: return "마지막 15초 동안 양념 효율이 크게 오른다.";
                case SkillId.PepperPowder: return "다른 플레이어의 시야를 잠시 가린다.";
                case SkillId.HomeRunSmash: return "다음 한 번의 타격이 재료를 멀리 날려 보낸다.";
                case SkillId.GoldenCabbage: return "김치 여러 개로 치는 황금 배추를 내 앞에 떨어뜨린다.";
                case SkillId.SeasoningBomb: return "조준한 곳 주변 재료의 양념을 한 번에 채운다.";
                default: return string.Empty;
            }
        }
    }
}
