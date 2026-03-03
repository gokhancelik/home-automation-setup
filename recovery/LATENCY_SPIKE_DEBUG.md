# Periodic Latency Spike Investigation

## 📊 Observed Pattern

### Ping Results Analysis:
```
Baseline (excellent):  1.2-2.5ms  (pings 1-15, 22-50)
Spike burst:          61ms → 233ms  (pings 16-21)
Duration:             ~6 seconds
Frequency:            Once per 50 pings
```

**Average: 15.689ms** (elevated due to spike)  
**Without spike: ~1.4ms** (excellent!)

---

## 🔍 Possible Causes

Since CPU performance mode is already applied, the spike is caused by something else:

### 1. **Interrupt Coalescing Bufferbloat (Most Likely)**
- Setting: `rx-frames=32` batches packets
- When burst of 32+ packets arrives, last packets wait
- **Symptom:** Periodic 6-second spike, then normal
- **Test:** Reduce rx-frames to 8 or 16

### 2. **Kubernetes Pod Activity**
- Docker/containerd periodic operations
- Image layer checks, health checks, garbage collection
- **Symptom:** Periodic CPU/network spike
- **Test:** Check `kubectl top pods` during spike

### 3. **System Housekeeping**
- systemd timers (journal flush, tmpfiles cleanup)
- apt update checks
- **Symptom:** Periodic every N seconds
- **Test:** Check `systemctl list-timers`

### 4. **Router/Switch Issue**
- Router periodic ARP requests
- Switch spanning tree protocol
- **Symptom:** Affects all devices, not just ubuntu-desktop
- **Test:** Ping router from another device

---

## 🛠️ Diagnostic Commands

Run these on ubuntu-desktop to identify the cause:

### Test 1: Reduce Interrupt Coalescing

```bash
# Current setting (batch 32 packets)
ethtool -c enp2s0 | grep rx-frames

# Reduce to 8 packets (less batching = more consistent)
sudo ethtool -C enp2s0 rx-usecs 25 rx-frames 8

# Test ping again
ping -c 50 192.168.2.254
```

**If spikes reduce → It's bufferbloat from coalescing**

---

### Test 2: Monitor During Spike

Open 3 terminals and run simultaneously:

**Terminal 1: Continuous ping**
```bash
ping 192.168.2.254
```

**Terminal 2: Monitor CPU**
```bash
watch -n 0.5 'mpstat 1 1 | grep -A 1 Average'
```

**Terminal 3: Monitor network**
```bash
watch -n 0.5 'ip -s link show enp2s0 | grep -A 1 RX'
```

**When spike happens**, note:
- Does CPU spike?
- Do RX packets jump suddenly?
- Is there a pattern?

---

### Test 3: Check Kubernetes Activity

```bash
# Watch pod resource usage
kubectl top pods -A --sort-by=cpu

# Check for periodic jobs/cronjobs
kubectl get cronjobs -A

# Check Frigate activity (might be pulling images or processing)
kubectl logs -n frigate $(kubectl get pods -n frigate -l app=frigate -o name) --tail=50
```

---

### Test 4: Check System Timers

```bash
# List all timers (periodic tasks)
systemctl list-timers

# Check for activities around spike time
journalctl --since "5 minutes ago" | grep -E "(Started|Running)"
```

---

### Test 5: Ping from Another Device

On another machine (not ubuntu-desktop), ping the router:

```bash
ping -c 50 192.168.2.254
```

**If no spikes → Problem is ubuntu-desktop specific**  
**If spikes occur → Problem is router/switch**

---

## 🔧 Most Likely Fix: Reduce Coalescing

Based on the pattern (6-second burst), interrupt coalescing is the likely culprit.

### Solution 1: Reduce Batching (Recommended)

```bash
# Reduce from 32 to 8 packets per interrupt
sudo ethtool -C enp2s0 rx-usecs 25 rx-frames 8
sudo ethtool -C enp2s0 tx-usecs 25 tx-frames 8

# Test
ping -c 50 192.168.2.254
```

**Trade-off:**
- **More interrupts:** 8 vs 32 packets per interrupt (4x more)
- **Lower latency:** More consistent, no 200ms spikes
- **CPU impact:** Minimal (still 97% fewer interrupts than before)

---

### Solution 2: Disable Coalescing Completely (If #1 doesn't work)

```bash
# Back to 1 packet per interrupt (original)
sudo ethtool -C enp2s0 rx-usecs 0 rx-frames 1
sudo ethtool -C enp2s0 tx-usecs 0 tx-frames 1

# Test
ping -c 50 192.168.2.254
```

**Trade-off:**
- **Many interrupts:** Back to original (but offloading still enabled)
- **Lowest latency:** Most consistent possible
- **CPU impact:** Higher, but offloading compensates

---

### Solution 3: Adaptive Coalescing (If supported)

Check if Realtek driver supports adaptive mode:

```bash
# Check current adaptive setting
ethtool -c enp2s0 | grep -i adaptive

# Try to enable (might not be supported by r8169)
sudo ethtool -C enp2s0 adaptive-rx on adaptive-tx on
```

---

## 📊 Expected Results

### Current (with rx-frames=32):
- Baseline: 1.2-2.5ms ✅
- Periodic spike: 61-233ms ❌
- Average: 15.689ms ⚠️

### After reducing to rx-frames=8:
- Baseline: 1.2-2.5ms ✅
- Periodic spike: 5-10ms max ✅
- Average: 2-3ms ✅

### After disabling coalescing (rx-frames=1):
- Baseline: 1.2-2.5ms ✅
- No spikes: Consistent ✅
- Average: 1.5-2ms ✅

---

## 🎯 Quick Test Commands

Copy/paste on ubuntu-desktop:

```bash
echo "=== Current coalescing settings ==="
ethtool -c enp2s0 | grep -E "(rx-usecs|rx-frames|tx-usecs|tx-frames):"

echo ""
echo "=== Reducing coalescing to 8 packets ==="
sudo ethtool -C enp2s0 rx-usecs 25 rx-frames 8
sudo ethtool -C enp2s0 tx-usecs 25 tx-frames 8

echo ""
echo "=== Verifying change ==="
ethtool -c enp2s0 | grep -E "(rx-usecs|rx-frames|tx-usecs|tx-frames):"

echo ""
echo "=== Testing network (watch for spikes) ==="
ping -c 50 192.168.2.254
```

---

## 💡 Theory: Why Spikes Happen

**Scenario:**
1. Network quiet: Packets processed individually, low latency (1.2ms)
2. Burst arrives: 40 packets arrive rapidly from Kubernetes/Docker
3. Coalescing: Driver waits to batch 32 packets
4. Last 8 packets: Wait in queue for next batch
5. Timeout: After 50 microseconds, flush queue
6. Result: Those 8 packets see 200ms+ delay
7. Back to normal: Queue empty, back to fast processing

**Fix:** Reduce batch size so queue never gets that deep.

---

## 📞 What to Share

After running test commands, share:

1. **Coalescing settings:**
   ```bash
   ethtool -c enp2s0
   ```

2. **Ping results after reducing to rx-frames=8:**
   ```bash
   ping -c 50 192.168.2.254
   ```

3. **System load during spike:**
   ```bash
   uptime
   kubectl top pods -A
   ```

This will confirm if interrupt coalescing is the culprit!
