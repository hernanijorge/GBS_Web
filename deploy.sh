#!/usr/bin/env bash
# Redeploy the current GHCR image on the Oracle Cloud VM.
#
# Assumes: the image for the current commit was already built and pushed by
# .github/workflows/docker-build.yml (triggered on push to master), and the
# VM already has /etc/gbs_web/gbs_web.env in place (see docs/DEPLOY.md).
#
# Usage: ./deploy.sh [ssh_key_path]
set -euo pipefail

VM_HOST="opc@129.80.215.94"
SSH_KEY="${1:-$HOME/.ssh/ssh-key-2026-09-18.key}"
IMAGE="ghcr.io/hernanijorge/gbs_web:latest"

echo "Pulling latest image and restarting gbs_web on $VM_HOST..."

ssh -i "$SSH_KEY" -o ConnectTimeout=15 "$VM_HOST" bash -s <<REMOTE
set -euo pipefail
sudo docker pull $IMAGE
sudo docker stop gbs_web 2>/dev/null || true
sudo docker rm gbs_web 2>/dev/null || true
sudo docker run -d --name gbs_web \\
  --restart unless-stopped \\
  --env-file /etc/gbs_web/gbs_web.env \\
  -p 127.0.0.1:8080:8080 \\
  $IMAGE
sleep 3
sudo docker ps --filter name=gbs_web
curl -s -o /dev/null -w 'Local health check: HTTP %{http_code}\n' http://localhost:8080/login
REMOTE

echo "Done. Check http://129.80.215.94/ (switch to https:// once a domain is set up per docs/DEPLOY.md)."
