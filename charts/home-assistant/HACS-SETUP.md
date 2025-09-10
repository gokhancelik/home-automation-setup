# HACS (Home Assistant Community Store) Setup

HACS is automatically installed by the Helm chart through an init container. After Home Assistant starts, you need to complete the HACS setup through the UI.

## Setup Steps

1. **Access Home Assistant**: Go to https://ha.gcelik.dev

2. **Add HACS Integration**:
   - Go to **Settings** → **Devices & Services** → **Integrations**
   - Click **+ Add Integration**
   - Search for "HACS" and select it

3. **GitHub Authentication**:
   - You'll need a GitHub Personal Access Token
   - Go to [GitHub Settings → Developer settings → Personal access tokens](https://github.com/settings/tokens)
   - Create a new token with no special scopes (just basic read access)
   - Copy the token and paste it in HACS setup

4. **Complete Setup**:
   - Follow the HACS setup wizard
   - Accept the terms and conditions
   - HACS will appear in your sidebar once configured

## Using HACS

- **Frontend**: Browse and install custom Lovelace cards and themes
- **Integrations**: Install custom Home Assistant integrations
- **Automations**: Find custom automation blueprints

## Automatic Installation

The Helm chart includes:
- **Init Container**: Downloads HACS from GitHub if not present
- **Directory Structure**: Creates required directories (`themes`, `www`, `custom_components`)
- **Frontend Config**: Enables theme loading for HACS themes

HACS files are installed to `/config/custom_components/hacs` and persist across pod restarts.
