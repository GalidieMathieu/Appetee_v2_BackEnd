# Generated SQL

Run `npm run build` from `data/tools` to regenerate these files. Do not edit them manually.

Execute them in filename order for a local development reset: schema, reference data, ingredients, then recipes. Database execution itself is intentionally outside this dataset tooling task.

## Schema compatibility

`01-schema.sql` is a generated, dataset-owned structure-only copy of the current
backend schema. The approved dataset contract defines nine recipe badge values,
while the current application schema and API support only three legacy slugs.
The copy expands the database check constraint to the nine contract values so
the generated seed corpus can load without weakening validation.

Application/API support for the six additional values is deliberately outside
this dataset task and remains a documented integration follow-up.
