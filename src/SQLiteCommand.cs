//
// Copyright (c) 2009-2018 Krueger Systems, Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sqlite3StatementHandle = SQLitePCL.sqlite3_stmt;
using Sqlite3DatabaseHandle = SQLitePCL.sqlite3;
using System.Diagnostics;

#pragma warning disable 1591 // XML Doc Comments

namespace SQLite
{
	public class SQLiteStatement : IDisposable
	{
		private readonly SQLiteConnection conn;
		private readonly Sqlite3StatementHandle statement;
		private readonly object?[] currentBindings;

		public string CommandText { get; }


		public SQLiteStatement(SQLiteConnection conn, string commandText)
		{
			this.conn = conn;
			CommandText = commandText;
			try {
				statement = SQLite3.Prepare2(conn.Handle, commandText);
			} catch (Exception e) {
                Debug.Assert(false, $"Couldn't prepare statement:\n{commandText}\nException:{e}");
				throw e;
            }
			currentBindings = new object[SQLite3.BindParameterCount(statement)];
		}

		public int ExecuteNonQuery()
		{
			var r = SQLite3.Result.OK;
			r = statement.Step();
			if(r == SQLite3.Result.Done) {
				int rowsAffected = SQLite3.Changes(conn.Handle);
				return rowsAffected;
			}
			else if(r == SQLite3.Result.Error) {
				string msg = SQLite3.GetErrmsg(conn.Handle);
				throw SQLiteException.New(r, msg);
			}
			else if(r == SQLite3.Result.Constraint) {
				if(SQLite3.ExtendedErrCode(conn.Handle) == SQLite3.ExtendedResult.ConstraintNotNull) {
					throw NotNullConstraintViolationException.New(r, SQLite3.GetErrmsg(conn.Handle));
				}
			}

			throw SQLiteException.New(r, SQLite3.GetErrmsg(conn.Handle));
		}

		public IEnumerable<T> ExecuteDeferredQuery<T>()
		{
			return ExecuteDeferredQuery<T>(conn.GetMapping(typeof(T)));
		}

		public List<T> ExecuteQuery<T>()
		{
			return ExecuteDeferredQuery<T>(conn.GetMapping(typeof(T))).ToList();
		}

		public List<T> ExecuteQuery<T>(TableMapping map)
		{
			return ExecuteDeferredQuery<T>(map).ToList();
		}

		public IEnumerable<T> ExecuteDeferredQuery<T>(TableMapping map)
		{
			if(conn.Trace) {
				conn.Tracer?.Invoke("Executing Query: " + this);
			}

			var cols = new TableMapping.Column[SQLite3.ColumnCount(statement)];

			for(int i = 0; i < cols.Length; i++) {
				var name = SQLite3.ColumnName16(statement, i);
				cols[i] = map.FindColumn(name);
			}

			while(SQLite3.Step(statement) == SQLite3.Result.Row) {
				var obj = Activator.CreateInstance(map.MappedType);
				for(int i = 0; i < cols.Length; i++) {
					if(cols[i] == null) {
						continue;
					}

					TableMapping.Column col = cols[i];
					col.SetProperty(obj, col.ReadColumn(statement, i));
				}
				yield return (T)obj;
			}
		}

		public T ExecuteScalar<T>()
		{
			// Waiting on Defaultable types support.
			T val = default!;

			var r = SQLite3.Step(statement);
			if(r == SQLite3.Result.Row) {
				Type t = typeof(T);
				var readFunc = conn.Config.ColumnReader(t);
				val = (T)readFunc(statement, 0);
			}
			else if(r == SQLite3.Result.Done) {
			}
			else {
				throw SQLiteException.New(r, SQLite3.GetErrmsg(conn.Handle));
			}

			return val;
		}

		public void Bind(string name, object val)
		{
			var index = SQLite3.BindParameterIndex(statement, name);
			Bind(index, val);
		}

		public void Bind(int index, object? val)
		{
			if (index - 1 < currentBindings.Length) {
				currentBindings[index - 1] = val;
			}
			Type type = val?.GetType() ?? typeof(int);
			var writer = conn.Config.ColumnWriter(type);
			writer(statement, index, val);

		}

		public override string ToString()
		{
			var parts = new string[1 + currentBindings.Length];
			parts[0] = CommandText;
			var i = 1;
			foreach(var b in currentBindings) {
				parts[i] = $"  {i}: {b}";
				i++;
			}
			return string.Join(Environment.NewLine, parts);
		}

		public void Reset()
		{
			statement.Reset();
			statement.ClearBindings();
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		void Dispose(bool disposing)
		{
			statement.Dispose();
		}

		~SQLiteStatement()
		{
			Dispose(false);
		}
	}

	/// <summary>
	/// Since the insert never changed, we only need to prepare once.
	/// </summary>
	class SQLiteInsertStatement : IDisposable
	{
		private readonly SQLiteConnection connection;
		private readonly string commandText;
		private readonly Sqlite3StatementHandle statement;

		public SQLiteInsertStatement(SQLiteConnection conn, string commandText)
		{
			connection = conn;
			this.commandText = commandText;
			statement = SQLite3.Prepare2(conn.Handle, commandText);
		}

		public int Execute(object?[] source)
		{
			var r = SQLite3.Result.OK;

			//bind the values.
			if(source != null) {
				for(int i = 0; i < source.Length; i++) {
					Type type = source[i]?.GetType() ?? typeof(int);
					var writer = connection.Config.ColumnWriter(type);
					writer(statement, i + 1, source[i]);
				}
			}
			r = SQLite3.Step(statement);

			if(r == SQLite3.Result.Done) {
				int rowsAffected = SQLite3.Changes(connection.Handle);
				SQLite3.Reset(statement);
				return rowsAffected;
			}
			else if(r == SQLite3.Result.Error) {
				string msg = SQLite3.GetErrmsg(connection.Handle);
				SQLite3.Reset(statement);
				throw SQLiteException.New(r, msg);
			}
			else if(r == SQLite3.Result.Constraint && SQLite3.ExtendedErrCode(connection.Handle) == SQLite3.ExtendedResult.ConstraintNotNull) {
				SQLite3.Reset(statement);
				throw NotNullConstraintViolationException.New(r, SQLite3.GetErrmsg(connection.Handle));
			}
			else {
				SQLite3.Reset(statement);
				throw SQLiteException.New(r, r.ToString());
			}
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		void Dispose(bool disposing)
		{
			statement.Dispose();
		}

		~SQLiteInsertStatement()
		{
			Dispose(false);
		}
	}
}
