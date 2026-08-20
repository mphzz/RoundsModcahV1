using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnboundLib;
using UnboundLib.Cards;
using UnboundLib.GameModes;
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
            // patch below), in addition to the later explosion damage. -20% as the card's
            // tradeoff for the sticky/explosive utility.
            gun.damage = 0.8f;

            // -30% attack speed (attackSpeedMultiplier, not attackSpeed - higher = faster,
            // confirmed via Gun.IsReady())
            gun.attackSpeedMultiplier = 0.7f;
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
            return "Bullets stick, then explode after a short fuse. -30% Attack Speed, -20% Damage.";
        }
        protected override GameObject GetCardArt()
        {
            return StickNadeArt.GetOrLoadArt();
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
                },

                new CardInfoStat()
                {
                    positive = false,
                    stat = "Attack Speed",
                    amount = "-30%",
                    simepleAmount = CardInfoStat.SimpleAmount.notAssigned
                },

                new CardInfoStat()
                {
                    positive = false,
                    stat = "Damage",
                    amount = "-20%",
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

    // Loads the card's art from an embedded PNG resource and builds a sprite GameObject
    // for GetCardArt() to return, since we don't have a Unity Editor to make a real prefab.
    public static class StickNadeArt
    {
        private static GameObject _artPrefab;
        private static bool _loadAttempted = false;

        public static GameObject GetOrLoadArt()
        {
            // Defense in depth: if we previously loaded successfully but the cached object
            // has since been destroyed (Unity's overloaded == null catches this), force a
            // fresh reload instead of returning a dead reference.
            if (_loadAttempted && _artPrefab == null)
            {
                _loadAttempted = false;
            }

            if (_loadAttempted) return _artPrefab;
            _loadAttempted = true;

            try
            {
                System.Reflection.Assembly asm = System.Reflection.Assembly.GetExecutingAssembly();
                string resourceName = "RoundsModcah.sticknade_art.png"; // adjust namespace prefix if needed

                byte[] imageBytes;
                using (System.IO.Stream stream = asm.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        string allNames = string.Join(", ", asm.GetManifestResourceNames());
                        UnityEngine.Debug.LogWarning($"[RM][StickNade] Card art resource '{resourceName}' not found. Available: {allNames}");
                        return null;
                    }

                    using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
                    {
                        stream.CopyTo(ms);
                        imageBytes = ms.ToArray();
                    }
                }

                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                UnityEngine.ImageConversion.LoadImage(tex, imageBytes); // auto-resizes to actual image dimensions

                Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);

                // Confirmed via CardVisuals.Start() source: our cardArt GameObject gets
                // Instantiate()'d as a child of "Canvas/Front/Background/Art", then its
                // localPosition/localScale get reset - but nothing sets up RectTransform
                // anchoring, so we need to stretch-fill to the parent's rect ourselves.
                GameObject artObj = new GameObject("StickNadeCardArt", typeof(RectTransform));
                RectTransform rt = artObj.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.pivot = new Vector2(0.5f, 0.5f);

                UnityEngine.UI.Image img = artObj.AddComponent<UnityEngine.UI.Image>();
                img.sprite = sprite;
                img.preserveAspect = true;
                img.color = new Color(0.75f, 0.75f, 0.75f, 1f); // 25% darker
                // Note: NOT calling SetActive(false) here - Object.Instantiate() copies the
                // active state of the source object, so every instantiated copy would also
                // start inactive. Since this master object has no Canvas parent, it won't
                // render on its own anyway.

                UnityEngine.Object.DontDestroyOnLoad(artObj);

                _artPrefab = artObj;
                UnityEngine.Debug.Log($"[RM][StickNade] Card art loaded successfully ({tex.width}x{tex.height}).");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[RM][StickNade] Failed to load card art: {e}");
                _artPrefab = null;
            }

            return _artPrefab;
        }
    }

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
        public Vector2 stickNormal = Vector2.up;
        public GameObject visualMarker;
        public float visualMarkerDesiredScale = 0.7f;
        public Renderer visualMarkerRenderer;
        public Vector3 bulletDesiredLocalScale = Vector3.one;
        // Captured from the bullet's own RayCastTrail before we disable it - combines
        // its wall mask and player mask, so our explosion only "sees" the same things a
        // real flying bullet would. Without this, non-interactable background/decoration
        // objects (which bullets deliberately ignore via layer masking) would incorrectly
        // block or absorb our explosion, since our own Physics2D queries default to
        // checking every layer.
        public int bulletInteractMask = ~0; // fallback: everything, in case capture fails

        // Safety net: if this component (and its host GameObject) gets destroyed by
        // ANYTHING other than our own ExplodeAfterFuse completing normally - e.g. the
        // bullet's original destroy logic firing from an unrelated collision - make sure
        // our separately-spawned visual marker sphere doesn't get left behind forever.
        // Continuously re-corrects the visual marker's scale every frame based on the
        // CURRENT parent scale, rather than a one-time snapshot taken at stick time.
        // Movable/destructible props can change their own scale during gameplay (damage
        // feedback, animations, etc.), which made a one-time compensation go stale and
        // drift the marker smaller/larger over time.
        private void Update()
        {
            if (transform.parent != null)
            {
                Vector3 bulletParentLossy = transform.parent.lossyScale;
                transform.localScale = new Vector3(
                    bulletParentLossy.x != 0f ? bulletDesiredLocalScale.x / bulletParentLossy.x : bulletDesiredLocalScale.x,
                    bulletParentLossy.y != 0f ? bulletDesiredLocalScale.y / bulletParentLossy.y : bulletDesiredLocalScale.y,
                    bulletParentLossy.z != 0f ? bulletDesiredLocalScale.z / bulletParentLossy.z : bulletDesiredLocalScale.z
                );
            }

            if (visualMarker == null) return;
            if (visualMarker.transform.parent == null) return; // unparented (static wall) - no correction needed

            Vector3 parentLossy = visualMarker.transform.parent.lossyScale;
            visualMarker.transform.localScale = new Vector3(
                parentLossy.x != 0f ? visualMarkerDesiredScale / parentLossy.x : visualMarkerDesiredScale,
                parentLossy.y != 0f ? visualMarkerDesiredScale / parentLossy.y : visualMarkerDesiredScale,
                parentLossy.z != 0f ? visualMarkerDesiredScale / parentLossy.z : visualMarkerDesiredScale
            );
        }

        private void OnDestroy()
        {
            if (visualMarker != null)
            {
                Destroy(visualMarker);
            }
        }

        // Spawns a ring that rapidly expands from 0 to targetRadius and fades out,
        // like a shockwave - used at the moment of explosion rather than during the fuse.
        private static IEnumerator ShockwaveRing(Vector3 center, float targetRadius, float duration, Vector2 surfaceNormal, int interactMask)
        {
            GameObject ringObj = new GameObject("StickNadeShockwave");
            LineRenderer lr = ringObj.AddComponent<LineRenderer>();

            const int segments = 48;
            lr.positionCount = segments + 1;
            lr.loop = true;
            lr.useWorldSpace = false;
            lr.startWidth = 0.15f;
            lr.endWidth = 0.15f;
            lr.material = new Material(Shader.Find("Sprites/Default"));

            ringObj.transform.position = center;

            // Precompute how far the ring can expand in each direction before hitting a
            // wall/object, so it visually stops at obstacles instead of drawing through
            // them. Players are ignored here (same rule as the damage line-of-sight check
            // above) so they never clip the ring visually either.
            Vector3 losOrigin = center + (Vector3)(surfaceNormal * 0.1f);
            float[] maxDistances = new float[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                float maxDist = targetRadius;
                RaycastHit2D[] wallHits = Physics2D.LinecastAll(losOrigin, (Vector2)losOrigin + dir * targetRadius, interactMask);
                foreach (RaycastHit2D wh in wallHits)
                {
                    if (wh.collider == null) continue;
                    if (wh.collider.GetComponentInParent<Player>() != null) continue; // players don't clip the ring
                    if (wh.distance < maxDist) maxDist = wh.distance;
                }
                maxDistances[i] = maxDist;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                float uncappedRadius = Mathf.Lerp(0f, targetRadius, t);
                for (int i = 0; i <= segments; i++)
                {
                    float angle = (float)i / segments * Mathf.PI * 2f;
                    float currentRadius = Mathf.Min(uncappedRadius, maxDistances[i]);
                    lr.SetPosition(i, new Vector3(Mathf.Cos(angle) * currentRadius, Mathf.Sin(angle) * currentRadius, 0f));
                }

                float alpha = Mathf.Lerp(0.9f, 0f, t);
                Color c = new Color(1f, 0.6f, 0.1f, alpha);
                lr.startColor = c;
                lr.endColor = c;

                yield return null;
            }

            Destroy(ringObj);
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
            src.volume = 0.0525f; // lowered another 50% from previous 0.105
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

            DoExplode();
        }

        // The actual detonation logic, extracted so it can be triggered either by the
        // normal fuse timer completing, OR forced early (e.g. the player we're stuck to
        // respawns before the fuse naturally runs out - see StickNade_RevivePatch below).
        public void DoExplode(Player immunePlayer = null)
        {
            // Shockwave ring - expands from 0 to the full blast radius rapidly and fades,
            // giving a clear visual read on the explosion's actual size. Runs on the
            // persistent plugin instance since THIS object gets destroyed moments from now.
            if (RoundsModcah.instance != null)
            {
                RoundsModcah.instance.StartCoroutine(ShockwaveRing(transform.position, explosionRadius, 0.35f, stickNormal, bulletInteractMask));
            }

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

            // Offset the line-of-sight origin slightly off the surface we're stuck to,
            // otherwise a raycast from our exact position could immediately hit that same
            // surface (since we're flush against it) and block everything.
            Vector3 losOrigin = transform.position + (Vector3)(stickNormal * 0.1f);

            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius, bulletInteractMask);
            foreach (Collider2D hit in hits)
            {
                try
                {
                    if (hit == null) continue;

                    // Line-of-sight check: skip anything blocked by a wall or non-player
                    // object between the explosion and the target. Players NEVER block -
                    // if one player is standing behind another, both still take damage.
                    RaycastHit2D[] losHits = Physics2D.LinecastAll(losOrigin, hit.bounds.center, bulletInteractMask);
                    bool blocked = false;
                    foreach (RaycastHit2D losHit in losHits)
                    {
                        if (losHit.collider == null) continue;
                        if (losHit.transform.root == hit.transform.root) continue; // this is the target itself
                        if (losHit.collider.GetComponentInParent<Player>() != null) continue; // players never block

                        blocked = true; // a wall or other non-player object is in the way
                        break;
                    }
                    if (blocked) continue;

                    Vector2 toTarget = (Vector2)hit.transform.position - (Vector2)transform.position;
                    float dist = toTarget.magnitude;
                    Vector2 dir = dist > 0.001f ? toTarget.normalized : Vector2.up;
                    float falloff = Mathf.Clamp01(1f - (dist / explosionRadius));

                    // Players: damage + knockback (skip the immune player, if any - used
                    // when forcing an early detonation on respawn, so the player doesn't
                    // immediately eat their own nade's blast the instant they come back)
                    HealthHandler hh = hit.GetComponentInParent<HealthHandler>();
                    if (hh != null)
                    {
                        Player hitPlayer = hh.GetComponent<Player>();
                        if (immunePlayer != null && hitPlayer == immunePlayer)
                        {
                            continue;
                        }

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
                catch (Exception e)
                {
                    // Don't let one bad/destroyed target abort the whole explosion and
                    // skip cleanup below - just log it and keep going
                    UnityEngine.Debug.LogWarning($"[RM][StickNade] Error processing explosion hit on {(hit != null ? hit.name : "null")}: {e}");
                }
            }

            // Cleanup ALWAYS runs now, regardless of what happened in the loop above
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

    // If a player dies with a stuck-but-not-yet-exploded nade parented to them, the player
    // object isn't destroyed on death (just repositioned/reset on Revive) - meaning our
    // stuck nade would otherwise silently ride along through respawn, frozen in place with
    // its fuse still ticking. This forces any stuck nades to detonate immediately the
    // moment their host player respawns instead.
    [HarmonyPatch(typeof(HealthHandler), "Revive")]
    class StickNade_RevivePatch
    {
        static void Postfix(HealthHandler __instance)
        {
            Player revivedPlayer = __instance.GetComponent<Player>();

            StickNadeProjectile[] stuckNades = __instance.GetComponentsInChildren<StickNadeProjectile>(true);
            foreach (StickNadeProjectile nade in stuckNades)
            {
                if (nade != null && nade.stuck)
                {
                    nade.StopAllCoroutines();
                    nade.DoExplode(revivedPlayer); // revived player is immune to this specific blast
                }
            }
        }
    }

    // Second, independent safety net: Revive() likely only covers the mid-round
    // downed-then-revived path, not necessarily a brand NEW round starting, which may
    // reset players through an entirely different mechanism. UnboundLib's own documented
    // round-start hook catches this regardless of whatever internal path the game uses.
    // Registered by piggybacking on the mod's own Start() via Harmony, so nothing needs
    // to be manually added to RoundsModcah.cs.
    [HarmonyPatch(typeof(RoundsModcah), "Start")]
    class StickNade_RegisterRoundStartHook
    {
        static bool _registered = false;

        static void Postfix()
        {
            if (_registered) return;
            _registered = true;

            GameModeManager.AddHook(GameModeHooks.HookRoundStart, OnRoundStart);
        }

        static IEnumerator OnRoundStart(IGameModeHandler gm)
        {
            StickNadeProjectile[] allStuck = UnityEngine.Object.FindObjectsOfType<StickNadeProjectile>();
            foreach (StickNadeProjectile nade in allStuck)
            {
                if (nade == null || !nade.stuck) continue;

                Player immune = null;
                if (nade.transform.parent != null)
                {
                    immune = nade.transform.parent.GetComponentInParent<Player>();
                }

                nade.StopAllCoroutines();
                nade.DoExplode(immune);
            }
            yield break;
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
            if (wasBlocked) return true; // let the game's normal block handling (DoBlock, reflect, etc.) run instead of sticking

            marker.stuck = true;
            marker.stickNormal = hitNormal != Vector2.zero ? hitNormal : Vector2.up;

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

            // Figure out what we hit. hitTransform is used for damage checks (works for both
            // players and walls). parentTransform is set whenever the hit object can actually
            // MOVE (players, and movable/destructible props with a Rigidbody2D or
            // NetworkPhysicsObject) - detected directly rather than assumed from viewID vs
            // colliderID, since movable props are often resolved via colliderID too.
            Transform hitTransform = null;
            Transform parentTransform = null;
            if (viewID != -1)
            {
                PhotonView pv = PhotonNetwork.GetPhotonView(viewID);
                if (pv != null)
                {
                    hitTransform = pv.transform;
                    parentTransform = pv.transform; // players always move
                }
            }
            else if (colliderID != -1)
            {
                Collider2D[] colliders = MapManager.instance.currentMap.Map.GetComponentsInChildren<Collider2D>();
                if (colliderID >= 0 && colliderID < colliders.Length)
                {
                    hitTransform = colliders[colliderID].transform;

                    // Only NetworkPhysicsObject counts as "movable" - many static wall
                    // pieces have a kinematic Rigidbody2D purely for collision purposes,
                    // which was incorrectly triggering parenting + scale compensation and
                    // distorting the marker on static geometry.
                    bool isMovable = hitTransform.GetComponentInParent<NetworkPhysicsObject>() != null;
                    if (isMovable)
                    {
                        parentTransform = hitTransform;
                    }
                    // otherwise stays null - genuinely static geometry doesn't need parenting
                }
            }

            if (parentTransform != null)
            {
                Vector3 preParentScale = __instance.transform.localScale;
                __instance.transform.SetParent(parentTransform, true);

                // Store the pre-parenting scale so Update() can continuously compensate
                // for the parent's scale every frame, instead of a one-time snapshot that
                // goes stale if the parent's scale changes afterward (e.g. breakable props
                // with damage-reaction or wobble animations).
                marker.bulletDesiredLocalScale = preParentScale;
            }

            // Deal normal direct hit damage if we hit anything Damagable - this covers BOTH
            // players (HealthHandler extends Damagable) AND world objects like Sandbox mode's
            // pickup-by-shooting cards (which use Damagable/DamagableEvent, not HealthHandler).
            // Originally this only checked HealthHandler, which silently broke card claiming.
            if (hitTransform != null)
            {
                try
                {
                    // Using GetComponentInParent (not GetComponent) to match how vanilla's
                    // RPCA_DoHit does this lookup - Damagable can live on a parent of the
                    // actual hit collider, e.g. on Sandbox mode's world-placed cards.
                    Damagable targetDamagable = hitTransform.GetComponentInParent<Damagable>();
                    if (targetDamagable != null)
                    {
                        Vector2 dmgVector = (Vector2)__instance.transform.forward * __instance.damage * __instance.dealDamageMultiplierr;
                        targetDamagable.CallTakeDamage(dmgVector, hitPoint, __instance.ownWeapon, __instance.ownPlayer, true);
                    }
                }
                catch (Exception e)
                {
                    // Don't let a failure here (e.g. target already dying/respawning) stop
                    // the rest of the sticking process from completing below
                    UnityEngine.Debug.LogWarning($"[RM][StickNade] Error dealing direct hit damage: {e}");
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
            if (parentTransform != null)
            {
                stickMarker.transform.SetParent(parentTransform, true);
                // Scale correction is now handled continuously every frame in Update()
                // instead of a one-time calculation here, so it stays accurate even if
                // the parent's scale changes after the initial stick.
            }
            marker.visualMarker = stickMarker;
            marker.visualMarkerRenderer = markerRenderer;
            marker.visualMarkerDesiredScale = 0.7f; // matches stickMarker's initial localScale

            // (radius ring now shown at explosion time as a shockwave instead of during the fuse)
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
                // Capture the real interaction masks BEFORE disabling - combining wall
                // mask + player mask gives us "everything a real bullet actually
                // interacts with", correctly excluding non-interactable background
                // objects that bullets pass through like air.
                marker.bulletInteractMask = (int)rayTrail.mask | (int)rayTrail.playerMask;
                rayTrail.enabled = false;
            }

            marker.StartCoroutine(marker.ExplodeAfterFuse());

            // Fire the same post-hit callbacks vanilla RPCA_DoHit normally calls at its end
            // (hitAction, hitActionWithData, deathEvent) - we skip the whole original method,
            // but other systems (like Sandbox mode's "shoot a card to claim it" mechanic)
            // hook into these exact events to detect a bullet hit, so we replicate them here.
            // Using Traverse since we don't know these fields' exact access modifiers.
            try
            {
                Action hitActionValue = Traverse.Create(__instance).Field("hitAction").GetValue<Action>();
                hitActionValue?.Invoke();

                Action<HitInfo> hitActionWithDataValue = Traverse.Create(__instance).Field("hitActionWithData").GetValue<Action<HitInfo>>();
                hitActionWithDataValue?.Invoke(stickHitInfo);

                UnityEngine.Events.UnityEvent deathEventValue = Traverse.Create(__instance).Field("deathEvent").GetValue<UnityEngine.Events.UnityEvent>();
                deathEventValue?.Invoke();
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[RM][StickNade] Error firing post-hit callbacks: {e}");
            }

            return false; // skip the original method entirely - no damage/destroy on the initial stick
        }
    }
}