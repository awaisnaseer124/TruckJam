// The player's persistent state and the rules that change it: level progression, coins, booster inventory,
// stars and booster introductions. Every mutation saves immediately (small JSON; levels are short).
using System;
using UnityEngine;

namespace TankerJam.Meta
{
    public sealed class PlayerProgress
    {
        readonly IPersistence store;
        readonly EconomyConfig economy;
        SaveData data;

        /// <summary>Coins, boosters or level changed.</summary>
        public event Action Changed;

        public PlayerProgress(IPersistence persistence, EconomyConfig economyConfig)
        {
            store = persistence;
            economy = economyConfig;
            data = LoadOrCreate();
        }

        SaveData LoadOrCreate()
        {
            string json = store.Load();
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var loaded = JsonUtility.FromJson<SaveData>(json);
                    if (loaded != null && loaded.Version <= SaveData.CurrentVersion) return Migrate(loaded);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Tanker Jam: save data unreadable, starting fresh. " + e.Message);
                }
            }
            return new SaveData
            {
                Coins = economy.StartCoins,
                Vip = economy.StartVip,
                Extra = economy.StartExtra,
            };
        }

        static SaveData Migrate(SaveData d)
        {
            // Version 1 is the first format; future versions upgrade here.
            d.Stars ??= new System.Collections.Generic.List<int>();
            d.Version = SaveData.CurrentVersion;
            return d;
        }

        void Commit()
        {
            store.Save(JsonUtility.ToJson(data));
            Changed?.Invoke();
        }

        public EconomyConfig Economy => economy;
        public int Level => data.Level;
        public int Coins => data.Coins;
        public int Boosters(BoosterKind kind) => kind == BoosterKind.Vip ? data.Vip : data.Extra;
        public int StarsFor(int level) => level < data.Stars.Count ? data.Stars[level] : 0;

        /// <summary>Coins a win on this level is worth.</summary>
        public int RewardFor(bool hard) => economy.CoinsPerWin * (hard ? economy.HardMultiplier : 1);

        /// <summary>3 stars without boosters, 2 with one, 1 with more.</summary>
        public static int StarsForBoosters(int boostersUsed) => boostersUsed == 0 ? 3 : boostersUsed == 1 ? 2 : 1;

        /// <summary>Records a win: coins, best stars, next level. Returns the coins earned.</summary>
        public int CompleteLevel(int level, bool hard, int boostersUsed)
        {
            int reward = RewardFor(hard);
            data.Coins += reward;
            while (data.Stars.Count <= level) data.Stars.Add(0);
            data.Stars[level] = Math.Max(data.Stars[level], StarsForBoosters(boostersUsed));
            if (level == data.Level) data.Level++;
            Commit();
            return reward;
        }

        public bool UseBooster(BoosterKind kind)
        {
            if (Boosters(kind) <= 0) return false;
            if (kind == BoosterKind.Vip) data.Vip--; else data.Extra--;
            Commit();
            return true;
        }

        public bool CanAfford(BoosterKind kind) => data.Coins >= economy.Price(kind);

        /// <summary>Buys one booster with coins. Returns false if the player can't afford it.</summary>
        public bool BuyBooster(BoosterKind kind)
        {
            int price = economy.Price(kind);
            if (data.Coins < price) return false;
            data.Coins -= price;
            if (kind == BoosterKind.Vip) data.Vip++; else data.Extra++;
            Commit();
            return true;
        }

        /// <summary>First time a booster is introduced: grant the free tutorial use. Returns true if granted now.</summary>
        public bool Introduce(BoosterKind kind)
        {
            bool already = kind == BoosterKind.Vip ? data.VipIntroduced : data.ExtraIntroduced;
            if (already) return false;
            if (kind == BoosterKind.Vip) { data.VipIntroduced = true; data.Vip += economy.IntroGrant; }
            else { data.ExtraIntroduced = true; data.Extra += economy.IntroGrant; }
            Commit();
            return true;
        }

        public bool IsIntroduced(BoosterKind kind) => kind == BoosterKind.Vip ? data.VipIntroduced : data.ExtraIntroduced;

        /// <summary>Debug: jump to a level (dev menu).</summary>
        public void SetLevel(int level)
        {
            data.Level = Math.Max(0, level);
            Commit();
        }

        public void Reset()
        {
            store.Clear();
            data = LoadOrCreate();
            Commit();
        }
    }
}
