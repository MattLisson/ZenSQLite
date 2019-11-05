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
using Sqlite3DatabaseHandle = SQLitePCL.sqlite3;
using Sqlite3StatementHandle = SQLitePCL.sqlite3_stmt;
using Sqlite3 = SQLitePCL.raw;

#pragma warning disable 1591 // XML Doc Comments

namespace SQLite
{
	public delegate object? ReadColumnDelegate(Sqlite3StatementHandle statement, int index);
	public delegate void WriteColumnDelegate(Sqlite3StatementHandle statement, int index, object? value);
	public delegate object NonNullReadColumnDelegate(Sqlite3StatementHandle statement, int index);
	public delegate void NonNullWriteColumnDelegate(Sqlite3StatementHandle statement, int index, object value);

	public static class SQLite3
	{
		public enum Result : int
		{
			OK = 0,
			Error = 1,
			Internal = 2,
			Perm = 3,
			Abort = 4,
			Busy = 5,
			Locked = 6,
			NoMem = 7,
			ReadOnly = 8,
			Interrupt = 9,
			IOError = 10,
			Corrupt = 11,
			NotFound = 12,
			Full = 13,
			CannotOpen = 14,
			LockErr = 15,
			Empty = 16,
			SchemaChngd = 17,
			TooBig = 18,
			Constraint = 19,
			Mismatch = 20,
			Misuse = 21,
			NotImplementedLFS = 22,
			AccessDenied = 23,
			Format = 24,
			Range = 25,
			NonDBFile = 26,
			Notice = 27,
			Warning = 28,
			Row = 100,
			Done = 101
		}

		public enum ExtendedResult : int
		{
			IOErrorRead = (Result.IOError | (1 << 8)),
			IOErrorShortRead = (Result.IOError | (2 << 8)),
			IOErrorWrite = (Result.IOError | (3 << 8)),
			IOErrorFsync = (Result.IOError | (4 << 8)),
			IOErrorDirFSync = (Result.IOError | (5 << 8)),
			IOErrorTruncate = (Result.IOError | (6 << 8)),
			IOErrorFStat = (Result.IOError | (7 << 8)),
			IOErrorUnlock = (Result.IOError | (8 << 8)),
			IOErrorRdlock = (Result.IOError | (9 << 8)),
			IOErrorDelete = (Result.IOError | (10 << 8)),
			IOErrorBlocked = (Result.IOError | (11 << 8)),
			IOErrorNoMem = (Result.IOError | (12 << 8)),
			IOErrorAccess = (Result.IOError | (13 << 8)),
			IOErrorCheckReservedLock = (Result.IOError | (14 << 8)),
			IOErrorLock = (Result.IOError | (15 << 8)),
			IOErrorClose = (Result.IOError | (16 << 8)),
			IOErrorDirClose = (Result.IOError | (17 << 8)),
			IOErrorSHMOpen = (Result.IOError | (18 << 8)),
			IOErrorSHMSize = (Result.IOError | (19 << 8)),
			IOErrorSHMLock = (Result.IOError | (20 << 8)),
			IOErrorSHMMap = (Result.IOError | (21 << 8)),
			IOErrorSeek = (Result.IOError | (22 << 8)),
			IOErrorDeleteNoEnt = (Result.IOError | (23 << 8)),
			IOErrorMMap = (Result.IOError | (24 << 8)),
			LockedSharedcache = (Result.Locked | (1 << 8)),
			BusyRecovery = (Result.Busy | (1 << 8)),
			CannottOpenNoTempDir = (Result.CannotOpen | (1 << 8)),
			CannotOpenIsDir = (Result.CannotOpen | (2 << 8)),
			CannotOpenFullPath = (Result.CannotOpen | (3 << 8)),
			CorruptVTab = (Result.Corrupt | (1 << 8)),
			ReadonlyRecovery = (Result.ReadOnly | (1 << 8)),
			ReadonlyCannotLock = (Result.ReadOnly | (2 << 8)),
			ReadonlyRollback = (Result.ReadOnly | (3 << 8)),
			AbortRollback = (Result.Abort | (2 << 8)),
			ConstraintCheck = (Result.Constraint | (1 << 8)),
			ConstraintCommitHook = (Result.Constraint | (2 << 8)),
			ConstraintForeignKey = (Result.Constraint | (3 << 8)),
			ConstraintFunction = (Result.Constraint | (4 << 8)),
			ConstraintNotNull = (Result.Constraint | (5 << 8)),
			ConstraintPrimaryKey = (Result.Constraint | (6 << 8)),
			ConstraintTrigger = (Result.Constraint | (7 << 8)),
			ConstraintUnique = (Result.Constraint | (8 << 8)),
			ConstraintVTab = (Result.Constraint | (9 << 8)),
			NoticeRecoverWAL = (Result.Notice | (1 << 8)),
			NoticeRecoverRollback = (Result.Notice | (2 << 8))
		}


		public enum ConfigOption : int
		{
			SingleThread = 1,
			MultiThread = 2,
			Serialized = 3
		}

		const string LibraryPath = "sqlite3";

		public static Result Open(string filename, out Sqlite3DatabaseHandle db)
		{
			return (Result)Sqlite3.sqlite3_open(filename, out db);
		}

		public static Result Open(string filename, out Sqlite3DatabaseHandle db, int flags, IntPtr zVfs)
		{
			return (Result)Sqlite3.sqlite3_open_v2(filename, out db, flags, null);
		}

		public static Result Close(Sqlite3DatabaseHandle db)
		{
			return (Result)Sqlite3.sqlite3_close(db);
		}

		public static Result Close2(Sqlite3DatabaseHandle db)
		{
			return (Result)Sqlite3.sqlite3_close_v2(db);
		}

		public static Result BusyTimeout(Sqlite3DatabaseHandle db, int milliseconds)
		{
			return (Result)Sqlite3.sqlite3_busy_timeout(db, milliseconds);
		}

		public static int Changes(Sqlite3DatabaseHandle db)
		{
			return Sqlite3.sqlite3_changes(db);
		}

		public static Sqlite3StatementHandle Prepare2(Sqlite3DatabaseHandle db, string query)
		{
			var r = Sqlite3.sqlite3_prepare_v2(db, query, out Sqlite3StatementHandle statement);
			if(r != 0) {
				throw SQLiteException.New((Result)r, GetErrmsg(db));
			}
			return statement;
		}

		public static Result Step(this Sqlite3StatementHandle statement)
		{
			return (Result)Sqlite3.sqlite3_step(statement);
		}

		public static Result Reset(this Sqlite3StatementHandle statement)
		{
			return (Result)Sqlite3.sqlite3_reset(statement);
		}

		public static Result ClearBindings(this Sqlite3StatementHandle statement)
		{
			return (Result)Sqlite3.sqlite3_clear_bindings(statement);
		}

		public static long LastInsertRowid(Sqlite3DatabaseHandle db)
		{
			return Sqlite3.sqlite3_last_insert_rowid(db);
		}

		public static string GetErrmsg(Sqlite3DatabaseHandle db)
		{
			return Sqlite3.sqlite3_errmsg(db).utf8_to_string();
		}

		public static int BindParameterIndex(Sqlite3StatementHandle statement, string name)
		{
			return Sqlite3.sqlite3_bind_parameter_index(statement, name);
		}

		public static int BindParameterCount(Sqlite3StatementHandle statement)
		{
			return Sqlite3.sqlite3_bind_parameter_count(statement);
		}

		public static int BindNull(Sqlite3StatementHandle statement, int index)
		{
			return Sqlite3.sqlite3_bind_null(statement, index);
		}

		public static int BindInt(Sqlite3StatementHandle statement, int index, int val)
		{
			return Sqlite3.sqlite3_bind_int(statement, index, val);
		}

		public static int BindInt64(Sqlite3StatementHandle statement, int index, long val)
		{
			return Sqlite3.sqlite3_bind_int64(statement, index, val);
		}

		public static int BindDouble(Sqlite3StatementHandle statement, int index, double val)
		{
			return Sqlite3.sqlite3_bind_double(statement, index, val);
		}

		public static int BindText(Sqlite3StatementHandle statement, int index, string val)
        {
			return Sqlite3.sqlite3_bind_text(statement, index, val);
		}

		public static int BindBlob(Sqlite3StatementHandle statement, int index, byte[] val, int n)
		{
			return Sqlite3.sqlite3_bind_blob(statement, index, val);
		}

		public static int ColumnCount(Sqlite3StatementHandle statement)
		{
			return Sqlite3.sqlite3_column_count(statement);
		}

		public static string ColumnName(Sqlite3StatementHandle statement, int index)
		{
			return Sqlite3.sqlite3_column_name(statement, index).utf8_to_string();
		}

		public static string ColumnName16(Sqlite3StatementHandle statement, int index)
		{
			return Sqlite3.sqlite3_column_name(statement, index).utf8_to_string();
		}

		public static ColType ColumnType(Sqlite3StatementHandle statement, int index)
		{
			return (ColType)Sqlite3.sqlite3_column_type(statement, index);
		}

		public static int ColumnInt(Sqlite3StatementHandle statement, int index)
		{
			return Sqlite3.sqlite3_column_int(statement, index);
		}

		public static long ColumnInt64(Sqlite3StatementHandle statement, int index)
		{
			return Sqlite3.sqlite3_column_int64(statement, index);
		}

		public static double ColumnDouble(Sqlite3StatementHandle statement, int index)
		{
			return Sqlite3.sqlite3_column_double(statement, index);
		}

		public static string ColumnText(Sqlite3StatementHandle statement, int index)
		{
			return Sqlite3.sqlite3_column_text(statement, index).utf8_to_string();
		}

		public static string ColumnText16(Sqlite3StatementHandle statement, int index)
		{
			return Sqlite3.sqlite3_column_text(statement, index).utf8_to_string();
		}

		public static System.ReadOnlySpan<byte> ColumnBlob(Sqlite3StatementHandle statement, int index)
		{
			return Sqlite3.sqlite3_column_blob(statement, index);
		}

		public static int ColumnBytes(Sqlite3StatementHandle statement, int index)
		{
			return Sqlite3.sqlite3_column_bytes(statement, index);
		}

		public static string ColumnString(Sqlite3StatementHandle statement, int index)
		{
			return Sqlite3.sqlite3_column_text(statement, index).utf8_to_string();
		}

		public static ReadOnlySpan<byte> ColumnByteArray(Sqlite3StatementHandle statement, int index)
		{
			int length = ColumnBytes(statement, index);
			if(length > 0) {
				return ColumnBlob(statement, index);
			}
			return new byte[0];
		}

		public static Result EnableLoadExtension(Sqlite3DatabaseHandle db, int onoff)
		{
			return (Result)Sqlite3.sqlite3_enable_load_extension(db, onoff);
		}

		public static int LibVersionNumber()
		{
			return Sqlite3.sqlite3_libversion_number();
		}

		public static ExtendedResult ExtendedErrCode(Sqlite3DatabaseHandle db)
		{
			return (ExtendedResult)Sqlite3.sqlite3_extended_errcode(db);
		}

		public enum ColType : int
		{
			Integer = 1,
			Float = 2,
			Text = 3,
			Blob = 4,
			Null = 5
		}
	}
}
