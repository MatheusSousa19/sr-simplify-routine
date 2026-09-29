# SR Simplify Routine

A private routine planner built with **C#, .NET 10, Blazor Interactive Server, ASP.NET Core Identity, Entity Framework Core and SQLite**.

Plan studies, workouts and personal activities in one calendar. Create your own categories, repeat routines and compare planned time with completed time.

Start with **[docs/QUICK_START.md](docs/QUICK_START.md)** for the shortest setup guide. See **[docs/VERIFICATION.md](docs/VERIFICATION.md)** for tested behaviour and remaining checks.

## Run locally

You need the **.NET 10 SDK**. Use a Visual Studio version that supports .NET 10, or any editor with the .NET CLI.

From the repository root:

```sh
dotnet restore
dotnet dev-certs https --trust
dotnet run --project src/SrSimplifyRoutine.Web --launch-profile https
```

Open **https://localhost:7153**. Alternatively, for local development only:

```sh
dotnet run --project src/SrSimplifyRoutine.Web --launch-profile http
```

Open **http://localhost:5127**. Do not expose the Development environment to the internet.

The SQLite database is created and migrated automatically in Development. No separate database server or seeded password is required. The database lives at `src/SrSimplifyRoutine.Web/Data/sr-routine.db` and is excluded from Git.

### Your first account

1. Select **Create your account**, enter your name and email, and choose a passphrase of at least 12 characters.
2. During development, with SMTP unset, confirmation emails are saved as HTML files in **`src/SrSimplifyRoutine.Web/.dev-mail/`**. Open the newest file locally and follow **Confirm email**. These files are never served by the website.
3. Sign in. Your private Studies, Workouts and Personal categories are created automatically.
4. Set your time zone under **Preferences**. The initial zone is Europe/Dublin.
5. Add a category if you need one, then choose **New activity**.
6. Complete activities using the checkbox beside each activity. Review your results under **Weekly progress**.

Passwords, confirmation links, cookies, databases and local email files are not included in the repository or download. There are no default credentials.

## Features

- Registration, confirmed-email login, logout, temporary lockout and password recovery.
- Framework account-management pages with authenticator MFA, recovery codes, passkeys and account deletion. Passkeys require a supported browser and secure origin.
- Overview with today's agenda, upcoming activities and weekly targets.
- Month, week and day calendars; category filtering and links to activity editing.
- Searchable activity list with date, category and status filters.
- Studies and Workouts sections powered by category types.
- Custom categories, colour selection, weekly goals, archive and restore.
- Create/edit/delete activities; notes for subject details, assignments, exercise sets or other plans.
- Daily/weekly repetition, bounded to 12 materialised occurrences; edit and complete each occurrence independently.
- Optional overlap check for the first occurrence being edited/created; overlapping activities are permitted.
- Weekly comparison of planned and completed time. Reports count activities by their start date in the user's time zone.
- Responsive layouts, labelled forms, keyboard focus states and reduced-motion support.

## Solution layout

| Path | Purpose |
| --- | --- |
| `SrSimplifyRoutine.sln` | Open this in Visual Studio. |
| `src/SrSimplifyRoutine.Web/Components/Pages` | Planner screens and forms. |
| `src/SrSimplifyRoutine.Web/Components/Account` | Identity authentication and account management. |
| `src/SrSimplifyRoutine.Web/Services/PlannerService.cs` | Ownership checks, validation, quotas and planner operations. |
| `src/SrSimplifyRoutine.Web/Data` | EF Core models and committed migrations. |
| `tests/SrSimplifyRoutine.Tests` | Automated security and behaviour tests. |
| `.github/workflows/ci.yml` | Restore, audit, build, test and publish verification on GitHub. |
| `docs/SECURITY.md` | Implemented controls, boundaries and production requirements. |
| `docs/DEVELOPMENT.md` | Architecture, future changes and database workflow. |

SQLite makes this version easy to run on Windows, macOS and Linux. It is intended for a small, single-instance deployment. Moving to SQL Server or PostgreSQL requires changing the EF provider and creating compatible migrations; changing only the connection string is insufficient.

## Tests and build

```sh
dotnet restore --locked-mode
dotnet build --configuration Release --no-restore -m:1
dotnet test --configuration Release --no-build -m:1
dotnet publish src/SrSimplifyRoutine.Web --configuration Release --no-build --output artifacts/app
```

Dependencies are pinned by project versions and `packages.lock.json`. Restore fails on known NuGet vulnerability advisories. CI runs when this repository is pushed to GitHub; a local test run does not constitute a completed GitHub Actions run.

## Git and future updates

This project is a Git repository on the **main** branch with committed history. The downloadable ZIP includes `.git`; extract the entire folder to keep that history.

```sh
git status
git log --oneline
git switch -c feature/reminders
# Make and test your changes.
git add .
git commit -m "feat: add reminders"
```

No remote is configured until a remote repository has actually been created. To connect an **empty private GitHub repository** you own:

```sh
git remote add origin https://github.com/YOUR-USERNAME/sr-simplify-routine.git
git push -u origin main
```

Do not initialise that remote with another README or unrelated history. Use your normal GitHub authentication; never put a token in a remote URL or committed file.

## Before a public deployment

The application deliberately refuses to start in Production without SMTP, enabled Turnstile bot verification, an HTTPS public origin and explicit allowed hosts. No production credentials are supplied.

Configure these using your hosting platform's secret store, not committed files:

| Setting | Example / requirement |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `AllowedHosts` | Your actual hostname, without wildcards. |
| `Application:PublicOrigin` | `https://planner.example.com` — actual public origin for account email links. |
| `Smtp:Host`, `Smtp:Port` | Your SMTP provider; STARTTLS port 587 by default. |
| `Smtp:From` | A verified sender address. |
| `Smtp:Username`, `Smtp:Password` | Provider credentials stored as secrets. |
| `BotProtection:Enabled` | `true` |
| `BotProtection:SiteKey`, `BotProtection:SecretKey` | Cloudflare Turnstile keys for the deployed hostname. |
| `ConnectionStrings:DefaultConnection` | An absolute path to a durable, access-controlled SQLite file. |
| `Application:KeysPath` | A persistent, access-controlled directory for Data Protection keys. |

For environment variables, replace colons with double underscores, for example `Smtp__Password`. Create the database's parent directory before migration.

Apply migrations as a separate deployment step, from the project/publish directory so configuration resolves correctly:

```sh
# Source checkout (with production configuration supplied):
dotnet run --no-launch-profile --project src/SrSimplifyRoutine.Web -- --migrate
# Published application, run from artifacts/app:
dotnet SrSimplifyRoutine.Web.dll --migrate
```

Serve HTTPS directly with a configured Kestrel certificate, or configure a trusted reverse proxy and explicit forwarded-header handling before using TLS termination. This project does not blindly trust `X-Forwarded-For` or `X-Forwarded-Proto`.

Configure upstream DDoS protection, connection limits, monitoring, durable backups and protection of stored Data Protection keys. Rate counters are process-local and need a shared gateway/store if you scale to multiple processes. Read `docs/SECURITY.md` before deployment. This repository is not a claim of immunity from intrusion or denial of service.
