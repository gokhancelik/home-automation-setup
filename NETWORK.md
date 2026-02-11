You are a Kubernetes and networking expert.

I run a k3s home cluster and want to implement home network bottleneck detection and traffic monitoring.

🎯 Goal

Detect:

WAN saturation

Top bandwidth consumers

Per-host traffic

Interface utilization

Packet drops and errors

Bufferbloat indicators (latency under load)

The solution must be production-ready but lightweight.

🧱 Architecture Requirements

Design and implement the following stack:

1️⃣ ntopng (Flow Analysis)

Runs in its own namespace: network-observability

Deployed via Helm chart (create a custom Helm chart if needed)

Exposed via Ingress

Persistent storage using local-path StorageClass

Configurable to receive NetFlow/sFlow from router

Service type: ClusterIP

Include:

Deployment

Service

PVC

Ingress

values.yaml

README

2️⃣ Prometheus + SNMP Exporter (Router Metrics)

Deploy:

Prometheus

Scrapes:

SNMP exporter

Retention: 15 days

PVC using local-path

SNMP Exporter

Configured for generic router monitoring

Monitor:

WAN interface utilization

Errors

Drops

CPU load

SNMP target must be configurable via values.yaml

3️⃣ Grafana Dashboards

Provide:

Dashboard JSON for:

WAN utilization %

Top talkers (if available via Prometheus)

Packet drops

Latency correlation panel

Pre-configured datasource for Prometheus

📦 GitOps Structure

Use this folder structure:

infrastructure/
  network-observability/
    ntopng/
      Chart.yaml
      values.yaml
      templates/
    prometheus/
    snmp-exporter/

All components must:

Use namespace network-observability

Be Helm-based

Use clean labels and selectors

Follow Kubernetes best practices

⚙️ Configuration Requirements

In values.yaml allow configuration of:

Router IP

SNMP community string

NetFlow listening port

Ingress host

Resource requests/limits

Storage size

🧠 Advanced (Optional but Preferred)

Add:

Alert rules for:

WAN > 90% utilization for 5 minutes

Packet drops > threshold

ServiceMonitor if Prometheus Operator is used

Example Grafana dashboard import instructions

📄 Output Format

Generate:

Complete Helm chart files

values.yaml examples

README with:

Installation instructions

Router configuration steps

How to verify data ingestion

How to detect bottlenecks

Do not omit YAML.

🧩 Technical Assumptions

k3s cluster

local-path StorageClass

Ingress controller already installed

Router supports SNMP and NetFlow

End of instructions.