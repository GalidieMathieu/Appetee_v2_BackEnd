using Appetee.DatabaseMigrator;

try
{
    var connectionString = MigrationSettings.LoadConnectionString(AppContext.BaseDirectory);

    using var cancellation = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cancellation.Cancel();
    };

    var runner = new MigrationRunner(connectionString, Console.Out);
    var command = args.FirstOrDefault()?.ToLowerInvariant() ?? "migrate";
    switch (command)
    {
        case "migrate":
            await runner.MigrateAsync(cancellation.Token);
            break;
        case "baseline":
            var throughIndex = Array.FindIndex(args, value => value == "--through");
            if (throughIndex < 0
                || throughIndex + 1 >= args.Length
                || !int.TryParse(args[throughIndex + 1], out var throughSequence))
            {
                throw new InvalidOperationException(
                    "Usage: baseline --through <migration-sequence>.");
            }

            await runner.BaselineExistingAsync(throughSequence, cancellation.Token);
            break;
        case "repair-account-closure":
            await runner.RepairAccountClosureAsync(cancellation.Token);
            break;
        case "permission":
            // Explicit trusted operator command. Never grants during migrate/bootstrap.
            if (args.Length is not (5 or 6) || args[1] is not ("grant" or "revoke") || !int.TryParse(args[2],out var targetId))
                throw new InvalidOperationException("Usage: permission <grant|revoke> <target-user-id> <Catalog.ReadAdmin|Catalog.Write> <operator-reference> [actor-user-id]");
            int? actorId=null;
            if (args.Length==6)
            {
                if (!int.TryParse(args[5],out var parsedActor)) throw new InvalidOperationException("Actor ID must be a positive integer.");
                actorId=parsedActor;
            }
            var changed=await new PermissionOperator(connectionString).ChangeAsync(targetId,args[3],args[1]=="grant",args[4],actorId,cancellation.Token);
            Console.WriteLine(changed ? "Permission changed and audited; existing account tickets are revoked." : "Permission already has the requested state; no changes.");
            break;
        default:
            throw new InvalidOperationException(
                "Supported commands are migrate, baseline, repair-account-closure, and permission.");
    }

    return 0;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Migration operation cancelled.");
    return 2;
}
catch (Exception exception)
{
    MigrationFailureDiagnostics.Write(exception, Console.Error);
    return 1;
}
