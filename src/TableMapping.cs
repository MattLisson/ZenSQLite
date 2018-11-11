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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using Sqlite3StatementHandle = SQLitePCL.sqlite3_stmt;

#pragma warning disable 1591 // XML Doc Comments

namespace SQLite
{
	public class TableMapping
	{
		public Type MappedType { get; }

		public string TableName { get; }

		public bool WithoutRowId { get; }

		public Column[] Columns { get; }

		public Column PK { get; }

		public string GetByPrimaryKeySql { get; }

		public CreateFlags CreateFlags { get; }

		readonly Column _autoPk;
		public bool HasAutoIncPK => _autoPk != null;

		public Column[] InsertColumns { get; }

		public Column[] InsertOrReplaceColumns { get; }

		public ManyToManyRelationship[] ManyToManys { get; }


		public TableMapping(Type type, CreateFlags createFlags = CreateFlags.None)
		{
			MappedType = type;
			CreateFlags = createFlags;

			var typeInfo = type.GetTypeInfo();
			var tableAttr =
				typeInfo.CustomAttributes
						.Where(x => x.AttributeType == typeof(TableAttribute))
						.Select(x => (TableAttribute)Orm.InflateAttribute(x))
						.FirstOrDefault();

			TableName = (tableAttr != null && !string.IsNullOrEmpty(tableAttr.Name)) ? tableAttr.Name : MappedType.Name;
			WithoutRowId = tableAttr != null ? tableAttr.WithoutRowId : false;

			var props = new List<PropertyInfo>();
			var baseType = type;
			var propNames = new HashSet<string>();
			while(baseType != typeof(object)) {
				var ti = baseType.GetTypeInfo();
				var newProps = (
					from p in ti.DeclaredProperties
					where
						!propNames.Contains(p.Name) &&
						p.CanRead && p.CanWrite &&
						(p.GetMethod != null) && (p.SetMethod != null) &&
						(p.GetMethod.IsPublic && p.SetMethod.IsPublic) &&
						(!p.GetMethod.IsStatic) && (!p.SetMethod.IsStatic)
					select p).ToList();
				foreach(var p in newProps) {
					propNames.Add(p.Name);
				}
				props.AddRange(newProps);
				baseType = ti.BaseType;
			}

			var cols = new List<Column>();
			var manyToManys = new List<ManyToManyRelationship>();
			foreach(var p in props) {
				var isColumn = !p.IsDefined(typeof(IgnoreAttribute), true);
				if(isColumn) {
					cols.Add(new Column(p, createFlags));
				}
				else if(p.IsDefined(typeof(ManyToManyAttribute), true)) {
					manyToManys.Add(new ManyToManyRelationship(p));
				}
			}
			ManyToManys = manyToManys.ToArray();
			Columns = cols.ToArray();
			foreach(var c in Columns) {
				if(c.IsAutoInc && c.IsPK) {
					_autoPk = c;
				}
				if(c.IsPK) {
					PK = c;
				}
			}

			if(PK != null) {
				GetByPrimaryKeySql = string.Format("select * from \"{0}\" where \"{1}\" = ?", TableName, PK.Name);
			}
			else {
				// People should not be calling Get/Find without a PK
				GetByPrimaryKeySql = string.Format("select * from \"{0}\" limit 1", TableName);
			}

			InsertColumns = Columns.Where(c => !c.IsAutoInc).ToArray();
			InsertOrReplaceColumns = Columns.ToArray();
		}

		public void WireForeignKeys(Func<Type, TableMapping> tableMap)
		{
			foreach(Column col in Columns) {
				col.WireForeignKeys(tableMap);
			}
			foreach(ManyToManyRelationship manyToMany in ManyToManys) {
				manyToMany.WireForeignKeys(tableMap);
			}
		}

		public void SetAutoIncPK(object obj, long id)
		{
			_autoPk?.SetProperty(obj, Convert.ChangeType(id, _autoPk.ClrType, null));
		}

		public Column FindColumnWithPropertyName(string propertyName)
		{
			var exact = Columns.FirstOrDefault(c => c.PropertyName == propertyName);
			return exact;
		}

		public Column FindColumn(string columnName)
		{
			var exact = Columns.FirstOrDefault(c => c.Name.ToLower() == columnName.ToLower());
			return exact;
		}

		public class Column
		{
			public string Name { get; }

			public PropertyInfo PropertyInfo { get; }

			public string PropertyName { get { return PropertyInfo.Name; } }

			public Type ClrType { get; }
			public bool IsEnum { get; }
			public string SqlType {
				get {
					var clrType = ClrType;
					if(clrType == typeof(Boolean) || clrType == typeof(Byte) || clrType == typeof(UInt16) || clrType == typeof(SByte) || clrType == typeof(Int16) || clrType == typeof(Int32) || clrType == typeof(UInt32) || clrType == typeof(Int64)) {
						return "integer";
					}
					else if(clrType == typeof(Single) || clrType == typeof(Double) || clrType == typeof(Decimal)) {
						return "float";
					}
					else if(clrType == typeof(String) || clrType == typeof(StringBuilder) || clrType == typeof(Uri) || clrType == typeof(UriBuilder)) {
						int? len = MaxStringLength;

						if(len.HasValue) {
							return "varchar(" + len.Value + ")";
						}

						return "varchar";
					}
					else if(clrType.GetTypeInfo().IsEnum) {
						if(StoreAsText) {
							return "varchar";
						}
						else {
							return "integer";
						}
					}
					else if(clrType == typeof(byte[])) {
						return "blob";
					}
					else if(clrType == typeof(Guid)) {
						return "varchar(36)";
					}
					else {
						throw new NotSupportedException("Don't know about " + clrType);
					}
				}
			}

			public TableMapping ForeignTable { get; private set; }
			public Column ForeignColumn { get; private set; }
			public bool IsForeignKey => ForeignTable != null;

			public IEnumerable<IndexedAttribute> Indices { get; set; }

			public bool IsPK { get; }

			public bool IsAutoInc { get; }
			public bool IsAutoGuid { get; }
			public bool IsNullable { get; }
			public bool StoreAsText { get; }

			public int? MaxStringLength { get; }
			public string Collation { get; }

			public delegate object ReadColumnDelegate(Sqlite3StatementHandle statement, int index);
			public delegate void WriteColumnDelegate(Sqlite3StatementHandle statement, int index, object value);
			private ReadColumnDelegate ReadColumnFunc { get; }
			private WriteColumnDelegate WriteColumnFunc { get; }
			private Func<object, object> GetFunc { get; }
			private Action<object, object> SetFunc { get; }

			public string SqlDecl {
				get {
					var constraints = new List<string>();
					if(IsPK) {
						string pk = "PRIMARY KEY";
						if(IsAutoInc) {
							pk += " AUTOINCREMENT";
						}
						constraints.Add(pk);
					}
					if(!IsNullable) {
						constraints.Add("NOT NULL");
					}
					if(!string.IsNullOrEmpty(Collation)) {
						constraints.Add("COLLATE " + Collation);
					}
					if(IsForeignKey) {
						constraints.Add(
							$@"REFERENCES ""{ForeignTable.TableName}"" (""{ForeignColumn.Name}"")");
					}

					string sqlType = SqlType;
					string constraintsString = string.Join(" ", constraints);
					return $@"""{Name}"" ""{sqlType}"" {constraintsString}";
				}
			}

			public Column(PropertyInfo prop, CreateFlags createFlags = CreateFlags.None)
			{
				var colAttr = prop.GetCustomAttribute<ColumnAttribute>();

				PropertyInfo = prop;
				GetFunc = prop.GetGetterDelegate();
				SetFunc = prop.GetSetterDelegate();
				Name = colAttr?.Name ?? prop.Name;
				//If this type is Nullable<T> then Nullable.GetUnderlyingType returns the T, otherwise it returns null, so get the actual type instead
				ClrType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
				IsEnum = ClrType.GetTypeInfo().IsEnum;

				Collation = Orm.Collation(prop);
				IsPK = Orm.IsPK(prop) ||
					(((createFlags & CreateFlags.ImplicitPK) == CreateFlags.ImplicitPK) &&
					 	string.Compare(prop.Name, Orm.ImplicitPkName, StringComparison.OrdinalIgnoreCase) == 0);

				var isAuto = Orm.IsAutoInc(prop) || (IsPK && ((createFlags & CreateFlags.AutoIncPK) == CreateFlags.AutoIncPK));
				IsAutoGuid = isAuto && ClrType == typeof(Guid);
				IsAutoInc = isAuto && !IsAutoGuid;

				Indices = Orm.GetIndices(prop);
				if(!Indices.Any()
					&& !IsPK
					&& ((createFlags & CreateFlags.ImplicitIndex) == CreateFlags.ImplicitIndex)
					&& Name.EndsWith(Orm.ImplicitIndexSuffix, StringComparison.OrdinalIgnoreCase)
					) {
					Indices = new IndexedAttribute[] { new IndexedAttribute() };
				}
				IsNullable = !(IsPK || Orm.IsMarkedNotNull(prop));
				MaxStringLength = Orm.MaxStringLength(prop);

				StoreAsText = prop.IsDefined(typeof(StoreAsTextAttribute));
				WriteColumnFunc = WriteDelegateFor(ClrType, IsEnum, StoreAsText);
				ReadColumnFunc = ReadDelegateFor(ClrType, IsEnum);
			}

			public void WireForeignKeys(Func<Type, TableMapping> tableGetter)
			{
				var foreignKeyAttr = PropertyInfo.GetCustomAttribute<ForeignKeyAttribute>();
				if(foreignKeyAttr != null) {
					ForeignTable = tableGetter(foreignKeyAttr.TargetType);
					ForeignColumn = ForeignTable.FindColumnWithPropertyName(foreignKeyAttr.TargetPropertyName);
				}
			}

			public void SetProperty(object obj, object val)
			{
				SetFunc(obj, val);
			}

			public object GetProperty(object obj)
			{
				return GetFunc(obj);
			}

			public object ReadColumn(Sqlite3StatementHandle statement, int index)
			{
				var colType = SQLite3.ColumnType(statement, index);
				if(colType == SQLite3.ColType.Null) {
					return null;
				}
				return ReadColumnFunc(statement, index);
			}

			public void WriteColumn(Sqlite3StatementHandle statement, int index, object value)
			{
				if(value == null) {
					SQLite3.BindNull(statement, index);
				}
				else {
					WriteColumnFunc(statement, index, value);
				}
			}

			public static WriteColumnDelegate WriteDelegateFor(Type clrType, bool isEnum, bool storeAsText)
			{
				WriteColumnDelegate innerDelegate = NonNullWriteDelegateFor(clrType, isEnum, storeAsText);
				return (s, i, v) => {
					if(v == null) {
						SQLite3.BindNull(s, i);
					}
					else {
						innerDelegate(s, i, v);
					}
				};
			}

			private static WriteColumnDelegate NonNullWriteDelegateFor(Type clrType, bool isEnum, bool storeAsText)
			{
				if(clrType == typeof(int)) {
					return (s, i, v) => SQLite3.BindInt(s, i, (int)v);
				}
				if(clrType == typeof(string)) {
					return (s, i, v) => SQLite3.BindText(s, i, (string)v, -1);
				}
				if(clrType == typeof(Byte) || clrType == typeof(UInt16)
					|| clrType == typeof(SByte) || clrType == typeof(Int16)) {
					return (s, i, v) => SQLite3.BindInt(s, i, Convert.ToInt32(v));
				}
				if(clrType == typeof(Boolean)) {
					return (s, i, v) => SQLite3.BindInt(s, i, (bool)v ? 1 : 0);
				}
				if(clrType == typeof(UInt32) || clrType == typeof(Int64)) {
					return (s, i, v) => SQLite3.BindInt64(s, i, Convert.ToInt64(v));
				}
				if(clrType == typeof(Single) || clrType == typeof(Double)
					|| clrType == typeof(Decimal)) {
					return (s, i, v) => SQLite3.BindDouble(s, i, Convert.ToDouble(v));
				}
				if(clrType == typeof(byte[])) {
					return (s, i, v) => SQLite3.BindBlob(s, i, (byte[])v, ((byte[])v).Length);
				}
				if(clrType == typeof(Guid)) {
					return (s, i, v) => SQLite3.BindText(s, i, ((Guid)v).ToString(), 72);
				}
				if(isEnum) {
					return (s, i, v) => {
						var enumIntValue = Convert.ToInt32(v);
						if(storeAsText) {
							SQLite3.BindText(s, i, Enum.GetName(clrType, v), -1);
						}
						else {
							SQLite3.BindInt(s, i, enumIntValue);
						}
					};
				}
				throw new NotSupportedException("Cannot store type: " + clrType);
			}

			public static ReadColumnDelegate ReadDelegateFor(Type clrType, bool isEnum)
			{
				ReadColumnDelegate innerDelegate = NonNullReadDelegateFor(clrType, isEnum);
				return (s, i) => {
					var colType = SQLite3.ColumnType(s, i);
					if(colType == SQLite3.ColType.Null) {
						return null;
					}
					else {
						return innerDelegate(s, i);
					}
				};
			}
			public static ReadColumnDelegate NonNullReadDelegateFor(Type clrType, bool isEnum)
			{
				if(clrType == typeof(String)) {
					return (s, i) => SQLite3.ColumnString(s, i);
				}
				if(clrType == typeof(Int32)) {
					return (s, i) => SQLite3.ColumnInt(s, i);
				}
				if(clrType == typeof(Boolean)) {
					return (s, i) => SQLite3.ColumnInt(s, i) == 1;
				}
				if(clrType == typeof(double)) {
					return (s, i) => SQLite3.ColumnDouble(s, i);
				}
				if(clrType == typeof(float)) {
					return (s, i) => (float)SQLite3.ColumnDouble(s, i);
				}
				if(clrType == typeof(Int64)) {
					return (s, i) => SQLite3.ColumnInt64(s, i);
				}
				if(clrType == typeof(UInt32)) {
					return (s, i) => (uint)SQLite3.ColumnInt64(s, i);
				}
				if(clrType == typeof(decimal)) {
					return (s, i) => (decimal)SQLite3.ColumnDouble(s, i);
				}
				if(clrType == typeof(Byte)) {
					return (s, i) => (byte)SQLite3.ColumnInt(s, i);
				}
				if(clrType == typeof(UInt16)) {
					return (s, i) => (ushort)SQLite3.ColumnInt(s, i);
				}
				if(clrType == typeof(Int16)) {
					return (s, i) => (short)SQLite3.ColumnInt(s, i);
				}
				if(clrType == typeof(sbyte)) {
					return (s, i) => (sbyte)SQLite3.ColumnInt(s, i);
				}
				if(clrType == typeof(byte[])) {
					return (s, i) => SQLite3.ColumnByteArray(s, i);
				}
				if(clrType == typeof(Guid)) {
					return (s, i) => {
						var text = SQLite3.ColumnString(s, i);
						return new Guid(text);
					};
				}
				if(isEnum) {
					return (s, i) => {
						var colType = SQLite3.ColumnType(s, i);
						if(colType == SQLite3.ColType.Text) {
							var value = SQLite3.ColumnString(s, i);
							return Enum.Parse(clrType, value, true);
						}
						return SQLite3.ColumnInt(s, i);
					};
				}
				throw new NotSupportedException("Don't know how to read " + clrType);
			}
		}
	}

	public class ManyToManyRelationship
	{
		public TableMapping Table { get; private set; }
		public TableMapping.Column ThisKeyColumn { get; private set; }
		public TableMapping.Column OtherKeyColumn { get; private set; }
		public PropertyInfo PropertyInfo { get; }

		private Type CollectionType { get; }
		private Type ElementType { get; }

		public ManyToManyRelationship(PropertyInfo prop)
		{
			if (!prop.TryUnpackEnumerableTypes(out Type collectionType, out Type elementType)) {
				throw new ArgumentException($"A Collection type is required for ManyToMany, can't use:\n{prop}");
			}
			CollectionType = collectionType;
			ElementType = elementType;
			PropertyInfo = prop;
		}

		public void WireForeignKeys(Func<Type, TableMapping> tableMap)
		{
			var attribute = PropertyInfo.GetCustomAttribute<ManyToManyAttribute>();
			Table = tableMap(attribute.RelationshipType);
			ThisKeyColumn = Table.FindColumnWithPropertyName(attribute.ThisKeyProperty);
			OtherKeyColumn = Table.FindColumnWithPropertyName(attribute.OtherKeyProperty);
		}


		public void SetIds(object obj, IEnumerable ids)
		{
			IList correctTypeList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(ElementType));
			foreach(object id in ids) {
				correctTypeList.Add(id);
			}
			PropertyInfo.SetValue(obj, Activator.CreateInstance(CollectionType, correctTypeList), null);
		}

		public System.Collections.IEnumerable GetIds(object obj)
		{
			return PropertyInfo.GetValue(obj, null) as System.Collections.IEnumerable
				?? new List<object>();
		}
	}
}
