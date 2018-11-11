using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace SQLite
{
	/// <summary>
	///  Extension methods for reflection types.
	/// </summary>
    public static class Extensions
	{
		/// <summary>
		/// Gets a compiled delegate for the Getter method of prop.
		/// </summary>
		/// <param name="prop"></param>
		public static Func<object, object> GetGetterDelegate(this PropertyInfo prop)
		{
			var obj = Expression.Parameter(typeof(object), "o");
			var expr =
				Expression.Lambda<Func<object, object>>(
					Expression.Convert(
						Expression.Call(
							Expression.Convert(obj, prop.DeclaringType),
							prop.GetMethod),
						typeof(object)),
					obj);
			return expr.Compile();
		}

		/// <summary>
		/// Gets a compiled delegate for the Setter method of prop.
		/// </summary>
		/// <param name="prop"></param>
		public static Action<object, object> GetSetterDelegate(this PropertyInfo prop)
		{
			var obj = Expression.Parameter(typeof(object), "o");
			var value = Expression.Parameter(typeof(object), "v");
			var expr =
				Expression.Lambda<Action<object, object>>(
					Expression.Call(
						Expression.Convert(obj, prop.DeclaringType),
						prop.SetMethod,
						Expression.Convert(value, prop.PropertyType)),
					obj,
					value);
			return expr.Compile();
		}

		/// <summary>
		/// Gets the type of the collection, and the type of the elements of the collection.
		/// </summary>
		/// <param name="property"></param>
		/// <param name="collectionType"></param>
		/// <param name="elementType"></param>
		/// <returns>Whether this property is actually a collection type.</returns>
		public static bool TryUnpackEnumerableTypes(this PropertyInfo property, out Type collectionType, out Type elementType)
		{
			collectionType = null;
			elementType = null;

			var propertyType = property.PropertyType;
			var typeInfo = propertyType.GetTypeInfo();
			if(propertyType.IsArray) {
				collectionType = propertyType;
				elementType = propertyType.GetElementType();
				return true;
			}
			if(typeInfo.IsGenericType
				&& typeof(IEnumerable<>).MakeGenericType(typeInfo.GenericTypeArguments[0]).IsAssignableFrom(propertyType)) {
				elementType = typeInfo.GenericTypeArguments[0];
				collectionType = propertyType;
				return true;
			}
			return false;
		}

		/// <summary>
		/// Whether type A is assignable from type B.
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		public static bool IsAssignableFrom(this Type a, Type b)
		{
			return a.GetTypeInfo().IsAssignableFrom(b.GetTypeInfo());
		}
	}
}
