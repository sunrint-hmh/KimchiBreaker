namespace KimchiBreaker.Core
{
    /// <summary>
    /// 서버가 접속을 거절하거나 끊을 때 보내는 사유 코드와 화면 표시용 문구.
    /// 코드는 짧은 영문 문자열로 주고받고, 문구는 받는 쪽에서 변환한다(상대가 보낸 임의 문자열을 그대로 표시하지 않기 위해).
    /// </summary>
    public static class DisconnectReasons
    {
        public const string RoomFull = "kb:room_full";
        public const string MatchInProgress = "kb:in_progress";
        public const string VersionMismatch = "kb:version";
        public const string BadRequest = "kb:bad_request";
        public const string Kicked = "kb:kicked";
        public const string HostLeft = "kb:host_left";
        public const string ServerError = "kb:server_error";

        /// <summary>사유 코드를 한국어 안내 문구로 바꾼다. 알 수 없는 값은 일반 문구로 처리한다.</summary>
        public static string ToMessage(string code)
        {
            switch (code)
            {
                case RoomFull: return "방이 가득 찼습니다.";
                case MatchInProgress: return "이미 게임이 진행 중인 방입니다.";
                case VersionMismatch: return "게임 버전이 달라 접속할 수 없습니다. 같은 버전으로 업데이트하세요.";
                case BadRequest: return "잘못된 접속 요청입니다.";
                case Kicked: return "비정상적인 요청이 반복되어 방에서 내보내졌습니다.";
                case HostLeft: return "방장이 방을 닫았습니다.";
                case ServerError: return "방에 문제가 생겨 연결이 끊어졌습니다.";
                default: return "연결이 끊어졌습니다.";
            }
        }
    }
}
