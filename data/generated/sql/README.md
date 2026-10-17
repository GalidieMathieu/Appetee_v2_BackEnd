# Generated SQL

Run `npm run build` from `data/tools` to regenerate the four numbered SQL files. Do not edit them manually.
Run `npm run check:generated` to validate the canonical dataset and check SQL, indexes and reports without writing files or contacting MySQL/Azure.

Execute them only through the guarded local development reset, in filename order:
schema, reference data, ingredients, then recipes. `01-schema.sql` drops application
tables; the scripts target `USE appetee;`. They are not production migrations and
must not be run against Azure `appeteedatabase` or an existing application database.

## Schema compatibility

`01-schema.sql` is an unmodified copy of repository `scriptDatabase.sql` plus a
generated-file header. The current backend schema and dataset both support the
nine canonical recipe badge values. The generator refuses a source schema
missing that constraint; it does not secretly expand or relax it.

All current application tables, including manual meal items, permissions and
account-closure cleanup, are present in that baseline. Partial existing databases
need the forward repair in [the migration runbook](../../../migrations/README.md),
not a replay of this destructive bootstrap.

The corpus remains 121 ingredients and 1,699 recipes. SQL stores nutrition,
costs, relationships and deterministic Blob names; Blob Storage holds only image
bytes. Existing provenance, historical price snapshots and private/test-only
media approvals are unchanged. Regeneration is not a price or licensing refresh.
