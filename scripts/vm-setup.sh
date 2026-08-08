#!/usr/bin/env bash
# One-time setup for a fresh Ubuntu VM (Oracle Cloud Always Free, Hetzner, or DigitalOcean).
# Run as a user with sudo, e.g.: curl -fsSL <raw-url> | bash
# or after `scp`'ing this file up: bash vm-setup.sh
set -euo pipefail

echo "== Installing Docker =="
if ! command -v docker >/dev/null; then
    curl -fsSL https://get.docker.com | sudo sh
    sudo usermod -aG docker "$USER"
fi

echo "== Firewall (ufw): allow only SSH, HTTP, HTTPS =="
sudo apt-get update -y
sudo apt-get install -y ufw unattended-upgrades
sudo ufw allow OpenSSH
sudo ufw allow 80,443/tcp
sudo ufw --force enable

echo "== Automatic security patches =="
sudo dpkg-reconfigure -f noninteractive unattended-upgrades

echo "== App directory =="
sudo mkdir -p /opt/carshowjudging
sudo chown "$USER":"$USER" /opt/carshowjudging
echo "Copy docker-compose.yml, Caddyfile, and a filled-in .env (see .env.example) into /opt/carshowjudging."

echo "== Daily backup cron (db + uploads volumes -> local tarball, kept 14 days) =="
sudo mkdir -p /opt/carshowjudging/backups
sudo tee /opt/carshowjudging/backup.sh >/dev/null <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
cd /opt/carshowjudging
STAMP=$(date +%F)
docker run --rm \
  -v carshowjudging_carshow-data:/data:ro \
  -v carshowjudging_carshow-uploads:/uploads:ro \
  -v /opt/carshowjudging/backups:/backup \
  alpine tar czf "/backup/carshow-${STAMP}.tar.gz" -C / data uploads
find /opt/carshowjudging/backups -name '*.tar.gz' -mtime +14 -delete
EOF
sudo chmod +x /opt/carshowjudging/backup.sh
(crontab -l 2>/dev/null | grep -v backup.sh; echo "0 3 * * * /opt/carshowjudging/backup.sh") | crontab -

echo "== Done. Log out/in for the docker group change to take effect. =="
echo "Next: copy docker-compose.yml, Caddyfile, .env into /opt/carshowjudging, then: docker compose up -d"
