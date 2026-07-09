using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

namespace NPCSystem.Tests
{
    /// <summary>
    /// Tests for PlayerAuthService static utilities — email conversion, URL resolution,
    /// and session data building — that don't require a live Supabase connection.
    /// These pure functions represent the auth flow's data-transformation layer.
    /// </summary>
    public class PlayerAuthServiceStaticTests
    {
        static PlayerAuthServiceStaticTests()
        {
            _emailFromUsername = typeof(PlayerAuthService).GetMethod(
                "EmailFromUsername",
                BindingFlags.Static | BindingFlags.NonPublic
            );
            _usernameFromEmail = typeof(PlayerAuthService).GetMethod(
                "UsernameFromEmail",
                BindingFlags.Static | BindingFlags.NonPublic
            );
            _replaceHost = typeof(PlayerAuthService).GetMethod(
                "ReplaceHost",
                BindingFlags.Static | BindingFlags.NonPublic
            );
            _buildSessionData = typeof(PlayerAuthService).GetMethod(
                "BuildSessionData",
                BindingFlags.Static | BindingFlags.NonPublic
            );
        }

        static readonly MethodInfo _emailFromUsername;
        static readonly MethodInfo _usernameFromEmail;
        static readonly MethodInfo _replaceHost;
        static readonly MethodInfo _buildSessionData;

        static T Invoke<T>(MethodInfo method, params object[] args)
        {
            return (T)method.Invoke(null, args);
        }

        // ── EmailFromUsername ──────────────────────────────────────

        [Test]
        public void EmailFromUsername_AppendsGameDomain()
        {
            string result = Invoke<string>(_emailFromUsername, "Alice");
            Assert.That(result, Is.EqualTo("Alice@npc-game.local"));
        }

        [Test]
        public void EmailFromUsername_TrimsWhitespace()
        {
            string result = Invoke<string>(_emailFromUsername, "  Bob  ");
            Assert.That(result, Is.EqualTo("Bob@npc-game.local"));
        }

        [Test]
        public void EmailFromUsername_EmptyReturnsDomainOnly()
        {
            string result = Invoke<string>(_emailFromUsername, "");
            Assert.That(result, Is.EqualTo("@npc-game.local"));
        }

        [Test]
        public void EmailFromUsername_NullReturnsDomainOnly()
        {
            string result = Invoke<string>(_emailFromUsername, null);
            Assert.That(result, Is.EqualTo("@npc-game.local"));
        }

        // ── UsernameFromEmail ──────────────────────────────────────

        [Test]
        public void UsernameFromEmail_ExtractsLocalPart()
        {
            string result = Invoke<string>(_usernameFromEmail, "alice@npc-game.local");
            Assert.That(result, Is.EqualTo("alice"));
        }

        [Test]
        public void UsernameFromEmail_EmptyReturnsUnknown()
        {
            string result = Invoke<string>(_usernameFromEmail, "");
            Assert.That(result, Is.EqualTo("unknown"));
        }

        [Test]
        public void UsernameFromEmail_NullReturnsUnknown()
        {
            string result = Invoke<string>(_usernameFromEmail, null);
            Assert.That(result, Is.EqualTo("unknown"));
        }

        [Test]
        public void UsernameFromEmail_NoAtSymbol_ReturnsWholeString()
        {
            string result = Invoke<string>(_usernameFromEmail, "notanemail");
            Assert.That(result, Is.EqualTo("notanemail"));
        }

        // ── ReplaceHost ────────────────────────────────────────────

        [Test]
        public void ReplaceHost_ReplacesHostname()
        {
            string result = Invoke<string>(_replaceHost, "http://localhost:8090/api", "myserver.local");
            Assert.That(result, Is.EqualTo("http://myserver.local:8090/api"));
        }

        [Test]
        public void ReplaceHost_PreservesPortAndPath()
        {
            string result = Invoke<string>(_replaceHost, "https://192.168.1.1:8080/v1/models", "gateway");
            Assert.That(result, Is.EqualTo("https://gateway:8080/v1/models"));
        }

        [Test]
        public void ReplaceHost_InvalidUrl_ReturnsOriginal()
        {
            string result = Invoke<string>(_replaceHost, "not-a-url", "newhost");
            Assert.That(result, Is.EqualTo("not-a-url"));
        }

        // ── BuildSessionData ───────────────────────────────────────

        [Test]
        public void BuildSessionData_IncludesPlayerIdAndUsername()
        {
            var session = new PlayerAuthSessionResponse
            {
                playerId = "p123",
                username = "Alice",
                sessionToken = "tok_abc",
                refreshToken = "ref_xyz",
                expiresAtUtc = "2026-07-10T00:00:00Z",
            };

            var data = Invoke<Dictionary<string, object>>(_buildSessionData, session);

            Assert.That(data["playerId"], Is.EqualTo("p123"));
            Assert.That(data["username"], Is.EqualTo("Alice"));
            Assert.That(data["hasRefreshToken"], Is.EqualTo("yes"));
            Assert.That(data["expiresAtUtc"], Is.EqualTo("2026-07-10T00:00:00Z"));
        }

        [Test]
        public void BuildSessionData_NullSession_ReturnsEmptyStrings()
        {
            var data = Invoke<Dictionary<string, object>>(_buildSessionData, null);

            Assert.That(data["playerId"], Is.EqualTo(""));
            Assert.That(data["username"], Is.EqualTo(""));
            Assert.That(data["hasRefreshToken"], Is.EqualTo("no"));
        }

        [Test]
        public void BuildSessionData_DetectsMissingRefreshToken()
        {
            var session = new PlayerAuthSessionResponse
            {
                playerId = "p456",
                username = "Bob",
                sessionToken = "tok_def",
                refreshToken = null,
                expiresAtUtc = null,
            };

            var data = Invoke<Dictionary<string, object>>(_buildSessionData, session);

            Assert.That(data["hasRefreshToken"], Is.EqualTo("no"));
            Assert.That(data["expiresAtUtc"], Is.EqualTo(""));
        }
    }
}
