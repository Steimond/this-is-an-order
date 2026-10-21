using Order.Contracts;
using UnityEngine;

namespace Order.Bootstrap
{
    public sealed class PlayerPrefsSettingsStore : ISettingsStore
    {
        private const string Prefix = "Order.Settings.v1.";
        public SettingsSnapshot Load() => new SettingsSnapshot(
            PlayerPrefs.GetFloat(Prefix + "MasterVolume", 1f),
            (FactionId)PlayerPrefs.GetInt(Prefix + "AssistantFaction", 0));
        public void Save(SettingsSnapshot settings)
        {
            PlayerPrefs.SetFloat(Prefix + "MasterVolume", settings.MasterVolume);
            PlayerPrefs.SetInt(Prefix + "AssistantFaction", (int)settings.AssistantFaction);
            PlayerPrefs.Save();
        }
    }
}
