# Development guide

## How a planner action works

1. A Razor component binds an `ActivityInput`, `CategoryInput` or `ProfileInput`.
2. The component calls `PlannerService`.
3. `CurrentUser` obtains the signed-in user ID from the server's authentication state. There is no user-ID form field to trust.
4. The service checks the account is still present and email-confirmed, applies operation limits, validates input and checks ownership.
5. A fresh EF Core context performs the operation. Quota checks and inserts run in the same SQLite transaction.
6. The component reloads the relevant date range.

Interactive components execute on the server through a Blazor circuit. Identity account pages use the framework's static server rendering so authentication cookies are set through normal HTTP requests. Do not convert the account pages to interactive components.

## Categories and activities

All activities share one table. A category has a kind: General, Study or Workout. A user-created Study category appears in the Studies section automatically. Colour values are validated as six-digit hex colours before reaching CSS.

A category's ID and user ID form an alternate key. The activity's composite foreign key references both, so the database also rejects another user's category. Categories belong to Identity users; deleting a user cascades to categories and activities. Archiving is reversible and does not delete history.

Times are saved as UTC `DateTime` values and displayed in the user's time zone. Repeating sessions are calculated from successive local dates before conversion to UTC. Invalid or ambiguous times around daylight-saving transitions are rejected with an explanatory message. The entire series is rolled back if any occurrence is invalid. Dates are restricted to 2000–2100, durations to 5–1440 minutes, and repetitions to 1–12 occurrences.

A `SeriesId` groups materialised occurrences. This first version intentionally creates a finite set of occurrences; no endless background recurrence generator runs. Editing or deleting a single occurrence leaves the others unchanged.

## Database changes

Keep migrations in Git. Never commit a live SQLite database.

```sh
dotnet tool restore
# Set ASPNETCORE_ENVIRONMENT=Development for the design-time host.
dotnet ef migrations add DescribeTheChange --project src/SrSimplifyRoutine.Web
dotnet ef database update --project src/SrSimplifyRoutine.Web
```

On PowerShell use `$env:ASPNETCORE_ENVIRONMENT="Development"`. On bash use `export ASPNETCORE_ENVIRONMENT=Development`. Back up production data before migrating. Development applies committed migrations at startup; production requires the explicit migration command in README.

## Good next features

- Reminder jobs with per-user/global email budgets and cancellation.
- Study subject and workout exercise-detail tables linked to activities.
- ICS export/import with size and recurrence-expansion limits.
- A carefully scoped support/admin area with required MFA and audit trails.
- Shared rate-limiting storage and a server database if usage grows.

Add each feature on a branch. For every data operation, test access by another user and validate limits at the service boundary. Keep browser-only validation as a convenience, never the authority.

## Browser testing

The normal browser workflow uses registration, local confirmation email, sign-in, category creation, activity creation/editing, completion, archive/restore and sign-out. Also check an anonymous protected URL redirects to login and an account POST without an antiforgery token fails.

`docs/VERIFICATION.md` records the checks performed for the initial delivery. Live SMTP, live Turnstile and production infrastructure require configured external services and separate deployment verification.
