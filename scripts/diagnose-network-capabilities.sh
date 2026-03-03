#!/bin/bash
# Network capability diagnostic script
# Run this on ubuntu-desktop to see what features are supported

echo "=== Network Interface Capabilities ==="
echo ""

echo "1. Checking Ring Buffer Support..."
ethtool -g enp2s0 2>&1
echo ""

echo "2. Checking Interrupt Coalescing Support..."
ethtool -c enp2s0 2>&1
echo ""

echo "3. Checking Hardware Offload Features..."
ethtool -k enp2s0 2>&1
echo ""

echo "4. Current Driver Information..."
ethtool -i enp2s0 2>&1
echo ""

echo "5. Testing Ring Buffer Change (dry-run)..."
sudo ethtool -G enp2s0 rx 4096 tx 4096 2>&1 && echo "✓ Ring buffer change SUPPORTED" || echo "✗ Ring buffer change NOT SUPPORTED"
echo ""

echo "6. Testing Interrupt Coalescing (dry-run)..."
sudo ethtool -C enp2s0 rx-usecs 50 rx-frames 64 2>&1 && echo "✓ Coalescing change SUPPORTED" || echo "✗ Coalescing change NOT SUPPORTED"
echo ""

echo "7. Testing Offload Features (checking which are available)..."
for feature in sg tso gso gro; do
    result=$(sudo ethtool -K enp2s0 $feature on 2>&1)
    if [ $? -eq 0 ]; then
        echo "  ✓ $feature: SUPPORTED"
    else
        echo "  ✗ $feature: NOT SUPPORTED ($result)"
    fi
done
echo ""

echo "=== Diagnostic Complete ==="
