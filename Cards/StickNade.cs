using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnboundLib;
using UnboundLib.Cards;
using UnityEngine;
using HarmonyLib;
using Photon.Pun;
using Sonigon;
using SoundImplementation;


namespace RoundsModcah.Cards
{
    class StickNade : CustomCard
    {
        public override void SetupCard(CardInfo cardInfo, Gun gun, ApplyCardStats cardStats, CharacterStatModifiers statModifiers, Block block)
        {
            //Edits values on card itself, which are then applied to the player in `ApplyCardStats`
            UnityEngine.Debug.Log($"[{RoundsModcah.ModInitials}][Card] {GetTitle()} has been setup.");

            // Normal bullet damage - now dealt directly on a player hit (see the Harmony
            // patch below), in addition to the later explosion damage
            gun.damage = 1f;
        }
        public override void OnAddCard(Player player, Gun gun, GunAmmo gunAmmo, CharacterData data, HealthHandler health, Gravity gravity, Block block, CharacterStatModifiers characterStats)
        {
            //Edits values on player when card is selected
            UnityEngine.Debug.Log($"[{RoundsModcah.ModInitials}][Card] {GetTitle()} has been added to player {player.playerID}.");

            StickNadeCardMarker marker = player.gameObject.AddComponent<StickNadeCardMarker>();
            marker.fuseTime = 1.5f;
            marker.explosionDamage = 60f;
            marker.explosionRadius = 4f;
            marker.explosionForce = 30f;
        }
        public override void OnRemoveCard(Player player, Gun gun, GunAmmo gunAmmo, CharacterData data, HealthHandler health, Gravity gravity, Block block, CharacterStatModifiers characterStats)
        {
            //Run when the card is removed from the player
            UnityEngine.Debug.Log($"[{RoundsModcah.ModInitials}][Card] {GetTitle()} has been removed from player {player.playerID}.");

            StickNadeCardMarker marker = player.gameObject.GetComponent<StickNadeCardMarker>();
            if (marker != null)
            {
                UnityEngine.Object.Destroy(marker);
            }
        }


        protected override string GetTitle()
        {
            return "Stick Nade";
        }
        protected override string GetDescription()
        {
            return "Bullets stick to whatever they hit, then explode after a short fuse.";
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
                    amount = "Sticky Explosive",
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

    // Marker attached to the PLAYER while they hold the card - stores the fuse/damage/radius
    // config so the bullet-spawning patch below knows this player's bullets should stick.
    public class StickNadeCardMarker : MonoBehaviour
    {
        public float fuseTime = 1.5f;
        public float explosionDamage = 60f;
        public float explosionRadius = 4f;
        public float explosionForce = 30f;
    }

    // Marker attached to each individual BULLET after it's spawned, carrying its own copy
    // of the config (in case the card is removed mid-flight) and tracking stuck/exploded state.
    public class StickNadeProjectile : MonoBehaviour
    {
        public bool stuck = false;
        public float fuseTime = 1.5f;
        public float explosionDamage = 60f;
        public float explosionRadius = 4f;
        public float explosionForce = 30f;
        public Player ownPlayer;
        public GameObject visualMarker;
        public Renderer visualMarkerRenderer;

        // Safety net: if this component (and its host GameObject) gets destroyed by
        // ANYTHING other than our own ExplodeAfterFuse completing normally - e.g. the
        // bullet's original destroy logic firing from an unrelated collision - make sure
        // our separately-spawned visual marker sphere doesn't get left behind forever.
        private void OnDestroy()
        {
            if (visualMarker != null)
            {
                Destroy(visualMarker);
            }
        }

        // Real, confirmed explosion sound - pulled from the firing player's own gun via
        // WeaponHandler.gun.soundGun.soundImpactModifierDamageToExplosionHuge, a purpose-built
        // explosion SoundImpactModifier the game itself uses (not a guessed borrow).
        private static SoundEvent GetExplosionSound(Player player)
        {
            if (player == null || player.data == null || player.data.weaponHandler == null) return null;
            if (player.data.weaponHandler.gun == null) return null;
            if (player.data.weaponHandler.gun.soundGun == null) return null;

            SoundImpactModifier huge = player.data.weaponHandler.gun.soundGun.soundImpactModifierDamageToExplosionHuge;
            if (huge == null) return null;

            return huge.impactEnvironment;
        }

        // Countdown beep - loaded from our own bundled .wav file rather than borrowing
        // from the game's sound system, since nothing suitable was found there.
        private static AudioClip _beepClip;
        private static bool _beepLoadStarted = false;

        private static void EnsureBeepLoading(MonoBehaviour runner)
        {
            if (_beepLoadStarted) return;
            _beepLoadStarted = true;

            // Extract the embedded .wav to a stable temp location that won't get wiped
            // by r2modmanPlus's build/deploy sync (unlike the plugin folder itself).
            string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sticknade_beep.wav");

            try
            {
                if (!System.IO.File.Exists(tempPath))
                {
                    System.Reflection.Assembly asm = System.Reflection.Assembly.GetExecutingAssembly();
                    string resourceName = "RoundsModcah.sticknade_beep.wav"; // adjust namespace prefix if needed

                    using (System.IO.Stream resourceStream = asm.GetManifestResourceStream(resourceName))
                    {
                        if (resourceStream == null)
                        {
                            // Fallback: list all embedded resource names to help diagnose a naming mismatch
                            string allNames = string.Join(", ", asm.GetManifestResourceNames());
                            UnityEngine.Debug.LogWarning($"[RM][StickNade] Embedded resource '{resourceName}' not found. Available: {allNames}");
                            return;
                        }

                        using (System.IO.FileStream fileStream = System.IO.File.Create(tempPath))
                        {
                            resourceStream.CopyTo(fileStream);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[RM][StickNade] Failed to extract embedded beep resource: {e}");
                return;
            }

            runner.StartCoroutine(LoadBeepClip(tempPath));
        }

        private static IEnumerator LoadBeepClip(string path)
        {
            UnityEngine.Debug.Log($"[RM][StickNade] Looking for beep at: {path}");
            UnityEngine.Debug.Log($"[RM][StickNade] File exists on disk: {System.IO.File.Exists(path)}");

            string url = "file:///" + path.Replace("\\", "/");
            using (UnityEngine.Networking.UnityWebRequest www =
                   UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV))
            {
                yield return www.SendWebRequest();

                if (www.isNetworkError || www.isHttpError)
                {
                    UnityEngine.Debug.LogWarning($"[RM][StickNade] Failed to load beep sound: {www.error}");
                }
                else
                {
                    _beepClip = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(www);
                    UnityEngine.Debug.Log($"[RM][StickNade] Beep clip loaded successfully: {_beepClip != null}");
                }
            }
        }

        private static void PlayBeepAt(Vector3 position)
        {
            if (_beepClip == null) return;

            GameObject tempAudio = new GameObject("StickNadeBeep");
            tempAudio.transform.position = position;
            AudioSource src = tempAudio.AddComponent<AudioSource>();
            src.clip = _beepClip;
            src.volume = 1f;
            src.spatialBlend = 0f; // fully 2D, always audible regardless of camera distance
            UnityEngine.Debug.Log($"[RM][StickNade] AudioListener.volume={AudioListener.volume}, AudioListener.pause={AudioListener.pause}");
            AudioListener anyListener = UnityEngine.Object.FindObjectOfType<AudioListener>();
            UnityEngine.Debug.Log($"[RM][StickNade] AudioListener found in scene: {anyListener != null}");
            UnityEngine.Debug.Log($"[RM][StickNade] Clip length={_beepClip.length}, channels={_beepClip.channels}, frequency={_beepClip.frequency}, samples={_beepClip.samples}");

            src.Play();
            UnityEngine.Debug.Log($"[RM][StickNade] src.isPlaying after Play(): {src.isPlaying}");
            UnityEngine.Object.Destroy(tempAudio, _beepClip.length + 0.1f);
        }

        // Landing/stick sound - borrowed directly from the firing player's own HealthHandler
        // or Block component. Try swapping the field below if this doesn't sound right:
        //   ownPlayer.data.healthHandler.soundDamagePassive   (current)
        //   ownPlayer.data.healthHandler.soundDamageLifeSteal
        //   ownPlayer.data.block.soundBlockBlocked
        //   ownPlayer.data.block.soundBlockStart
        public void PlayLandSound()
        {
            if (ownPlayer == null || ownPlayer.data == null || ownPlayer.data.healthHandler == null) return;

            SoundEvent landSound = ownPlayer.data.healthHandler.soundDamagePassive;
            if (landSound != null && SoundManager.Instance != null)
            {
                SoundManager.Instance.Play(landSound, transform);
            }
        }

        private const float FlashDuration = 1f;      // how long before explosion the flashing starts
        private const float FlashInterval = 0.12f;    // how fast it flickers

        public IEnumerator ExplodeAfterFuse()
        {
            EnsureBeepLoading(this);

            float waitBeforeFlash = Mathf.Max(0f, fuseTime - FlashDuration);
            yield return new WaitForSeconds(waitBeforeFlash);

            // Flash the marker rapidly as a warning right before it detonates
            float flashTimer = 0f;
            bool bright = false;
            while (flashTimer < FlashDuration)
            {
                if (this == null) yield break;

                if (visualMarkerRenderer != null)
                {
                    bright = !bright;
                    Color flashColor = bright ? Color.white : Color.red;
                    visualMarkerRenderer.material.color = flashColor;
                    visualMarkerRenderer.material.SetColor("_EmissionColor", flashColor * (bright ? 5f : 2f));
                }

                PlayBeepAt(transform.position);

                yield return new WaitForSeconds(FlashInterval);
                flashTimer += FlashInterval;
            }

            if (this == null) yield break;

            // Reuse the same hit-particle system for a visible "boom" - scaled up via
            // the damage value passed in, since a bigger reported damage produces a
            // more dramatic effect in vanilla's particle scaling
            if (DynamicParticles.instance != null)
            {
                HitInfo explodeHitInfo = new HitInfo();
                explodeHitInfo.point = transform.position;
                explodeHitInfo.normal = Vector2.up;
                DynamicParticles.instance.PlayBulletHit(explosionDamage * 2f, transform, explodeHitInfo, Color.red);
            }

            // Play an explosion sound, using the game's real explosion-tier sound
            SoundEvent explosionSound = GetExplosionSound(ownPlayer);
            if (explosionSound != null && SoundManager.Instance != null)
            {
                SoundManager.Instance.Play(explosionSound, transform);
            }

            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
            foreach (Collider2D hit in hits)
            {
                Vector2 toTarget = (Vector2)hit.transform.position - (Vector2)transform.position;
                float dist = toTarget.magnitude;
                Vector2 dir = dist > 0.001f ? toTarget.normalized : Vector2.up;
                float falloff = Mathf.Clamp01(1f - (dist / explosionRadius));

                // Players: damage + knockback
                HealthHandler hh = hit.GetComponentInParent<HealthHandler>();
                if (hh != null)
                {
                    hh.CallTakeDamage(dir * explosionDamage * falloff, transform.position, null, ownPlayer, true);
                    hh.CallTakeForce(dir * explosionForce * falloff, ForceMode2D.Impulse, false, false, 0f);
                    continue;
                }

                // Physics props (crates, movable objects): push via BulletPush
                NetworkPhysicsObject physObj = hit.GetComponentInParent<NetworkPhysicsObject>();
                if (physObj != null)
                {
                    Vector2 localPoint = hit.transform.InverseTransformPoint(transform.position);
                    CharacterData askerData = (ownPlayer != null) ? ownPlayer.data : null;
                    physObj.BulletPush(dir * explosionForce * falloff * 800f, localPoint, askerData);
                }
            }

            if (visualMarker != null)
            {
                Destroy(visualMarker);
            }

            if (gameObject != null)
            {
                Destroy(gameObject);
            }
        }
    }

    // Attaches a StickNadeProjectile marker to each bullet spawned by a player holding the card,
    // copying the config from their StickNadeCardMarker.
    [HarmonyPatch(typeof(Gun), "BulletInit")]
    class StickNade_AttachMarkerPatch
    {
        static void Postfix(Gun __instance, GameObject bullet)
        {
            if (__instance.player == null) return;

            StickNadeCardMarker cardMarker = __instance.player.gameObject.GetComponent<StickNadeCardMarker>();
            if (cardMarker == null) return;

            // Bullets appear to be object-pooled (reused) rather than freshly created each
            // shot - reuse and fully reset any existing marker instead of stacking a new
            // one on top, which was causing stale "stuck" state to bleed between shots.
            StickNadeProjectile proj = bullet.GetComponent<StickNadeProjectile>();
            if (proj == null)
            {
                proj = bullet.AddComponent<StickNadeProjectile>();
            }
            else
            {
                // Clean up any leftover visual marker from a previous use of this pooled bullet
                if (proj.visualMarker != null)
                {
                    UnityEngine.Object.Destroy(proj.visualMarker);
                    proj.visualMarker = null;
                    proj.visualMarkerRenderer = null;
                }
            }

            proj.stuck = false;
            proj.ownPlayer = __instance.player;
            proj.fuseTime = cardMarker.fuseTime;
            proj.explosionDamage = cardMarker.explosionDamage;
            proj.explosionRadius = cardMarker.explosionRadius;
            proj.explosionForce = cardMarker.explosionForce;

            // Re-enable anything we disabled last time this pooled bullet was used
            foreach (Collider2D c in bullet.GetComponentsInChildren<Collider2D>(true))
            {
                c.enabled = true;
            }
            MoveTransform moveReset = bullet.GetComponent<MoveTransform>();
            if (moveReset != null)
            {
                moveReset.enabled = true;
            }
            RayCastTrail rayTrailReset = bullet.GetComponent<RayCastTrail>();
            if (rayTrailReset != null)
            {
                rayTrailReset.enabled = true;
            }
        }
    }

    // Intercepts the bullet's own hit logic BEFORE it applies damage / destroys itself.
    // If the bullet has a StickNadeProjectile marker and hasn't stuck yet, we take over
    // entirely (return false skips the original method): stop its movement, snap it to
    // the hit point, parent it to whatever it hit so it travels along with a moving target,
    // disable its collider so it doesn't keep re-triggering hits, and start the fuse timer.
    [HarmonyPatch(typeof(ProjectileHit), "RPCA_DoHit")]
    class StickNade_StickPatch
    {
        static bool Prefix(ProjectileHit __instance, Vector2 hitPoint, Vector2 hitNormal, Vector2 vel, int viewID, int colliderID, bool wasBlocked)
        {
            StickNadeProjectile marker = __instance.GetComponent<StickNadeProjectile>();
            if (marker == null || marker.stuck) return true; // not our bullet, or already stuck - run vanilla behavior

            marker.stuck = true;

            // Stop the bullet's own movement (disable the component entirely so gravity
            // doesn't keep getting re-applied every frame in its Update())
            MoveTransform moveComp = __instance.GetComponent<MoveTransform>();
            if (moveComp != null)
            {
                moveComp.velocity = Vector2.zero;
                moveComp.localForce = Vector3.zero;
                moveComp.worldForce = Vector3.zero;
                moveComp.enabled = false;
            }

            __instance.transform.position = hitPoint;

            // Play the same hit-impact particle effect vanilla bullets use, since we're
            // skipping the original method entirely (which normally triggers this)
            HitInfo stickHitInfo = new HitInfo();
            stickHitInfo.point = hitPoint;
            stickHitInfo.normal = hitNormal;
            if (DynamicParticles.instance != null)
            {
                DynamicParticles.instance.PlayBulletHit(marker.explosionDamage, __instance.transform, stickHitInfo, Color.white);
            }

            // Force any renderers on the bullet to stay visible while it's stuck (best-effort,
            // may not be the actual visual element)
            foreach (Renderer r in __instance.GetComponentsInChildren<Renderer>())
            {
                r.enabled = true;
            }

            // Figure out what we hit so we can parent both the bullet and our visual
            // marker to it (sticks to moving targets too)
            Transform hitTransform = null;
            if (viewID != -1)
            {
                PhotonView pv = PhotonNetwork.GetPhotonView(viewID);
                if (pv != null) hitTransform = pv.transform;
            }
            else if (colliderID != -1)
            {
                Collider2D[] colliders = MapManager.instance.currentMap.Map.GetComponentsInChildren<Collider2D>();
                if (colliderID >= 0 && colliderID < colliders.Length)
                {
                    hitTransform = colliders[colliderID].transform;
                }
            }

            if (hitTransform != null)
            {
                __instance.transform.SetParent(hitTransform, true);
            }

            // Deal normal direct hit damage if we hit a player (has a HealthHandler) -
            // walls/environment have no HealthHandler so this is naturally skipped for them.
            // This happens once, immediately, separate from the later explosion damage.
            if (hitTransform != null)
            {
                HealthHandler targetHealth = hitTransform.GetComponent<HealthHandler>();
                if (targetHealth != null)
                {
                    Vector2 dmgVector = (Vector2)__instance.transform.forward * __instance.damage * __instance.dealDamageMultiplierr;
                    targetHealth.CallTakeDamage(dmgVector, hitPoint, __instance.ownWeapon, __instance.ownPlayer, true);
                }
            }

            // Spawn our own guaranteed-visible marker at the stick point, in case the
            // bullet's own sprite isn't what we think it is / doesn't stay visible.
            // Parented the same way as the bullet so it sticks to moving targets too.
            GameObject stickMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            stickMarker.transform.localScale = Vector3.one * 0.7f;
            stickMarker.transform.position = hitPoint;
            UnityEngine.Object.Destroy(stickMarker.GetComponent<Collider>()); // visual only, no physics
            Renderer markerRenderer = stickMarker.GetComponent<Renderer>();
            if (markerRenderer != null)
            {
                markerRenderer.material.color = Color.red;
                markerRenderer.material.EnableKeyword("_EMISSION");
                markerRenderer.material.SetColor("_EmissionColor", Color.red * 2.5f);
            }
            if (hitTransform != null)
            {
                stickMarker.transform.SetParent(hitTransform, true);
            }
            marker.visualMarker = stickMarker;
            marker.visualMarkerRenderer = markerRenderer;

            marker.PlayLandSound();

            // Disable the bullet's own collider so it doesn't keep re-triggering hits while stuck
            // Disable ALL colliders (including on child objects, not just the root) so
            // nothing can physically bump into the stuck bullet and re-trigger a hit
            foreach (Collider2D c in __instance.GetComponentsInChildren<Collider2D>())
            {
                c.enabled = false;
            }

            // Also disable the physical-contact collision detector itself, in case it
            // doesn't strictly depend on Collider2D.enabled to stop firing
            ProjectileCollision projCollision = __instance.GetComponent<ProjectileCollision>();
            if (projCollision != null)
            {
                projCollision.enabled = false;
            }

            // THE REAL FIX: RayCastTrail runs its own independent Physics2D.CircleCastAll
            // check against the player layer every frame in its own Update(), completely
            // bypassing Collider2D.enabled entirely. This is what was still detecting the
            // player touching the stuck bullet even after we disabled its colliders.
            RayCastTrail rayTrail = __instance.GetComponent<RayCastTrail>();
            if (rayTrail != null)
            {
                rayTrail.enabled = false;
            }

            marker.StartCoroutine(marker.ExplodeAfterFuse());

            return false; // skip the original method entirely - no damage/destroy on the initial stick
        }
    }
}