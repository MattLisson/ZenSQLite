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

		public Column? PK { get; }

		public string GetByPrimaryKeySql { get; }

		public CreateFlags CreateFlags { get; }

		readonly Column? _autoPk;
		public bool HasAutoIncPK => _autoPk != null;

		public Column[] InsertColumns { get; }

		public Column[] InsertOrReplaceColumns { get; }

		public ManyToManyRelationship[] ManyToManys { get; }


		public TableMapping(Type type, SQLiteConfig config)
		{
			CreateFlags createFlags = config.CreateFlags;
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
						p.GetMethod.IsPublic &&
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
					cols.Add(new Column(p, config));
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

		public void WireForeignKeys(SQLiteConfig config)
		{
			foreach(Column col in Columns) {
				col.WireForeignKeys(config);
			}
			foreach(ManyToManyRelationship manyToMany in ManyToManys) {
				manyToMany.WireForeignKeys(config);
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
			var exact = Columns.FirstOrDefault(c => string.Equals(c.Name, columnName, StringComparison.OrdinalIgnoreCase));
			return exact;
		}

		public class Column
		{
			public string Name { get; }

			public PropertyInfo PropertyInfo { get; }

			public string PropertyName { get { return PropertyInfo.Name; } }

			public Type ClrType { get; }
			public string SqlType { get; }

			public TableMapping ForeignTable { get; private set; }
			public Column ForeignColumn { get; private set; }
			public bool IsForeignKey => ForeignTable != null;

			public IEnumerable<IndexedAttribute> Indices { get; set; }

			public bool IsPK { get; }

			public bool IsAutoInc { get; }
			public bool IsAutoGuid { get; }
			public bool IsNullable { get; }

			public int? MaxStringLength { get; }
			public string Collation { get; }

			private ReadColumnDelegate ReadColumnFunc { get; }
			private WriteColumnDelegate WriteColumnFunc { get; }
			private Func<object, object?> GetFunc { get; }
			private Action<object, object?> SetFunc { get; }

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

#pragma warning disable CS8618 // Non-nullable field is uninitialized.
			public Column(PropertyInfo prop, SQLiteConfig config)
#pragma warning restore CS8618 // Non-nullable field is uninitialized.
			{
				var colAttr = prop.GetCustomAttribute<ColumnAttribute>();
				CreateFlags createFlags = config.CreateFlags;
				PropertyInfo = prop;
				GetFunc = prop.GetGetterDelegate();
				SetFunc = prop.GetSetterDelegate();
				Name = colAttr?.Name ?? prop.Name;
				//If this type is Nullable<T> then Nullable.GetUnderlyingType returns the T, otherwise it returns null, so get the actual type instead
				ClrType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

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
				SqlType = config.SqlTypeFor(ClrType, MaxStringLength);

				WriteColumnFunc = config.ColumnWriter(ClrType);
				ReadColumnFunc = config.ColumnReader(ClrType);
			}

			public void WireForeignKeys(SQLiteConfig config)
			{
				var foreignKeyAttr = PropertyInfo.GetCustomAttribute<ForeignKeyAttribute>();
				if(foreignKeyAttr != null) {
					ForeignTable = config.GetTable(foreignKeyAttr.TargetType);
					string? targetPropertyName = foreignKeyAttr.TargetPropertyName;
					Column? otherPK = ForeignTable.PK;
					if(targetPropertyName != null) {
						ForeignColumn = ForeignTable.FindColumnWithPropertyName(targetPropertyName);
					} else if (otherPK != null) {
						ForeignColumn = otherPK;
					} else {
						throw new ArgumentException("Foreign Key Target Property must be provided when" +
							" the other table has no Primary Key");
					}
				}
			}

			public void SetProperty(object obj, object? val)
			{
				SetFunc(obj, val);
			}

			public object? GetProperty(object obj)
			{
				return GetFunc(obj);
			}

			public object? ReadColumn(Sqlite3StatementHandle statement, int index)
			{
				return ReadColumnFunc(statement, index);
			}

			public void WriteColumn(Sqlite3StatementHandle statement, int index, object value)
			{
				WriteColumnFunc(statement, index, value);
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

#pragma warning disable CS8618 // Non-nullable field is uninitialized.
		public ManyToManyRelationship(PropertyInfo prop)
#pragma warning restore CS8618 // Non-nullable field is uninitialized.
		{
			if (!prop.TryUnpackEnumerableTypes(out Type? collectionType, out Type? elementType)) {
				throw new ArgumentException($"A Collection type is required for ManyToMany, can't use:\n{prop}");
			}
			CollectionType = collectionType!;
			ElementType = elementType!;
			PropertyInfo = prop;
		}

		public void WireForeignKeys(SQLiteConfig config)
		{
			var attribute = PropertyInfo.GetCustomAttribute<ManyToManyAttribute>();
			Table = config.GetTable(attribute.RelationshipType);
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
