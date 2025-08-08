# Raspberry Pi Home Automation Kubernetes 

## Step 1: Ubuntu Server Setup

### Hardware

- **Model:** Raspberry Pi 4B (8GB RAM)
- **Storage:** 64GB SD Card (system), External SSD (to be mounted later)

### OS Install

1. Downloaded Ubuntu Server 25.04 64-bit for Raspberry Pi from [ubuntu.com](https://ubuntu.com/download/raspberry-pi).
2. Used Raspberry Pi Imager to flash SD card:
   - Hostname: `<SET-HOSTNAME>`
   - Username: `<SET-USERNAME>`
   - Password: `<SET-PASSWORD>`
   - SSH: `enabled`

### First Boot

1. Inserted SD card, powered on Pi.
2. Found IP via router/admin page.
3. Connected with SSH:

    ```bash
    ssh <username>>@<pi-ip-address>
    ```

4. Updated packages:

    ```bash
    sudo apt update && sudo apt upgrade -y
    sudo reboot`
    ```

## Step 2: Install MicroK8s on Raspberry Pi

### Prerequisites

- Completed Ubuntu Server installation and SSH access as described in Step 1.
- Hostname: `<SET-HOSTNAME>`
- Username: `<SET-USERNAME>`

### Raspberry Pi Specific Requirement: Enable Memory Cgroup

MicroK8s requires Linux kernel memory cgroup support to run on Raspberry Pi devices.  
**Without this, MicroK8s will not start and will report errors related to memory cgroups or dqlite.**

### How to Enable Memory Cgroup
---

1. **Edit the boot command line:**

    ```bash
   sudo nano /boot/firmware/cmdline.txt
   ```

2. **Add the following at the end of the (single) line:**

    ```ini
    cgroup_enable=memory cgroup_memory=1
    ```

     *(Ensure there are no line breaks—all parameters must be on one line.)*

3. **Save and reboot the Raspberry Pi:**

    ```bash
    sudo reboot
    ```

### Installation Steps

1. **Install MicroK8s:**

    ```bash
    sudo snap install microk8s --classic
    ```

2. **Add user to microk8s group:**

    ```bash
    sudo usermod -aG microk8s <SET-USERNAME>
    sudo chown -f -R <SET-USERNAME> ~/.kube
    ```

    *Log out and log back in for group changes to apply.*

3. **Check MicroK8s status:**

    ```bash
    microk8s status --wait-ready
    ```

    *Output should indicate that MicroK8s is running.*

4. **(Optional) Alias kubectl for convenience:**

    ```bash

    sudo snap alias microk8s.kubectl kubectl

    ```

    *Now you can use kubectl instead of microk8s kubectl.*

---

## Step 3: Prepare SSD Disk for Kubernetes Persistent Volumes (Optional)

### Goal

Mount an external SSD on the Raspberry Pi and use it as persistent storage for Kubernetes workloads (such as Postgres, InfluxDB, etc).

### Instructions
---

1. **Identify the SSD device**

    List attached storage devices:

    ```bash
    lsblk
    ```

    Find your SSD (e.g., /dev/sda).

    **Double-check you have the correct device before formatting.**

2. **(Optional) Partition and Format the SSD**

    If the disk is new or you want to wipe it:

    ```bash
    sudo parted /dev/sda -- mklabel gpt
    sudo parted -a opt /dev/sda -- mkpart primary ext4 0% 100%
    sudo mkfs.ext4 /dev/sda1

    ```

    *Replace /dev/sda with your actual device if different.*

3. **Mount the SSD**

    Create a mount point and mount the disk:

    ```bash
    sudo mkdir -p /mnt/ssd-data
    sudo mount /dev/sda1 /mnt/ssd-data
    ```

4. **(Optional) Make Mount Persistent**

    Get the disk's UUID:

    ```bash
    blkid /dev/sda1
    ```

    Edit /etc/fstab to add:

    ```ini
    UUID=<YOUR-SSD-UUID> /mnt/ssd-data ext4 defaults,noatime 0 2
    ```

5. **Set Permissions for Kubernetes**
    Change ownership so MicroK8s pods can use the volume:

    ```bash
    sudo chown -R <SET-USERNAME>:microk8s /mnt/ssd-data
    ```

---

That’s a great idea—**storage class creation and all storage management can be versioned and controlled via your GitOps repo** with Flux, instead of manually.
This ensures everything is reproducible and visible in code!

---

## Step 5: Bootstrap Flux and Prepare for GitOps

### Goal

Set up Flux CD (GitOps operator) to manage Kubernetes manifests and Helm releases from a version-controlled repository.

### Instructions

1. **Install the Flux CLI (on your Raspberry Pi)**

    ```bash
    curl -s https://fluxcd.io/install.sh | sudo bash
    flux --version
    ```

2. **Generate a Personal Access Token (PAT) on GitHub**

    - Go to GitHub → Settings → Developer Settings → Personal access tokens (classic)
    - Generate a classic token with `repo` and `workflow` scopes.
    - **Copy the token value.**

3. **Export MicroK8s Kubeconfig**

    ```bash
    export KUBECONFIG=/var/snap/microk8s/current/credentials/client.config
    ```

    ```bash
    flux get kustomizations
    ```

    Or, if you’re running `flux bootstrap`:

    ```bash
    KUBECONFIG=/var/snap/microk8s/current/credentials/client.config flux bootstrap github \
    --owner=$GITHUB_USER \
    --repository=$GITHUB_REPO \
    --branch=$GITHUB_BRANCH \
    --path=clusters/microk8s \
    --personal
    ```

4. **(Optional) Make kubectl Use MicroK8s by Default**

    If you want `kubectl` and `flux` to always work, you can copy the kubeconfig:

    ```bash
    mkdir -p ~/.kube
    cp /var/snap/microk8s/current/credentials/client.config ~/.kube/config
    ```

    *(This will overwrite any existing \~/.kube/config!)*

5. **Bootstrap Flux with Your Repository**

    Set these environment variables, replacing the placeholders:

    ```bash
    export GITHUB_TOKEN=<YOUR-GITHUB-PERSONAL-ACCESS-TOKEN>
    export GITHUB_USER=<YOUR-GITHUB-USERNAME>
    export GITHUB_REPO=<YOUR-GITOPS-REPO-NAME>
    export GITHUB_BRANCH=main
    ```

    Run the bootstrap command (replace `microk8s` if you use a different cluster path):

    ```bash
    flux bootstrap github \
    --owner=$GITHUB_USER \
    --repository=$GITHUB_REPO \
    --branch=$GITHUB_BRANCH \
    --path=clusters/microk8s \
    --personal
    ```

    This will:
    - Create a `flux-system` namespace on your cluster.
    - Push Flux configuration to your repository at `clusters/microk8s/`.
    - Set up GitOps synchronization.

6. **Confirm Flux is Running**

    ```bash
    kubectl get pods -n flux-system
    flux get kustomizations
    ```

    All Flux pods should be running and ready.

## Home Assistant with Matter Support

This repository includes a Home Assistant deployment with full Matter support for smart home automation.

### Features

- **Home Assistant 2025.8.0b2** with built-in Matter integration
- **Chart version 0.4.1** with native Matter support (no separate containers)
- **Host networking** enabled for proper Matter multicast communication
- **UDP ports 5540 and 5580** exposed for Matter Thread and commissioning
- **Privileged security context** for network interface access
- **PostgreSQL backend** for reliable data storage
- **Ingress with TLS** at `https://ha.gcelik.dev`

### Matter Device Support

- Smart lights (Philips Hue, IKEA, etc.)
- Smart switches and outlets
- Door/window sensors
- Motion sensors
- Thermostats
- Smart locks
- And more Matter-certified devices

### Documentation

For detailed information about the Matter setup, see: [`docs/MATTER_SETUP.md`](docs/MATTER_SETUP.md)

### Quick Start

1. Ensure your Flux setup is running (see above)
2. Access Home Assistant at `https://ha.gcelik.dev`
3. Go to Settings → Devices & Services → Integrations
4. Add Matter integration and commission your devices
