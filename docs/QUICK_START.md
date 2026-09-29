# Build SR Simplify Routine yourself

## 1. Open the project

Extract the entire ZIP. Open **SrSimplifyRoutine.sln** in a Visual Studio version that supports **.NET 10**. Choose **SrSimplifyRoutine.Web** as the startup project.

You do not need to create all the files manually: the complete source and folder structure are already included.

## 2. Restore and run

From the extracted project folder, use a terminal:

```sh
dotnet restore
dotnet dev-certs https --trust
dotnet run --project src/SrSimplifyRoutine.Web --launch-profile https
```

Open **https://localhost:7153**. The application creates its local SQLite database automatically.

## 3. Register your first account

There is no default password. Register your own account. Open the newest HTML email in:

`src/SrSimplifyRoutine.Web/.dev-mail/`

Click **Confirm email**, then sign in. This local email folder replaces SMTP only during development.

## 4. Find the code you want to change

| What you want to change | Where to look |
| --- | --- |
| App startup, authentication and security configuration | `src/SrSimplifyRoutine.Web/Program.cs` |
| Dashboard | `Components/Pages/Dashboard.razor` |
| Calendar | `Components/Pages/Calendar.razor` |
| Study/workout/activity lists | `Components/Pages/Planner.razor` |
| Add/edit an activity | `Components/Pages/ActivityEditor.razor` |
| Custom categories and goals | `Components/Pages/Categories.razor` |
| Progress reports | `Components/Pages/Progress.razor` |
| Registration and login | `Components/Account/Pages/` |
| Database entities | `Data/ApplicationUser.cs`, `Data/PlannerModels.cs` |
| EF database configuration | `Data/ApplicationDbContext.cs` |
| Ownership checks, CRUD and quotas | `Services/PlannerService.cs` |
| Colours, spacing and responsive layouts | `wwwroot/app.css` |

Paths in the lower rows are relative to `src/SrSimplifyRoutine.Web/`.

## 5. Track your own changes

Git is already initialised and the source has committed history:

```sh
git status
git log --oneline
git switch -c feature/my-first-change
```

When ready, connect an empty GitHub repository using the commands in the main README. A remote repository has not been created for you.

The main README and `SECURITY.md` explain production configuration. Development mode should stay on your own machine.
