#!/usr/bin/env python3
"""
Update all Datadog monitors that contain '@your-team' placeholder
notifications to use the real Slack channel.

Usage:
    export DD_API_KEY=...
    export DD_APP_KEY=...
    python update-monitor-notifications.py
"""

import os
import re
import json
import urllib.request
import urllib.error

DD_API_KEY = os.environ["DD_API_KEY"]
DD_APP_KEY = os.environ["DD_APP_KEY"]
DD_SITE = os.environ.get("DD_SITE", "us5.datadoghq.com")
SLACK_TARGET = "@slack-npc-platform"

BASE = f"https://api.{DD_SITE}/api/v1"


def api(method, path, body=None):
    url = f"{BASE}{path}"
    headers = {
        "DD-API-KEY": DD_API_KEY,
        "DD-APPLICATION-KEY": DD_APP_KEY,
        "Content-Type": "application/json",
    }
    data = json.dumps(body).encode() if body else None
    req = urllib.request.Request(url, data=data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(req) as resp:
            return json.loads(resp.read())
    except urllib.error.HTTPError as e:
        print(f"  HTTP {e.code}: {e.read().decode()[:200]}")
        return None


def main():
    # 1. Fetch all monitors
    print("Fetching all monitors...")
    monitors = api("GET", "/monitor")
    if not monitors:
        print("No monitors found or API error.")
        return

    # 2. Find monitors with placeholder notifications
    placeholder_pattern = re.compile(r"@your-team\b", re.IGNORECASE)
    to_update = []
    for m in monitors:
        msg = m.get("message", "")
        if placeholder_pattern.search(msg):
            to_update.append(m)
            print(f"  [{m['id']}] {m['name']}")

    print(f"\nFound {len(to_update)} monitors with '@your-team' placeholders.")

    # 3. Update each monitor
    for m in to_update:
        mid = m["id"]
        old_msg = m["message"]
        new_msg = placeholder_pattern.sub(SLACK_TARGET, old_msg)

        if old_msg == new_msg:
            print(f"  [{mid}] No change needed (skipping)")
            continue

        print(f"  [{mid}] Updating notification...")
        result = api("PUT", f"/monitor/{mid}", {
            "message": new_msg,
            "options": m.get("options", {}),
        })
        if result:
            print(f"    ✅ Updated")
        else:
            print(f"    ❌ Failed")

    print("\nDone.")


if __name__ == "__main__":
    main()
