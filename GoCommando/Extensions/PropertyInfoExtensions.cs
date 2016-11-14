using System;
using System.Linq;
using System.Reflection;

namespace GoCommando.Extensions
{
    internal static class PropertyInfoExtensions
    {
        public static TAttribute GetSingleAttributeOrNull<TAttribute>(this PropertyInfo p) where TAttribute : Attribute
        {
            return p.GetCustomAttributes(typeof(TAttribute), false)
                .Cast<TAttribute>()
                .FirstOrDefault();
        }
    }
}