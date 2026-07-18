mergeInto(LibraryManager.library, {

  /**
   * Grant Datadog RUM tracking consent.
   * Call this from C# after the user accepts the privacy/consent dialog:
   *   DatadogConsent.Grant();
   * Before this is called, no RUM data is collected (tracking consent is 'pending').
   */
  DDGrantTrackingConsent: function () {
    if (typeof window !== 'undefined' && typeof window.ddGrantTrackingConsent === 'function') {
      window.ddGrantTrackingConsent();
    } else {
      console.warn('[DD-RUM] ddGrantTrackingConsent not available (RUM not loaded).');
    }
  },

  /**
   * Revoke Datadog RUM tracking consent.
   * Call this from C# if the user withdraws consent in settings:
   *   DatadogConsent.Revoke();
   */
  DDRevokeTrackingConsent: function () {
    if (typeof window !== 'undefined' && typeof window.ddRevokeTrackingConsent === 'function') {
      window.ddRevokeTrackingConsent();
    } else {
      console.warn('[DD-RUM] ddRevokeTrackingConsent not available (RUM not loaded).');
    }
  },

  /**
   * Check current tracking consent state.
   * Returns 0 = pending, 1 = granted, 2 = not-granted (revoked).
   */
  DDGetTrackingConsent: function () {
    if (typeof window !== 'undefined' && window.DD_RUM && typeof window.DD_RUM.getTrackingConsent === 'function') {
      var consent = window.DD_RUM.getTrackingConsent();
      if (consent === 'granted') return 1;
      if (consent === 'not-granted') return 2;
    }
    return 0; // pending
  }

});
