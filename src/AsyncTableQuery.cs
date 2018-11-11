//
// Copyright (c) 2012-2017 Krueger Systems, Inc.
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
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace SQLite
{
	//
	// TODO: Bind to AsyncConnection.GetConnection instead so that delayed
	// execution can still work after a Pool.Reset.
	//

	/// <summary>
	/// Query to an asynchronous database connection.
	/// </summary>
	public class AsyncTableQuery<T>
		where T : new()
	{
		TableQuery<T> _innerQuery;

		/// <summary>
		/// Creates a new async query that uses given the synchronous query.
		/// </summary>
		public AsyncTableQuery(TableQuery<T> innerQuery)
		{
			_innerQuery = innerQuery;
		}

		Task<U> ReadAsync<U>(Func<SQLiteConnectionWithLock, U> read)
		{
			return Task.Factory.StartNew(() => {
				var conn = (SQLiteConnectionWithLock)_innerQuery.Connection;
				using(conn.Lock()) {
					return read(conn);
				}
			}, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
		}

		Task<U> WriteAsync<U>(Func<SQLiteConnectionWithLock, U> write)
		{
			return Task.Factory.StartNew(() => {
				var conn = (SQLiteConnectionWithLock)_innerQuery.Connection;
				using(conn.Lock()) {
					return write(conn);
				}
			}, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
		}

		/// <summary>
		/// Filters the query based on a predicate.
		/// </summary>
		public AsyncTableQuery<T> Where(Expression<Func<T, bool>> predExpr)
		{
			return new AsyncTableQuery<T>(_innerQuery.Where(predExpr));
		}

		/// <summary>
		/// Skips a given number of elements from the query and then yields the remainder.
		/// </summary>
		public AsyncTableQuery<T> Skip(int n)
		{
			return new AsyncTableQuery<T>(_innerQuery.Skip(n));
		}

		/// <summary>
		/// Yields a given number of elements from the query and then skips the remainder.
		/// </summary>
		public AsyncTableQuery<T> Take(int n)
		{
			return new AsyncTableQuery<T>(_innerQuery.Take(n));
		}

		/// <summary>
		/// Order the query results according to a key.
		/// </summary>
		public AsyncTableQuery<T> OrderBy<U>(Expression<Func<T, U>> orderExpr)
		{
			return new AsyncTableQuery<T>(_innerQuery.OrderBy<U>(orderExpr));
		}

		/// <summary>
		/// Order the query results according to a key.
		/// </summary>
		public AsyncTableQuery<T> OrderByDescending<U>(Expression<Func<T, U>> orderExpr)
		{
			return new AsyncTableQuery<T>(_innerQuery.OrderByDescending<U>(orderExpr));
		}

		/// <summary>
		/// Order the query results according to a key.
		/// </summary>
		public AsyncTableQuery<T> ThenBy<U>(Expression<Func<T, U>> orderExpr)
		{
			return new AsyncTableQuery<T>(_innerQuery.ThenBy<U>(orderExpr));
		}

		/// <summary>
		/// Order the query results according to a key.
		/// </summary>
		public AsyncTableQuery<T> ThenByDescending<U>(Expression<Func<T, U>> orderExpr)
		{
			return new AsyncTableQuery<T>(_innerQuery.ThenByDescending<U>(orderExpr));
		}

		/// <summary>
		/// Queries the database and returns the results as a List.
		/// </summary>
		public Task<List<T>> ToListAsync()
		{
			return ReadAsync(conn => _innerQuery.ToList());
		}

		/// <summary>
		/// Queries the database and returns the results as an array.
		/// </summary>
		public Task<T[]> ToArrayAsync()
		{
			return ReadAsync(conn => _innerQuery.ToArray());
		}

		/// <summary>
		/// Execute SELECT COUNT(*) on the query
		/// </summary>
		public Task<int> CountAsync()
		{
			return ReadAsync(conn => _innerQuery.Count());
		}

		/// <summary>
		/// Execute SELECT COUNT(*) on the query with an additional WHERE clause.
		/// </summary>
		public Task<int> CountAsync(Expression<Func<T, bool>> predExpr)
		{
			return ReadAsync(conn => _innerQuery.Count(predExpr));
		}

		/// <summary>
		/// Returns the element at a given index
		/// </summary>
		public Task<T> ElementAtAsync(int index)
		{
			return ReadAsync(conn => _innerQuery.ElementAt(index));
		}

		/// <summary>
		/// Returns the first element of this query.
		/// </summary>
		public Task<T> FirstAsync()
		{
			return ReadAsync(conn => _innerQuery.First());
		}

		/// <summary>
		/// Returns the first element of this query, or null if no element is found.
		/// </summary>
		public Task<T> FirstOrDefaultAsync()
		{
			return ReadAsync(conn => _innerQuery.FirstOrDefault());
		}

		/// <summary>
		/// Returns the first element of this query that matches the predicate.
		/// </summary>
		public Task<T> FirstAsync(Expression<Func<T, bool>> predExpr)
		{
			return ReadAsync(conn => _innerQuery.First(predExpr));
		}

		/// <summary>
		/// Returns the first element of this query that matches the predicate.
		/// </summary>
		public Task<T> FirstOrDefaultAsync(Expression<Func<T, bool>> predExpr)
		{
			return ReadAsync(conn => _innerQuery.FirstOrDefault(predExpr));
		}

		/// <summary>
		/// Delete all the rows that match this query and the given predicate.
		/// </summary>
		public Task<int> DeleteAsync(Expression<Func<T, bool>> predExpr)
		{
			return WriteAsync(conn => _innerQuery.Delete(predExpr));
		}

		/// <summary>
		/// Delete all the rows that match this query.
		/// </summary>
		public Task<int> DeleteAsync()
		{
			return WriteAsync(conn => _innerQuery.Delete());
		}
	}
}

