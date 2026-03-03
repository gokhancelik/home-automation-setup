# Network Performance Fix - Safe Mode

## ⚠️ Error Troubleshooting

If you got "**netlink error: Operation not supported**", it means some features aren't available on your network card.

---

## 🔍 Step 1: Diagnose What's Supported

Run this diagnostic script to see which features your network card actually supports:

```bash
# Copy the diagnostic script
cat > /tmp/diagnose-network.sh << 'EOF'
#!/bin/bash
echo "=== Checking Ring Buffer Support ==="
ethtool -g enp2s0
echo ""
echo "=== Checking Interrupt Coalescing Support ==="
ethtool -c enp2s0
echo ""
echo "=== Checking Offload Features ==="
ethtool -k enp2s0 | grep -E "(scatter-gather|segmentation-offload|receive-offload)"
echo ""
echo "=== Driver Info ==="
ethtool -i enp2s0
EOF

chmod +x /tmp/diagnose-network.sh
sudo /tmp/diagnose-network.sh
```

**📋 Copy the output and share it with me so I can create a custom fix for your hardware.**

---

## 🛠️ Step 2: Try Safe Commands One-by-One

Instead of running all commands together, try each one separately to identify which fails:

### Test Ring Buffer:
```bash
sudo ethtool -G enp2s0 rx 4096 tx 4096
```

**If this fails**, try smaller values:
```bash
# Try 2048
sudo ethtool -G enp2s0 rx 2048 tx 2048

# Or try 1024
sudo ethtool -G enp2s0 rx 1024 tx 1024
```

---

### Test Interrupt Coalescing:
```bash
sudo ethtool -C enp2s0 rx-usecs 50 rx-frames 64
```

**If this fails**, try simpler settings:
```bash
# Just rx-usecs
sudo ethtool -C enp2s0 rx-usecs 50

# Or just rx-frames
sudo ethtool -C enp2s0 rx-frames 32
```

---

### Test Hardware Offloading (safest):
```bash
# Try each feature individually
sudo ethtool -K enp2s0 sg on
sudo ethtool -K enp2s0 tso on
sudo ethtool -K enp2s0 gso on
sudo ethtool -K enp2s0 gro on
```

**These are most likely to work!** Hardware offloading is usually supported.

---

## 🔧 Step 3: Apply Only What Works

Based on Step 2, create a fix script with **only the commands that succeeded**:

```bash
sudo nano /usr/local/bin/tune-network-safe.sh
```

**Template (remove lines for unsupported features):**
```bash
#!/bin/bash
# Apply only supported optimizations

# Ring buffer (REMOVE IF FAILED)
ethtool -G enp2s0 rx 4096 tx 4096

# Interrupt coalescing (REMOVE IF FAILED)
ethtool -C enp2s0 rx-usecs 50 rx-frames 64

# Hardware offloading (KEEP WHAT WORKS)
ethtool -K enp2s0 sg on
ethtool -K enp2s0 tso on
ethtool -K enp2s0 gso on
ethtool -K enp2s0 gro on

exit 0
```

Save and make executable:
```bash
sudo chmod +x /usr/local/bin/tune-network-safe.sh
```

Test it:
```bash
sudo /usr/local/bin/tune-network-safe.sh
```

---

## 🔄 Step 4: Make It Permanent

If the script works, create the systemd service:

```bash
sudo nano /etc/systemd/system/network-tuning.service
```

Paste:
```ini
[Unit]
Description=Network Performance Tuning
After=network-online.target
Wants=network-online.target

[Service]
Type=oneshot
ExecStart=/usr/local/bin/tune-network-safe.sh
RemainAfterExit=yes

[Install]
WantedBy=multi-user.target
```

Enable it:
```bash
sudo systemctl daemon-reload
sudo systemctl enable network-tuning.service
sudo systemctl start network-tuning.service
sudo systemctl status network-tuning.service
```

---

## 📊 What to Share

Please run the diagnostic and share:
1. Output of the diagnostic script
2. Which specific command gave the error
3. Your network card model: `lspci | grep -i ethernet`

I'll create a custom fix tailored to your hardware! 🎯
