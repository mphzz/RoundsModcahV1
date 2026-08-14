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
    class SugarRushPill : CustomCard
    {
        public override void SetupCard(CardInfo cardInfo, Gun gun, ApplyCardStats cardStats, CharacterStatModifiers statModifiers, Block block)
        {
            //Edits values on card itself, which are then applied to the player in `ApplyCardStats`
            UnityEngine.Debug.Log($"[{RoundsModcah.ModInitials}][Card] {GetTitle()} has been setup.");

            // -15% movement speed (base penalty)
            statModifiers.movementSpeed = 0.85f;
        }
        public override void OnAddCard(Player player, Gun gun, GunAmmo gunAmmo, CharacterData data, HealthHandler health, Gravity gravity, Block block, CharacterStatModifiers characterStats)
        {
            //Edits values on player when card is selected
            UnityEngine.Debug.Log($"[{RoundsModcah.ModInitials}][Card] {GetTitle()} has been added to player {player.playerID}.");

            SugarRushEffect effect = player.gameObject.AddComponent<SugarRushEffect>();
            effect.Setup(block, characterStats);
        }
        public override void OnRemoveCard(Player player, Gun gun, GunAmmo gunAmmo, CharacterData data, HealthHandler health, Gravity gravity, Block block, CharacterStatModifiers characterStats)
        {
            //Run when the card is removed from the player
            UnityEngine.Debug.Log($"[{RoundsModcah.ModInitials}][Card] {GetTitle()} has been removed from player {player.playerID}.");

            SugarRushEffect effect = player.gameObject.GetComponent<SugarRushEffect>();
            if (effect != null)
            {
                UnityEngine.Object.Destroy(effect);
            }
        }


        protected override string GetTitle()
        {
            return "Sugar Rush Pill";
        }
        protected override string GetDescription()
        {
            return "-15% Movement Speed. Blocking grants +400% Movement Speed, decaying over 10s. 15s cooldown.";
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
                    positive = false,
                    stat = "Movement Speed",
                    amount = "-15%",
                    simepleAmount = CardInfoStat.SimpleAmount.notAssigned
                },

                new CardInfoStat()
                {
                    positive = true,
                    stat = "Block Speed Boost",
                    amount = "+400%",
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

    public class SugarRushEffect : MonoBehaviour
    {
        private Block _block;
        private CharacterStatModifiers _statModifiers;

        private const float BoostAmount = 4.0f;       // +400%
        private const float DecayDuration = 10f;      // seconds
        private const float Cooldown = 15f;            // seconds

        private float _cooldownRemaining = 0f;
        private float _decayTimeRemaining = 0f;
        private float _lastAppliedBoost = 0f;

        public void Setup(Block block, CharacterStatModifiers statModifiers)
        {
            _block = block;
            _statModifiers = statModifiers;

            if (_block != null)
            {
                _block.BlockAction += OnBlock;
            }
        }

        private void OnBlock(BlockTrigger.BlockTriggerType trigger)
        {
            if (_cooldownRemaining > 0f) return;

            _decayTimeRemaining = DecayDuration;
            _cooldownRemaining = Cooldown;
        }

        private void Update()
        {
            if (_cooldownRemaining > 0f)
            {
                _cooldownRemaining -= Time.deltaTime;
            }

            float targetBoost = 0f;

            if (_decayTimeRemaining > 0f)
            {
                _decayTimeRemaining -= Time.deltaTime;
                float t = Mathf.Clamp01(_decayTimeRemaining / DecayDuration);
                targetBoost = BoostAmount * t;
            }

            // Remove our previous contribution, then apply the new one.
            // This only ever touches OUR delta, leaving other cards' effects intact.
            _statModifiers.movementSpeed -= _lastAppliedBoost;
            _statModifiers.movementSpeed += targetBoost;
            _lastAppliedBoost = targetBoost;
        }

        private void OnDestroy()
        {
            if (_block != null)
            {
                _block.BlockAction -= OnBlock;
            }

            // Clean up our contribution when the component is destroyed
            if (_statModifiers != null)
            {
                _statModifiers.movementSpeed -= _lastAppliedBoost;
            }
        }
    }
}