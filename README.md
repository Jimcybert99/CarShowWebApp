# CarShowJudging

A self-hosted web app for running a car show: participants register vehicles (with a photo and optional class assignments), judges score each entry against five criteria, and admins manage entries, classes, users, and the live scoring leaderboard.

Built with **Blazor Server on .NET 10**, **SQLite** for data, and local-disk storage for vehicle photos. Three roles: **Admin**, **Judge**, **User**. See [CLAUDE.md](CLAUDE.md) for the full architecture reference (data model, service layer, page structure, scoring model) — this README is about *running and deploying* the app, not its internals.

## Current state (as of this session)

This app was dev-only until now. This session did three things to make it deployable:

1. **Removed the SQL Server / Azure Blob Storage code paths entirely.** The app now always uses SQLite and always stores photos on local disk — no more environment-based branching, no more Azure SDK dependency. This was a deliberate simplification for this app's actual scale (a single small event, low concurrency), not a limitation to work around.
2. **Fixed a real security bug**: `Program.cs` used to seed an `admin`/`password123` account into whichever database it started against, unconditionally, in every environment — including a real production deploy. It now reads the admin password from configuration (`Seed:AdminPassword` / `SEED__ADMINPASSWORD` env var) and **refuses to start outside Development** if that hasn't been set to something other than the default. See "Common prerequisites" below — this is the one step you cannot skip.
3. **Mobile-responsiveness pass.** The Bootstrap foundation (viewport meta tag, collapsible nav, `table-responsive` wrappers) was already solid. What was fixed: the widest data tables (`/admin/scores` especially — 11 columns) now hide lower-priority columns (Owner, Classes) below the `md` breakpoint instead of forcing horizontal scroll on a phone; a couple of non-responsive Bootstrap column classes (`col-5` instead of `col-12 col-sm-5`) were corrected; modals got `modal-dialog-centered`; a `theme-color` meta tag was added.

This session also added **Plan C**: Docker + Caddy + GitHub Actions + DuckDNS, for a push-to-deploy setup on a cloud VM. In practice, signing up for a cloud VM hit a wall: Oracle Cloud's Always Free signup rejected the account with an opaque anti-fraud error across multiple payment methods, and Hetzner's plan selector wasn't offering the cheap tier either. The deployment target pivoted to local self-hosting instead — but the *first* version of that plan (DuckDNS + router port-forwarding) hit its own wall: this machine's internet is **Starlink**, whose Standard/Mobile plans put every customer behind CGNAT with a router that flatly does not support port forwarding (confirmed — no reachable public IPv4 or IPv6 on this connection). So **Plan D** now uses **Cloudflare Tunnel** instead: `cloudflared` makes an outbound connection to Cloudflare's edge, so nothing needs to be reachable from the internet and CGNAT is a non-issue. This did mean buying a real domain (Cloudflare Tunnel needs one for a stable hostname), a small step back from the original "no domain" ask, but was the most robust option once port-forwarding was ruled out entirely. The `Dockerfile`, `docker-compose.yml`, `.env.example`, and `.github/workflows/build.yml` are shared by Plans C and D (via Docker Compose profiles — `vps` runs Caddy for Plan C, `cloudflare` runs `cloudflared` for Plan D); `Caddyfile` and `scripts/vm-setup.sh` are Plan C-only; `scripts/poll-deploy.ps1` / `backup.ps1` / `register-scheduled-tasks.ps1` are Plan D-only. Plans A and B need none of this.

### Plan D was live, now superseded by Plan C on Google Cloud

Plan D (this laptop + Cloudflare Tunnel) ran successfully for about a day, but the point of buying the domain and setting up Cloudflare DNS was always to eventually get off a machine that has to stay on — so it's since been migrated to **Plan C on Google Cloud's Always Free tier**, with Cloudflare now used purely for DNS (no Tunnel).

**Current live setup**: a Google Cloud `e2-micro` VM (1 shared vCPU, 1GB RAM — free forever under the Always Free program, not a trial) in `us-central1`, running Ubuntu 26.04 LTS Minimal. `docker compose up -d` there runs the app + Caddy (the `vps` profile), with Caddy handling automatic HTTPS via Let's Encrypt directly — no Cloudflare proxy in front (the DNS record is deliberately "DNS only," not proxied, so Caddy's ACME HTTP-01 challenge reaches the VM directly). `rutabegacarshow.com`'s Cloudflare DNS record is now a plain **A** record pointing at the VM's external IP, replacing the old Tunnel CNAME.

Since Always Free's `e2-micro` is genuinely tight on RAM for Docker + a .NET app, a 2GB swap file was added (`/swapfile`, persisted via `/etc/fstab`) as a safety net against OOM kills rather than paying for a bigger tier — this app's actual footprint (a handful of concurrent users, a SQLite file currently under 200KB) doesn't need more than `e2-micro`, it just needed the swap cushion.

Also note: this VM's project has **OS Login enabled**, which makes GCP ignore SSH keys added via instance metadata (the console's "SSH Keys" field) — access is instead via a manually-created `deploy` user (`useradd`, key dropped straight into `~/.ssh/authorized_keys`, passwordless sudo via `/etc/sudoers.d/deploy`) rather than the metadata-key route the setup docs below describe. If OS Login is enabled on a future VM, do the same thing rather than fighting the metadata-key prompt.

Migration itself: the existing SQLite DB + uploaded photos were tarred from the laptop's `carshowwebapp_carshow-data`/`carshowwebapp_carshow-uploads` Docker volumes, copied to the VM, and extracted into new volumes (`carshowjudging_carshow-data`/`carshowjudging_carshow-uploads` — the different prefix is just `docker compose`'s project-name-from-directory-name behavior, `/opt/carshowjudging` vs. the laptop's folder name) *before* first bringing up the stack there, so the app started with real data already in place rather than seeding fresh. Verified end-to-end afterward: DNS resolves to the VM, Caddy obtained a real Let's Encrypt cert on the first retry after DNS settled, and `/login` returns 200 with the migrated admin account intact.

The laptop's local deployment has been fully decommissioned: `docker compose down` was run there, and the `CarShowJudging-PollDeploy` / `CarShowJudging-Backup` Windows Scheduled Tasks (which only make sense for Plan D) are `Disabled`, not just idle — they'd otherwise be managing a docker-compose stack that no longer needs to run. The old `cloudflared` Tunnel object in the Cloudflare dashboard (Networking → Tunnels) was left in place but is now unused/inert (its DNS record was deleted); safe to delete from the dashboard too, just not urgent.

**Backups**: `vm-setup.sh`'s cron job (`/opt/carshowjudging/backup.sh`, daily at 3am) tars both volumes to `/opt/carshowjudging/backups` (kept 14 days locally), then also uploads that day's tarball to the `rutabegacarshow-backups` Cloud Storage bucket (`us-central1`, Standard class — comfortably inside the Always Free 5GB allowance at ~10MB/day) via `gcloud storage cp`, using the VM's own service account rather than a separately-managed key file. Verified working end-to-end: a manual run of `backup.sh` produced a matching object in the bucket.

Getting the VM authorized to write to Cloud Storage required changing its access scope from the default (read-only for Storage) to read/write — this can only be changed while the instance is stopped, so it *did* cause a short, deliberate outage (the app came back on its own via `restart: unless-stopped` once the VM rebooted). Before that stop/start cycle, `34.123.218.188` was promoted from an ephemeral to a **static** external IP specifically to guarantee the reboot wouldn't hand out a different address and silently break DNS — worth doing on any VM before its first stop/start, not just this one.

`vm-setup.sh` now takes an optional `GCS_BACKUP_BUCKET` environment variable to wire this up automatically on a future re-provision (`GCS_BACKUP_BUCKET=your-bucket-name bash vm-setup.sh`) — it only adds the `gcloud storage cp` step to the generated `backup.sh` if that variable is set, so the script stays usable as-is on providers without a Google Cloud Storage option. The Storage read/write access scope and the `gcloud` CLI itself (`sudo snap install google-cloud-cli --classic`) still need to be set up separately, the same way they were here.

The disk was also grown from its initial 10GB to the Always Free tier's full **30GB** allowance (`growpart` + `resize2fs` after resizing the disk in the Console) — not because 10GB was actually running low, just to bank the free headroom while resizing was easy (growing later requires the same live-resize dance; shrinking a disk isn't possible at all without a rebuild).

One gotcha hit during this migration worth remembering for next time: **Minimal Ubuntu images don't include `cron`** — `vm-setup.sh` assumes it's present (it's part of standard Ubuntu Server), so on a Minimal image `sudo apt-get install -y cron && sudo systemctl enable --now cron` has to run before the script's crontab step will work. Worth fixing in the script itself if a Minimal image gets used again.

**A significant bug was found and fixed post-deploy**: the app used the classic `AddServerSideBlazor()`/`MapBlazorHub()` Blazor Server hosting model, which has a real, confirmed .NET 10 regression — `_framework/blazor.server.js` 404s ([dotnet/aspnetcore#66175](https://github.com/dotnet/aspnetcore/issues/66175)), silently breaking *all* interactive Blazor components sitewide (buttons/forms appeared to do nothing — vehicle registration, admin user management, anything depending on a live SignalR circuit). Classic Razor Pages forms (login, user registration) were unaffected, which is what made this non-obvious. Confirmed via a from-scratch `dotnet new blazorserver` reproduction — that template doesn't even exist anymore in the .NET 10 SDK, replaced by the unified "Blazor Web App" model. **Fix**: migrated to `AddRazorComponents().AddInteractiveServerComponents()` / `MapRazorComponents<App>().AddInteractiveServerRenderMode()`, added a new root `App.razor` (replacing `_Host.cshtml`, which was deleted) and `Routes.razor` (the old `App.razor`'s router, moved), and added `<RequiresAspNetWebAssets>true</RequiresAspNetWebAssets>` to the `.csproj` (without it, the framework script still wasn't included in the static assets manifest even after the model migration). The Auth Razor Pages (`Pages/Auth/*`) and their shared `_Layout.cshtml` were deliberately left untouched — they don't depend on the Blazor circuit and were already working; touching them wasn't necessary and would have added risk. If those pages ever need embedded interactive components again, note `_Layout.cshtml` still references the old `_framework/blazor.server.js` path and would need the same fix.

### What's explicitly NOT done yet

- **No real-device testing of the mobile-responsiveness CSS pass specifically** — general app functionality *has* now been tested on a real phone (that's how the Blazor interactivity bug above was actually caught), but nobody has specifically gone through the mobile layout/breakpoint fixes from earlier in this session on a real device. Treat that specific pass as "should work" not "confirmed."
- **Password-reset email is a real gotcha, not hypothetical.** `appsettings.json` has `Smtp:Host` set to `smtp.gmail.com` with blank `User`/`Password`. That means it is **not** a silent no-op in production — `EmailService` only falls back to log-only mode when `Smtp:Host` itself is empty, which it isn't. Deploy without setting real `Smtp:User`/`Smtp:Password` and the *first* password-reset request will throw an SMTP auth error. Either set real SMTP credentials (env vars, see below) or accept that "Forgot password" is broken and users needing a reset must be handled manually by an admin.
- **CDN dependency for Bootstrap/Bootstrap Icons.** Both are loaded from jsdelivr's CDN, not vendored locally. If your hosting environment has flaky/no outbound internet, or the CDN has an outage, the app will render unstyled. Not fixed this pass — flagged as a possible follow-up if it becomes a problem.

## Common prerequisites (all plans)

Whichever plan you follow, do this first:

1. **Build a Release copy:**
   ```
   dotnet publish CarShowJudging.Web -c Release -o ./publish
   ```
2. **Pick a strong admin password and set it as an environment variable** on the target machine before first run:
   ```
   SEED__ADMINPASSWORD=<a real password, not password123>
   ```
   The app will throw on startup and refuse to run without this (outside Development). This seeds the one-time `admin` account (username `admin`) on first run only — if an `admin` user already exists in the database, this is ignored.
3. **Decide about password-reset email.** If you want "Forgot password" to work, also set:
   ```
   Smtp__User=<a real sending address>
   Smtp__Password=<its app password / SMTP password>
   ```
   If you skip this, don't tell participants "forgot password" works — an admin will need to handle resets manually (there's currently no admin-driven "reset this user's password" button; that would be a small follow-up feature if this matters).
4. **Migrations run automatically on startup** (`db.Database.MigrateAsync()` in `Program.cs`) — there is no separate manual migration step. The app just needs write access to wherever its SQLite file and `wwwroot/uploads/vehicles/` folder live.
5. **Know where your data lives:** the SQLite file (`carshow.db` by default, path set by `ConnectionStrings__DefaultConnection`) and `wwwroot/uploads/vehicles/` (uploaded photos) are the *entire* state of this app. Back up both. Both plans below include a backup step — don't skip it.

---

## Plan A — Self-host on your own Windows machine

For running this from a Windows PC/server you control (e.g. at home), reachable either only on your LAN or forwarded out to the internet.

### 1. Install the .NET 10 runtime
Download and install the **ASP.NET Core Runtime 10.0** (Hosting Bundle if you might ever front this with IIS, otherwise the plain runtime is enough) from https://dotnet.microsoft.com/download/dotnet/10.0 — get the Windows x64 installer.

### 2. Publish and copy the app
From a dev machine (or the server itself, if it has the SDK):
```
dotnet publish CarShowJudging.Web -c Release -o C:\CarShowJudging
```
Copy the `C:\CarShowJudging` folder to the target machine if publishing elsewhere.

### 3. Configure environment variables
Set these as **System** environment variables on the target machine (System Properties → Environment Variables), not just user variables, so they're visible no matter how the app is launched:
- `ASPNETCORE_ENVIRONMENT=Production`
- `SEED__ADMINPASSWORD=<your chosen password>`
- `ASPNETCORE_URLS=http://0.0.0.0:5000` (or your chosen port; add a second `https://0.0.0.0:5001` entry once you have a cert — see step 5)
- Optionally `ConnectionStrings__DefaultConnection=Data Source=C:\CarShowJudging\data\carshow.db` if you want the DB file somewhere specific (create that folder first)
- Optionally `Smtp__User=...` / `Smtp__Password=...` per the prerequisites above

### 4. Run it as a background service (so it survives reboots/logouts)
The simplest reliable option is **NSSM** (Non-Sucking Service Manager, https://nssm.cc/):
```
nssm install CarShowJudging
```
In the GUI that opens: Path = `C:\CarShowJudging\CarShowJudging.Web.exe`, Startup directory = `C:\CarShowJudging`. Then:
```
nssm start CarShowJudging
```
It'll now start automatically on boot. To update the app later: `nssm stop CarShowJudging`, replace the files with a fresh publish, `nssm start CarShowJudging`.

### 5. Make it reachable + get TLS
- **LAN-only** (simplest, fine for an on-site event where everyone's on the same WiFi): skip TLS, just share `http://<the machine's LAN IP>:5000` with judges/participants. Add a Windows Firewall inbound rule for the port (`New-NetFirewallRule -DisplayName "CarShowJudging" -Direction Inbound -LocalPort 5000 -Protocol TCP -Action Allow` in an admin PowerShell).
- **Reachable from the internet with a real domain**: forward port 443 (and 80, for the ACME challenge) on your router to this machine, point a domain's DNS A record at your public IP, then use **win-acme** (https://www.win-acme.com/) to get a free Let's Encrypt certificate and bind it — win-acme can create the IIS/Kestrel binding for you interactively, or export a `.pfx` you point Kestrel at via `ASPNETCORE_URLS` + a `Kestrel:Certificates:Default:Path`/`Password` config entry. This is the fiddliest part of self-hosting; budget real time for it if you go this route.

### 6. Back up your data
Set up a Windows Task Scheduler task (daily, or right after the event) running:
```
robocopy C:\CarShowJudging\data C:\Backups\CarShowJudging\data /E
robocopy C:\CarShowJudging\wwwroot\uploads\vehicles C:\Backups\CarShowJudging\uploads /E
```
(adjust paths to wherever your DB file and uploads folder actually are). Copy the backup folder off the machine too (external drive, cloud sync folder) — a backup that lives only on the same disk isn't much of a backup.

---

## Plan B — Minimal-budget Linux VPS ($0-10/mo)

For a small cloud VM you rent. Concrete options at this budget:
- **Oracle Cloud "Always Free" tier** — genuinely $0/mo forever for a small ARM instance, the best option to try first if you're comfortable with a slightly more involved signup.
- **Hetzner Cloud CX22** (~€4/mo) or **DigitalOcean Basic Droplet** (~$4-6/mo) — simpler signup, still cheap, straightforward if Oracle's signup is too much friction.

Pick Ubuntu 22.04 or 24.04 LTS as the OS image for any of these.

### 1. Install the .NET 10 ASP.NET Core runtime
SSH into the VPS, then:
```bash
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 10.0 --runtime aspnetcore --install-dir /usr/share/dotnet
sudo ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet
```

### 2. Publish and copy the app up
From your dev machine:
```
dotnet publish CarShowJudging.Web -c Release -o ./publish
scp -r ./publish/* youruser@your-vps-ip:/opt/carshowjudging/
```
(create `/opt/carshowjudging` on the VPS first: `sudo mkdir -p /opt/carshowjudging && sudo chown $USER /opt/carshowjudging`)

### 3. Create a systemd service
On the VPS, create `/etc/systemd/system/carshowjudging.service`:
```ini
[Unit]
Description=CarShowJudging
After=network.target

[Service]
WorkingDirectory=/opt/carshowjudging
ExecStart=/usr/bin/dotnet /opt/carshowjudging/CarShowJudging.Web.dll
Restart=always
RestartSec=5
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:5000
Environment=SEED__ADMINPASSWORD=<your chosen password>
# Environment=Smtp__User=...
# Environment=Smtp__Password=...

[Install]
WantedBy=multi-user.target
```
Make sure `/opt/carshowjudging` (and wherever the SQLite file/uploads folder end up) is writable by `www-data`: `sudo chown -R www-data:www-data /opt/carshowjudging`. Then:
```bash
sudo systemctl daemon-reload
sudo systemctl enable --now carshowjudging
sudo systemctl status carshowjudging   # confirm it's running
```
Binding to `127.0.0.1` (not `0.0.0.0`) is intentional — the app is only reachable through the reverse proxy set up next, not directly from the internet.

### 4. Put Caddy in front of it for automatic HTTPS
Caddy is the simplest option here — it gets you a real Let's Encrypt certificate with almost no config, as long as you have a domain pointed at the VPS's IP (an A record).
```bash
sudo apt install -y debian-keyring debian-archive-keyring apt-transport-https curl
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' | sudo gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' | sudo tee /etc/apt/sources.list.d/caddy-stable.list
sudo apt update && sudo apt install -y caddy
```
Edit `/etc/caddy/Caddyfile` to just:
```
yourdomain.com {
    reverse_proxy 127.0.0.1:5000
}
```
Then `sudo systemctl restart caddy`. That's it — Caddy handles the certificate and HTTPS redirect automatically. No domain yet? You can reach the app directly at `http://your-vps-ip:5000` over plain HTTP by opening that port in the firewall (step 5) instead — fine for a quick one-time event, not recommended if participants are entering personal info/passwords.

### 5. Firewall
```bash
sudo ufw allow OpenSSH
sudo ufw allow 80,443/tcp   # if using Caddy
sudo ufw enable
```

### 6. Back up your data
A cron job scp'ing back to your own machine is enough for this scale. On your **own** machine (not the VPS), add to crontab (`crontab -e`) or run manually before/after the event:
```bash
scp youruser@your-vps-ip:/opt/carshowjudging/carshow.db ./backups/carshow-$(date +%F).db
scp -r youruser@your-vps-ip:/opt/carshowjudging/wwwroot/uploads/vehicles ./backups/uploads-$(date +%F)/
```

### Updating later
```
dotnet publish CarShowJudging.Web -c Release -o ./publish
scp -r ./publish/* youruser@your-vps-ip:/opt/carshowjudging/
ssh youruser@your-vps-ip 'sudo systemctl restart carshowjudging'
```

---

## Plan C — Docker + GitHub Actions + DuckDNS on a cloud VM

For someone who's never run a server before and wants `git push` to be the entire deploy step afterward, at $0-6/month for about a month. This builds on Plan B's foundation (Linux VM + Caddy for automatic HTTPS) but containerizes the app and automates the deploy with GitHub Actions, instead of manual `dotnet publish`/`scp`.

**Superseded by Plan D for this deployment** — Oracle Cloud's free-tier signup rejected the account (a known, opaque anti-fraud false-positive), and Hetzner's cheap tier wasn't selectable at signup time. Left here in case a cloud VM becomes viable again later (different provider, retrying Oracle, etc.) — the Docker/Caddy files are shared with Plan D either way.

**Why this shape:** SQLite + local-disk photos means the app's entire state is a file and a folder — that rules out most free PaaS tiers (Render/Railway free web services use ephemeral disks that wipe on every redeploy). A real VM with a persistent volume is required.

### 1. Get a VM
- **Oracle Cloud "Always Free" tier** (try first) — genuinely free forever, no 1-month expiry to track. Sign up, create a Compute instance: Ubuntu 24.04, Ampere A1 (ARM) or VM.Standard.E2.1.Micro shape, upload/generate an SSH key pair during creation, note the public IP.
- **Fallback**: Hetzner CX22 (~€4/mo) or DigitalOcean Basic Droplet (~$4-6/mo) if Oracle's signup/capacity is a blocker — a few dollars total for a 1-month deployment.

### 2. DNS via DuckDNS (free custom URL, no domain purchase)
Go to [duckdns.org](https://www.duckdns.org), sign in, create a subdomain (e.g. `carshowjudging`), and point it at the VM's public IP. You'll get `carshowjudging.duckdns.org`. Update the IP there any time the VM's address changes.

### 3. Bootstrap the VM
SSH in (`ssh ubuntu@<vm-ip>`), copy up `scripts/vm-setup.sh`, and run it:
```bash
scp scripts/vm-setup.sh ubuntu@<vm-ip>:~
ssh ubuntu@<vm-ip> 'bash vm-setup.sh'
```
This installs Docker, sets up `ufw` (only 22/80/443 open), enables `unattended-upgrades`, creates `/opt/carshowjudging`, and installs a daily backup cron job (tars the DB + uploads volumes to `/opt/carshowjudging/backups`, keeps 14 days).

### 4. Ship the compose files and secrets to the VM
From your dev machine:
```bash
scp docker-compose.yml Caddyfile ubuntu@<vm-ip>:/opt/carshowjudging/
```
On the VM, create `/opt/carshowjudging/.env` (copy `.env.example` as a template — **never commit the real `.env`**):
```
DUCKDNS_SUBDOMAIN=carshowjudging
SEED__ADMINPASSWORD=<a real password, not password123>
SMTP_USER=
SMTP_PASSWORD=
IMAGE=ghcr.io/jimcybert99/carshowwebapp:latest
```
Then bring it up once manually to confirm it works:
```bash
cd /opt/carshowjudging && docker compose up -d
```

### 5. Wire up GitHub Actions for push-to-deploy
`.github/workflows/deploy.yml` already exists in this repo: on push to `main` it builds the Docker image, pushes to `ghcr.io/jimcybert99/carshowwebapp`, then SSHes into the VM and runs `docker compose pull && docker compose up -d`. Add these repo secrets (Settings → Secrets and variables → Actions):
- `VM_HOST` — the VM's public IP
- `VM_USER` — e.g. `ubuntu`
- `VM_SSH_KEY` — the **private** key matching a public key already in the VM's `~/.ssh/authorized_keys` (generate a dedicated deploy key pair, don't reuse your personal one)

No `GHCR` login secret is needed — the workflow uses the automatically-provided `GITHUB_TOKEN`. The image is public to your GitHub account by default; the VM doesn't need to authenticate to pull it unless you make the package private.

After this, every `git push` to `main` rebuilds and redeploys automatically — no manual steps.

### 6. Firewall, patching, and backups
Already handled by `scripts/vm-setup.sh` in step 3 above (`ufw`, `unattended-upgrades`, daily backup cron). If this deployment outlives its ~1-month lifespan, the one manual follow-up worth adding is copying `/opt/carshowjudging/backups` off-box (the cron job only keeps local, on-VM backups).

### Verifying it worked
- `docker compose up` locally (repo root) to confirm the image builds and the app starts (needs a local `.env` — copy `.env.example`).
- Restart the containers and confirm the SQLite data and an uploaded vehicle photo both survive — proves the volume mounts are correct.
- Push a trivial commit to `main` and watch the Actions tab: build → push to GHCR → SSH deploy should all go green.
- Visit `https://<subdomain>.duckdns.org` and confirm a valid Let's Encrypt padlock (Caddy handles this automatically) and that login/registration/scoring work end-to-end.
- Check `/opt/carshowjudging/backups` on the VM the next day for a dated tarball.

---

## Plan D — Local self-host (Docker Desktop) via Cloudflare Tunnel

For running this on a home Windows machine instead of a rented VM. This is the path actually being followed for this deployment, after Oracle Cloud's signup rejected the account, Hetzner's cheap tier wasn't selectable, and — the real forcing factor — the home internet connection turned out to be **Starlink**, whose Standard/Mobile plans put every customer behind CGNAT with a router that doesn't support port forwarding at all (confirmed: no reachable public IPv4 *or* IPv6 from this connection). DuckDNS + router port-forwarding, the original idea for Plan D, is not possible here — it's not a matter of router config, Starlink's own router rejects the concept entirely on these plans.

**Why Cloudflare Tunnel instead**: `cloudflared` (running as a container in this repo's `docker-compose.yml`) makes an *outbound* connection from this machine to Cloudflare's edge and keeps it open — Cloudflare routes public HTTPS traffic for your domain down that same connection. Nothing needs to be reachable *from* the internet *to* this machine, so CGNAT is irrelevant, there's no router config, no port-forwarding, no dynamic-DNS updater needed (IP changes don't matter — the tunnel just reconnects), and no inbound firewall rules. It replaces both Caddy and DuckDNS from the original Plan D design. Caddy is still in this repo (behind a Docker Compose `profiles: ["vps"]` flag) for Plan C if a real VPS is ever used instead.

**Key difference from Plan C**: same as before — no SSH exposed on the home network, so no push-to-deploy from GitHub Actions. `.github/workflows/build.yml` builds and pushes the image to GHCR, and a Scheduled Task on this machine (`scripts/poll-deploy.ps1`) periodically pulls a new one.

### 1. Install Docker Desktop
Docker Desktop on Windows needs WSL2 as its backend.
```powershell
wsl --install   # run as Administrator; reboot when it tells you to
```
After rebooting, install Docker Desktop from docker.com/products/docker-desktop, using the WSL2 backend (default). Once installed, open **Settings → General** and confirm **"Start Docker Desktop when you log in"** is on — combined with this repo's `restart: unless-stopped` policy, that's what makes the app come back up automatically after a reboot.

### 2. Buy a cheap domain
Cloudflare Tunnel needs a real domain in a Cloudflare account (a *stable* hostname isn't possible without one — Cloudflare's free "quick tunnels" generate a new random URL every restart). Buy one from a normal registrar — **Porkbun** (porkbun.com) is a good pick, plain `.com` names run about $10-11/year with no bait-and-switch renewal pricing. Cloudflare Registrar itself is cheaper but only handles transfers of domains you already own elsewhere, not new registrations, so it's not the first stop.

### 3. Add the domain to Cloudflare and delegate DNS
1. Sign up for a free account at cloudflare.com.
2. **Add a site** → enter your new domain → choose the **Free** plan.
3. Cloudflare scans for existing DNS records (there won't be any yet) and gives you **two nameservers** (e.g. `xxx.ns.cloudflare.com`).
4. Log into Porkbun, find the domain's **NS (nameserver)** settings, and replace the default ones with the two Cloudflare gave you.
5. Wait for Cloudflare to detect the change (usually well under an hour) — it'll email you and the dashboard will show the site as **Active**.

### 4. Create the Tunnel
Cloudflare's dashboard layout for this changes periodically — as of this writing, Tunnels moved out of the separate Zero Trust dashboard into the regular per-site one:
1. In the Cloudflare dashboard, select your domain, then in the left sidebar find **Networking → Tunnels** (not Zero Trust — that menu was retired for this).
2. **Create a tunnel** → choose **Cloudflared** as the connector type → name it (e.g. `carshowjudging`) → **Save tunnel**.
3. On the next screen ("Install and run a connector"), ignore the OS-specific install commands — copy just the **token** (the long string after `--token` in the example command). That goes in `.env` as `CLOUDFLARE_TUNNEL_TOKEN`.
4. Go to the **Routes** tab (this replaced what used to be called "Public Hostname") → add a route: pick your domain, leave the subdomain blank (or use `www`), and for **Service URL** enter the full URL including scheme in one field: `http://carshowjudging:8080` (the app container's name and port on the Docker network — Cloudflare terminates HTTPS for you, plain HTTP to the origin is correct here). Save.

**Known gotcha**: when Cloudflare first scans a newly-added domain, it often imports the registrar's existing DNS records (parking-page A records, `www` CNAME, MX/TXT for email forwarding) into the new zone automatically. If those still exist after adding the Tunnel route, the app will be unreachable and Cloudflare returns an **error 525** (SSL handshake failed) — because the proxied hostname is still pointing at the registrar's parking server, not the Tunnel. Fix: go to **DNS → Records**, delete any leftover **A** record(s) for the bare domain that point somewhere other than the Tunnel, then reopen and re-save the Tunnel's route so Cloudflare creates the correct record. The `www` CNAME / MX / TXT records (if they're your registrar's email-forwarding setup) are unrelated and safe to leave.

**Also expect a real propagation wait after switching nameservers.** Google's (`8.8.8.8`) and Cloudflare's (`1.1.1.1`) public resolvers tend to pick up the change within minutes, but many ISP and mobile-carrier DNS resolvers cache nameserver delegation for up to 24-48 hours before re-checking. Verify the deployment against a fast public resolver first (or `curl --resolve` straight to Cloudflare's IP) rather than assuming it's broken just because your own phone/laptop still shows the registrar's old parking page.

### 5. Configure `.env`
Copy `.env.example` to `.env` in the repo root (already gitignored). Set `CLOUDFLARE_TUNNEL_TOKEN` (from step 4) and `SEED__ADMINPASSWORD` at minimum. Leave `COMPOSE_PROFILES=cloudflare` as-is.

### 6. Make the GHCR image pullable
`docker compose pull` on this machine runs without any registry login, so the GHCR package needs to be public. After the first push from `build.yml`, go to your GitHub profile → **Packages** → the `carshowwebapp` package → **Package settings** → **Change visibility** → **Public**.

### 7. First run, then automate it
```powershell
docker compose up -d
```
This starts the app and `cloudflared` (the `cloudflare` profile from `.env`) — no Caddy, no exposed ports. Confirm it's reachable at `https://yourdomain.com`. Once that works, register the recurring tasks so nothing needs to be run by hand again:
```powershell
.\scripts\register-scheduled-tasks.ps1
```
This sets up two Windows Scheduled Tasks: `CarShowJudging-PollDeploy` (every 15 min, pulls new images from GHCR and restarts if changed) and `CarShowJudging-Backup` (daily, tars the DB + uploads volumes into `.\backups`, pruned after 14 days).

### Verifying it worked
- `docker compose up -d` succeeds and `https://yourdomain.com` loads with a valid HTTPS padlock (Cloudflare-issued, not Let's Encrypt, but just as real).
- Restart the containers (`docker compose restart`) and confirm the SQLite data and an uploaded vehicle photo both survive.
- Push a trivial commit to `main`, watch the `build.yml` Action go green, then manually run `.\scripts\poll-deploy.ps1` and confirm it detects and pulls the new image (or just wait 15 minutes for the Scheduled Task).
- Manually run `.\scripts\backup.ps1` and confirm a dated `.tar.gz` appears in `.\backups`.
- From a phone on cellular data (not your home WiFi), visit the domain to confirm it's actually reachable from outside your network.
- Unplug/reconnect the internet connection (or just wait for the IP to naturally change) and confirm the site is still reachable a few minutes later without touching any config — this is the whole point of using a tunnel instead of port-forwarding.

---

## Note for whoever picks this up next (human or Claude)

Whichever plan actually gets executed, **come back and update the "Current state" section above afterward** — record what was actually deployed, where, and any deviations from these steps (a different VPS provider, a different port, whatever). Keep this README accurate to reality, not just to the plan as written — a stale deployment doc is worse than no doc.
