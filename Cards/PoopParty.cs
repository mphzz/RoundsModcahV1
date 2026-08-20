using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnboundLib;
using UnboundLib.Cards;
using UnityEngine;


namespace RoundsModcah.Cards
{
    class PoopParty : CustomCard
    {
        public override void SetupCard(CardInfo cardInfo, Gun gun, ApplyCardStats cardStats, CharacterStatModifiers statModifiers, Block block)
        {
            //Edits values on card itself, which are then applied to the player in `ApplyCardStats`
            UnityEngine.Debug.Log($"[{RoundsModcah.ModInitials}][Card] {GetTitle()} has been setup.");

            // -95% damage
            gun.damage = 0.05f;

            // Brown bullets
            
        }
        public override void OnAddCard(Player player, Gun gun, GunAmmo gunAmmo, CharacterData data, HealthHandler health, Gravity gravity, Block block, CharacterStatModifiers characterStats)
        {
            //Edits values on player when card is selected
            UnityEngine.Debug.Log($"[{RoundsModcah.ModInitials}][Card] {GetTitle()} has been added to player {player.playerID}.");

            // +60 bullets
            gunAmmo.maxAmmo += 60;
            gun.projectileColor = new Color(0.55f, 0.35f, 0.15f);
            PoopPartyEffect effect = player.gameObject.AddComponent<PoopPartyEffect>();
            effect.Setup(gun, data);
        }
        public override void OnRemoveCard(Player player, Gun gun, GunAmmo gunAmmo, CharacterData data, HealthHandler health, Gravity gravity, Block block, CharacterStatModifiers characterStats)
        {
            //Run when the card is removed from the player
            UnityEngine.Debug.Log($"[{RoundsModcah.ModInitials}][Card] {GetTitle()} has been removed from player {player.playerID}.");

            PoopPartyEffect effect = player.gameObject.GetComponent<PoopPartyEffect>();
            if (effect != null)
            {
                UnityEngine.Object.Destroy(effect);
            }
        }


        protected override string GetTitle()
        {
            return "Poop Party";
        }
        protected override string GetDescription()
        {
            return "+60 Bullets, -95% Damage. +200% Attack Speed and Auto-Fire while standing still. Brown bullets.";
        }
        protected override GameObject GetCardArt()
        {
            return null;
        }
        protected override CardInfo.Rarity GetRarity()
        {
            return CardInfo.Rarity.Common;
        }
        protected override CardInfoStat[] GetStats()
        {
            return new CardInfoStat[]
            {
                new CardInfoStat()
                {
                    positive = true,
                    stat = "Bullets",
                    amount = "+60",
                    simepleAmount = CardInfoStat.SimpleAmount.notAssigned
                },

                new CardInfoStat()
                {
                    positive = false,
                    stat = "Damage",
                    amount = "-95%",
                    simepleAmount = CardInfoStat.SimpleAmount.notAssigned
                },

                new CardInfoStat()
                {
                    positive = true,
                    stat = "Attack Speed (Standing Still)",
                    amount = "+200%",
                    simepleAmount = CardInfoStat.SimpleAmount.notAssigned
                }
            };
        }
        protected override CardThemeColor.CardThemeColorType GetTheme()
        {
            return CardThemeColor.CardThemeColorType.ColdBlue;
        }
        public override string GetModName()
        {
            return RoundsModcah.ModInitials;
        }
    }

    public class PoopPartyEffect : MonoBehaviour
    {
        private Gun _gun;
        private CharacterData _data;
        private Vector3 _lastPosition;

        private const float StillnessThreshold = 0.02f; // distance per frame considered "not moving"
        private const float BoostedAttackSpeed = 3f;     // +200% = 3x speed (higher = faster, confirmed via IsReady())
        private const float NormalAttackSpeed = 1f;

        public void Setup(Gun gun, CharacterData data)
        {
            _gun = gun;
            _data = data;
            _lastPosition = gun.transform.root.position;
        }

        private void Update()
        {
            if (_gun == null || _data == null) return;

            Vector3 currentPosition = _gun.transform.root.position;
            float distanceMoved = Vector3.Distance(currentPosition, _lastPosition);
            bool standingStill = distanceMoved <= StillnessThreshold;

            // Confirmed via IsReady(): higher attackSpeedMultiplier = ready sooner = faster fire rate
            _gun.attackSpeedMultiplier = standingStill ? BoostedAttackSpeed : NormalAttackSpeed;

            // Manual auto-fire loop: the base weapon is semi-auto and only fires on button-down,
            // so we poll the held state ourselves and call the gun's public Attack() directly
            // whenever it's ready, bypassing the vanilla single-press-only behavior.
            if (standingStill && _data.input != null && _data.input.shootIsPressed)
            {
                if (_gun.IsReady(0f))
                {
                    _gun.Attack(0f, false, 1f, 1f, true);
                }
            }

            _lastPosition = currentPosition;
        }
    }
}