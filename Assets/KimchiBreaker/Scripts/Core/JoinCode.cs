using System.Text;

namespace KimchiBreaker.Core
{
    /// <summary>방 코드 입력값 정리/검증.</summary>
    public static class JoinCode
    {
        public const int MinLength = 4;
        public const int MaxLength = 12;

        /// <summary>처리 전에 허용하는 입력 최대 길이. 이보다 길면 바로 거절한다.</summary>
        const int k_MaxRawLength = 64;

        /// <summary>
        /// 사용자가 입력한 방 코드를 표준 형태(대문자 영숫자)로 바꾼다.
        /// 공백과 하이픈은 무시한다. 형식이 맞지 않으면 false.
        /// </summary>
        public static bool TryNormalize(string raw, out string code)
        {
            code = string.Empty;
            if (string.IsNullOrEmpty(raw) || raw.Length > k_MaxRawLength)
            {
                return false;
            }

            var builder = new StringBuilder(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                if (c == ' ' || c == '-' || c == '\t' || c == '\r' || c == '\n')
                {
                    continue;
                }

                if (c >= 'a' && c <= 'z')
                {
                    c = (char)(c - 'a' + 'A');
                }

                bool valid = (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
                if (!valid)
                {
                    return false;
                }

                builder.Append(c);
            }

            if (builder.Length < MinLength || builder.Length > MaxLength)
            {
                return false;
            }

            code = builder.ToString();
            return true;
        }
    }
}
