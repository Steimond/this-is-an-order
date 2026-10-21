using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Order.Contracts;
using UnityEngine;

namespace Order.Bootstrap
{
    public sealed class PlayerPrefsSettingsStore : ISettingsStore
    {
        private const string Prefix = "Order.Settings.v1.";
        public SettingsSnapshot Load()
        {
            var options = new Dictionary<string, float>();
            foreach (var pair in Split(PlayerPrefs.GetString(Prefix + "Options", "")))
                if (float.TryParse(pair.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) options[pair.Key] = value;
            return new SettingsSnapshot(
                PlayerPrefs.GetFloat(Prefix + "MasterVolume", 1f),
                (FactionId)PlayerPrefs.GetInt(Prefix + "AssistantFaction", 0),
                options, Split(PlayerPrefs.GetString(Prefix + "Keys", "")));
        }
        public void Save(SettingsSnapshot settings)
        {
            PlayerPrefs.SetFloat(Prefix + "MasterVolume", settings.MasterVolume);
            PlayerPrefs.SetInt(Prefix + "AssistantFaction", (int)settings.AssistantFaction);
            var options = new StringBuilder();
            foreach (var pair in settings.Options) options.Append(pair.Key).Append('=').Append(pair.Value.ToString(CultureInfo.InvariantCulture)).Append(';');
            PlayerPrefs.SetString(Prefix + "Options", options.ToString());
            var keys = new StringBuilder();
            foreach (var pair in settings.Keys) keys.Append(pair.Key).Append('=').Append(pair.Value).Append(';');
            PlayerPrefs.SetString(Prefix + "Keys", keys.ToString());
            PlayerPrefs.Save();
        }

        // "id=value;id=value"; ids and values never contain '=' or ';'.
        private static Dictionary<string, string> Split(string stored)
        {
            var result = new Dictionary<string, string>();
            foreach (var entry in stored.Split(';'))
            {
                var separator = entry.IndexOf('=');
                if (separator > 0) result[entry.Substring(0, separator)] = entry.Substring(separator + 1);
            }
            return result;
        }
    }
}
