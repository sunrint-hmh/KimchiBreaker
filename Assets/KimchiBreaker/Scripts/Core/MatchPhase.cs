namespace KimchiBreaker.Core
{
    /// <summary>
    /// 매치 진행 단계. 호스트가 결정하고 모든 클라이언트에 동기화된다.
    /// 값(숫자)은 네트워크로 전송되므로 순서를 바꾸지 말 것.
    /// </summary>
    public enum MatchPhase : byte
    {
        /// <summary>대기실. 입장/준비/방장 설정.</summary>
        Lobby = 0,

        /// <summary>라운드 시작 연출(탑뷰 → 1인칭 줌인, 카운트다운). 아직 조작 불가.</summary>
        RoundIntro = 1,

        /// <summary>라운드 진행 중. 타이머가 흐르고 점수를 얻을 수 있다.</summary>
        RoundPlaying = 2,

        /// <summary>라운드 정산. 라운드 승자 결정, 번호표를 장독대에 투입.</summary>
        RoundSettlement = 3,

        /// <summary>김치전사 추첨. 당첨 번호와 스킬이 확정되어 있다.</summary>
        Lottery = 4,

        /// <summary>라운드 결과 창.</summary>
        RoundResult = 5,

        /// <summary>최종 승자 피날레. 방장이 대기실 복귀를 선택할 때까지 유지.</summary>
        Finale = 6,
    }

    /// <summary>매치가 대기실로 돌아간 이유.</summary>
    public enum MatchEndReason : byte
    {
        None = 0,

        /// <summary>정상 종료 후 방장이 대기실 복귀를 선택.</summary>
        HostReturnedToLobby = 1,

        /// <summary>진행 중 인원이 최소 인원 미만으로 줄어 중단됨.</summary>
        NotEnoughPlayers = 2,

        /// <summary>방장이 진행 중인 매치를 중단함.</summary>
        HostAborted = 3,
    }
}
