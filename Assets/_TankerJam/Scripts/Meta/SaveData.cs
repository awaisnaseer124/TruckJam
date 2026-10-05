using System;
using System.Collections.Generic;

namespace TankerJam.Meta
{
    public enum BoosterKind { Vip, Extra }

    /// <summary>Everything persisted about the player. Versioned so later builds can migrate old saves.</summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;
        /// <summary>0-based position of the next level to play.</summary>
        public int Level;
        public int Coins;
        public int Vip;
        public int Extra;
        public bool VipIntroduced;
        public bool ExtraIntroduced;
        /// <summary>Best stars per level position (0 = not cleared).</summary>
        public List<int> Stars = new List<int>();
    }

    /// <summary>Where the save lives. PlayerPrefs for the MVP; a cloud save can implement the same interface.</summary>
    public interface IPersistence
    {
        string Load();
        void Save(string json);
        void Clear();
    }
}
