using Photon.Pun;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnboundLib;
using UnboundLib.Cards;
using UnityEngine;


namespace RoundsModcah.Cards
{
    class Decoy : CustomCard
    {
        public override void SetupCard(CardInfo cardInfo, Gun gun, ApplyCardStats cardStats, CharacterStatModifiers statModifiers, Block block)
        {
            //Edits values on card itself, which are then applied to the player in `ApplyCardStats`
            UnityEngine.Debug.Log($"[{RoundsModcah.ModInitials}][Card] {GetTitle()} has been setup.");
        }
        public override void OnAddCard(Player player, Gun gun, GunAmmo gunAmmo, CharacterData data, HealthHandler health, Gravity gravity, Block block, CharacterStatModifiers characterStats)
        {
            //Edits values on player when card is selected
            UnityEngine.Debug.Log($"[{RoundsModcah.ModInitials}][Card] {GetTitle()} has been added to player {player.playerID}.");

            DecoySpawner spawner = player.gameObject.AddComponent<DecoySpawner>();
            spawner.Setup(player, block);
        }
        public override void OnRemoveCard(Player player, Gun gun, GunAmmo gunAmmo, CharacterData data, HealthHandler health, Gravity gravity, Block block, CharacterStatModifiers characterStats)
        {
            //Run when the card is removed from the player
            UnityEngine.Debug.Log($"[{RoundsModcah.ModInitials}][Card] {GetTitle()} has been removed from player {player.playerID}.");

            DecoySpawner spawner = player.gameObject.GetComponent<DecoySpawner>();
            if (spawner != null)
            {
                UnityEngine.Object.Destroy(spawner);
            }
        }


        protected override string GetTitle()
        {
            return "Decoy";
        }
        protected override string GetDescription()
        {
            return "Blocking leaves behind a stationary decoy of yourself for a few seconds.";
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
                    stat = "Effect",
                    amount = "Decoy on Block",
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

    // Handles the block-trigger and cooldown for spawning decoys.
    public class DecoySpawner : MonoBehaviour
    {
        private Player _player;
        private Block _block;

        private const float Cooldown = 13f;
        private const float DecoyDuration = 4f;

        private float _cooldownRemaining = 0f;

        public void Setup(Player player, Block block)
        {
            _player = player;
            _block = block;

            if (_block != null)
            {
                _block.BlockAction += OnBlock;
            }
        }

        private void OnBlock(BlockTrigger.BlockTriggerType trigger)
        {
            if (_cooldownRemaining > 0f) return;
            if (_player == null) return;

            _cooldownRemaining = Cooldown;
            SpawnDecoy();
        }

        private void Update()
        {
            if (_cooldownRemaining > 0f)
            {
                _cooldownRemaining -= Time.deltaTime;
            }
        }

        private void SpawnDecoy()
        {
            Vector3 spawnPos = _player.transform.position;
            spawnPos.z = 0f; // avoid the Z-depth culling issue found earlier

            GameObject decoyObj = UnityEngine.Object.Instantiate(_player.gameObject, spawnPos, _player.transform.rotation);
            decoyObj.name = "DecoyClone";

            // --- Networking safety: destroy anything Photon-related. A cloned PhotonView
            // is not a valid networked object and would be dangerous to leave active. ---
            PhotonView pv = decoyObj.GetComponent<PhotonView>();
            if (pv != null) UnityEngine.Object.Destroy(pv);

            ChildRPC crpc = decoyObj.GetComponent<ChildRPC>();
            if (crpc != null) UnityEngine.Object.Destroy(crpc);

            // --- Purely visual: strip all colliders so it can't physically interact with anything ---
            foreach (Collider2D col in decoyObj.GetComponentsInChildren<Collider2D>(true))
            {
                UnityEngine.Object.Destroy(col);
            }

            // --- Hide UI overlays that shouldn't appear on a silent decoy (health bar,
            // name tag, chat bubble, minimap node) ---
            Transform wobbleObjects = decoyObj.transform.Find("WobbleObjects");
            if (wobbleObjects != null) wobbleObjects.gameObject.SetActive(false);

            Transform levelMapper = decoyObj.transform.Find("LevelMapper");
            if (levelMapper != null) levelMapper.gameObject.SetActive(false);

            // --- Freeze everything else: disable every remaining script (movement, IK,
            // input, health, networking, animation) so the clone just sits still in
            // whatever pose it was in at the moment of cloning, without erroring or trying
            // to act like a real player. Native rendering components (SpriteRenderer,
            // ParticleSystem, etc.) are untouched and keep showing exactly how they looked. ---
            foreach (MonoBehaviour mb in decoyObj.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                if (mb is DecoyLifetime) continue; // don't disable our own lifetime script once added below
                mb.enabled = false;
            }

            DecoyLifetime lifetime = decoyObj.AddComponent<DecoyLifetime>();
            lifetime.duration = DecoyDuration;

            UnityEngine.Debug.Log($"[RM][Decoy] Spawned real character clone decoy at {spawnPos} for player {_player.playerID}.");
        }

        private void OnDestroy()
        {
            if (_block != null)
            {
                _block.BlockAction -= OnBlock;
            }
        }
    }

    // Destroys the decoy after its duration, with a brief fade-out at the end.
    public class DecoyLifetime : MonoBehaviour
    {
        public float duration = 4f;

        private void Start()
        {
            StartCoroutine(LifetimeRoutine());
        }

        private IEnumerator LifetimeRoutine()
        {
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            Color[] baseColors = renderers.Select(r => r.color).ToArray();

            float waitTime = Mathf.Max(0f, duration - 0.5f);
            yield return new WaitForSeconds(waitTime);

            float fadeElapsed = 0f;
            const float fadeDuration = 0.5f;
            while (fadeElapsed < fadeDuration)
            {
                fadeElapsed += Time.deltaTime;
                float t = fadeElapsed / fadeDuration;

                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] == null) continue;
                    Color c = baseColors[i];
                    c.a = Mathf.Lerp(baseColors[i].a, 0f, t);
                    renderers[i].color = c;
                }
                yield return null;
            }

            Destroy(gameObject);
        }
    }
}