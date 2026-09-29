# Initial verification

- Built the application and test project with .NET SDK 10.0.401 / runtime 10.0.12.
- A Release build completed with zero warnings and zero errors before the final middleware-order correction.
- All 25 automated tests passed. They cover ownership and cross-user access, the database's composite ownership constraint, category archive/restore, activity mutations, input validation, recurrence, DST handling, transactional rollback, quotas, email uniqueness and operation/circuit limits.
- Live HTTP checks confirmed anonymous redirects, antiforgery rejection, registration, blocked unconfirmed login, email confirmation, verified login and rendering of the authenticated planner pages.
- Those HTTP checks identified a missing-URL error caused by the placement of status-code middleware. The source was corrected to place it before authentication/authorization/antiforgery. The final correction has not yet had a successful repeat HTTP run.
- Full interactive browser and visual checks were not completed: Chromium could not start because this execution environment restricted its local Unix sockets. `tests/browser/smoke.cjs` is supplied for a compatible local environment, but is not claimed as a passed test.
- Live SMTP, live Turnstile, production hosting and GitHub Actions were not exercised. No remote repository or live website was created.

The user elected to build locally. Run the README build/test commands, then check registration, calendar interactions and mobile layouts before deployment. No runtime databases, credentials or confirmation messages are part of the delivery.
