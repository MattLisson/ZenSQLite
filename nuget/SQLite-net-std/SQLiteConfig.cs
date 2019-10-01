using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace SQLite
{
	/// <summary>
	/// Configuration options for the SQLite-net connection.
	/// </summary>
	public class SQLiteConfig
	{
		private readonly Dictionary<string, TableMapping> mappings = new Dictionary<string, TableMapping>();

		/// <summary>
		/// Get's all the tables this configuration understands.
		/// </summary>
		public List<TableMapping> Tables {
			get {
				lock(mappings) {
					return new List<TableMapping>(mappings.Values);
				}
			}
		}

		private readonly Dictionary<Type, ReadColumnDelegate> columnReaders =
			new Dictionary<Type, ReadColumnDelegate>();

		private readonly Dictionary<Type, WriteColumnDelegate> columnWriters =
			new Dictionary<Type, WriteColumnDelegate>();

		private readonly Dictionary<Type, string> sqlTypeOverrides =
			new Dictionary<Type, string>();

        /// <summary>
        /// The User Version of the database schema. Update this when the schema changes to execute custom upgrades.
        /// </summary>
        public int UserVersion { get; private set; } = 1;

		/// <summary>
		/// A migration action that changes database UserVersions.
		/// </summary>
		public class Migration
		{
			/// <summary>
			/// The version of the database expected at the start of the upgrade action.
			/// </summary>
			public int StartVersion { get; }

			/// <summary>
			/// The version of the database once the upgrade action has completed.
			/// </summary>
            public int EndVersion { get; }

			/// <summary>
			/// The action that will upgrade the database from StartVersion to EndVersion.
			/// </summary>
            public Action<SQLiteConnection> UpgradeAction { get; set; }

			/// <summary>
			/// Contructs a migration action with its version metadata.
			/// </summary>
            public Migration(int startVersion, int endVersion, Action<SQLiteConnection> upgradeAction)
			{
				this.StartVersion = startVersion;
				this.EndVersion = endVersion;
				this.UpgradeAction = upgradeAction;
			}
		}

		private readonly Dictionary<int, List<Migration>> migrations
			= new Dictionary<int, List<Migration>>();

		/// <summary>
		/// Flags to be used when creating tables.
		/// </summary>
		public CreateFlags CreateFlags { get; }

		/// <summary>
		/// The file path to create or open for this database.
		/// </summary>
		public string DatabaseFilePath { get; }

		/// <summary>
		/// Creates a new SQLiteConfig.
		/// </summary>
		/// <param name="createFlags">Flags to be used when creating tables.</param>
		/// <param name="databaseFilePath">The file path to create or open the database at.</param>
		public SQLiteConfig(CreateFlags createFlags, string databaseFilePath)
		{
			CreateFlags = createFlags;
			DatabaseFilePath = databaseFilePath;

			// Set up default types;
			columnReaders[typeof(string)] =
				NullHandlingReader(SQLite3.ColumnString);
			columnReaders[typeof(int)] =
				NullHandlingReader((s, i) => SQLite3.ColumnInt(s, i));
			columnReaders[typeof(double)] =
				NullHandlingReader((s, i) => SQLite3.ColumnDouble(s, i));
			columnReaders[typeof(bool)] =
				NullHandlingReader((s, i) => SQLite3.ColumnInt(s, i) == 1);
			columnReaders[typeof(float)] =
				NullHandlingReader((s, i) => (float)SQLite3.ColumnDouble(s, i));
			columnReaders[typeof(long)] =
				NullHandlingReader((s, i) => SQLite3.ColumnInt64(s, i));
			columnReaders[typeof(uint)] =
				NullHandlingReader((s, i) => (uint)SQLite3.ColumnInt64(s, i));
			columnReaders[typeof(decimal)] =
				NullHandlingReader((s, i) => (decimal)SQLite3.ColumnDouble(s, i));
			columnReaders[typeof(byte)] =
				NullHandlingReader((s, i) => (byte)SQLite3.ColumnInt(s, i));
			columnReaders[typeof(ushort)] =
				NullHandlingReader((s, i) => (ushort)SQLite3.ColumnInt(s, i));
			columnReaders[typeof(short)] =
				NullHandlingReader((s, i) => (short)SQLite3.ColumnInt(s, i));
			columnReaders[typeof(sbyte)] =
				NullHandlingReader((s, i) => (sbyte)SQLite3.ColumnInt(s, i));
			columnReaders[typeof(byte[])] =
				NullHandlingReader((s, i) => SQLite3.ColumnByteArray(s, i).ToArray());
			columnReaders[typeof(Guid)] =
				NullHandlingReader((s, i) => new Guid(SQLite3.ColumnString(s, i)));

			columnWriters[typeof(int)] =
				NullHandlingWriter((s, i, v) => SQLite3.BindInt(s, i, (int)v));

			columnWriters[typeof(string)] =
				NullHandlingWriter((s, i, v) => SQLite3.BindText(s, i, (string)v));

			columnWriters[typeof(byte)] =
				NullHandlingWriter((s, i, v) => SQLite3.BindInt(s, i, Convert.ToInt32(v)));
			columnWriters[typeof(ushort)] = columnWriters[typeof(byte)];
			columnWriters[typeof(sbyte)] = columnWriters[typeof(byte)];
			columnWriters[typeof(short)] = columnWriters[typeof(byte)];

			columnWriters[typeof(bool)] =
				NullHandlingWriter((s, i, v) => SQLite3.BindInt(s, i, (bool)v ? 1 : 0));

			columnWriters[typeof(uint)] =
				NullHandlingWriter((s, i, v) => SQLite3.BindInt64(s, i, Convert.ToInt64(v)));
			columnWriters[typeof(long)] = columnWriters[typeof(uint)];

			columnWriters[typeof(float)] =
				NullHandlingWriter((s, i, v) => SQLite3.BindDouble(s, i, Convert.ToDouble(v)));
			columnWriters[typeof(double)] = columnWriters[typeof(float)];
			columnWriters[typeof(decimal)] = columnWriters[typeof(float)];

			columnWriters[typeof(byte[])] =
				NullHandlingWriter((s, i, v) => SQLite3.BindBlob(s, i, (byte[])v, ((byte[])v).Length));

			columnWriters[typeof(Guid)] =
				NullHandlingWriter((s, i, v) => SQLite3.BindText(s, i, ((Guid)v).ToString()));
		}

		/// <summary>
		/// Adds a table mapping from the given type.
		/// </summary>
		public SQLiteConfig AddTable<T>()
		{
			return AddTable(typeof(T));
		}

		/// <summary>
		/// Adds a table mapping from the given type.
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public SQLiteConfig AddTable(Type type)
		{
			TableMapping map;
			var key = type.FullName;
			lock(mappings) {
				if(mappings.ContainsKey(key)) {
					throw new ArgumentException($"A mapping for {key} already exists!");
				}
				map = new TableMapping(type, this);
				mappings[key] = map;
			}
			return this;
		}

		/// <summary>
		/// Sets the current User Version of the database. Used to decide which migration actions to run.
		/// </summary>
		public SQLiteConfig SetUserVersion(int userVersion)
		{
			UserVersion = userVersion;
			return this;
		}

		/// <summary>
		/// Adds a migration action between the specified user versions.
		/// </summary>
		/// <param name="oldUserVersion">The user version at the beginning of the upgrade.</param>
		/// <param name="newUserVersion">The user version after the upgrade is complete.</param>
		/// <param name="upgradeAction"> Will be called to migrate the database to the schema expected by
		/// newUserVersion.</param>
		public SQLiteConfig AddMigration(int oldUserVersion, int newUserVersion, Action<SQLiteConnection> upgradeAction)
		{
			if (!migrations.ContainsKey(oldUserVersion)) {
				migrations[oldUserVersion] = new List<Migration>();
			}
			migrations[oldUserVersion].Add(new Migration(oldUserVersion, newUserVersion, upgradeAction));
			return this;
		}

		/// <summary>
		/// Connects foreign key properties to their remote tables. This should only be called after
		/// all table types have been registered.
		/// </summary>
		public SQLiteConfig WireForeignKeys()
		{
			lock(mappings) {
				foreach(var table in mappings.Values) {
					table.WireForeignKeys(this);
				}
			}
			return this;
		}

		/// <summary>
		/// Returns the TableMapping for the given type if it exists.
		/// </summary>
		/// <exception cref="ArgumentException">Thrown if the TableMapping does not exist for the given type.</exception>
		public TableMapping GetTable(Type type)
		{
			string key = type.FullName;
			lock(mappings) {
				if(!mappings.ContainsKey(key)) {
					throw new ArgumentException($"Table mapping not found for {key}.");
				}
				return mappings[key];
			}
		}

		/// <summary>
		/// Adds support for a custom type inside TableMappings.
		/// </summary>
		/// <param name="type">The type, which can now be included in Table definition classes.</param>
		/// <param name="sqlType">The type of the SQL column used to store this type.</param>
		/// <param name="reader">
		///		A function that can read from the database and returns an object of type.
		///		You should make use of SQLite3.Column*() functions.
		///		You shouldn't handle null values, that will be done by the library.
		/// </param>
		/// <param name="writer">
		///		A function that can take an object of type, and write it to the database.
		///		You should make use of SQLite3.Bind*() functions.
		///		You shouldn't handle null values, that will be done by the library.
		/// </param>
		/// <returns>This config for chaining.</returns>
		public SQLiteConfig AddType(Type type, string sqlType, NonNullReadColumnDelegate reader, NonNullWriteColumnDelegate writer)
		{
			columnReaders[type] = NullHandlingReader(reader);
			columnWriters[type] = NullHandlingWriter(writer);
			sqlTypeOverrides[type] = sqlType;
			return this;
		}

		/// <summary>
		/// Configures the given enum types to store as a string value, instead of ints.
		/// This is not supported for Flags enums.
		/// </summary>
		public SQLiteConfig AddEnumTypesAsText(params Type[] types)
		{
			foreach(var type in types) {
				if(!type.GetTypeInfo().IsEnum) {
					throw new ArgumentException($"{type.FullName} is not an enum!");
				}
				AddType(type, "varchar",
					(s, i) => {
						var colType = SQLite3.ColumnType(s, i);
						if(colType == SQLite3.ColType.Text) {
							var value = SQLite3.ColumnString(s, i);
							return Enum.Parse(type, value, true);
						}
						return Enum.ToObject(type, SQLite3.ColumnInt(s, i));
					},
				   (s, i, v) => SQLite3.BindText(s, i, Enum.GetName(type, v))
                );
			}
			return this;
		}

		/// <summary> Configures the given enum types to store as a int values. </summary>
		public SQLiteConfig AddEnumTypesAsInts(params Type[] types)
		{
			foreach(var type in types) {
				if(!type.GetTypeInfo().IsEnum) {
					throw new ArgumentException($"{type.FullName} is not an enum!");
				}
				AddType(type, "integer",
					(s, i) => {
						var colType = SQLite3.ColumnType(s, i);
						if(colType == SQLite3.ColType.Text) {
							var value = SQLite3.ColumnString(s, i);
							return Enum.Parse(type, value, true);
						}
						return Enum.ToObject(type, SQLite3.ColumnInt(s, i));
					},
					(s, i, v) => SQLite3.BindInt(s, i, Convert.ToInt32(v))
				);
			}
			return this;
		}

		/// <summary>
		/// Returns a delegate to read the give type out of the database.
		/// </summary>
		/// <exception cref="ArgumentException">
		/// Throws when clrType is not known in advance. Use AddEnumTypes* and AddType methods to
		/// support custom types.
		/// </exception>
		public ReadColumnDelegate ColumnReader(Type clrType)
		{
			if(!columnReaders.ContainsKey(clrType)) {
				throw new ArgumentException("Don't know how to read " + clrType);
			}
			return columnReaders[clrType];
		}

		/// <summary>
		/// Returns a delegate to write the give type into the database.
		/// </summary>
		/// <exception cref="ArgumentException">
		/// Throws when clrType is not known in advance. Use AddEnumTypes* and AddType methods to
		/// support custom types.
		/// </exception>
		public WriteColumnDelegate ColumnWriter(Type clrType)
		{
			if(!columnWriters.ContainsKey(clrType)) {
				throw new ArgumentException("Don't know how to write " + clrType);
			}
			return columnWriters[clrType];
		}

		private static WriteColumnDelegate NullHandlingWriter(NonNullWriteColumnDelegate innerDelegate)
		{
			return (s, i, v) => {
				if(v == null) {
					SQLite3.BindNull(s, i);
				} else {
					innerDelegate(s, i, v);
				}
			};
		}

		private static ReadColumnDelegate NullHandlingReader(NonNullReadColumnDelegate innerDelegate)
		{
			return (s, i) => {
				var colType = SQLite3.ColumnType(s, i);
				if(colType == SQLite3.ColType.Null) {
					return null;
				} else {
					return innerDelegate(s, i);
				}
			};
		}

		/// <summary>
		/// Returns the default type of the underlying sql column.
		/// </summary>
		/// <param name="clrType">The .NET type to find the SQL type for.</param>
		/// <param name="maxStringLength">The maximum length of a string, if clrType is a string type.</param>
		/// <returns></returns>
		public string SqlTypeFor(Type clrType, int? maxStringLength = null)
		{
			if(sqlTypeOverrides.ContainsKey(clrType)) {
				return sqlTypeOverrides[clrType];
			}
			if(clrType == typeof(bool) || clrType == typeof(byte) || clrType == typeof(ushort) || clrType == typeof(sbyte) || clrType == typeof(short) || clrType == typeof(int) || clrType == typeof(uint) || clrType == typeof(long)) {
				return "integer";
			}
			if(clrType == typeof(float) || clrType == typeof(double) || clrType == typeof(decimal)) {
				return "float";
			}
			if(clrType == typeof(string)) {
				if(maxStringLength.HasValue) {
					return "varchar(" + maxStringLength.Value + ")";
				}
				return "varchar";
			}
			if(clrType == typeof(byte[])) {
				return "blob";
			}
			if(clrType == typeof(Guid)) {
				return "varchar(36)";
			}
			throw new NotSupportedException("Don't know about " + clrType);
		}

		/// <summary>
		/// Finds an upgrade path from the current version to the configured version.
		/// </summary>
		/// <param name="currentUserVersion">The current version of the database.</param>
		/// <returns>A function that will upgrade the database to UserVersion from this config.</returns>
		public Action<SQLiteConnection> GetUpgradePath(int currentUserVersion)
		{
			if (currentUserVersion == 0) {
				return connection => {
					connection.CreateAllTables();
					connection.ExecuteScalar<string>($"PRAGMA user_version = {UserVersion}");
				};
			}
			List<Migration> migrationSteps = new List<Migration>();
			int startingVersion = currentUserVersion;
			while(startingVersion != UserVersion) {
				Migration? furthestAlong = null;
				if(migrations.ContainsKey(startingVersion)) {
					foreach(Migration migration in migrations[startingVersion]) {
						if(migration.EndVersion > (furthestAlong?.EndVersion ?? 0)) {
							furthestAlong = migration;
						}
					}
				}
				if (furthestAlong == null) {
					// Try AutoMigration.
					migrationSteps.Add(new Migration(startingVersion, UserVersion, (conn) => conn.CreateAllTables()));
					break;
				}
				migrationSteps.Add(furthestAlong);
				startingVersion = furthestAlong.EndVersion;
			}

			return (connection) => {
				connection.BeginTransaction();
				foreach(Migration migration in migrationSteps) {
					migration.UpgradeAction(connection);
					connection.ExecuteScalar<string>($"PRAGMA user_version = {migration.EndVersion}");
				}
				connection.Commit();
			};
		}
	}
}
