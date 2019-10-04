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
	public class SQLiteStatement<T> : IDisposable
	{
		private readonly SQLiteConnection conn;
		private readonly Sqlite3StatementHandle statement;
		private readonly object?[] currentBindings;

		public string CommandText { get; }


		private readonly TableMapping outputMapping;
		private readonly TableMapping.Column[] outputColumns;

		public SQLiteStatement(SQLiteConnection conn, TableMapping outputMapping, string commandText)
		{
			Debug.Assert(outputMapping.MappedType == typeof(T),
				$"TableMapping doesn't create the correct type {typeof(T)}");
			this.conn = conn;
			CommandText = commandText;
			this.outputMapping = outputMapping;

			statement = SQLite3.Prepare2(conn.Handle, commandText);
			currentBindings = new object[SQLite3.BindParameterCount(statement)];


			outputColumns = new TableMapping.Column[SQLite3.ColumnCount(statement)];

			for(int i = 0; i < outputColumns.Length; i++) {
				var name = SQLite3.ColumnName16(statement, i);
				outputColumns[i] = outputMapping.FindColumn(name);
			}
		}
	

		public IEnumerable<T> ExecuteQuery()
		{
			if(conn.Trace) {
				conn.Tracer?.Invoke("Executing Query: " + this);
			}

			while(SQLite3.Step(statement) == SQLite3.Result.Row) {
				var obj = Activator.CreateInstance(outputMapping.MappedType);
				for(int i = 0; i < outputColumns.Length; i++) {
					if(outputColumns[i] == null) {
						continue;
					}

					TableMapping.Column col = outputColumns[i];
					col.SetProperty(obj, col.ReadColumn(statement, i));
				}
				// TODO: Don't load ManyToMany for columns that were missing above.
				for(int i = 0; i < outputMapping.ManyToManys.Length; i++) {
					ManyToManyRelationship manyToMany = outputMapping.ManyToManys[i];
					manyToMany.SetProperty(obj, manyToMany.ReadChildren(conn, obj));
				}
				yield return (T)obj;
			}
		}

		public void Bind(string name, object val)
		{
			var index = SQLite3.BindParameterIndex(statement, name);
			Bind(index, val);
		}

		public void Bind(int index, object? val)
		{
			if(index - 1 < currentBindings.Length) {
				currentBindings[index - 1] = val;
			}
			Type type = val?.GetType() ?? typeof(int);
			var writer = conn.Config.ColumnWriter(type);
			writer(statement, index, val);
		}

		public SQLiteStatement<T> Bind(params object?[] ps)
		{
			for(int i = 0; i < ps.Length; i++) {
				Bind(i + 1, ps[i]);
			}
			return this;
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
}
