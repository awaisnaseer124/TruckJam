using UnityEngine;

namespace TankerJam.Meta
{
    /// <summary>Economy tuning (GDD starting values). No ads or IAP: coins come from wins only.</summary>
    [CreateAssetMenu(menuName = "Tanker Jam/Economy Config", fileName = "EconomyConfig")]
    public sealed class EconomyConfig : ScriptableObject
    {
        [Header("Start")]
        public int StartVip = 2;
        public int StartExtra = 2;
        public int StartCoins = 0;

        [Header("Prices (coins)")]
        public int VipPrice = 900;
        public int ExtraPrice = 600;

        [Header("Rewards")]
        public int CoinsPerWin = 40;
        public int HardMultiplier = 2;

        [Header("Introductions (free tutorial use)")]
        public int IntroGrant = 1;

        public int Price(BoosterKind kind) => kind == BoosterKind.Vip ? VipPrice : ExtraPrice;
    }
}
