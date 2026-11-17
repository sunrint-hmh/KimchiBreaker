using System;
using System.Text;
using KimchiBreaker.Core;
using NUnit.Framework;

namespace KimchiBreaker.Tests
{
    public class TextSanitizerTests
    {
        const string Fallback = "플레이어1";

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("\t\r\n")]
        [TestCase("<><><>")]
        [TestCase("​‎‮")]
        public void EmptyOrInvisibleInput_UsesFallback(string raw)
        {
            Assert.AreEqual(Fallback, TextSanitizer.SanitizeNickname(raw, Fallback));
        }

        [TestCase("김치왕", "김치왕")]
        [TestCase("Player_1", "Player_1")]
        [TestCase("  앞뒤 공백  ", "앞뒤 공백")]
        [TestCase("여러    칸   공백", "여러 칸 공백")]
        [TestCase("줄\n바꿈", "줄 바꿈")]
        public void OrdinaryNames_ArePreserved(string raw, string expected)
        {
            Assert.AreEqual(expected, TextSanitizer.SanitizeNickname(raw, Fallback));
        }

        [Test]
        public void RichTextTagCharacters_AreRemoved()
        {
            string result = TextSanitizer.SanitizeNickname("<b>굵게</b>", Fallback);
            Assert.IsFalse(result.Contains("<"));
            Assert.IsFalse(result.Contains(">"));
            Assert.AreEqual("b굵게/b", result);
        }

        [Test]
        public void ControlAndFormatCharacters_AreRemoved()
        {
            // 널 문자, 벨, 방향 전환 문자, 폭 없는 공백
            string result = TextSanitizer.SanitizeNickname("가\0나\u0007다‮라​마", Fallback);
            Assert.AreEqual("가나다라마", result);
        }

        [Test]
        public void CombiningMarks_AreRemoved()
        {
            // 결합 문자를 겹겹이 쌓아 화면을 뒤덮는 글자(일명 잘고 텍스트) 방지
            string result = TextSanitizer.SanitizeNickname("á̂̃̄b", Fallback);
            Assert.AreEqual("ab", result);
        }

        [Test]
        public void LongNames_AreTruncatedToTheLimit()
        {
            string result = TextSanitizer.SanitizeNickname("가나다라마바사아자차카타파하", Fallback);
            Assert.AreEqual("가나다라마바사아자차카타", result);
            Assert.AreEqual(GameRules.MaxNicknameLength, result.Length);
        }

        [Test]
        public void HugeInput_IsHandledQuickly()
        {
            string raw = new string('가', 1000000);
            string result = TextSanitizer.SanitizeNickname(raw, Fallback);
            Assert.AreEqual(GameRules.MaxNicknameLength, result.Length);
        }

        [Test]
        public void SurrogatePairs_AreNeverSplit()
        {
            string emoji = char.ConvertFromUtf32(0x1F96C); // 잎채소 이모지
            string raw = string.Concat(emoji, emoji, emoji, emoji, emoji, emoji, emoji, emoji, emoji, emoji, emoji, emoji, emoji, emoji);
            string result = TextSanitizer.SanitizeNickname(raw, Fallback);
            Assert.AreEqual(0, result.Length % 2);
            for (int i = 0; i < result.Length; i += 2)
            {
                Assert.IsTrue(char.IsSurrogatePair(result[i], result[i + 1]));
            }

            Assert.LessOrEqual(Encoding.UTF8.GetByteCount(result), GameRules.MaxNicknameUtf8Bytes);
        }

        [Test]
        public void LoneSurrogates_AreDropped()
        {
            string result = TextSanitizer.SanitizeNickname("가\uD83E나\uDD6C다", Fallback);
            Assert.AreEqual("가나다", result);
        }

        [Test]
        public void Result_AlwaysFitsTheNetworkByteBudget()
        {
            var random = new Random(1234);
            var builder = new StringBuilder();
            for (int round = 0; round < 3000; round++)
            {
                builder.Length = 0;
                int length = random.Next(0, 80);
                for (int i = 0; i < length; i++)
                {
                    builder.Append((char)random.Next(0, 0x10000));
                }

                string result = TextSanitizer.SanitizeNickname(builder.ToString(), Fallback);
                Assert.IsNotNull(result);
                Assert.IsTrue(result.Length > 0);
                Assert.LessOrEqual(Encoding.UTF8.GetByteCount(result), GameRules.MaxNicknameUtf8Bytes);
                Assert.IsFalse(result.Contains("<"));
                Assert.IsFalse(result.Contains(">"));
                foreach (char c in result)
                {
                    Assert.IsFalse(char.IsControl(c));
                }

                Assert.AreEqual(result, result.Trim());
            }
        }
    }

    public class JoinCodeTests
    {
        [TestCase("ABC123", "ABC123")]
        [TestCase("abc123", "ABC123")]
        [TestCase(" ab-c1 23 ", "ABC123")]
        [TestCase("ABCD", "ABCD")]
        [TestCase("ABCDEFGH1234", "ABCDEFGH1234")]
        public void ValidCodes_AreNormalized(string raw, string expected)
        {
            Assert.IsTrue(JoinCode.TryNormalize(raw, out string code));
            Assert.AreEqual(expected, code);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("ABC")]
        [TestCase("ABCDEFGH12345")]
        [TestCase("ABC!23")]
        [TestCase("가나다라마바")]
        [TestCase("ABC123; DROP")]
        [TestCase("../../etc")]
        [TestCase("ＡＢＣ１２３")]
        public void InvalidCodes_AreRejected(string raw)
        {
            Assert.IsFalse(JoinCode.TryNormalize(raw, out string code));
            Assert.AreEqual(string.Empty, code);
        }

        [Test]
        public void OverlongInput_IsRejectedWithoutProcessing()
        {
            Assert.IsFalse(JoinCode.TryNormalize(new string('A', 100000), out _));
        }
    }

    public class TokenBucketTests
    {
        [Test]
        public void AllowsBurstUpToCapacity_ThenBlocks()
        {
            var bucket = new TokenBucket(5, 10);
            for (int i = 0; i < 5; i++)
            {
                Assert.IsTrue(bucket.TryConsume(0.0));
            }

            Assert.IsFalse(bucket.TryConsume(0.0));
        }

        [Test]
        public void RefillsOverTime_ButNeverAboveCapacity()
        {
            var bucket = new TokenBucket(5, 10);
            for (int i = 0; i < 5; i++)
            {
                bucket.TryConsume(0.0);
            }

            Assert.IsFalse(bucket.TryConsume(0.05));
            Assert.IsTrue(bucket.TryConsume(0.1));
            Assert.IsFalse(bucket.TryConsume(0.1));

            int allowed = 0;
            for (int i = 0; i < 100; i++)
            {
                if (bucket.TryConsume(1000.0))
                {
                    allowed++;
                }
            }

            Assert.AreEqual(5, allowed);
        }

        [Test]
        public void SustainedRate_IsLimitedToRefillRate()
        {
            var bucket = new TokenBucket(10, 20);
            int allowed = 0;
            // 10초 동안 초당 1000번 요청
            for (int i = 0; i < 10000; i++)
            {
                if (bucket.TryConsume(i / 1000.0))
                {
                    allowed++;
                }
            }

            Assert.GreaterOrEqual(allowed, 200);
            Assert.LessOrEqual(allowed, 211);
        }

        [Test]
        public void TimeGoingBackwards_DoesNotGrantTokens()
        {
            var bucket = new TokenBucket(2, 100);
            Assert.IsTrue(bucket.TryConsume(10.0));
            Assert.IsTrue(bucket.TryConsume(10.0));
            Assert.IsFalse(bucket.TryConsume(5.0));
            Assert.IsFalse(bucket.TryConsume(-1000.0));
        }

        [Test]
        public void InvalidTime_IsRejected()
        {
            var bucket = new TokenBucket(2, 1);
            Assert.IsFalse(bucket.TryConsume(double.NaN));
            Assert.IsFalse(bucket.TryConsume(double.PositiveInfinity));
            Assert.IsTrue(bucket.TryConsume(0.0));
        }

        [Test]
        public void Reset_RestoresFullCapacity()
        {
            var bucket = new TokenBucket(1, 0);
            Assert.IsTrue(bucket.TryConsume(0.0));
            Assert.IsFalse(bucket.TryConsume(100.0));
            bucket.Reset();
            Assert.IsTrue(bucket.TryConsume(100.0));
        }

        [Test]
        public void InvalidConfiguration_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TokenBucket(0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TokenBucket(double.NaN, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TokenBucket(1, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TokenBucket(1, double.PositiveInfinity));
        }
    }

    public class SecureRandomSourceTests
    {
        [Test]
        public void Values_StayInRange()
        {
            using (var random = new SecureRandomSource())
            {
                foreach (int max in new[] { 1, 2, 3, 7, 9, 10, 1000, int.MaxValue })
                {
                    for (int i = 0; i < 2000; i++)
                    {
                        int value = random.NextInt(max);
                        Assert.GreaterOrEqual(value, 0);
                        Assert.Less(value, max);
                    }
                }
            }
        }

        [Test]
        public void NonPositiveMax_Throws()
        {
            using (var random = new SecureRandomSource())
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt(0));
                Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt(-3));
            }
        }

        [Test]
        public void Distribution_IsRoughlyUniform()
        {
            using (var random = new SecureRandomSource())
            {
                const int buckets = 9;
                const int samples = 90000;
                var counts = new int[buckets];
                for (int i = 0; i < samples; i++)
                {
                    counts[random.NextInt(buckets)]++;
                }

                foreach (int count in counts)
                {
                    // 기대값 10000, 표준편차 약 94. ±6% 는 우연히 벗어날 수 없는 범위.
                    Assert.That(count, Is.InRange(9400, 10600));
                }
            }
        }
    }

    public class SettingsAndCatalogTests
    {
        [Test]
        public void DefaultSettings_AreValid()
        {
            Assert.IsTrue(MatchSettings.Default.IsValid);
            Assert.AreEqual(5, MatchSettings.Default.TargetWins);
        }

        [Test]
        public void Clamped_BringsValuesIntoRange()
        {
            MatchSettings clamped = new MatchSettings(int.MinValue, int.MaxValue).Clamped();
            Assert.AreEqual(GameRules.MinTargetWins, clamped.TargetWins);
            Assert.AreEqual(GameRules.MaxRoundSeconds, clamped.RoundSeconds);
            Assert.IsTrue(clamped.IsValid);
        }

        [Test]
        public void Timings_SanitizeBadValues()
        {
            var timings = new MatchTimings
            {
                RoundIntroSeconds = double.NaN,
                SettlementSeconds = -5,
                LotterySeconds = double.PositiveInfinity,
                ResultSeconds = 1e9,
                FinaleMinSeconds = 2,
            }.Sanitized();
            Assert.AreEqual(4.0, timings.RoundIntroSeconds);
            Assert.AreEqual(4.0, timings.SettlementSeconds);
            Assert.AreEqual(7.0, timings.LotterySeconds);
            Assert.AreEqual(600.0, timings.ResultSeconds);
            Assert.AreEqual(2.0, timings.FinaleMinSeconds);
        }

        [Test]
        public void Catalog_HasFivePassivesAndFourActives()
        {
            int passive = 0;
            int active = 0;
            foreach (SkillId id in SkillCatalog.All)
            {
                Assert.IsTrue(SkillCatalog.IsDefined(id));
                Assert.AreNotEqual(SkillCatalog.IsPassive(id), SkillCatalog.IsActive(id), "패시브/액티브 중 정확히 하나여야 한다");
                Assert.IsNotEmpty(SkillCatalog.DisplayName(id));
                Assert.IsNotEmpty(SkillCatalog.Description(id));
                if (SkillCatalog.IsPassive(id))
                {
                    passive++;
                }
                else
                {
                    active++;
                }
            }

            Assert.AreEqual(5, passive);
            Assert.AreEqual(4, active);
        }

        [Test]
        public void None_AndUnknownValues_AreNotSkills()
        {
            Assert.IsFalse(SkillCatalog.IsDefined(SkillId.None));
            Assert.IsFalse(SkillCatalog.IsActive(SkillId.None));
            Assert.IsFalse(SkillCatalog.IsPassive(SkillId.None));
            Assert.IsFalse(SkillCatalog.IsDefined((SkillId)200));
            Assert.IsFalse(SkillCatalog.IsActive((SkillId)200));
        }
    }
}
