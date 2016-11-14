using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

namespace GoCommando.Extensions
{
    internal static class TypeExtensions
    {
        private static readonly Type GenericEnumerableType = typeof(IEnumerable<>);
        private static readonly Type EnumerableType = typeof(IEnumerable);
        private static readonly Type BooleanType = typeof(bool);
        private static readonly Type GenericCollectionType = typeof(ICollection<>);

        public static bool IsEnumerableOrCollection(this Type type)
        {
            // strings inherit from IEnumerable, but shouldn't be treated as a collection here.
            if (type == typeof(string))
                return false;

            if (type == EnumerableType)
                return true;

            var interfaces = type.GetInterfaces();
            return interfaces.Contains(EnumerableType) ||
                   interfaces.Contains(GenericEnumerableType);
        }

        public static Type GetEnumerableOrCollectionItemType(this Type type)
        {
            if (type == EnumerableType)
                return typeof(object);

            if (type.IsGenericType)
            {
                var genericType = type.GetGenericTypeDefinition();
                if (genericType == GenericEnumerableType || genericType == GenericCollectionType)
                    return type.GetTypeInfo().GenericTypeArguments[0];
            }

            var implementedCollectionContracts =
                type.GetInterfaces()
                    .Where(intf => intf.IsGenericType)
                    .Select(intf => new
                    {
                        GenericInterface = intf.GetGenericTypeDefinition(),
                        Interface = intf
                    })
                    .FirstOrDefault(
                        x =>
                            x.GenericInterface == GenericCollectionType ||
                            x.GenericInterface == GenericEnumerableType);

            if (implementedCollectionContracts != null)
                return implementedCollectionContracts.Interface.GetTypeInfo().GenericTypeArguments[0];

            return typeof(object);
        }

        public static bool IsBoolean(this Type type)
        {
            return type == BooleanType;
        }

        public static bool IsConstructable(this Type type)
        {
            return !type.IsInterface && !type.IsAbstract && type.HasDefaultConstructor();
        }

        public static bool HasDefaultConstructor(this Type type)
        {
            return !type.IsInterface && type.GetConstructors().Any(x => x.GetParameters().Length == 0);
        }
    }
}