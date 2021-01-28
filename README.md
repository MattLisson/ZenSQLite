
# SQLite-net Incompatible Fork Edition

## New Features

* Explicit configuration via SQLiteConfig builder.
* Version rewind with SQLiteConfig.AsVersion(x).
* Easily configured migrations with: SQLiteConfig.AddMigration(versionFrom, versionTo, (SQLiteConnection conn) => {});
* Foreign Key support (Via the ForeignKeyAttribute).
* Custom column type conversions with SQLiteConfig.AddType. (I use it to include NodaTime types in my Table definitions)
* ManyToManyAttribute makes it a little easier to use ForeignKeys.


## Broken things

* Removed default support for DateTimes, StringBuilder and other object types (except for Guid).
  * Easily readded with SQLiteConfig.AddType<DateTime>(/* conversion functions */).
* Split classes into separate files for my sanity. This combined with the divergent design means no updates from upstream.
