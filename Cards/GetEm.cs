using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnboundLib;
using UnboundLib.Cards;
using UnboundLib.Networking;
using UnityEngine;
using Photon.Pun;
using static BlockTrigger;
using static CardInfoStat;
using static CardThemeColor;


namespace RoundsModcah.Cards
{
    class GetEm : CustomCard
    {
        public override void SetupCard(
            CardInfo cardInfo,
            Gun gun,
            ApplyCardStats cardStats,
            CharacterStatModifiers statModifiers,
            Block block)
        {
            // Edits values on card itself, which are then applied to the player
            // in ApplyCardStats

            UnityEngine.Debug.Log(
                $"[{RoundsModcah.ModInitials}][Card] {GetTitle()} has been setup."
            );

            cardInfo.allowMultiple = false;

            // +15% damage
            gun.damage = 1.15f;

            // -15% movement speed
            statModifiers.movementSpeed = 0.85f;
        }


        public override void OnAddCard(
            Player player,
            Gun gun,
            GunAmmo gunAmmo,
            CharacterData data,
            HealthHandler health,
            Gravity gravity,
            Block block,
            CharacterStatModifiers characterStats)
        {
            // Edits values on player when card is selected

            UnityEngine.Debug.Log(
                $"[{RoundsModcah.ModInitials}][Card] {GetTitle()} has been added to player {player.playerID}."
            );

            // +20 ammo
            gunAmmo.maxAmmo += 20;

            // Add the teleport-on-block effect
            player.gameObject.AddComponent<TeleportBlockEffect>();
        }


        public override void OnRemoveCard(
            Player player,
            Gun gun,
            GunAmmo gunAmmo,
            CharacterData data,
            HealthHandler health,
            Gravity gravity,
            Block block,
            CharacterStatModifiers characterStats)
        {
            // Run when the card is removed from the player

            UnityEngine.Debug.Log(
                $"[{RoundsModcah.ModInitials}][Card] {GetTitle()} has been removed from player {player.playerID}."
            );

            // Remove the teleport effect
            TeleportBlockEffect effect =
                player.gameObject.GetComponent<TeleportBlockEffect>();

            if (effect != null)
            {
                UnityEngine.Object.Destroy(effect);
            }
        }


        protected override string GetTitle()
        {
            return "Get Em";
        }


        protected override string GetDescription()
        {
            return "Blocking teleports you to the nearest enemy. +20 Ammo, +15% Damage, -15% Movement Speed.";
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
                    stat = "Ammo",
                    amount = "+20",
                    simepleAmount = CardInfoStat.SimpleAmount.notAssigned
                },

                new CardInfoStat()
                {
                    positive = true,
                    stat = "Damage",
                    amount = "+15%",
                    simepleAmount = CardInfoStat.SimpleAmount.notAssigned
                },

                new CardInfoStat()
                {
                    positive = false,
                    stat = "Movement Speed",
                    amount = "-15%",
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


    public class TeleportBlockEffect : MonoBehaviour
    {
        private Player _player;
        private Block _block;


        private void Start()
        {
            _player = GetComponent<Player>();

            if (_player == null)
            {
                UnityEngine.Debug.LogError(
                    "[RoundsModcah] TeleportBlockEffect could not find Player."
                );

                return;
            }

            _block = _player.data.block;

            if (_block == null)
            {
                UnityEngine.Debug.LogError(
                    "[RoundsModcah] TeleportBlockEffect could not find Block."
                );

                return;
            }

            _block.BlockAction += OnBlock;
        }


        private void OnBlock(BlockTrigger.BlockTriggerType trigger)
        {
            if (_player == null)
                return;

            if (_player.data.dead)
                return;

            Player target = FindClosestEnemy();

            if (target == null)
                return;

            // Teleport directly to the enemy
            _player.transform.position = target.transform.position;
        }


        private Player FindClosestEnemy()
        {
            Player closest = null;

            float closestDistance = float.MaxValue;

            foreach (Player other in PlayerManager.instance.players)
            {
                if (other == null)
                    continue;

                // Don't target yourself
                if (other == _player)
                    continue;

                // Don't target dead players
                if (other.data.dead)
                    continue;

                // Don't target teammates
                if (other.teamID == _player.teamID)
                    continue;

                float distance = Vector2.Distance(
                    _player.transform.position,
                    other.transform.position
                );

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = other;
                }
            }

            return closest;
        }


        private void OnDestroy()
        {
            if (_block != null)
            {
                _block.BlockAction -= OnBlock;
            }
        }
    }
}