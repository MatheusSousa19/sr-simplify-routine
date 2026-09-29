# Security design and deployment boundary

## Implemented in this application

- ASP.NET Core Identity hashing, confirmed-email sign-in, unique normalized-email database index, 12–128 character registration passphrases, and framework reset tokens.
- Five failed password attempts trigger a five-minute temporary lockout. Login, recovery and registration also have source/target/global request budgets. Registration and reset responses avoid explicit account-existence disclosures; this is not a constant-time implementation.
- Secure, HttpOnly production cookies with SameSite=Lax and an absolute eight-hour lifetime. Development permits HTTP on localhost. Identity revalidates circuit security stamps; changes are not guaranteed to invalidate every open circuit instantly.
- Framework authenticator MFA, recovery codes and passkeys are available in account settings. No administrator accounts or roles are automatically provisioned.
- Every planner service operation uses the server's authenticated user ID. Reads, edits, status changes, category links and deletions are ownership-checked. `[Authorize]` protects page entry; UI visibility is not relied upon for data protection.
- Category ownership is also enforced with a composite database foreign key.
- Server-side data annotations and domain validation, parameterized EF queries, default Razor encoding and framework antiforgery validation for HTTP forms.
- Bounded HTTP bodies (32 KiB), form fields, strings, date ranges and calendar queries. Titles cannot become raw markup and colour values cannot inject arbitrary CSS.
- Production Turnstile validation is server-side and checks success, hostname and action. It fails closed on network errors. Only Development can run with bot verification disabled. A honeypot provides an additional low-cost filter.
- Account email links are rebased onto the configured public origin. Explicit allowed hosts protect origin handling. No proxy headers are trusted by default.
- Development email messages and Data Protection keys are outside `wwwroot`, excluded from Git and excluded from the delivery archive. The registration confirmation page never generates or exposes an account-confirmation token.
- Unverified accounts older than seven days are removed hourly. Account deletion cascades to planner data.
- Security headers set nosniff, frame blocking, referrer policy, restricted browser features and a baseline CSP (frame ancestors, object sources and base URI). The CSP is not a complete script nonce policy.
- Known NuGet vulnerability warnings fail dependency restore. GitHub Actions has read-only repository permissions.

## Resource budgets

| Resource | Default budget |
| --- | --- |
| All HTTP traffic | 6,000/minute globally; 400/minute per source IP |
| Account POST requests | 300/minute globally; 20/minute per IP |
| Account POST per route and target email | 8/15 minutes |
| Registration attempts | 100/hour globally; 10/hour per IP |
| Interactive planner calls | 3,000/minute globally |
| Planner mutations per account | 40/minute |
| Planner reads per account | 180/minute |
| Categories | 30/account, including archived categories |
| Activities | 2,000/account |
| Repetition | At most 12 occurrences per creation |
| Query window | At most 93 days |
| Active/retained circuits | 200 total; disconnected retention also limited to 100 and one minute |
| Incoming SignalR message | 32 KiB |

HTTP and interactive service limits are both necessary: an established Blazor connection is not a new HTTP request for every click. Fixed windows permit boundary bursts. The in-memory counter cache is bounded and counters are lost at restart; these are application-level controls, not durable anti-abuse accounting. Tune budgets with legitimate traffic, including shared school and household IP addresses. IP addresses are not a reliable person identity.

## Before production

- Configure SMTP and Turnstile with real keys and test failure cases. Local verification does not test third-party delivery or challenges.
- Run with Production environment, HTTPS, strict allowed hosts, secrets supplied by the host and development diagnostics disabled.
- Add a WAF/DDoS service and per-source connection controls upstream. A circuit cap bounds resources but an attacker can still occupy available slots.
- Configure forwarded headers only for known proxies. A reverse proxy's address must not accidentally become the identity for every rate limit. Do not accept arbitrary client-supplied forwarding headers.
- Persist the database and Data Protection keys on protected storage. Add encryption-at-rest appropriate to the host, especially protection of persisted key files. Test backup restoration.
- Restrict filesystem access to the application account and administrators. Do not publish `.dev-mail`, `.keys`, database files, logs or backups.
- Add monitoring/alerts for sign-up spikes, lockouts, throttling, mail failures, circuit exhaustion and database growth. Current logs avoid recording passwords or token contents.
- Add distributed limits before horizontal scaling, and move away from SQLite when concurrency requires it.
- Apply migration changes through the deployment process; do not let public startup mutate a production schema automatically.
- If an administrator area is added, enforce MFA and least privilege in its server policies. This release has no admin backdoor and no seeded administrator credentials.

No implementation can promise that one person has only one account or that an application cannot be attacked. Unique emails, bot checks, quotas and throttling reduce abuse; availability against distributed attacks also depends on deployment infrastructure.
