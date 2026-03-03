# Network Recovery Guide - Lost Connection After Reboot

## 🚨 You'll Need Physical Access to ubuntu-desktop

Since the connection is lost, you need to access ubuntu-desktop directly (monitor + keyboard).

---

## 🔧 Quick Recovery Steps

### Step 1: Check Network Interface Status

```bash
# Check if interface is up
ip link show enp2s0

# Check if you have an IP address
ip addr show enp2s0

# Check network connectivity
ping -c 5 192.168.2.254  # Router
ping -c 5 8.8.8.8        # Internet
```

**If interface is DOWN:**
```bash
sudo ip link set enp2s0 up
```

---

### Step 2: Check Systemd Service Status

```bash
sudo systemctl status network-tuning.service
```

**If service failed**, check the error:
```bash
journalctl -u network-tuning.service -n 50
```

**Disable the service temporarily:**
```bash
sudo systemctl stop network-tuning.service
sudo systemctl disable network-tuning.service
```

---

### Step 3: Reset Network Settings to Default

```bash
# Reset all ethtool settings to defaults
sudo ethtool -K enp2s0 sg off
sudo ethtool -K enp2s0 tso off
sudo ethtool -K enp2s0 gso off
sudo ethtool -C enp2s0 rx-usecs 0 rx-frames 1
sudo ethtool -C enp2s0 tx-usecs 0 tx-frames 1

# Restart networking
sudo systemctl restart NetworkManager

# OR if using systemd-networkd
sudo systemctl restart systemd-networkd
```

**Test connection:**
```bash
ping -c 5 192.168.2.254
```

---

### Step 4: Restart Network Interface

```bash
# Bring interface down and up
sudo ip link set enp2s0 down
sudo ip link set enp2s0 up

# Request new DHCP lease
sudo dhclient -r enp2s0
sudo dhclient enp2s0

# Check if you got IP
ip addr show enp2s0
```

---

### Step 5: Full Network Restart

```bash
# Restart all network services
sudo systemctl restart NetworkManager
sudo systemctl restart systemd-networkd
sudo systemctl restart systemd-resolved

# Check status
systemctl status NetworkManager
```

---

### Step 6: Nuclear Option - Reboot

```bash
sudo reboot
```

After reboot, the network should come back with default settings (before our changes).

---

## 🔍 Diagnostic Commands

Once network is back, check what went wrong:

```bash
# Check system logs for network errors
dmesg | grep -i "enp2s0\|r8169\|network\|eth"

# Check NetworkManager logs
journalctl -u NetworkManager -n 100

# Check if ethtool settings are applied
ethtool -k enp2s0 | grep -E "(scatter-gather|segmentation-offload)"
ethtool -c enp2s0 | grep -E "(rx-usecs|rx-frames)"

# Check for kernel errors
dmesg | tail -50
```

---

## 🎯 What Likely Happened

### Scenario 1: Systemd Service Timing Issue
- Service ran too early before network interface was ready
- **Fix:** Adjust service timing (see below)

### Scenario 2: Realtek Driver Doesn't Support Settings
- r8169 driver rejected the ethtool commands on this kernel version
- **Fix:** Use safe mode with only supported features

### Scenario 3: NetworkManager Reset Settings
- NetworkManager overwrote our ethtool changes
- **Fix:** Configure NetworkManager to preserve settings

---

## 🛠️ Proper Fix After Recovery

Once network is back, try this safer approach:

### Option A: Safe Mode (Conservative)

```bash
# Create script with ONLY offloading (skip coalescing for now)
sudo tee /usr/local/bin/tune-network-safe.sh > /dev/null << 'EOF'
#!/bin/bash
# Safe network tuning - only hardware offloading

# Enable offloading (very safe, widely supported)
ethtool -K enp2s0 sg on 2>/dev/null || true
ethtool -K enp2s0 tso on 2>/dev/null || true
ethtool -K enp2s0 gso on 2>/dev/null || true

# Log success
logger "Network tuning applied successfully"
exit 0
EOF

sudo chmod +x /usr/local/bin/tune-network-safe.sh

# Test it manually first
sudo /usr/local/bin/tune-network-safe.sh

# Verify network still works
ping -c 5 192.168.2.254
```

**If that works**, then enable the service:
```bash
sudo tee /etc/systemd/system/network-tuning.service > /dev/null << 'EOF'
[Unit]
Description=Network Performance Tuning (Safe Mode)
After=network-online.target NetworkManager.service systemd-networkd.service
Wants=network-online.target

[Service]
Type=oneshot
ExecStart=/usr/local/bin/tune-network-safe.sh
RemainAfterExit=yes

[Install]
WantedBy=multi-user.target
EOF

sudo systemctl daemon-reload
sudo systemctl enable network-tuning.service
```

---

### Option B: NetworkManager Dispatcher (Recommended)

This runs AFTER NetworkManager brings up the interface:

```bash
# Create NetworkManager dispatcher script
sudo tee /etc/NetworkManager/dispatcher.d/99-network-tuning > /dev/null << 'EOF'
#!/bin/bash
# Network tuning via NetworkManager dispatcher

INTERFACE="$1"
ACTION="$2"

if [ "$INTERFACE" = "enp2s0" ] && [ "$ACTION" = "up" ]; then
    # Apply tuning when interface comes up
    /usr/bin/ethtool -K enp2s0 sg on 2>/dev/null || true
    /usr/bin/ethtool -K enp2s0 tso on 2>/dev/null || true
    /usr/bin/ethtool -K enp2s0 gso on 2>/dev/null || true
    
    # Optional: Add coalescing if it worked before
    # /usr/bin/ethtool -C enp2s0 rx-usecs 50 rx-frames 32 2>/dev/null || true
    
    /usr/bin/logger "Network tuning applied to $INTERFACE"
fi

exit 0
EOF

sudo chmod +x /etc/NetworkManager/dispatcher.d/99-network-tuning

# Test by restarting interface
sudo nmcli connection down "$(nmcli -g NAME connection show --active | grep -v lo | head -1)"
sudo nmcli connection up "$(nmcli -g NAME connection show | grep -v lo | head -1)"

# Check if settings applied
ethtool -k enp2s0 | grep "scatter-gather:"
```

---

## 📞 Share This Info

Once you get network back, share:
1. Output of `dmesg | tail -50`
2. Output of `journalctl -u network-tuning.service` (if service was created)
3. Output of `systemctl status NetworkManager`

This will help me figure out exactly what went wrong! 🔍

---

## 🎯 TL;DR - Quick Recovery

```bash
# 1. Disable the service
sudo systemctl stop network-tuning.service
sudo systemctl disable network-tuning.service

# 2. Reset network settings
sudo ethtool -K enp2s0 sg off
sudo ethtool -K enp2s0 tso off
sudo ethtool -K enp2s0 gso off

# 3. Restart networking
sudo systemctl restart NetworkManager

# 4. Reboot if needed
sudo reboot
```

After this, your network should be back to the original (slow but working) state.
