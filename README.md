# Appetee Backend (Dapper + MySQL) - Starter Template

This is a minimal, clean starter backend using:
- ASP.NET Core (controllers)
- Dapper (explicit SQL)
- MySqlConnector (MySQL driver)

It includes **Users** endpoints:
- `GET /api/users/{id}`
- `PUT /api/users/{id}` (updates `display_name` and/or `image_url`)
- `DELETE /api/users/{id}`

## Prereqs
- Install **.NET 10 SDK** (LTS).
- A running MySQL instance with the `spot` database and the schema you pasted.

## Configure
Edit `src/Appetee.Api/appsettings.json`:
- Set `ConnectionStrings:AppeteeDb` to your MySQL connection.

## Run
From the repository root:
```bash
cd src/Appetee.Api
dotnet restore
dotnet run
```

The API will print its listening URLs in the console.

## Testing with Postman
No special changes are required for Postman.

## When you add Angular later (CORS)
In `src/Appetee.Api/Program.cs` there is a commented CORS policy.
- Uncomment it and set your Angular origin (e.g. `http://localhost:4200`).

