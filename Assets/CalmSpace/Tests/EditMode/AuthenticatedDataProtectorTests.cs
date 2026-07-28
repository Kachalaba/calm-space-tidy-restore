using System.Text;
using CalmSpace.Monetization;
using NUnit.Framework;

namespace CalmSpace.Tests.EditMode
{
    public sealed class AuthenticatedDataProtectorTests
    {
        private static readonly byte[] Secret =
            Encoding.UTF8.GetBytes(
                "calm-space-test-secret-with-at-least-thirty-two-bytes");

        [Test]
        public void ProtectedPayloadRoundTrips()
        {
            var protector = new ManagedAuthenticatedDataProtector(Secret);
            var plaintext = Encoding.UTF8.GetBytes("lifetime-no-ads=true");
            var associatedData = Encoding.UTF8.GetBytes(
                "com.calmspace.tidyrestore|monetization|v1");

            var protectedData = protector.Protect(plaintext, associatedData);
            var status = protector.TryUnprotect(
                protectedData,
                associatedData,
                out var restored);

            Assert.That(status, Is.EqualTo(DataUnprotectStatus.Success));
            Assert.That(restored, Is.EqualTo(plaintext));
        }

        [Test]
        public void OneByteTamperAndWrongAssociatedDataFailAuthentication()
        {
            var protector = new ManagedAuthenticatedDataProtector(Secret);
            var plaintext = Encoding.UTF8.GetBytes("owned");
            var associatedData = Encoding.UTF8.GetBytes("correct-aad");
            var protectedData = protector.Protect(plaintext, associatedData);
            protectedData[protectedData.Length - 1] ^= 0x01;

            Assert.That(
                protector.TryUnprotect(
                    protectedData,
                    associatedData,
                    out var tamperedPlaintext),
                Is.EqualTo(DataUnprotectStatus.AuthenticationFailed));
            Assert.That(tamperedPlaintext, Is.Null);

            protectedData = protector.Protect(plaintext, associatedData);

            Assert.That(
                protector.TryUnprotect(
                    protectedData,
                    Encoding.UTF8.GetBytes("wrong-aad"),
                    out var wrongAadPlaintext),
                Is.EqualTo(DataUnprotectStatus.AuthenticationFailed));
            Assert.That(wrongAadPlaintext, Is.Null);
        }

        [Test]
        public void TruncatedAndUnknownVersionPayloadsFailClosed()
        {
            var protector = new ManagedAuthenticatedDataProtector(Secret);

            Assert.That(
                protector.TryUnprotect(
                    new byte[] { 1, 2, 3 },
                    new byte[0],
                    out _),
                Is.EqualTo(DataUnprotectStatus.InvalidPayload));

            var protectedData = protector.Protect(
                Encoding.UTF8.GetBytes("owned"),
                new byte[0]);
            protectedData[0] = 99;

            Assert.That(
                protector.TryUnprotect(
                    protectedData,
                    new byte[0],
                    out _),
                Is.EqualTo(DataUnprotectStatus.UnsupportedVersion));
        }
    }
}
