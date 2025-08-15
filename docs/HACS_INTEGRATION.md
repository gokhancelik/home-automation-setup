# HACS Integration Guide for Home Assistant

This guide explains how to integrate HACS (Home Assistant Community Store) into your Kubernetes-based Home Assistant deployment.

## What is HACS?

HACS is a custom component that gives you a powerful UI to handle downloads of all your custom needs for Home Assistant. It provides access to:

- Custom integrations
- Custom frontend elements (cards, themes, etc.)
- AppDaemon apps
- NetDaemon apps

## Prerequisites

1. A GitHub account (for authentication during setup)
2. Home Assistant running in Kubernetes

**Note**: HACS no longer requires a Personal Access Token! It now uses secure device OAuth flow for authentication.

## Setup Instructions

### Step 1: Create a GitHub Personal Access Token

1. Go to GitHub Settings → Developer settings → Personal access tokens → Tokens (classic)
2. Click "Generate new token (classic)"
3. Give it a descriptive name like "HACS Home Assistant"
4. Select the required scopes:
   - ✅ `public_repo`
   - ✅ `repo` (optional, for private repos)
5. Click "Generate token"
6. **Important**: Copy the token immediately as you won't be able to see it again

### Step 2: Create the GitHub Token Secret

Create the Kubernetes secret with your GitHub token:

```bash
# Replace YOUR_GITHUB_TOKEN with your actual token
kubectl create secret generic github-token-secret --from-literal=token=YOUR_GITHUB_TOKEN
```

Or if using a different namespace:
```bash
kubectl create secret generic github-token-secret --from-literal=token=YOUR_GITHUB_TOKEN -n your-namespace
```

### Step 3: Deploy the Updated Home Assistant

The chart has been updated with:
- An init container that automatically installs HACS using the official installer (`wget -O - https://get.hacs.xyz | bash -`)
- HACS configuration in Home Assistant
- GitHub token environment variable

Deploy the updated chart:

```bash
helm upgrade home-assistant ./charts/home-assistant
```

### Step 2: Initial HACS Setup

1. After deployment, restart Home Assistant (or wait for the pod to restart)
2. Go to Home Assistant web interface
3. Navigate to **Settings** → **Devices & services**
4. **Clear your browser cache** (important!)
5. Click **+ Add integration**
6. Search for "HACS"
7. Follow the setup wizard:
   - Acknowledge the statements and click Submit
   - You'll see a device code - copy it
   - Click the link to https://github.com/login/device
   - Sign in to GitHub if needed
   - Enter the device code and authorize HACS
   - Return to Home Assistant and complete the setup

### Step 5: Configure HACS (Optional)

You can customize HACS behavior by updating the configuration in `values.yaml`:

```yaml
homeAssistant:
  configuration: |
    hacs:
      token: !env_var GITHUB_TOKEN
      appdaemon: true          # Enable AppDaemon apps
      netdaemon: true          # Enable NetDaemon apps
      sidepanel_title: HACS    # Custom sidebar title
      sidepanel_icon: mdi:alpha-c-box  # Custom sidebar icon
      experimental: true       # Enable experimental features
```

## Using HACS

Once installed, HACS will appear in your Home Assistant sidebar. You can:

1. **Browse integrations**: Find custom integrations for various devices and services
2. **Install themes**: Customize the look of your Home Assistant interface
3. **Add custom cards**: Enhance your Lovelace dashboard with community cards
4. **Manage updates**: Keep your custom components updated

## Popular HACS Integrations

Some popular integrations you might want to install:

- **Browser Mod**: Control your browser tabs from Home Assistant
- **Auto Entities**: Automatically populate cards with entities
- **Card Mod**: Modify the appearance of any card
- **Mini Graph Card**: Beautiful and customizable graph cards
- **Mushroom Cards**: Modern and clean card designs

## Troubleshooting

### HACS not appearing in integrations

- Check that the init container ran successfully: `kubectl logs <pod-name> -c install-hacs`
- Clear your browser cache or perform a hard refresh
- Restart Home Assistant

### Authentication issues

- Ensure you have a valid GitHub account
- Try the device OAuth flow again
- Check Home Assistant logs for authentication errors

### Custom components not loading

- Check Home Assistant logs for errors
- Ensure the custom_components directory is properly mounted
- Verify file permissions

## Security Considerations

1. **Device OAuth**: HACS uses secure device OAuth flow for GitHub authentication
2. **Repository Trust**: Only install integrations from trusted sources
3. **Updates**: Regularly update HACS and installed integrations
4. **Backup**: Backup your configuration before installing new components

## Maintenance

- HACS will auto-update when Home Assistant restarts (due to the init container)
- Regularly check for updates to installed integrations
- Review HACS logs for any issues or warnings

## Alternative Installation Methods

If the init container approach doesn't suit your needs, you have these options:

### 1. Custom Docker Image (Recommended for Production)

Build a custom image with HACS pre-installed:

```bash
# Build the custom image
docker build -t my-home-assistant-hacs:latest -f docker/Dockerfile.hacs .

# Push to your registry (optional)
docker push my-registry/my-home-assistant-hacs:latest
```

Then update `values.yaml`:
```yaml
image:
  repository: my-home-assistant-hacs  # or my-registry/my-home-assistant-hacs
  tag: latest
```

And remove the `install-hacs` init container from `deployment.yaml`.

**Use this when**:
- You want faster pod startup times
- You're in an offline/restricted environment
- You prefer fixed HACS versions for stability
- You have a CI/CD pipeline for image builds

### 2. Manual Installation

You can also install HACS manually in the persistent volume:

```bash
# Access the Home Assistant pod
kubectl exec -it <pod-name> -- /bin/bash

# Install HACS
cd /config
wget -O - https://get.hacs.xyz | bash -
```

### 3. Volume Pre-population

Pre-install HACS in the persistent volume before first deployment.

## Recommendation

- **Development/Home Lab**: Use the **init container** approach (default in this chart)
- **Production**: Consider the **custom Docker image** approach for faster startups and version control
