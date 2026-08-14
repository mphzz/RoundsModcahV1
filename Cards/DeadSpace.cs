using System;
using HarmonyLib;
using UnboundLib;
using UnboundLib.Cards;
using UnityEngine;

namespace RoundsModcah.Cards
{
    class DeadSpace : CustomCard
    {
        public override void SetupCard(CardInfo cardInfo, Gun gun, ApplyCardStats cardStats, CharacterStatModifiers statModifiers, Block block)
        {
            UnityEngine.Debug.Log($"[{RoundsModcah.ModInitials}][Card] {GetTitle()} has been setup.");
        }

        public override void OnAddCard(Player player, Gun gun, GunAmmo gunAmmo, CharacterData data, HealthHandler health, Gravity gravity, Block block, CharacterStatModifiers characterStats)
        {
            UnityEngine.Debug.Log($"[{RoundsModcah.ModInitials}][Card] {GetTitle()} has been added to player {player.playerID}.");

            // Marker component so our Harmony patch knows this player's kills should prevent revives
            player.gameObject.AddComponent<DeadSpaceMarker>();
        }

        public override void OnRemoveCard(Player player, Gun gun, GunAmmo gunAmmo, CharacterData data, HealthHandler health, Gravity gravity, Block block, CharacterStatModifiers characterStats)
        {
            UnityEngine.Debug.Log($"[{RoundsModcah.ModInitials}][Card] {GetTitle()} has been removed from player {player.playerID}.");

            DeadSpaceMarker marker = player.gameObject.GetComponent<DeadSpaceMarker>();
            if (marker != null)
            {
                UnityEngine.Object.Destroy(marker);
            }
        }

        protected override string GetTitle() => "Dead Space";
        protected override string GetDescription() => "Kills from your bullets cannot be revived.";
        protected override GameObject GetCardArt() => null;
        protected override CardInfo.Rarity GetRarity() => CardInfo.Rarity.Common;

        protected override CardInfoStat[] GetStats()
        {
            return new CardInfoStat[]
            {
                new CardInfoStat()
                {
                    positive = true,
                    stat = "Effect",
                    amount = "No Revive",
                    simepleAmount = CardInfoStat.SimpleAmount.notAssigned
                }
            };
        }

        protected override CardThemeColor.CardThemeColorType GetTheme() => CardThemeColor.CardThemeColorType.ColdBlue;
        public override string GetModName() => RoundsModcah.ModInitials;
    }

    // Marker component: its presence on a player means "this player's kills prevent revive"
    public class DeadSpaceMarker : MonoBehaviour { }

    [HarmonyPatch(typeof(HealthHandler), "DoDamage")]
    class DeadSpace_DoDamagePatch
    {
        static void Prefix(HealthHandler __instance, Vector2 damage, Player damagingPlayer, bool lethal)
        {
            if (damagingPlayer == null) return;
            if (!lethal) return;

            DeadSpaceMarker marker = damagingPlayer.gameObject.GetComponent<DeadSpaceMarker>();
            if (marker == null) return;

            CharacterData data = __instance.GetComponent<CharacterData>();
            if (data == null) return;
            if (data.dead) return;

            // Is this hit actually the killing blow?
            if (data.health - damage.magnitude < 0f)
            {
                // Force permanent death instead of the revivable "downed" state
                data.stats.remainingRespawns = 0;
            }
        }
    }
}