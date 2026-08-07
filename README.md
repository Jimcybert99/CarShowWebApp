# CarShowJudging

A self-hosted web app for running a car show: participants register vehicles (with a photo and optional class assignments), judges score each entry against five criteria, and admins manage entries, classes, users, and the live scoring leaderboard.

Built with **Blazor Server on .NET 10**, **SQLite** for data, and local-disk storage for vehicle photos. Three roles: **Admin**, **Judge**, **User**. See [CLAUDE.md](CLAUDE.md) for the full architecture reference (data model, service layer, page structure, scoring model) — this README is about *running and deploying* the app, not its internals.

## Current state (as of this session)

This app was dev-only until now. This session did three things to make it deployable:

1. **Removed the SQL Server / Azure Blob Storage code paths entirely.** The app now always uses SQLite and always stores photos on local disk — no more environment-based branching, no more Azure SDK dependency. This was a deliberate simplification for this app's actual scale (a single small event, low concurrency), not a limitation to work around.
2. **Fixed a real security bug**: `Program.cs` used to seed an `admin`/`password123` account into whichever database it started against, unconditionally, in every environment — including a real production deploy. It now reads the admin password from configuration (`Seed:AdminPassword` / `SEED__ADMINPASSWORD` env var) and **refuses to start outside Development** if that hasn't been set to something other than the default. See "Common prerequisites" below — this is the one step you cannot skip.
3. **Mobile-responsiveness pass.** The Bootstrap foundation (viewport meta tag, collapsible nav, `table-responsive` wrappers) was already solid. What was fixed: the widest data tables (`/admin/scores` especially — 11 columns) now hide lower-priority columns (Owner, Classes) below the `md` breakpoint instead of forcing horizontal scroll on a phone; a couple of non-responsive Bootstrap column classes (`col-5` instead of `col-12 col-sm-5`) were corrected; modals got `modal-dialog-centered`; a `theme-color` meta tag was added.

### What's explicitly NOT done yet

- **No real-device testing.** All mobile fixes were verified with browser dev-tools viewport emulation, not an actual phone — the user deploying this can't test on a real device until it's actually live somewhere. Treat the mobile pass as "should work" not "confirmed."
- **No CI/CD, no Docker/containerization.** Deployment below is manual, step-by-step. This was a deliberate choice (see Part 1 context) — not an oversight.
- **No HTTPS/TLS wiring in code.** `Program.cs` calls `UseHttpsRedirection()`/`UseHsts()`, but there's no certificate bound anywhere in the repo. Both plans below cover getting a real cert at the infrastructure level.
- **Password-reset email is a real gotcha, not hypothetical.** `appsettings.json` has `Smtp:Host` set to `smtp.gmail.com` with blank `User`/`Password`. That means it is **not** a silent no-op in production — `EmailService` only falls back to log-only mode when `Smtp:Host` itself is empty, which it isn't. Deploy without setting real `Smtp:User`/`Smtp:Password` and the *first* password-reset request will throw an SMTP auth error. Either set real SMTP credentials (env vars, see below) or accept that "Forgot password" is broken and users needing a reset must be handled manually by an admin.
- **CDN dependency for Bootstrap/Bootstrap Icons.** Both are loaded from jsdelivr's CDN, not vendored locally. If your hosting environment has flaky/no outbound internet, or the CDN has an outage, the app will render unstyled. Not fixed this pass — flagged as a possible follow-up if it becomes a problem.

## Common prerequisites (both plans)

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

## Note for whoever picks this up next (human or Claude)

Whichever plan actually gets executed, **come back and update the "Current state" section above afterward** — record what was actually deployed, where, and any deviations from these steps (a different VPS provider, a different port, whatever). Keep this README accurate to reality, not just to the plan as written — a stale deployment doc is worse than no doc.
