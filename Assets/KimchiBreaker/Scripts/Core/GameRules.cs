namespace KimchiBreaker.Core
{
    /// <summary>
    /// 게임 전체에서 공유하는 고정 규칙 값.
    /// 네트워크로 받은 값은 반드시 이 범위로 검증/보정한 뒤 사용한다.
    /// </summary>
    public static class GameRules
    {
        /// <summary>호스트와 클라이언트의 통신 규약 버전. 서로 다르면 접속을 거절한다.</summary>
        public const int ProtocolVersion = 1;

        public const int MaxPlayers = 4;

        /// <summary>정식 매치를 시작하는 데 필요한 최소 인원.</summary>
        public const int MinPlayersToStart = 2;

        public const int MinTargetWins = 1;
        public const int MaxTargetWins = 20;
        public const int DefaultTargetWins = 5;

        public const int MinRoundSeconds = 30;
        public const int MaxRoundSeconds = 180;
        public const int DefaultRoundSeconds = 90;

        /// <summary>닉네임 최대 글자 수(화면에 보이는 글자 기준).</summary>
        public const int MaxNicknameLength = 12;

        /// <summary>닉네임 최대 UTF-8 바이트 수. 네트워크 고정 길이 문자열에 들어가야 한다.</summary>
        public const int MaxNicknameUtf8Bytes = 48;

        /// <summary>재료 1개 완성으로 얻을 수 있는 최대 김치 수(황금 배추 = 2~3개).</summary>
        public const int MaxKimchiPerAward = 3;

        /// <summary>한 라운드에 한 명이 모을 수 있는 김치 상한(오버플로 방지용 안전장치).</summary>
        public const int MaxKimchiPerRound = 9999;

        /// <summary>'김치전사의 가호' 피버 타임 길이(라운드 종료 전 N초).</summary>
        public const int FeverSeconds = 15;

        /// <summary>플레이어가 없음을 뜻하는 ClientId 값.</summary>
        public const ulong NoPlayer = ulong.MaxValue;

        /// <summary>좌석이 없음을 뜻하는 값.</summary>
        public const int NoSeat = -1;
    }
}
