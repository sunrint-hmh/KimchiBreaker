using System.Globalization;
using System.Text;

namespace KimchiBreaker.Core
{
    /// <summary>
    /// 다른 플레이어가 보낸 문자열(닉네임)을 화면에 표시해도 안전하게 만든다.
    /// 서버는 받은 닉네임을 반드시 이 함수로 거른 뒤 저장/전파한다.
    /// </summary>
    public static class TextSanitizer
    {
        /// <summary>처리 전에 잘라내는 입력 최대 길이(UTF-16 단위). 과도하게 긴 입력으로 인한 부하 방지.</summary>
        const int k_MaxRawLength = 64;

        /// <summary>
        /// 닉네임을 정리한다.
        /// 제어 문자, 보이지 않는 서식 문자(방향 전환 등), 결합 문자, 리치 텍스트 태그 기호를 없애고
        /// 공백을 정리한 뒤 길이를 제한한다. 결과가 비면 <paramref name="fallback"/> 을 돌려준다.
        /// </summary>
        public static string SanitizeNickname(string raw, string fallback)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return fallback;
            }

            if (raw.Length > k_MaxRawLength)
            {
                raw = raw.Substring(0, k_MaxRawLength);
            }

            var builder = new StringBuilder(raw.Length);
            int elementCount = 0;
            int byteCount = 0;
            bool pendingSpace = false;

            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                int codePoint;
                int charLength;

                if (char.IsHighSurrogate(c))
                {
                    if (i + 1 >= raw.Length || !char.IsLowSurrogate(raw[i + 1]))
                    {
                        continue; // 짝이 없는 서러게이트는 버린다
                    }

                    codePoint = char.ConvertToUtf32(c, raw[i + 1]);
                    charLength = 2;
                }
                else if (char.IsLowSurrogate(c))
                {
                    continue;
                }
                else
                {
                    codePoint = c;
                    charLength = 1;
                }

                UnicodeCategory category = charLength == 1
                    ? CharUnicodeInfo.GetUnicodeCategory(c)
                    : CharUnicodeInfo.GetUnicodeCategory(raw, i);

                if (charLength == 2)
                {
                    i++; // 서러게이트 쌍의 두 번째 문자는 건너뛴다
                }

                if (IsWhitespaceCategory(category) || c == '\t' || c == '\n' || c == '\r')
                {
                    pendingSpace = builder.Length > 0;
                    continue;
                }

                if (!IsAllowedCategory(category) || codePoint == '<' || codePoint == '>')
                {
                    continue;
                }

                int addedBytes = Utf8Length(codePoint) + (pendingSpace ? 1 : 0);
                int addedElements = 1 + (pendingSpace ? 1 : 0);
                if (elementCount + addedElements > GameRules.MaxNicknameLength ||
                    byteCount + addedBytes > GameRules.MaxNicknameUtf8Bytes)
                {
                    break;
                }

                if (pendingSpace)
                {
                    builder.Append(' ');
                    pendingSpace = false;
                }

                if (charLength == 2)
                {
                    builder.Append(char.ConvertFromUtf32(codePoint));
                }
                else
                {
                    builder.Append(c);
                }

                elementCount += addedElements;
                byteCount += addedBytes;
            }

            return builder.Length == 0 ? fallback : builder.ToString();
        }

        static bool IsWhitespaceCategory(UnicodeCategory category)
        {
            return category == UnicodeCategory.SpaceSeparator ||
                   category == UnicodeCategory.LineSeparator ||
                   category == UnicodeCategory.ParagraphSeparator;
        }

        static bool IsAllowedCategory(UnicodeCategory category)
        {
            switch (category)
            {
                case UnicodeCategory.UppercaseLetter:
                case UnicodeCategory.LowercaseLetter:
                case UnicodeCategory.TitlecaseLetter:
                case UnicodeCategory.OtherLetter:
                case UnicodeCategory.DecimalDigitNumber:
                case UnicodeCategory.LetterNumber:
                case UnicodeCategory.OtherNumber:
                case UnicodeCategory.ConnectorPunctuation:
                case UnicodeCategory.DashPunctuation:
                case UnicodeCategory.OpenPunctuation:
                case UnicodeCategory.ClosePunctuation:
                case UnicodeCategory.InitialQuotePunctuation:
                case UnicodeCategory.FinalQuotePunctuation:
                case UnicodeCategory.OtherPunctuation:
                case UnicodeCategory.MathSymbol:
                case UnicodeCategory.CurrencySymbol:
                case UnicodeCategory.OtherSymbol:
                    return true;
                default:
                    // 제어/서식/결합/수정자/비공개/미할당 문자는 모두 제외
                    return false;
            }
        }

        static int Utf8Length(int codePoint)
        {
            if (codePoint < 0x80)
            {
                return 1;
            }

            if (codePoint < 0x800)
            {
                return 2;
            }

            return codePoint < 0x10000 ? 3 : 4;
        }
    }
}
