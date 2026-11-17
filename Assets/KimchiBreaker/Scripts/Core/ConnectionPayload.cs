using System;
using System.Text;

namespace KimchiBreaker.Core
{
    /// <summary>
    /// 접속 요청 때 클라이언트가 보내는 데이터(통신 규약 버전 + 닉네임)의 인코딩/디코딩.
    /// 서버는 신뢰할 수 없는 바이트를 받으므로, 디코딩은 어떤 입력에도 예외 없이 true/false 만 돌려준다.
    /// 형식: [매직 4바이트][규약 버전 int32 LE][닉네임 길이 1바이트][닉네임 UTF-8]
    /// </summary>
    public static class ConnectionPayload
    {
        /// <summary>이 크기를 넘는 접속 데이터는 읽지 않고 거절한다.</summary>
        public const int MaxBytes = 4 + 4 + 1 + GameRules.MaxNicknameUtf8Bytes;

        const uint k_Magic = 0x314B424B; // "KBK1"

        static readonly UTF8Encoding s_StrictUtf8 = new UTF8Encoding(false, true);

        public static byte[] Encode(string nickname)
        {
            string clean = TextSanitizer.SanitizeNickname(nickname, string.Empty);
            byte[] name = s_StrictUtf8.GetBytes(clean);
            if (name.Length > GameRules.MaxNicknameUtf8Bytes)
            {
                // SanitizeNickname 이 바이트 수를 보장하므로 도달하지 않지만, 방어적으로 처리한다.
                name = Array.Empty<byte>();
            }

            var payload = new byte[4 + 4 + 1 + name.Length];
            WriteUInt32(payload, 0, k_Magic);
            WriteUInt32(payload, 4, (uint)GameRules.ProtocolVersion);
            payload[8] = (byte)name.Length;
            Buffer.BlockCopy(name, 0, payload, 9, name.Length);
            return payload;
        }

        public static bool TryDecode(byte[] payload, out int protocolVersion, out string nickname)
        {
            protocolVersion = 0;
            nickname = string.Empty;
            if (payload == null || payload.Length < 9 || payload.Length > MaxBytes)
            {
                return false;
            }

            if (ReadUInt32(payload, 0) != k_Magic)
            {
                return false;
            }

            protocolVersion = unchecked((int)ReadUInt32(payload, 4));
            int nameLength = payload[8];
            if (nameLength > GameRules.MaxNicknameUtf8Bytes || 9 + nameLength != payload.Length)
            {
                return false;
            }

            try
            {
                nickname = s_StrictUtf8.GetString(payload, 9, nameLength);
            }
            catch (ArgumentException)
            {
                nickname = string.Empty;
                return false; // 잘못된 UTF-8
            }

            return true;
        }

        static void WriteUInt32(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }

        static uint ReadUInt32(byte[] buffer, int offset)
        {
            return buffer[offset] | ((uint)buffer[offset + 1] << 8) | ((uint)buffer[offset + 2] << 16) | ((uint)buffer[offset + 3] << 24);
        }
    }
}
