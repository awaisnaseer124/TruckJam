using UnityEngine;

namespace TankerJam.Meta
{
    public sealed class PlayerPrefsPersistence : IPersistence
    {
        readonly string key;

        public PlayerPrefsPersistence(string key = "tj.save") { this.key = key; }

        public string Load() => PlayerPrefs.GetString(key, null);

        public void Save(string json)
        {
            PlayerPrefs.SetString(key, json);
            PlayerPrefs.Save();
        }

        public void Clear()
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }

    /// <summary>In-memory persistence for tests.</summary>
    public sealed class MemoryPersistence : IPersistence
    {
        public string Json;
        public string Load() => Json;
        public void Save(string json) => Json = json;
        public void Clear() => Json = null;
    }
}
