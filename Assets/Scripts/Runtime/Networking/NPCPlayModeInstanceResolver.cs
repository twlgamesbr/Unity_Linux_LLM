using System;

namespace NPCSystem
{
    public static class NPCPlayModeInstanceResolver
    {
        const string NameArg = "-name";
        const string ClientBindPortArg = "-clientBindPort";
        const string PlayerPrefix = "Player";

        public static bool TryGetPlayerName(out string playerName)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int index = 0; index < args.Length - 1; index++)
            {
                if (!string.Equals(args[index], NameArg, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string candidate = args[index + 1]?.Trim();
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    playerName = candidate;
                    return true;
                }
            }

            playerName = string.Empty;
            return false;
        }

        public static bool TryGetPlayerIndex(out int playerIndex)
        {
            if (TryGetPlayerName(out string playerName))
            {
                return TryParsePlayerIndex(playerName, out playerIndex);
            }

            playerIndex = 0;
            return false;
        }

        public static bool TryParsePlayerIndex(string playerName, out int playerIndex)
        {
            playerIndex = 0;
            if (
                string.IsNullOrWhiteSpace(playerName)
                || !playerName.StartsWith(PlayerPrefix, StringComparison.OrdinalIgnoreCase)
            )
            {
                return false;
            }

            string suffix = playerName.Substring(PlayerPrefix.Length).Trim();
            return int.TryParse(suffix, out playerIndex) && playerIndex > 0;
        }

        public static bool TryGetCommandLineClientBindPort(out ushort clientBindPort)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int index = 0; index < args.Length - 1; index++)
            {
                if (
                    !string.Equals(
                        args[index],
                        ClientBindPortArg,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    continue;
                }

                if (ushort.TryParse(args[index + 1], out clientBindPort))
                {
                    return true;
                }
            }

            clientBindPort = 0;
            return false;
        }

        public static ushort ResolveClientBindPortForPlayerIndex(
            int playerIndex,
            ushort serverPort,
            ushort explicitOverride = 0
        )
        {
            if (explicitOverride != 0)
            {
                return explicitOverride;
            }

            if (playerIndex <= 1)
            {
                return 0;
            }

            int resolved = serverPort + playerIndex - 1;
            if (resolved > ushort.MaxValue)
            {
                resolved = ushort.MaxValue;
            }

            return (ushort)resolved;
        }
    }
}
