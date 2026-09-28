# Development Documentation

Use this area for local setup, development workflow, testing guidance, release practices, and current engineering notes. Keep commands aligned with the repository and avoid documenting unverified steps.

## Local PostgreSQL Connection

Set `ConnectionStrings__DefaultConnection` in the current shell to provide the local PostgreSQL connection string. ASP.NET Core loads environment variables after JSON configuration, so this value overrides any configured default without storing credentials in the repository.

PowerShell example for the current session:

```powershell
$env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=5432;Database=smartshoppos;Username=smartshoppos_app;Password=YOUR_LOCAL_PASSWORD"
```

Replace `YOUR_LOCAL_PASSWORD` locally; do not commit a real password or place it in a checked-in configuration file.
