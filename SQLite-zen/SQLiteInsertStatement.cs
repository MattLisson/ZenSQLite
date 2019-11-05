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
using Sqlite3StatementHandle = SQLitePCL.sqlite3_stmt;

#pragma warning disable 1591 // XML Doc Comments

namespace SQLite
{
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
				string msg = SQLite3.GetErrmsg(connection.Handle);
				SQLite3.Reset(statement);
				throw SQLiteException.New(r, msg);
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
