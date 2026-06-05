# Portainer Setup for Docker Monitoring

## Deployment Status
✅ Portainer deployed to Kubernetes
✅ Accessible at: https://portainer.gcelik.dev
✅ TLS certificate issued

## Initial Setup Steps

### 1. Access Portainer
Open https://portainer.gcelik.dev in your browser

### 2. Create Admin User
- On first access, you'll be prompted to create an admin user
- Set a strong password (minimum 12 characters)

### 3. Add Docker Environment
After creating the admin user:

1. Click "Get Started" or go to "Environments" → "Add environment"
2. Select "Docker Standalone"
3. Choose "API" as the connection method
4. Configure:
   - **Name**: `pi-lab-docker`
   - **Docker API URL**: `192.168.2.59:2375`
   - **Public IP**: `192.168.2.59`
5. Click "Add environment"

### 4. Verify Connection
- Navigate to the `pi-lab-docker` environment
- You should see:
  - 4 running containers (homeassistant, zigbee2mqtt, mosquitto, matter-server)
  - Container statistics
  - Volume information
  - Network details

## Docker API Configuration
The Docker API on pi-lab (192.168.2.59) is exposed on port 2375:
- **URL**: `http://192.168.2.59:2375`
- **Protocol**: HTTP (insecure - internal network only)
- **Access**: No TLS, no authentication (internal use only)

## Security Notes
⚠️ **Important**: The Docker API is exposed without authentication on port 2375. This is acceptable for internal networks but should NEVER be exposed to the internet.

- Docker API is only accessible from the internal network (192.168.2.x)
- Portainer access is protected by:
  - User authentication
  - TLS encryption (Let's Encrypt certificate)
  - Kubernetes ingress controller

## Kubernetes Resources
- **Namespace**: `portainer`
- **Deployment**: `portainer-d5889f68-x8g9p`
- **Service**: `portainer` (ClusterIP)
- **Ingress**: `portainer.gcelik.dev`
- **PVC**: `portainer-data` (1Gi, microk8s-hostpath)

## Useful Commands

### Check Portainer status
```bash
microk8s kubectl get pods -n portainer
microk8s kubectl logs -n portainer -l app=portainer
```

### Check ingress
```bash
microk8s kubectl get ingress -n portainer
microk8s kubectl describe ingress portainer -n portainer
```

### Check certificate
```bash
microk8s kubectl get certificate -n portainer
```

### Test Docker API
```bash
curl http://192.168.2.59:2375/version
```

## Monitoring Capabilities
Once connected, you can monitor:
- Container status and logs
- Resource usage (CPU, memory, network)
- Volume management
- Image management
- Docker Compose stacks
- Container console access

## GitOps
All Portainer manifests are stored in:
```
clusters/microk8s/apps/portainer/
├── namespace.yaml
├── pvc.yaml
├── deployment.yaml
├── service.yaml
├── ingress.yaml
└── kustomization.yaml
```

Changes are tracked in Git and can be applied with:
```bash
kubectl apply -k clusters/microk8s/apps/portainer/
```
