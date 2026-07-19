// <copyright file="DatadogConsent.cs" company="NPC System">
// Copyright (c) NPC System. All rights reserved.
// </copyright>

namespace NPCSystem.Monitoring
{
    /// <summary>
    /// Manages Datadog Browser RUM tracking consent for the WebGL client.
    ///
    /// Compliance requirement: tracking consent starts as "pending" (no data collected)
    /// and is only set to "granted" once the user explicitly accepts the privacy dialog.
    ///
    /// Usage:
    /// <code>
    /// // After user accepts privacy/consent dialog:
    /// DatadogConsent.Grant();
    ///
    /// // If user withdraws consent in settings:
    /// DatadogConsent.Revoke();
    ///
    /// // Check current state:
    /// var state = DatadogConsent.CurrentState;
    /// </code>
    ///
    /// On non-WebGL platforms, these calls are no-ops (RUM is browser-only).
    /// </summary>
    public static class DatadogConsent
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void DDGrantTrackingConsent();

        [DllImport("__Internal")]
        private static extern void DDRevokeTrackingConsent();

        [DllImport("__Internal")]
        private static extern int DDGetTrackingConsent();
#else
        // No-ops on non-WebGL platforms (server, editor, standalone)
        private static void DDGrantTrackingConsent() { }
        private static void DDRevokeTrackingConsent() { }
        private static int DDGetTrackingConsent() => 0;
#endif

        /// <summary>
        /// Tracking consent state values matching the Datadog RUM SDK.
        /// </summary>
        public enum ConsentState
        {
            /// <summary>No data collected. Default state until user grants consent.</summary>
            Pending = 0,
            /// <summary>User has granted consent. RUM data is collected and sent.</summary>
            Granted = 1,
            /// <summary>User has revoked consent. No data collected.</summary>
            NotGranted = 2,
        }

        /// <summary>
        /// Gets the current tracking consent state.
        /// </summary>
        public static ConsentState CurrentState => (ConsentState)DDGetTrackingConsent();

        /// <summary>
        /// Grants tracking consent. Call this after the user accepts the privacy dialog.
        /// RUM will begin collecting sessions, views, errors, and user interactions.
        /// </summary>
        public static void Grant()
        {
            DDGrantTrackingConsent();
            NPCFlowLogger.FindOrCreate().Log(
                NPCFlowStage.ConfigurationValidation,
                NPCFlowStatus.Success,
                NPCFlowLogLevel.Info,
                "Datadog RUM tracking consent granted by user.",
                source: nameof(DatadogConsent)
            );
        }

        /// <summary>
        /// Revokes tracking consent. Call this if the user withdraws consent in settings.
        /// RUM will stop collecting data for the current session.
        /// </summary>
        public static void Revoke()
        {
            DDRevokeTrackingConsent();
            NPCFlowLogger.FindOrCreate().Log(
                NPCFlowStage.ConfigurationValidation,
                NPCFlowStatus.Success,
                NPCFlowLogLevel.Info,
                "Datadog RUM tracking consent revoked by user.",
                source: nameof(DatadogConsent)
            );
        }
    }
}
