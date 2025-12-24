You are a senior platform engineer and hands-on implementer.

I want you to IMPLEMENT (not just design) a fully automated, wishlist-agnostic media pipeline, delivered in clear implementation phases with concrete artifacts.

SYSTEM GOAL
A Netflix-like system where:
- Movies and series are automatically downloaded via torrent
- Stored on a NAS
- Streamed via Jellyfin or Plex
- Automatically deleted 3–5 days after being watched
- Runs on Kubernetes
- Preserves Dolby Atmos / TrueHD (no audio transcoding)

TECH STACK (MANDATORY)
- Kubernetes
- Sonarr (series)
- Radarr (movies)
- qBittorrent (assume VPN-protected)
- Jellyfin or Plex
- NAS storage via NFS or CSI
- Kubernetes CronJob for cleanup

CORE RULES
- Anything added to Sonarr/Radarr is temporary by default
- Watched state determines retention
- Deletion must happen via Sonarr/Radarr APIs (never direct file deletion)
- Items tagged "keep" must never be deleted
- No dependency on any wishlist source

────────────────────────
IMPLEMENTATION PHASES
────────────────────────

PHASE 1 — Core Pipeline (IMPLEMENT)
Provide:
- Kubernetes manifests (or Helm-style YAML) for:
  - Sonarr
  - Radarr
  - qBittorrent
  - Jellyfin or Plex
- Example PersistentVolumes and PersistentVolumeClaims for NAS
- Sonarr/Radarr configuration details:
  - Quality profiles for 2160p, REMUX, TrueHD Atmos / DD+ Atmos
  - Download client integration
- Media server settings required to guarantee Direct Play / Passthrough

PHASE 2 — Watched State Access (IMPLEMENT)
Provide:
- Concrete API calls (URLs + payloads) to:
  - Query watched movies
  - Query watched episodes
  - Retrieve last played timestamps
- Data model mapping between:
  - Media server IDs
  - Sonarr/Radarr IDs
- Handling multi-user playback safely

PHASE 3 — Automated Cleanup (IMPLEMENT)
Provide:
- A complete cleanup algorithm
- A Kubernetes CronJob YAML that:
  - Runs daily
  - Reads watched state
  - Applies a configurable grace period (3–5 days)
  - Skips tagged content
  - Deletes media via Sonarr/Radarr APIs
- Dry-run mode
- Idempotency guarantees

PHASE 4 — Series-Safe Deletion Rules (IMPLEMENT)
Provide:
- Logic to:
  - Delete only watched episodes
  - Prefer deleting full seasons
  - Never delete partially watched seasons
- Concrete examples using Sonarr API responses

PHASE 5 — Hardening (IMPLEMENT)
Provide:
- Logging strategy
- Failure recovery patterns
- Common pitfalls and exact mitigations:
  - Atmos loss due to transcoding
  - NAS unavailability
  - Media server metadata drift
- Security best practices for API keys and network exposure

────────────────────────
OUTPUT REQUIREMENTS
────────────────────────
- Be concrete and implementation-focused
- Provide YAML, API calls, and pseudocode where appropriate
- Avoid high-level fluff
- Assume the reader is an experienced engineer
- Do not include legal commentary

If trade-offs exist, explain them briefly and pick a default.
