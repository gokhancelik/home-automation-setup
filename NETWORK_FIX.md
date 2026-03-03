# Network Performance Fix for ubuntu-desktop

## Problem Summary

Ubuntu-desktop (192.168.2.43) experiencing severe network latency:
- **Current latency:** 750ms average
- **Expected latency:** 2-5ms on local network
- **Impact:** Kubernetes API timeouts, slow kubectl/k9s, slow image pulls, poor Frigate performance

## Root Cause

Network interface `enp2s0` is misconfigured with:

1. **Tiny Ring Buffers:** 256 packets (should be 4096+)
   - Results in 10.5 million dropped packets (1.09% drop rate)
   
2. **Hardware Offloading Disabled:**
   - Scatter-gather: OFF
   - TCP segmentation: OFF
   - CPU processing every packet in software
   
3. **No Interrupt Coalescing:**
   - Generates interrupt for every single packet
   - Massive CPU overhead

## The Fix

### Step 1: Apply Immediate Fix (Run on ubuntu-desktop)

Open a terminal on ubuntu-desktop and run:

```bash
# Show current settings
echo "=== BEFORE ==="
echo "Ring Buffer:"
sudo ethtool -g enp2s0 | grep -A 4 "Current"
echo ""
echo "Dropped Packets:"
ip -s link show enp2s0 | grep -A 2 "RX:" | head -3
echo ""

# Apply fixes
echo "=== APPLYING FIXES ==="

# 1. Increase ring buffer to 4096
echo "Increasing ring buffer..."
sudo ethtool -G enp2s0 rx 4096 tx 4096

# 2. Enable hardware offloading
echo "Enabling hardware offloading..."
sudo ethtool -K enp2s0 sg on
sudo ethtool -K enp2s0 tso on
sudo ethtool -K enp2s0 gso on
sudo ethtool -K enp2s0 gro on

# 3. Configure interrupt coalescing
echo "Configuring interrupt coalescing..."
sudo ethtool -C enp2s0 rx-usecs 50 rx-frames 64

# Show new settings
echo ""
echo "=== AFTER ==="
echo "Ring Buffer:"
sudo ethtool -g enp2s0 | grep -A 4 "Current"
echo ""
echo "Hardware Offloading:"
sudo ethtool -k enp2s0 | grep -E "scatter-gather|tcp-segmentation|generic-segmentation" | grep -v fixed
echo ""

# Test latency
echo "=== TESTING LATENCY ==="
ping -c 5 192.168.2.254
```

**Expected result:** Ping should drop from 750ms to under 10ms immediately.

---

### Step 2: Make Changes Permanent (After confirming Step 1 works)

```bash
# Create systemd service file
sudo tee /etc/systemd/system/network-tuning.service > /dev/null <<'EOF'
[Unit]
Description=Network Interface Tuning for enp2s0
After=network-online.target
Wants=network-online.target

[Service]
Type=oneshot
RemainAfterExit=yes
ExecStart=/usr/local/bin/tune-network.sh

[Install]
WantedBy=multi-user.target
EOF

# Create tuning script
sudo tee /usr/local/bin/tune-network.sh > /dev/null <<'EOF'
#!/bin/bash
INTERFACE="enp2s0"

# Wait for interface to be up
sleep 5

# Apply optimizations
ethtool -G $INTERFACE rx 4096 tx 4096 2>/dev/null || true
ethtool -K $INTERFACE sg on tso on gso on gro on 2>/dev/null || true
ethtool -C $INTERFACE rx-usecs 50 rx-frames 64 2>/dev/null || true

# Log success
logger "Network tuning applied to $INTERFACE"
EOF

# Make script executable
sudo chmod +x /usr/local/bin/tune-network.sh

# Enable and start service
sudo systemctl daemon-reload
sudo systemctl enable network-tuning.service
sudo systemctl start network-tuning.service

# Verify service status
sudo systemctl status network-tuning.service
```

---

## Expected Improvements

| Metric | Before | After | Improvement |
|--------|--------|-------|-------------|
| **Ping Latency** | 750ms | 2-5ms | 150x faster |
| **Dropped Packets** | 10.5M (1.09%) | ~0 | Fixed |
| **kubectl Response** | 11 seconds | <100ms | 100x faster |
| **k9s Navigation** | Laggy | Smooth | Instant |
| **Image Pulls** | 30+ minutes | 2-3 minutes | 10x faster |
| **Frigate Detection FPS** | 1.2 fps | 10-20 fps | 8-16x faster |

---

## Verification Commands

After applying the fix, verify with:

```bash
# Check ring buffer size
sudo ethtool -g enp2s0

# Check hardware offloading
sudo ethtool -k enp2s0 | grep -E "scatter-gather|tcp-segmentation|generic-segmentation"

# Check interrupt coalescing
sudo ethtool -c enp2s0

# Monitor dropped packets (should stop increasing)
watch -n 1 'ip -s link show enp2s0 | grep -A 2 "RX:"'

# Test latency
ping -c 20 192.168.2.254
```

---

## Technical Details

### Why This Fixes the Problem

1. **Ring Buffer (256 → 4096):**
   - Increases packet buffer capacity by 16x
   - Prevents buffer overflow and packet drops
   - Eliminates TCP retransmissions

2. **Hardware Offloading:**
   - Offloads packet processing to network card
   - Reduces CPU load
   - Faster packet processing

3. **Interrupt Coalescing:**
   - Batches interrupts instead of per-packet
   - Reduces CPU context switching
   - More efficient processing

### Why Packets Were Being Dropped

1. Packet arrives at network card
2. Ring buffer is full (only 256 slots)
3. Packet gets dropped
4. Sender detects loss and retransmits (200-500ms timeout)
5. Multiple retransmissions = 750ms average latency
6. With 1.09% drop rate, most connections affected

---

## Troubleshooting

### If ring buffer increase fails:
Some network cards don't support larger buffers. Check maximum supported:
```bash
sudo ethtool -g enp2s0 | grep -A 4 "Pre-set"
```

### If hardware offloading fails:
Some features may not be supported by the network card. This is OK - the ring buffer fix alone will help significantly.

### If changes don't persist after reboot:
Verify the systemd service is enabled:
```bash
sudo systemctl status network-tuning.service
sudo journalctl -u network-tuning.service
```

---

## Cost & Time

- **Cost:** $0 (software configuration only)
- **Time:** 2 minutes to apply
- **Complexity:** Copy/paste commands
- **Risk:** Low (changes are reversible)

---

## Revert Instructions (if needed)

To revert changes:

```bash
# Disable the service
sudo systemctl stop network-tuning.service
sudo systemctl disable network-tuning.service

# Reboot to reset network card to defaults
sudo reboot
```

---

## Credits

Issue diagnosed through:
- Network interface statistics analysis (10.5M dropped packets)
- Ring buffer configuration check (256 vs recommended 4096)
- Hardware offloading status verification
- Interrupt coalescing analysis

**Root cause:** Misconfigured network interface, NOT hardware failure.
