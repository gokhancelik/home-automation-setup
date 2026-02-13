# Tailscale VPN Setup

## Purpose
Provides secure remote access to the home network from anywhere.

## Setup Instructions

### 1. Sign up for Tailscale
Visit: https://login.tailscale.com/start (FREE for personal use)

### 2. Generate Auth Key
1. Go to Tailscale Admin Console → Settings → Keys
2. Click "Generate auth key"
3. Enable options:
   - ✅ Reusable
   - ✅ Ephemeral
4. Copy the auth key (starts with tskey-auth-...)

### 3. Create Sealed Secret
`ash
kubectl create secret generic tailscale-auth \
  --from-literal=TS_AUTHKEY='YOUR_AUTH_KEY_HERE' \
  --namespace=tailscale \
  --dry-run=client -o yaml | \
  kubeseal --format=yaml > clusters/microk8s/infrastructure/tailscale/tailscale-sealed-secret.yaml
`

### 4. Enable in kustomization.yaml
Uncomment: - tailscale-sealed-secret.yaml

### 5. Deploy
`ash
git add clusters/microk8s/infrastructure/tailscale/
git commit -m "feat: add Tailscale VPN"
git push
flux reconcile kustomization flux-system --with-source
`

### 6. Approve Subnet Router in Tailscale Admin
- Find device "home-k8s-gateway"
- Edit route settings → Approve 192.168.2.0/24

## Access Your Home Network
- Home Assistant: http://192.168.2.59:8123
- Grafana: http://192.168.2.43:3000
- NAS: \\192.168.2.24
- SSH: ssh gokhan@192.168.2.43
