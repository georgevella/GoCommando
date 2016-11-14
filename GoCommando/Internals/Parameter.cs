using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using GoCommando.Extensions;

namespace GoCommando.Internals
{
    internal class Parameter : IEquatable<Parameter>
    {
        public Parameter(PropertyInfo propertyInfo, string name, string shortname, bool optional, string descriptionText,
            IEnumerable<string> exampleValues, string defaultValue, bool allowAppSetting, bool allowConnectionString,
            bool allowEnvironmentVariable)
        {
            PropertyInfo = propertyInfo;
            Name = name;
            Shortname = shortname;
            Optional = optional;
            DescriptionText = GetText(descriptionText, allowAppSetting, allowConnectionString, allowEnvironmentVariable);
            DefaultValue = defaultValue;
            AllowAppSetting = allowAppSetting;
            AllowConnectionString = allowConnectionString;
            AllowEnvironmentVariable = allowEnvironmentVariable;
            ExampleValues = exampleValues.ToArray();
            IsMultiValueParameter = propertyInfo.PropertyType.IsEnumerableOrCollection();
        }

        public bool AllowAppSetting { get; }
        public bool AllowConnectionString { get; }
        public bool AllowEnvironmentVariable { get; }
        public string DefaultValue { get; }
        public string DescriptionText { get; }
        public string[] ExampleValues { get; }

        public bool HasDefaultValue => DefaultValue != null;

        public bool IsFlag => PropertyInfo.PropertyType == typeof(bool);

        public bool IsMultiValueParameter { get; }
        public string Name { get; }
        public bool Optional { get; }
        public PropertyInfo PropertyInfo { get; }
        public string Shortname { get; }

        public bool Equals(Parameter other)
        {
            return (other != null) && Name.Equals(other.Name);
        }

        private string GetText(string descriptionText, bool allowAppSetting, bool allowConnectionString,
            bool allowEnvironmentVariable)
        {
            if (!allowAppSetting && !allowConnectionString && !allowEnvironmentVariable)
                return $"{descriptionText ?? ""}";

            var autoBindings = new List<string>();

            if (allowEnvironmentVariable)
                autoBindings.Add("ENV");

            if (allowAppSetting)
                autoBindings.Add("APP");

            if (allowConnectionString)
                autoBindings.Add("CONN");

            return $"{descriptionText ?? ""} ({string.Join(", ", autoBindings)})";
        }

        public bool MatchesKey(string key)
        {
            return (key == Name)
                   || ((Shortname != null) && (key == Shortname));
        }

        public void SetValue(object commandInstance, Switch s)
        {
            if (PropertyInfo.PropertyType.IsBoolean() && s.IsFlag)
            {
                PropertyInfo.SetValue(commandInstance, true);
                return;
            }

            if (s.IsMultiValue && PropertyInfo.PropertyType.IsEnumerableOrCollection())
            {
                var itemType = PropertyInfo.PropertyType.GetEnumerableOrCollectionItemType();

                //var typedValues = s.Values.Select(value => Convert.ChangeType(value, itemType));

                if (PropertyInfo.PropertyType.IsConstructable())
                {
                    // TODO handle
                    throw new NotImplementedException();
                    return;
                }

                var genericMakeList = ((MethodCallExpression)MakeListExpression.Body).Method.GetGenericMethodDefinition();
                var actualMakeList = genericMakeList.MakeGenericMethod(itemType);
                var list = actualMakeList.Invoke(null, new object[] { s });

                //var listType = typeof(List<>).MakeGenericType(itemType);
                //var list = Activator.CreateInstance(listType);
                PropertyInfo.SetValue(commandInstance, list);

                return;
            }

            if (!s.IsMultiValue)
            {
                SetValue(commandInstance, s.Values.FirstOrDefault());
                return;
            }

            throw new InvalidOperationException();
        }

        private Expression<Func<Switch, object>> MakeListExpression = s => MakeList<object>(s);

        private static object MakeList<T>(Switch s)
        {
            var typedValues = s.Values.Select(value => (T)Convert.ChangeType(value, typeof(T)));
            return new List<T>(typedValues);

        }

        public void SetValue(object commandInstance, string value)
        {
            try
            {
                var valueInTheRightType = PropertyInfo.PropertyType.IsBoolean()
                    ? true
                    : Convert.ChangeType(value, PropertyInfo.PropertyType);

                PropertyInfo.SetValue(commandInstance, valueInTheRightType);
            }
            catch (Exception exception)
            {
                throw new FormatException(
                    $"Could not set value '{value}' on property named '{PropertyInfo.Name}' on {PropertyInfo.DeclaringType}",
                    exception);
            }
        }

        public void ApplyDefaultValue(ICommand commandInstance)
        {
            if (!HasDefaultValue)
                throw new InvalidOperationException(
                    $"Cannot apply default value of '{Name}' parameter because it has no default!");

            SetValue(commandInstance, DefaultValue);
        }
    }
}