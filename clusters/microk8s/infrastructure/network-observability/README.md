# Network Observability Stack

Complete network monitoring solution for home Kubernetes clusters with bottleneck detection and traffic analysis.

## 📋 Components

- **ntopng**: Network traffic analysis and NetFlow/sFlow collector
- **Prometheus**: Metrics storage and alerting (15-day retention)
- **SNMP Exporter**: Router metrics collection via SNMP
- **Grafana Dashboards**: Pre-configured network visualization

## 🎯 Capabilities

### Monitoring
- WAN saturation percentage
- Per-interface bandwidth utilization
- Packet drops and errors
- Interface status
- Top bandwidth consumers (via ntopng)
- Historical traffic patterns

### Alerting
- WAN > 90% utilization for 5+ minutes
- High packet drop rates
- Interface errors
- Interface down status

## 📦 Installation

### Prerequisites
- MicroK8s/K3s cluster
- `microk8s-hostpath` storage class
- Ingress controller (nginx)
- Router with SNMP and NetFlow support

### Step 1: Update Configuration

Edit router IP and SNMP settings in:

**snmp-exporter/deployment.yaml:**
```yaml
# Update router IP in Prometheus scrape config
targets:
  - 192.168.YOUR.ROUTER
```

**ntopng-helmrelease.yaml:**
```yaml
router:
  ip: "192.168.YOUR.ROUTER"
config:
  localNetworks: "192.168.0.0/16"  # Your LAN ranges
```

### Step 2: Deploy via Flux

```bash
# Add to main kustomization
cat >> clusters/microk8s/infrastructure/kustomization.yaml <<EOF
  - network-observability
EOF

# Reconcile Flux
flux reconcile kustomization flux-system --with-source
```

### Step 3: Configure Router

#### Enable SNMP (Example for common routers)

**For UniFi:**
```
Settings → System Settings → SNMP
- Enable SNMPv2c
- Community String: public
```

**For pfSense/OPNsense:**
```
Services → SNMP
- Enable: ✓
- Community: public
```

#### Enable NetFlow (Example)

**For UniFi:**
```
Settings → Internet → WAN → NetFlow
- Enable: ✓
- Host: <ntopng-service-IP>
- Port: 2055
```

**For pfSense:**
```
Services → softflowd
- Interface: WAN
- Host: <ntopng-service-IP>
- Port: 2055
- Version: 9
```

### Step 4: Get Service IPs

```bash
# Get ntopng service IP for NetFlow configuration
kubectl get svc -n network-observability ntopng -o jsonpath='{.spec.clusterIP}'

# Access ntopng UI
kubectl port-forward -n network-observability svc/ntopng 3000:3000
# Open: http://localhost:3000
# Login: admin / changeme (change this!)
```

## 🔍 Verification

### Check Data Ingestion

**Verify SNMP metrics:**
```bash
# Check SNMP exporter
kubectl logs -n network-observability deployment/snmp-exporter

# Query Prometheus for interface metrics
kubectl port-forward -n network-observability svc/prometheus-server 9090:80
# Open: http://localhost:9090
# Query: ifHCInOctets
```

**Verify NetFlow:**
```bash
# Check ntopng logs
kubectl logs -n network-observability deployment/ntopng

# Should see: "Flow collection started on port 2055"
```

### Test Connectivity

```bash
# Test SNMP from pod
kubectl run -n network-observability snmp-test --rm -it --image=alpine --restart=Never -- sh
apk add net-snmp net-snmp-tools
snmpwalk -v2c -c public YOUR_ROUTER_IP system
```

## 📊 Grafana Dashboard

### Import Dashboard

1. Port-forward to Grafana (from observability namespace):
```bash
kubectl port-forward -n observability svc/grafana 3000:3000
```

2. Login to Grafana (http://localhost:3000)

3. Import dashboard:
   - Go to Dashboards → Import
   - Upload `grafana-dashboard-network.json`
   - Select Prometheus datasource
   - Click Import

### Expected Panels
- WAN Utilization %
- WAN Bandwidth (Mbps)
- Packet Drops
- Interface Errors
- All Interfaces Traffic

## 🚨 Detecting Bottlenecks

### Via Grafana
1. Check "WAN Utilization %" panel
   - **> 90%** = saturated link, traffic queueing likely
   - **70-90%** = high usage, monitor closely
   
2. Check "Packet Drops" panel
   - **> 0** = buffer overflow, congestion

3. Correlate with latency:
   - Add ping/latency monitoring
   - High latency + high utilization = bufferbloat

### Via ntopng
1. Access ntopng UI
2. Go to "Flows" → "Active Flows"
3. Sort by "Bytes" to see top consumers
4. Check "Hosts" → "Top Hosts" for per-device usage

### Via Alerts
Check Prometheus alerts:
```bash
kubectl port-forward -n network-observability svc/prometheus-server 9090:80
# Open: http://localhost:9090/alerts
```

## 🔧 Troubleshooting

### No SNMP Data

```bash
# Check SNMP exporter logs
kubectl logs -n network-observability deployment/snmp-exporter

# Test SNMP manually
kubectl run -it --rm debug --image=alpine --restart=Never -- sh
apk add net-snmp-tools
snmpwalk -v2c -c public YOUR_ROUTER_IP 1.3.6.1.2.1.2.2.1.10
```

Common issues:
- Wrong community string
- SNMP disabled on router
- Firewall blocking UDP port 161
- Wrong router IP

### No NetFlow Data

```bash
# Check ntopng logs
kubectl logs -n network-observability deployment/ntopng | grep -i flow

# Verify UDP port is open
kubectl get svc -n network-observability ntopng
```

Common issues:
- NetFlow disabled on router
- Wrong IP configured
- Wrong port (should be 2055)
- Firewall blocking UDP

### ntopng Not Accessible

```bash
# Check pod status
kubectl get pods -n network-observability

# Check ingress
kubectl get ingress -n network-observability

# Port-forward directly
kubectl port-forward -n network-observability deployment/ntopng 3000:3000
```

## 📈 Resource Usage

Expected resource consumption:

| Component | CPU | Memory | Storage |
|-----------|-----|--------|---------|
| ntopng | 500m-2000m | 512Mi-2Gi | 10Gi |
| Prometheus | 500m-2000m | 1Gi-4Gi | 20Gi |
| SNMP Exporter | 100m-500m | 128Mi-512Mi | - |

## 🔐 Security

**Default Credentials:**
- ntopng: `admin` / `changeme`
- **IMPORTANT**: Change immediately after first login!

**SNMP Community:**
- Default: `public`
- For production, use SNMPv3 with authentication

**Ingress:**
- Add TLS certificates
- Configure authentication
- Restrict IP access if possible

## 📚 Additional Configuration

### Change ntopng Password

```bash
kubectl exec -it -n network-observability deployment/ntopng -- ntopng -A
# Follow prompts to set new password
```

### Adjust Data Retention

**Prometheus (edit helmrelease.yaml):**
```yaml
retention: 30d  # Increase to 30 days
```

**ntopng (edit values.yaml):**
```yaml
config:
  dataRetention: 60d  # Increase to 60 days
```

### Add More SNMP Targets

Edit `prometheus/helmrelease.yaml`:
```yaml
- targets:
  - 192.168.1.1  # Router
  - 192.168.1.2  # Switch
  - 192.168.1.3  # Access Point
```

## 🌐 Router-Specific Guides

### UniFi Dream Machine Pro
1. Enable SNMP: Settings → System → SNMP
2. Enable NetFlow: Settings → Internet → WAN → NetFlow
3. Set sampling rate: 1:1000 for 1Gbps links

### pfSense
1. Services → SNMP → Enable
2. Services → softflowd → Add new
3. Select WAN interface, set ntopng IP:2055

### MikroTik
```
/snmp set enabled=yes contact="admin" location="home"
/ip traffic-flow set enabled=yes interfaces=ether1
/ip traffic-flow target add address=<ntopng-ip>:2055 version=9
```

## 📞 Support

For issues or questions:
1. Check logs: `kubectl logs -n network-observability <pod-name>`
2. Verify router configuration
3. Test network connectivity from pods
4. Check Flux reconciliation: `flux get helmreleases -n network-observability`
