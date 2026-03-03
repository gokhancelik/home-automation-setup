# SSH Recovery - Port 22 Not Responding

## 🔍 Diagnosis

✅ **Network:** Machine is reachable (ping works, latency: 16-394ms)  
❌ **SSH:** Port 22 not responding (connection refused/timeout)

**Likely causes:**
1. SSH service crashed or stopped
2. Firewall blocking SSH after network changes
3. System under heavy load, SSH queue full

---

## 🛠️ Recovery Steps (Physical Access Required)

### Step 1: Check SSH Service Status

```bash
# Check if SSH is running
sudo systemctl status ssh

# Check SSH on non-Ubuntu systems
sudo systemctl status sshd
```

**If SSH is not running:**
```bash
# Start SSH service
sudo systemctl start ssh

# Enable it to start on boot
sudo systemctl enable ssh

# Check status again
sudo systemctl status ssh
```

---

### Step 2: Check SSH Logs

```bash
# View recent SSH logs
sudo journalctl -u ssh -n 50

# Check for errors
sudo journalctl -u ssh | grep -i "error\|fail"

# Check auth log
sudo tail -50 /var/log/auth.log
```

---

### Step 3: Check Firewall

```bash
# Check if ufw is blocking SSH
sudo ufw status

# If active and blocking, allow SSH
sudo ufw allow 22/tcp
sudo ufw reload

# Verify rule is added
sudo ufw status numbered
```

---

### Step 4: Check SSH Configuration

```bash
# Verify SSH is listening
sudo ss -tlnp | grep :22

# Check SSH config for issues
sudo sshd -t

# View SSH config
cat /etc/ssh/sshd_config | grep -v "^#" | grep -v "^$"
```

---

### Step 5: Restart SSH Service

```bash
# Stop SSH
sudo systemctl stop ssh

# Start SSH with verbose output to see errors
sudo /usr/sbin/sshd -d

# If no errors, stop debug mode (Ctrl+C) and start service
sudo systemctl start ssh
sudo systemctl status ssh
```

---

### Step 6: Check Network Interface Issues

The network tuning might have caused issues:

```bash
# Check if network is congested
netstat -s | grep -i "retrans\|drop\|error"

# Check if interface has errors
ip -s link show enp2s0

# Check if system is overloaded
top -n 1 | head -20
```

**If network is severely degraded**, disable the tuning service:
```bash
sudo systemctl stop network-tuning.service 2>/dev/null
sudo systemctl disable network-tuning.service 2>/dev/null
sudo reboot
```

---

### Step 7: Alternative Access Methods

If you have physical access but want remote terminal:

**Option A: VNC/Desktop Sharing**
```bash
# If you have GUI access via monitor
# Open Terminal from GUI and run commands
```

**Option B: Recovery Mode**
```bash
# Reboot and select "Advanced options" in GRUB
# Select "Recovery mode"
# Select "Root shell prompt"
# Mount filesystem read-write:
mount -o remount,rw /

# Then fix SSH
systemctl start ssh
```

**Option C: Tailscale SSH (if Tailscale is running)**
```bash
# Check if Tailscale is up
tailscale status

# Try SSH via Tailscale IP (100.x.x.x)
# From another machine:
ssh user@100.x.x.x
```

---

## 🎯 Quick Fix Commands

Copy/paste this on ubuntu-desktop terminal:

```bash
echo "=== SSH Recovery Script ==="

# 1. Check current status
echo "1. Checking SSH service..."
sudo systemctl status ssh --no-pager

# 2. Restart SSH
echo "2. Restarting SSH service..."
sudo systemctl restart ssh

# 3. Check if it's running
echo "3. Verifying SSH is running..."
sudo systemctl is-active ssh

# 4. Check listening ports
echo "4. Checking SSH listening on port 22..."
sudo ss -tlnp | grep :22

# 5. Check firewall
echo "5. Checking firewall..."
sudo ufw status

# 6. If needed, allow SSH
if sudo ufw status | grep -q "Status: active"; then
    echo "   Firewall is active, ensuring SSH is allowed..."
    sudo ufw allow 22/tcp
    sudo ufw reload
fi

# 7. Test connection locally
echo "7. Testing SSH connection locally..."
ssh -v -o ConnectTimeout=5 localhost

echo ""
echo "=== Recovery Complete ==="
echo "Try SSH from remote machine now!"
```

---

## 🔍 Diagnostic Output to Share

After running commands, share this output:

```bash
# Service status
sudo systemctl status ssh

# Listening ports
sudo ss -tlnp | grep :22

# Recent errors
sudo journalctl -u ssh -n 20

# Network statistics
ip -s link show enp2s0 | head -20

# System load
uptime
```

---

## 💡 Why SSH Might Have Failed

**Theory 1: TCP Connection Queue Full**
- Network tuning changed TCP behavior
- SSH connection queue filled up
- New connections rejected

**Theory 2: SSH Service Crashed**
- Systemd service dependency issue
- Network tuning service affected SSH startup order

**Theory 3: Firewall Auto-Applied**
- Some security tools auto-enable firewall on network config changes
- Port 22 blocked by default

**Theory 4: Network Still Unstable**
- Ping works (ICMP) but TCP connections fail
- Packet loss affecting TCP handshake (you see 16-394ms variable latency)
- TCP connections timing out before completing

---

## ⚡ Emergency Access

If SSH won't start and you need remote access:

**Install and use Tailscale SSH:**
```bash
# On ubuntu-desktop (via physical terminal)
sudo tailscale up --ssh

# From remote machine (your laptop)
ssh ubuntu-desktop  # Uses Tailscale Magic DNS
```

This bypasses the problematic local network interface!

---

## 🔄 Last Resort: Full Reset

```bash
# 1. Disable network tuning
sudo systemctl stop network-tuning.service 2>/dev/null
sudo systemctl disable network-tuning.service 2>/dev/null
sudo rm /etc/systemd/system/network-tuning.service 2>/dev/null
sudo rm /usr/local/bin/tune-network*.sh 2>/dev/null

# 2. Reset network to defaults
sudo ethtool -K enp2s0 sg off
sudo ethtool -K enp2s0 tso off  
sudo ethtool -K enp2s0 gso off
sudo ethtool -C enp2s0 rx-usecs 0 rx-frames 1 2>/dev/null

# 3. Reset SSH
sudo systemctl restart ssh

# 4. Reboot
sudo reboot
```

After reboot, everything should be back to original (slow but working) state, including SSH.
