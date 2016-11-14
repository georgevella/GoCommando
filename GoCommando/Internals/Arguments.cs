using System;
using System.Collections.Generic;
using System.Linq;

namespace GoCommando.Internals
{
    internal class Arguments
    {
        readonly Settings _settings;
        private readonly Dictionary<string, Switch> _switches;
        private readonly List<KeyValuePair<string, string>> _rawSwitchList;

        public Arguments(string command, IEnumerable<KeyValuePair<string, string>> rawSwitchList, Settings settings)
        {
            _settings = settings;
            _rawSwitchList = rawSwitchList.ToList();

            _switches = _rawSwitchList.GroupBy(s => s.Key, s => s.Value)
                .ToDictionary(x => x.Key, x => new Switch(x.Key, x.Where(s => !string.IsNullOrEmpty(s))));

            Command = command;
        }

        public string Command { get; }

        public TValue Get<TValue>(string key)
        {
            var desiredType = typeof(TValue);

            try
            {
                if (desiredType == typeof(bool))
                {
                    return (TValue)Convert.ChangeType(_switches.Any(s => s.Key == key), desiredType);
                }

                Switch relevantSwitch = null;

                if (_switches.TryGetValue(key, out relevantSwitch))
                {
                    // TODO: this ought to be handled better
                    //return (TValue)Convert.ChangeType(relevantSwitch.Value, desiredType);
                    return (TValue)Convert.ChangeType(relevantSwitch.Values.FirstOrDefault(), desiredType);
                }

                throw new GoCommandoException($"Could not find switch '{key}'");
            }
            catch (Exception exception)
            {
                throw new FormatException($"Could not get switch '{key}' as a {desiredType}", exception);
            }
        }

        public IEnumerable<Switch> Switches => _switches.Values;

        public Switch GetSwitch(string key)
        {
            return _switches.ContainsKey(key) ? _switches[key] : null;
        }

        //        public override string ToString()
        //        {
        //            return $@"{Command}

        //{string.Join(Environment.NewLine, _rawSwitchList.Select(s => "    " + s.Key))}";
        //        }

        /// <summary>
        ///     Returns all arguments that are declared multiple times on the command line.
        /// </summary>
        //public IEnumerable<string> GetMultiValueArguments()
        //{
        //    return _switches.Where(item => item.Value.IsMultiValue).Select(x => x.Key).ToList();
        //}

        public bool IsSwitchSet(string switchIdentifier)
        {
            return _switches.ContainsKey(switchIdentifier);
        }

        public int Count => _switches.Count;
    }
}