/**
 * Datadog Browser RUM (Real User Monitoring) initialization for Unity WebGL client.
 *
 * This script is injected into index.html via nginx sub_filter and initializes
 * the Datadog RUM SDK to track page loads, errors, and user interactions in
 * the WebGL client. Logs and traces are forwarded through the ddproxy sidecar
 * (port 9090) to avoid CORS issues with the Datadog intake.
 *
 * Required environment:
 *   - ddproxy sidecar running on port 9090 (see docker-compose.yml)
 *   - Datadog client token and application ID (configured below)
 *
 * To obtain credentials:
 *   1. Go to https://us5.datadoghq.com/rum/application/new
 *   2. Create a RUM application for "webgl-client"
 *   3. Copy the Client Token and Application ID
 *   4. Update the values below
 */
(function () {
  'use strict';

  // ── Configuration ─────────────────────────────────────────────────
  // These values are specific to your Datadog RUM application.
  // Replace with your actual credentials from the Datadog RUM setup page.
  var DD_CLIENT_TOKEN = 'pubd0c26b48193b2c8d1ab9ca5f163ebadd';
  var DD_APPLICATION_ID = '09a6d234-82f6-4084-984e-40ac779b642a';
  var DD_SITE = 'us5.datadoghq.com';
  var DD_SERVICE = 'webgl-client';
  var DD_ENV = 'production';
  var DD_VERSION = '1.0.0'; // Update with build version if available

  // ── Guard: skip if already initialized or credentials not set ─────
  if (window.DD_RUM) {
    console.warn('[DD-RUM] Already initialized, skipping.');
    return;
  }
  if (DD_CLIENT_TOKEN.indexOf('REPLACE_WITH') === 0) {
    console.warn('[DD-RUM] Client token not configured. RUM disabled. See datadog-rum-init.js comments.');
    return;
  }

  // ── Load the Datadog Browser RUM library ──────────────────────────
  var script = document.createElement('script');
  script.async = true;
  script.src = 'https://www.datadoghq-browser-agent.com/' + DD_SITE.split('.')[0] + '/v5/datadog-rum.js';
  script.onload = function () {
    window.DD_RUM.init({
      clientToken: DD_CLIENT_TOKEN,
      applicationId: DD_APPLICATION_ID,
      site: DD_SITE,
      service: DD_SERVICE,
      env: DD_ENV,
      version: DD_VERSION,
      // Compliance: tracking consent is 'pending' by default.
      // No RUM data is collected until the user grants consent.
      // Unity calls DD_RUM.setTrackingConsent('granted') after the user accepts.
      trackingConsent: 'pending',
      // Sample rates: 100% for errors, 20% for sessions, 10% for resources
      sessionSampleRate: 20,
      sessionReplaySampleRate: 0, // Disable session replay for performance
      trackUserInteractions: true,
      trackResources: true,
      trackLongTasks: true,
      // Use the ddproxy sidecar to avoid CORS issues
      proxy: {
        url: '/dd-intake',
        routerPath: '/dd-intake',
      },
      // Allowed Tracing Origins: trace requests to the backend
      allowedTracingOrigins: [
        'http://localhost:8085',
        window.location.origin,
      ],
      // Custom action name mapping for Unity WebGL interactions
      actionNameAttribute: 'data-dd-action-name',
    });

    console.info(
      '[DD-RUM] Initialized (tracking consent: pending) — service=' + DD_SERVICE +
      ' env=' + DD_ENV +
      ' app=' + DD_APPLICATION_ID.substring(0, 8) + '...'
    );

    // Track Unity WebGL lifecycle events
    if (typeof unityInstance !== 'undefined' && unityInstance) {
      window.DD_RUM.setGlobalContextProperty('unity_version', unityInstance.Module ? unityInstance.Module.productVersion : 'unknown');
    }

    // ── Consent management ────────────────────────────────────────
    // Unity calls this after the user accepts the privacy/consent dialog.
    // Once granted, all subsequent RUM data (sessions, views, errors,
    // resources) is collected and sent to Datadog.
    window.ddGrantTrackingConsent = function () {
      if (window.DD_RUM) {
        window.DD_RUM.setTrackingConsent('granted');
        console.info('[DD-RUM] Tracking consent granted by user.');
      }
    };

    // Unity can also revoke consent (e.g. user withdraws in settings).
    window.ddRevokeTrackingConsent = function () {
      if (window.DD_RUM) {
        window.DD_RUM.setTrackingConsent('not-granted');
        console.info('[DD-RUM] Tracking consent revoked by user.');
      }
    };
  };
  script.onerror = function () {
    console.warn('[DD-RUM] Failed to load RUM library from CDN. RUM disabled.');
  };
  document.head.appendChild(script);

  // ── Global error handler: forward uncaught errors to RUM ──────────
  window.addEventListener('error', function (event) {
    if (window.DD_RUM && window.DD_RUMaddAction) {
      window.DD_RUM.addAction('uncaught_error', {
        message: event.message || 'Unknown error',
        filename: event.filename || '',
        lineno: event.lineno || 0,
        colno: event.colno || 0,
      });
    }
  });

  // ── Promise rejection handler ─────────────────────────────────────
  window.addEventListener('unhandledrejection', function (event) {
    if (window.DD_RUM && window.DD_RUM.addAction) {
      window.DD_RUM.addAction('unhandled_rejection', {
        message: event.reason ? event.reason.toString() : 'Unhandled rejection',
      });
    }
  });
})();
