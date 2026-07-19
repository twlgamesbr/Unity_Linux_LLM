using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;


using NPCSystem.Monitoring;
using NPCSystem.Monitoring.Datadog;
using NPCSystem.Dialogue.Core;
using NPCSystem.Network.Core;
using NPCSystem.Character.Player;
using NPCSystem.Auth;
using NPCSystem.Items;
using NPCSystem.LocalAI;
using NPCSystem.Initialization;
using NPCSystem.Character.NPC;
using NPCSystem.Dialogue.Session;
using NPCSystem.Dialogue.UI;
using NPCSystem.Dialogue.RAG;
using NPCSystem.Dialogue.Persistence;
namespace NPCSystem.Network.Core
{
    /// <content>Transport configuration and CLI argument parsing extracted from NPCNetworkBootstrap.</content>
    public partial class NPCNetworkBootstrap
    {
        /// <summary>
        /// Parse command-line args and override transport config / startup mode.
        /// Supports: -npc-server, -npc-host, -npc-client, -port N, -address ADDR
        /// </summary>
        void ApplyCommandLineOverrides()
        {
            if (!Application.isBatchMode && !Application.isEditor)
                return;

            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i].ToLowerInvariant();

                if (arg == "-npc-server")
                {
                    TransportConfig.AutoStartMode = NPCNetworkAutoStartMode.Server;
                    NPCFlowLogger
                        .FindOrCreate()
                        ?.Log(
                            NPCFlowStage.ConfigurationValidation,
                            NPCFlowStatus.Success,
                            NPCFlowLogLevel.Info,
                            "CLI override applied: autoStartMode = Server.",
                            source: nameof(NPCNetworkBootstrap)
                        );
                }
                else if (arg == "-npc-websockets")
                {
                    TransportConfig.UseWebSockets = true;
                    NPCFlowLogger
                        .FindOrCreate()
                        ?.Log(
                            NPCFlowStage.ConfigurationValidation,
                            NPCFlowStatus.Success,
                            NPCFlowLogLevel.Info,
                            "CLI override applied: useWebSockets = true.",
                            source: nameof(NPCNetworkBootstrap)
                        );
                }
                else if (arg == "-npc-host")
                {
                    TransportConfig.AutoStartMode = NPCNetworkAutoStartMode.Host;
                    NPCFlowLogger
                        .FindOrCreate()
                        ?.Log(
                            NPCFlowStage.ConfigurationValidation,
                            NPCFlowStatus.Success,
                            NPCFlowLogLevel.Info,
                            "CLI override applied: autoStartMode = Host.",
                            source: nameof(NPCNetworkBootstrap)
                        );
                }
                else if (arg == "-npc-client")
                {
                    TransportConfig.AutoStartMode = NPCNetworkAutoStartMode.Client;
                    NPCFlowLogger
                        .FindOrCreate()
                        ?.Log(
                            NPCFlowStage.ConfigurationValidation,
                            NPCFlowStatus.Success,
                            NPCFlowLogLevel.Info,
                            "CLI override applied: autoStartMode = Client.",
                            source: nameof(NPCNetworkBootstrap)
                        );
                }
                else if (arg == "-port" && i + 1 < args.Length)
                {
                    if (ushort.TryParse(args[i + 1], out ushort port))
                    {
                        TransportConfig.Port = port;
                        NPCFlowLogger
                            .FindOrCreate()
                            ?.Log(
                                NPCFlowStage.ConfigurationValidation,
                                NPCFlowStatus.Success,
                                NPCFlowLogLevel.Info,
                                $"CLI override applied: port = {port}.",
                                source: nameof(NPCNetworkBootstrap)
                            );
                        i++;
                    }
                }
                else if (arg == "-address" && i + 1 < args.Length)
                {
                    TransportConfig.ConnectAddress = args[i + 1];
                    NPCFlowLogger
                        .FindOrCreate()
                        ?.Log(
                            NPCFlowStage.ConfigurationValidation,
                            NPCFlowStatus.Success,
                            NPCFlowLogLevel.Info,
                            $"CLI override applied: connectAddress = {TransportConfig.ConnectAddress}.",
                            source: nameof(NPCNetworkBootstrap)
                        );
                    i++;
                }
            }
        }

        public void ApplyTransportConfiguration()
        {
            ResolveReferences();

            if (UnityTransport == null)
            {
                NPCFlowLogger
                    .FindOrCreate()
                    ?.Log(
                        NPCFlowStage.ConfigurationValidation,
                        NPCFlowStatus.Error,
                        NPCFlowLogLevel.Error,
                        "Could not find a UnityTransport component.",
                        source: nameof(NPCNetworkBootstrap)
                    );
                return;
            }

            if (NetworkManager != null)
            {
                NetworkManager.NetworkConfig.NetworkTransport = UnityTransport;
                if (PlayerPrefab != null)
                {
                    NetworkManager.NetworkConfig.PlayerPrefab = PlayerPrefab;
                }

                RegisterNetworkPrefabs();
            }

            TransportConfig.NormalizeInPlace();
#if UNITY_WEBGL && !UNITY_EDITOR
            TransportConfig.UseWebSockets = true;
#endif
            if (!TransportConfig.TryValidate(out string errorMessage))
            {
                NPCFlowLogger
                    .FindOrCreate()
                    ?.Log(
                        NPCFlowStage.ConfigurationValidation,
                        NPCFlowStatus.Error,
                        NPCFlowLogLevel.Error,
                        errorMessage,
                        source: nameof(NPCNetworkBootstrap)
                    );
                return;
            }

            UnityTransport.UseWebSockets = TransportConfig.UseWebSockets;

            UnityTransport.ConnectionAddressData connectionData = UnityTransport.ConnectionData;
            connectionData.Address = TransportConfig.ConnectAddress;
            connectionData.Port = TransportConfig.Port;
            connectionData.ServerListenAddress = TransportConfig.ListenAddress;
            connectionData.WebSocketPath = TransportConfig.WebSocketPath;
            connectionData.ClientBindPort = ResolveClientBindPort();
            UnityTransport.ConnectionData = connectionData;

            NPCFlowLogger
                .FindOrCreate()
                ?.Log(
                    NPCFlowStage.ConfigurationValidation,
                    NPCFlowStatus.Success,
                    NPCFlowLogLevel.Info,
                    "Transport configured.",
                    source: nameof(NPCNetworkBootstrap),
                    data: new Dictionary<string, object>
                    {
                        ["connectAddress"] = connectionData.Address,
                        ["port"] = connectionData.Port,
                        ["listenAddress"] = connectionData.ServerListenAddress,
                        ["clientBindPort"] = connectionData.ClientBindPort,
                        ["autoStartMode"] = TransportConfig.AutoStartMode.ToString(),
                        ["player"] = NPCPlayModeInstanceResolver.TryGetPlayerName(
                            out string playerName
                        )
                            ? playerName
                            : "unknown",
                    }
                );
        }

        ushort ResolveClientBindPort()
        {
            if (
                NPCPlayModeInstanceResolver.TryGetCommandLineClientBindPort(
                    out ushort commandLineBindPort
                )
            )
            {
                return commandLineBindPort;
            }

            if (!AutoAssignClientBindPort)
            {
                return ClientBindPortOverride;
            }

            if (!NPCPlayModeInstanceResolver.TryGetPlayerIndex(out int playerIndex))
            {
                return ClientBindPortOverride;
            }

            return NPCPlayModeInstanceResolver.ResolveClientBindPortForPlayerIndex(
                playerIndex,
                TransportConfig.Port,
                ClientBindPortOverride
            );
        }
    }
}
