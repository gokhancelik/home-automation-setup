# oauth2-proxy: GitHub login in front of trading.gcelik.dev

Not wired yet: the line in `clusters/microk8s/infrastructure/kustomization.yaml` is commented out. The
`HelmRepository` in `clusters/microk8s/sources/oauth2-proxy.yaml` is live (it only indexes the chart).

Only the GitHub user `gokhancelik` may log in (`github-user`). Never add `github-org` or `github-team`; that widens access.

## Before enabling
1. **Gate: the IP allow-list must not be spoofable.** After the ingress-nginx change in this repo (`use-forwarded-headers: "false"`)
   has synced, check `kubectl -n ingress-nginx get cm ingress-nginx-controller -o yaml` and
   `kubectl -n ingress-nginx get svc -o yaml | grep externalTrafficPolicy` (must be `Local`). Then, from a phone on mobile
   data with no VPN:
   ```
   curl -sk -o /dev/null -w "%{http_code}\n" https://paperclip.gcelik.dev/
   curl -sk -o /dev/null -w "%{http_code}\n" -H "X-Forwarded-For: 192.168.1.10" https://paperclip.gcelik.dev/
   ```
   Both must return 403. Repeat for `trading.gcelik.dev` once its ingress exists.
2. Create a GitHub OAuth app with callback `https://trading.gcelik.dev/oauth2/callback`.
3. Seal `oauth2-proxy-secrets` (namespace `oauth2-proxy`) into `sealed-secret.yaml` in this folder:
   ```bash
   kubectl create secret generic oauth2-proxy-secrets --namespace oauth2-proxy \
     --from-literal=client-id=<id> --from-literal=client-secret=<secret> \
     --from-literal=cookie-secret="$(openssl rand -base64 32 | tr -- '+/' '-_')" \
     --dry-run=client -o yaml | \
   kubeseal --controller-name=sealed-secrets-controller --controller-namespace=kube-system --format=yaml > sealed-secret.yaml
   ```
4. Check `helmrelease.yaml` against `helm show values oauth2-proxy/oauth2-proxy --version 10.7.0` (the pinned version was
   not verified against the chart index from here), and run `helm template` to confirm the pod passes Pod Security `restricted`.
5. Uncomment `- oauth2-proxy/` in `clusters/microk8s/infrastructure/kustomization.yaml`.
6. Then the app repo adds `ingress.yaml` and `oauth2-ingress.yaml` to its `k8s/kustomization.yaml`, and the manual login
   tests in its `docs/runbooks/owner-checklist.md` (step 10) are run.
