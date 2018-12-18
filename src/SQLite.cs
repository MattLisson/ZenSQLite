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
using System.Linq.Expressions;
using System.Reflection;

#pragma warning disable 1591 // XML Doc Comments

namespace SQLite
{
	public static class Orm
	{
		public const int DefaultMaxStringLength = 140;
		public const string ImplicitPkName = "Id";
		public const string ImplicitIndexSuffix = "Id";

		public static Type GetType(object obj)
		{
			if(obj == null) {
				return typeof(object);
			}

			var rt = obj as IReflectableType;
			if(rt != null) {
				return rt.GetTypeInfo().AsType();
			}

			return obj.GetType();
		}

		public static bool IsPK(MemberInfo p)
		{
			return p.CustomAttributes.Any(x => x.AttributeType == typeof(PrimaryKeyAttribute));
		}

		public static string Collation(MemberInfo p)
		{
			return p.GetCustomAttribute<CollationAttribute>()?.Value;
		}

		public static bool IsAutoInc(MemberInfo p)
		{
			return p.CustomAttributes.Any(x => x.AttributeType == typeof(AutoIncrementAttribute));
		}

		public static FieldInfo GetField(TypeInfo t, string name)
		{
			var f = t.GetDeclaredField(name);
			if(f != null) {
				return f;
			}

			return GetField(t.BaseType.GetTypeInfo(), name);
		}

		public static PropertyInfo GetProperty(TypeInfo t, string name)
		{
			var f = t.GetDeclaredProperty(name);
			if(f != null) {
				return f;
			}

			return GetProperty(t.BaseType.GetTypeInfo(), name);
		}

		public static object InflateAttribute(CustomAttributeData x)
		{
			var atype = x.AttributeType;
			var typeInfo = atype.GetTypeInfo();
			var args = x.ConstructorArguments.Select(a => a.Value).ToArray();
			var r = Activator.CreateInstance(x.AttributeType, args);
			foreach(var arg in x.NamedArguments) {
				if(arg.IsField) {
					GetField(typeInfo, arg.MemberName).SetValue(r, arg.TypedValue.Value);
				}
				else {
					GetProperty(typeInfo, arg.MemberName).SetValue(r, arg.TypedValue.Value);
				}
			}
			return r;
		}

		public static IEnumerable<IndexedAttribute> GetIndices(MemberInfo p)
		{
			var indexedInfo = typeof(IndexedAttribute).GetTypeInfo();
			return
				p.CustomAttributes
				 .Where(x => indexedInfo.IsAssignableFrom(x.AttributeType.GetTypeInfo()))
				 .Select(x => (IndexedAttribute)InflateAttribute(x));
		}

		public static int? MaxStringLength(PropertyInfo p)
		{
			var attr = p.CustomAttributes.FirstOrDefault(x => x.AttributeType == typeof(MaxLengthAttribute));
			if(attr != null) {
				var attrv = (MaxLengthAttribute)InflateAttribute(attr);
				return attrv.Value;
			}
			return null;
		}

	}
}
