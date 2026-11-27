# TradingEngine (paper trading only)

The app's manifests (`k8s/` in the private repo `gokhancelik/TradingEngine`) are applied by the Flux Kustomization
`tradingengine`, which runs as the namespace-scoped service account `tradingengine-reconciler` (`rbac.yaml`) with
`targetNamespace: trading`. A bad merge in the app repo therefore cannot create cluster-wide objects. The `trading`
namespace (Pod Security `restricted`, never pruned) is cluster-scoped and is applied from here.

No broker keys, no live broker URLs. Live trading is out of scope.

## Turning it on, in order

Each step is one commit to `main` here; Flux applies it. Never `kubectl apply`.

1. **Live after this folder is merged:** `namespace.yaml`, `rbac.yaml`. Nothing else runs yet.
2. **Flux source and image automation.** Create a fine-grained PAT with `contents: read and write` on
   `gokhancelik/TradingEngine` only (expiry in your calendar), seal it and commit `github-token-sealed.yaml`:
   ```bash
   kubectl create secret generic tradingengine-github-token --namespace flux-system \
     --from-literal=username=gokhancelik --from-literal=password=<PAT> --dry-run=client -o yaml | \
   kubeseal --controller-name=sealed-secrets-controller --controller-namespace=kube-system --format=yaml \
     > clusters/microk8s/apps/tradingengine/github-token-sealed.yaml
   ```
   Add the PAT's expiry date as a comment in `gitrepository.yaml`, then uncomment the step 2 lines in `kustomization.yaml`.
   The image objects reuse the existing cluster-wide `ghcr-flux-pull` secret. Decide first how image automation
   pushes to the protected `main` of TradingEngine (owner bypass, or a `flux/image-updates` branch), or it fails silently.
3. **Database.** Seal `trading-db-credentials` (key `password`, namespace `data-storage`, the same password as the
   app repo's `trading-db` connection string) into `clusters/microk8s/databases/postgresql/trading-db-sealed.yaml`
   and uncomment the two trading lines in `clusters/microk8s/databases/postgresql/kustomization.yaml`.
4. **App.** Once the app repo holds its own sealed secrets (`trading-db`, `trading-proxy`, `ghcr-pull`) and its
   NetworkPolicies, uncomment `flux-kustomization.yaml`. Then run the NetworkPolicy tests from the app repo's
   `docs/runbooks/owner-checklist.md` (step 8).

The ingress (`trading.gcelik.dev`) and the GitHub login are not part of this folder: see
`clusters/microk8s/infrastructure/oauth2-proxy/README.md`. The ingress must not go live before the
X-Forwarded-For test there passes.
