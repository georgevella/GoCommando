using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GoCommando.Extensions;

namespace GoCommando.Internals
{
    internal class CommandInvoker
    {
        private readonly Action<ICommand> _releaser;
        private readonly Settings _settings;
        //private Dictionary<string, Parameter> _nameToParametersMapping;
        //private Dictionary<string, Parameter> _shortnameToParametersMapping;

        public CommandInvoker(string command, Type type, Settings settings, string group = null,
            ICommandFactory commandFactory = null)
            : this(
                command, settings, CreateInstance(type, GetFactoryMethod(commandFactory)), group,
                GetReleaseMethod(commandFactory))
        {
        }

        public CommandInvoker(string command, Settings settings, ICommand commandInstance, string group = null,
            Action<ICommand> releaseMethod = null)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (commandInstance == null) throw new ArgumentNullException(nameof(commandInstance));

            _settings = settings;
            CommandInstance = commandInstance;
            _releaser = releaseMethod ?? DefaultReleaseMethod;

            Command = command;
            Group = group;
            Parameters = GetParameters(Type);

            //_nameToParametersMapping = Parameters.ToDictionary(x => x.Name);
            //_shortnameToParametersMapping = Parameters.ToDictionary(x => x.Shortname);
        }

        public string Command { get; }

        public ICommand CommandInstance { get; }

        public string Description => Type.GetCustomAttribute<DescriptionAttribute>()?.DescriptionText ??
                                     "(no help text for this command)";

        public string Group { get; }

        public IList<Parameter> Parameters { get; }

        public Type Type => CommandInstance.GetType();

        private static void DefaultReleaseMethod(ICommand command)
        {
            var disposable = command as IDisposable;
            disposable?.Dispose();
        }

        private static Func<Type, ICommand> GetFactoryMethod(ICommandFactory commandFactory)
        {
            if (commandFactory == null) return null;

            return commandFactory.Create;
        }

        private static Action<ICommand> GetReleaseMethod(ICommandFactory commandFactory)
        {
            if (commandFactory == null) return null;

            return commandFactory.Release;
        }

        private static ICommand CreateInstance(Type type, Func<Type, ICommand> commandFactory = null)
        {
            try
            {
                var instance = commandFactory?.Invoke(type)
                               ?? Activator.CreateInstance(type);

                if (!(instance is ICommand))
                    throw new ApplicationException($"{instance} does not implement ICommand!");

                return (ICommand)instance;
            }
            catch (Exception exception)
            {
                throw new ApplicationException($"Could not use type {type} as a GoCommando command", exception);
            }
        }

        private static IList<Parameter> GetParameters(Type type)
        {
            return type
                .GetProperties()
                .Select(p => new
                {
                    Property = p,
                    ParameterAttribute = p.GetSingleAttributeOrNull<ParameterAttribute>(),
                    DescriptionAttribute = p.GetSingleAttributeOrNull<DescriptionAttribute>(),
                    ExampleAttributes = p.GetCustomAttributes<ExampleAttribute>()
                })
                .Where(a => a.ParameterAttribute != null)
                .Select(a => new Parameter(a.Property,
                    a.ParameterAttribute.Name,
                    a.ParameterAttribute.ShortName,
                    a.ParameterAttribute.Optional,
                    a.DescriptionAttribute?.DescriptionText,
                    a.ExampleAttributes.Select(e => e.ExampleValue),
                    a.ParameterAttribute.DefaultValue,
                    a.ParameterAttribute.AllowAppSetting,
                    a.ParameterAttribute.AllowConnectionString,
                    a.ParameterAttribute.AllowEnvironmentVariable))
                .ToList();
        }

        public void Invoke(IEnumerable<Switch> switches, EnvironmentSettings environmentSettings)
        {
            try
            {

                CanInvoke(switches, environmentSettings);

                InnerInvoke(switches, environmentSettings);
            }
            finally
            {
                _releaser(CommandInstance);
            }
        }

        private void CanInvoke(IEnumerable<Switch> switches, EnvironmentSettings environmentSettings)
        {
            // determine if all arguments are supported by the command
            var switchesWithoutMatchingParameter = switches
                .Where(s => !Parameters.Any(p => p.MatchesKey(s.Name)))
                .ToList();

            if (switchesWithoutMatchingParameter.Any())
            {
                var switchesWithoutMathingParameterString = string.Join(Environment.NewLine,
                    switchesWithoutMatchingParameter.Select(p => $"    {_settings.SwitchPrefix}{p}"));

                throw new GoCommandoException(
                    $@"The following switches do not have a corresponding parameter:

{switchesWithoutMathingParameterString}");
            }

            // determine if we have duplicate arguments
            var parametersDeclaredMultipleTimes = switches.Where(item => item.IsMultiValue).ToDictionary(x => x, GetParameter);
            if (!parametersDeclaredMultipleTimes.Values.All(p => p.IsMultiValueParameter))
            {
                var duplicateSwitchKeys = parametersDeclaredMultipleTimes
                    .Where(x => !x.Value.IsMultiValueParameter).Select(x => $"{_settings.SwitchPrefix}{x.Key}");

                var dupes = string.Join(", ", duplicateSwitchKeys);

                throw new GoCommandoException($"The following switches have been specified more than once: {dupes}");
            }

            // check for missing parameters
            var requiredParametersMissing = Parameters
                    .Where(p => !p.Optional
                                && !p.HasDefaultValue
                                && !CanBeResolvedFromSwitches(switches, p)
                                && !CanBeResolvedFromEnvironmentSettings(environmentSettings, p))
                    .ToList();

            if (requiredParametersMissing.Any())
            {
                var requiredParametersMissingString = string.Join(Environment.NewLine,
                    requiredParametersMissing.Select(p => $"    {_settings.SwitchPrefix}{p.Name} - {p.DescriptionText}"));

                throw new GoCommandoException(
                    $@"The following required parameters are missing:

{requiredParametersMissingString}");
            }
        }

        private Parameter GetParameter(Switch s)
        {
            return Parameters.FirstOrDefault(p => p.MatchesKey(s.Name));
        }

        private void InnerInvoke(IEnumerable<Switch> switches, EnvironmentSettings environmentSettings)
        {
            var commandInstance = CommandInstance;

            var setParameters = new HashSet<Parameter>();

            ResolveParametersFromSwitches(switches, commandInstance, setParameters);

            ResolveParametersFromEnvironmentSettings(environmentSettings, commandInstance, setParameters, Parameters);

            ResolveParametersWithDefaultValues(setParameters, commandInstance);

            commandInstance.Run();
        }

        private static void ResolveParametersFromEnvironmentSettings(EnvironmentSettings environmentSettings,
            ICommand commandInstance, HashSet<Parameter> setParameters, IEnumerable<Parameter> parameters)
        {
            foreach (var parameter in parameters.Where(p => p.AllowAppSetting && !setParameters.Contains(p)))
            {
                if (!environmentSettings.HasAppSetting(parameter.Name)) continue;

                var appSettingValue = environmentSettings.GetAppSetting(parameter.Name);

                SetParameter(commandInstance, setParameters, parameter, appSettingValue);
            }

            foreach (var parameter in parameters.Where(p => p.AllowConnectionString && !setParameters.Contains(p)))
            {
                if (!environmentSettings.HasConnectionString(parameter.Name)) continue;

                var appSettingValue = environmentSettings.GetConnectionString(parameter.Name);

                SetParameter(commandInstance, setParameters, parameter, appSettingValue);
            }

            foreach (var parameter in parameters.Where(p => p.AllowEnvironmentVariable && !setParameters.Contains(p)))
            {
                if (!environmentSettings.HasEnvironmentVariable(parameter.Name)) continue;

                var appSettingValue = environmentSettings.GetEnvironmentVariable(parameter.Name);

                SetParameter(commandInstance, setParameters, parameter, appSettingValue);
            }
        }

        private void ResolveParametersWithDefaultValues(IEnumerable<Parameter> setParameters, ICommand commandInstance)
        {
            foreach (var parameterWithDefaultValue in Parameters.Where(p => p.HasDefaultValue).Except(setParameters))
                parameterWithDefaultValue.ApplyDefaultValue(commandInstance);
        }

        private void ResolveParametersFromSwitches(IEnumerable<Switch> switches, ICommand commandInstance,
            ISet<Parameter> setParameters)
        {
            foreach (var s in switches)
            {
                var correspondingParameter = Parameters.FirstOrDefault(p => p.MatchesKey(s.Name));

                if (correspondingParameter == null)
                    throw new GoCommandoException(
                        $"The switch {_settings.SwitchPrefix}{s.Name} does not correspond to a parameter of the '{Command}' command!");

                SetParameter(commandInstance, setParameters, correspondingParameter, s);
            }
        }

        private static void SetParameter(ICommand commandInstance, ISet<Parameter> setParameters, Parameter parameter,
            Switch s)
        {
            parameter.SetValue(commandInstance, s);
            setParameters.Add(parameter);
        }
        private static void SetParameter(ICommand commandInstance, ISet<Parameter> setParameters, Parameter parameter,
            string value)
        {
            parameter.SetValue(commandInstance, value);
            setParameters.Add(parameter);
        }

        private static bool CanBeResolvedFromEnvironmentSettings(EnvironmentSettings environmentSettings,
            Parameter parameter)
        {
            var name = parameter.Name;

            if (parameter.AllowAppSetting && environmentSettings.HasAppSetting(name))
                return true;

            if (parameter.AllowConnectionString && environmentSettings.HasConnectionString(name))
                return true;

            if (parameter.AllowEnvironmentVariable && environmentSettings.HasEnvironmentVariable(name))
                return true;

            return false;
        }

        private static bool CanBeResolvedFromSwitches(IEnumerable<Switch> switches, Parameter p)
        {
            return switches.Any(s => p.MatchesKey(s.Name));
        }
    }
}