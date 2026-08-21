# Database Tools

Run from `data/tools`:

```powershell
npm run db:reset:local
npm run db:verify:local
```

`db:reset:local` validates and regenerates the canonical dataset before it drops and recreates the database. It accepts only `localhost`, `127.0.0.1`, or `::1` with the database name exactly `appetee`; there is no bypass.

`db:verify:local` is read-only. Both commands use `ConnectionStrings__AppeteeDb`, then `appsettings.Development.json`, then `appsettings.json`. Neither command prints the connection string or credentials.

Do not run two local reset commands concurrently.
