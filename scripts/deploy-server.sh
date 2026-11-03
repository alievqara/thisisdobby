#!/usr/bin/env bash
# Run on the homelab server: bash scripts/deploy-server.sh
set -euo pipefail
base=${DOBBY_DEPLOY_ROOT:-/srv/homelab/apps/thisisdobby}
source_dir="$base/source"
service=${DOBBY_SERVICE:-thisisdobby}

sudo -v
previous=$(readlink -f "$base/current")
[ -f "$previous/DobbyBot.Worker.dll" ] || { echo "Əvvəlki versiya tapılmadı."; exit 1; }
git -C "$source_dir" fetch origin main
commit=$(git -C "$source_dir" rev-parse origin/main)
release="$base/releases/${commit:0:12}-$(date -u +%Y%m%dT%H%M%SZ)-$$"
stage=$(mktemp -d)
switch_dir=$(mktemp -d "$base/.switch.XXXXXX")
switched=0

cleanup() {
    code=$?
    trap - EXIT
    if [ "$code" -ne 0 ] && [ "$switched" -eq 1 ]; then
        echo "Yeni versiya başlamadı; əvvəlki versiya qaytarılır."
        sudo -n journalctl -u "$service" -n 40 --no-pager || true
        sudo -n ln -s "$previous" "$switch_dir/rollback"
        sudo -n mv -Tf "$switch_dir/rollback" "$base/current"
        sudo -n systemctl restart "$service" || true
    fi
    rm -rf -- "$stage" "$switch_dir"
    exit "$code"
}
trap cleanup EXIT

git -C "$source_dir" archive "$commit" | tar -x -C "$stage"
dotnet publish "$stage/DobbyBot.Worker/DobbyBot.Worker.csproj" -c Release -o "$release"
sudo -n chown -R root:dobby "$release"
sudo -n chmod -R u=rwX,g=rX,o= "$release"
if command -v setfacl >/dev/null; then
    sudo -n setfacl -R -m u:dobby:rX "$release"
fi
# Verify an actual open, rather than relying on a sudo test -r invocation.
sudo -n -u dobby /bin/sh -c 'head -c 1 "$1" >/dev/null' sh "$release/DobbyBot.Worker.dll"
sudo -n ln -s "$release" "$switch_dir/current"
sudo -n mv -Tf "$switch_dir/current" "$base/current"
switched=1
sudo -n systemctl restart "$service"
sleep 8
sudo -n systemctl is-active --quiet "$service"
switched=0
echo "Yeni versiya aktivdir: ${commit:0:12}"
sudo -n journalctl -u "$service" -n 25 --no-pager
