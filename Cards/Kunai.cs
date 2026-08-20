using HarmonyLib;
using SoundImplementation;
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
    class Kunai : CustomCard
    {
        public override void SetupCard(CardInfo cardInfo, Gun gun, ApplyCardStats cardStats, CharacterStatModifiers statModifiers, Block block)
        {
            //Edits values on card itself, which are then applied to the player in `ApplyCardStats`
            UnityEngine.Debug.Log($"[{RoundsModcah.ModInitials}][Card] {GetTitle()} has been setup.");

            // Effectively removes the cooldown gate (Gun.IsReady() checks
            // sinceAttack + readuIn * attackSpeedMultiplier > usedCooldown - a very high
            // multiplier means it's ready again almost instantly). This only affects how
            // FAST you can fire when manually clicking - it does NOT enable holding the
            // button for auto-fire, since that's a separate input-handling behavior we're
            // not touching (vanilla only fires on the press event, not the held state).
            gun.attackSpeedMultiplier = 30f;

            // Fast, knife-like projectiles: quick and flying straight
            gun.projectileSpeed = 2.5f;
            gun.gravity = 0.3f;

            // Silver/metallic tint to read as a thrown blade rather than a bullet
            gun.projectileColor = new Color(0.75f, 0.78f, 0.8f);
        }
        public override void OnAddCard(Player player, Gun gun, GunAmmo gunAmmo, CharacterData data, HealthHandler health, Gravity gravity, Block block, CharacterStatModifiers characterStats)
        {
            //Edits values on player when card is selected
            UnityEngine.Debug.Log($"[{RoundsModcah.ModInitials}][Card] {GetTitle()} has been added to player {player.playerID}.");

            player.gameObject.AddComponent<KunaiCardMarker>();

            // Using Holding.handPos instead of data.hand - Holding.Update() is completely
            // empty, meaning held items are likely parented ONCE (in Start/Awake) rather
            // than repositioned every frame, and handPos is the actual anchor for that.
            // data.hand may just be a generic reference point unrelated to visible items.
            Holding holding = player.gameObject.GetComponentInChildren<Holding>();
            Transform anchor = data.hand;
            if (holding != null)
            {
                Transform handPosViaTraverse = Traverse.Create(holding).Field("handPos").GetValue<Transform>();
                if (handPosViaTraverse != null)
                {
                    anchor = handPosViaTraverse;
                }
            }
            UnityEngine.Debug.Log($"[RM][Kunai] Holding found: {holding != null}. Using anchor: {(anchor != null ? anchor.name : "null")}");

            KunaiThrowAnim throwAnim = player.gameObject.AddComponent<KunaiThrowAnim>();
            throwAnim.Setup(anchor);

            KunaiHandVisual handVisual = player.gameObject.AddComponent<KunaiHandVisual>();
            handVisual.Setup(gun, anchor, data);
        }
        public override void OnRemoveCard(Player player, Gun gun, GunAmmo gunAmmo, CharacterData data, HealthHandler health, Gravity gravity, Block block, CharacterStatModifiers characterStats)
        {
            //Run when the card is removed from the player
            UnityEngine.Debug.Log($"[{RoundsModcah.ModInitials}][Card] {GetTitle()} has been removed from player {player.playerID}.");

            KunaiCardMarker marker = player.gameObject.GetComponent<KunaiCardMarker>();
            if (marker != null)
            {
                UnityEngine.Object.Destroy(marker);
            }

            KunaiThrowAnim throwAnim = player.gameObject.GetComponent<KunaiThrowAnim>();
            if (throwAnim != null)
            {
                UnityEngine.Object.Destroy(throwAnim);
            }

            KunaiHandVisual handVisual = player.gameObject.GetComponent<KunaiHandVisual>();
            if (handVisual != null)
            {
                handVisual.Cleanup(); // restore gun visuals before the component is destroyed
                UnityEngine.Object.Destroy(handVisual);
            }
        }


        protected override string GetTitle()
        {
            return "Kunai";
        }
        protected override string GetDescription()
        {
            return "Bullets become fast knives. No cooldown on spam-clicking, but no auto-fire.";
        }
        protected override GameObject GetCardArt()
        {
            return KunaiCardArt.GetOrLoadArt();
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
                    stat = "Fire Rate",
                    amount = "Uncapped (Click)",
                    simepleAmount = CardInfoStat.SimpleAmount.notAssigned
                },

                new CardInfoStat()
                {
                    positive = true,
                    stat = "Projectile Speed",
                    amount = "+150%",
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

    // Marker on the PLAYER while they hold the card - tells the BulletInit patch below
    // that this player's bullets should get the kunai visual attached.
    // Loads Kunai's card art from an embedded PNG resource and builds a UI.Image
    // GameObject for GetCardArt() to return - same pattern as Stick Nade's card art,
    // now using the CONFIRMED correct art frame aspect ratio (1100x864.9, ~1.272:1)
    // instead of guessing, so no overshoot hack is needed.
    public static class KunaiCardArt
    {
        private static GameObject _artPrefab;
        private static bool _loadAttempted = false;

        public static GameObject GetOrLoadArt()
        {
            if (_loadAttempted && _artPrefab == null)
            {
                _loadAttempted = false; // retry if the cached object was destroyed (e.g. scene transition)
            }

            if (_loadAttempted) return _artPrefab;
            _loadAttempted = true;

            try
            {
                System.Reflection.Assembly asm = System.Reflection.Assembly.GetExecutingAssembly();
                string resourceName = "RoundsModcah.kunai_art.png"; // adjust if the fallback log shows a different name

                byte[] imageBytes;
                using (System.IO.Stream stream = asm.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        string allNames = string.Join(", ", asm.GetManifestResourceNames());
                        UnityEngine.Debug.LogWarning($"[RM][Kunai] Card art resource '{resourceName}' not found. Available: {allNames}");
                        return null;
                    }

                    using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
                    {
                        stream.CopyTo(ms);
                        imageBytes = ms.ToArray();
                    }
                }

                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                UnityEngine.ImageConversion.LoadImage(tex, imageBytes);

                Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);

                GameObject artObj = new GameObject("KunaiCardArt", typeof(RectTransform));
                RectTransform rt = artObj.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.pivot = new Vector2(0.5f, 0.5f);

                UnityEngine.UI.Image img = artObj.AddComponent<UnityEngine.UI.Image>();
                img.sprite = sprite;
                img.preserveAspect = false; // image should already match the frame's real 1.272:1 aspect ratio

                UnityEngine.Object.DontDestroyOnLoad(artObj); // survive scene transitions, same fix Stick Nade needed

                _artPrefab = artObj;
                UnityEngine.Debug.Log($"[RM][Kunai] Card art loaded successfully ({tex.width}x{tex.height}).");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[RM][Kunai] Failed to load card art: {e}");
                _artPrefab = null;
            }

            return _artPrefab;
        }
    }

    public class KunaiCardMarker : MonoBehaviour { }

    // Hard guarantee against auto-fire: gates every attack attempt through
    // data.input.shootWasPressed (true only on the exact frame a fresh click begins,
    // unlike shootIsPressed which stays true for the whole duration a button is held).
    // This blocks held-fire regardless of WHY it might otherwise be possible - e.g.
    // stacking with other attack-speed cards (like Over Powered) apparently drops the
    // effective cooldown low enough for the base game's own input handling to start
    // treating a held button as valid repeated presses, even though neither card
    // explicitly enables auto-fire on its own.
    [HarmonyPatch(typeof(Gun), "DoAttack")]
    class Kunai_NoAutoFirePatch
    {
        static bool Prefix(Gun __instance)
        {
            if (__instance.player == null) return true;
            if (__instance.player.gameObject.GetComponent<KunaiCardMarker>() == null) return true;
            if (__instance.player.data == null || __instance.player.data.input == null) return true;

            return __instance.player.data.input.shootWasPressed;
        }
    }

    // Procedural "throw" flick for the hand, since we have no way to author a real
    // animation clip from code. Applies a temporary additive rotation offset on top of
    // whatever the game's own aim system sets that frame (runs in LateUpdate, after aim
    // logic normally runs in Update), so it layers a quick swing without fighting normal
    // aiming or causing rotation to drift/accumulate over repeated throws.
    //
    // Honest limitation: the gun model itself stays visually attached to the hand the
    // whole time - we have no way to hide/swap it from code. This only adds the swinging
    // motion, not a true "now holding a knife instead of a gun" visual.
    // Hides the currently-held gun's visuals and replaces them with a static kunai sprite
    // in the hand, so it looks like you're holding a blade instead of a gun.
    //
    // Known limitation: this only hides the renderers on the Gun reference captured when
    // the card was added. If the player picks up/switches to a different weapon while
    // this card is active, the new weapon's renderers won't be caught by this and would
    // show normally - not accounted for yet.
    public class KunaiHandVisual : MonoBehaviour
    {
        private readonly List<Renderer> _hiddenRenderers = new List<Renderer>();
        private GameObject _staticKunaiObj;
        private Transform _followTarget;
        private CharacterData _data;

        private const float RotationOffset = 132.64f; // computed from the actual kunai_blade.png art's diagonal orientation

        public void Setup(Gun gun, Transform hand, CharacterData data)
        {
            _data = data;

            if (gun != null)
            {
                foreach (Renderer r in gun.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.enabled)
                    {
                        _hiddenRenderers.Add(r);
                        r.enabled = false;
                    }
                }
            }

            if (hand != null)
            {
                // Deliberately NOT parented (unlike our first attempt) - a real child of
                // handPos never rendered despite every value checking out correctly on
                // paper (active, correct scale, correct layer/sorting/material), while an
                // unparented sibling at nearly the same position rendered fine. Something
                // about the handPos hierarchy itself breaks rendering for children, so we
                // sidestep it entirely: keep this as an independent root object and just
                // copy the anchor's position every frame instead.
                _staticKunaiObj = new GameObject("KunaiHandSprite");
                _followTarget = hand;
                Vector3 initialPos = hand.position;
                initialPos.z = 0f; // force to the same Z plane as the working isolated test - handPos itself sits at Z=0.6, which may be getting clipped/occluded
                _staticKunaiObj.transform.position = initialPos;
                _staticKunaiObj.transform.rotation = ComputeRotation();
                _staticKunaiObj.transform.localScale = Vector3.one * 0.3f; // ~90% smaller than the old 3f; adjust further as needed

                if (gun != null)
                {
                    _staticKunaiObj.layer = gun.gameObject.layer;
                }

                SpriteRenderer sr = _staticKunaiObj.AddComponent<SpriteRenderer>();
                sr.material = new Material(Shader.Find("Sprites/Default"));
                sr.sprite = KunaiSprite.Get();
                sr.color = Color.white;
                sr.enabled = true;

                if (_hiddenRenderers.Count > 0 && _hiddenRenderers[0] != null)
                {
                    sr.sortingLayerID = _hiddenRenderers[0].sortingLayerID;
                    sr.sortingOrder = _hiddenRenderers[0].sortingOrder + 1;
                }
                else
                {
                    sr.sortingOrder = 10;
                }

                UnityEngine.Debug.Log($"[RM][Kunai] Hand sprite created. worldPos={_staticKunaiObj.transform.position}, sortingOrder={sr.sortingOrder}.");
            }
        }

        // Uses CharacterData.input.aimDirection (the actual aim vector toward the cursor)
        // instead of hand.rotation, which wasn't reliably reflecting aim direction.
        private Quaternion ComputeRotation()
        {
            if (_data != null && _data.input != null)
            {
                Vector2 aim = _data.input.aimDirection;
                if (aim.sqrMagnitude > 0.0001f)
                {
                    float aimAngle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
                    return Quaternion.Euler(0f, 0f, aimAngle + RotationOffset);
                }
            }

            // Fallback to hand's own Z rotation if aimDirection isn't available for some reason
            if (_followTarget != null)
            {
                float zAngle = _followTarget.rotation.eulerAngles.z;
                return Quaternion.Euler(0f, 0f, zAngle + RotationOffset);
            }

            return Quaternion.identity;
        }

        private void Update()
        {
            if (_staticKunaiObj != null && _followTarget != null)
            {
                Vector3 pos = _followTarget.position;
                pos.z = 0f; // keep forcing to the working Z plane every frame
                _staticKunaiObj.transform.position = pos;
                _staticKunaiObj.transform.rotation = ComputeRotation();
            }
        }

        public void Cleanup()
        {
            foreach (Renderer r in _hiddenRenderers)
            {
                if (r != null) r.enabled = true;
            }
            _hiddenRenderers.Clear();

            if (_staticKunaiObj != null)
            {
                Destroy(_staticKunaiObj);
                _staticKunaiObj = null;
            }
        }

        private void OnDestroy()
        {
            Cleanup();
        }
    }

    public class KunaiThrowAnim : MonoBehaviour
    {
        private Transform _hand;
        private float _animTime = -1f; // -1 = not currently animating
        private const float Duration = 0.12f; // quick flick, matches Kunai's rapid-fire pace

        public void Setup(Transform hand)
        {
            _hand = hand;
        }

        public void TriggerThrow()
        {
            _animTime = 0f; // (re)start the flick - safe to call again mid-swing, just restarts
        }

        private void LateUpdate()
        {
            if (_hand == null || _animTime < 0f) return;

            _animTime += Time.deltaTime;
            float t = Mathf.Clamp01(_animTime / Duration);

            float offsetAngle;
            if (t < 0.25f)
            {
                // Quick wind-back
                offsetAngle = Mathf.Lerp(0f, -15f, t / 0.25f);
            }
            else
            {
                // Snap through and settle back to neutral (0 offset) by the end
                float throwT = (t - 0.25f) / 0.75f;
                float eased = 1f - (1f - throwT) * (1f - throwT); // ease-out
                offsetAngle = Mathf.Lerp(-15f, 0f, eased);
            }

            _hand.localRotation = _hand.localRotation * Quaternion.Euler(0f, 0f, offsetAngle);

            if (_animTime >= Duration)
            {
                _animTime = -1f; // done - stop applying any offset, hand returns to normal aim
            }
        }
    }

    // Loads the real kunai blade sprite from an embedded PNG resource, same pipeline used
    // for Stick Nade's card art (embedded resource -> decode -> Sprite). Pivot is centered
    // (0.5, 0.5) since this sprite is used both rotating in-flight and static in the hand.
    //
    // IMPORTANT: for the rotation logic elsewhere (KunaiVisual facing direction of travel)
    // to look correct, draw the blade pointing UP (+Y) in the source image, same
    // orientation the old procedural version used.
    public static class KunaiSprite
    {
        private static Sprite _sprite;
        private static bool _loadAttempted = false;

        public static Sprite Get()
        {
            if (_loadAttempted) return _sprite;
            _loadAttempted = true;

            try
            {
                System.Reflection.Assembly asm = System.Reflection.Assembly.GetExecutingAssembly();
                string resourceName = "RoundsModcah.kunai_blade.png"; // adjust if the fallback log shows a different name

                byte[] imageBytes;
                using (System.IO.Stream stream = asm.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        string allNames = string.Join(", ", asm.GetManifestResourceNames());
                        UnityEngine.Debug.LogWarning($"[RM][Kunai] Embedded resource '{resourceName}' not found. Available: {allNames}");
                        return null;
                    }

                    using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
                    {
                        stream.CopyTo(ms);
                        imageBytes = ms.ToArray();
                    }
                }

                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                UnityEngine.ImageConversion.LoadImage(tex, imageBytes);

                // Pivot set to the approximate grip point (near the handle/ring), NOT the
                // center - computed by analyzing the actual image: tip at bottom-left,
                // handle+ring at top-right. This makes rotation swing around the grip
                // (which stays anchored at the hand) so the tip naturally points toward
                // wherever the character is aiming.
                _sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.755f, 0.782f), 100f);
                UnityEngine.Debug.Log($"[RM][Kunai] Blade sprite loaded successfully ({tex.width}x{tex.height}).");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[RM][Kunai] Failed to load blade sprite: {e}");
                _sprite = null;
            }

            return _sprite;
        }
    }

    // Follows a bullet and rotates to face its direction of travel, giving the flying
    // knife visual instead of the default round bullet sprite. Parented to the bullet so
    // position is inherited automatically - only rotation needs per-frame updating.
    public class KunaiVisual : MonoBehaviour
    {
        private MoveTransform _moveComp;
        private Transform _bulletTransform;

        public void Setup(Transform bulletTransform, MoveTransform moveComp)
        {
            // Deliberately NOT a true child of the bullet (unlike our first attempt here)
            // - we already found that true parenting to certain transforms (handPos)
            // silently breaks rendering despite everything checking out correctly on
            // paper. Using the same proven fix: stay unparented, track position/rotation
            // manually every frame instead.
            _bulletTransform = bulletTransform;
            _moveComp = moveComp;
            transform.position = bulletTransform.position;
        }

        private void LateUpdate()
        {
            if (_bulletTransform == null)
            {
                Destroy(gameObject);
                return;
            }

            transform.position = _bulletTransform.position;

            if (_moveComp == null) return;

            Vector2 dir = ((Vector2)_moveComp.velocity);
            if (dir.sqrMagnitude < 0.0001f) return; // keep last facing if not moving (e.g. briefly at rest)

            dir.Normalize();
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            // Offset computed from the actual image: the blade's tip points at roughly
            // -132.64 degrees relative to the grip pivot in the source art (diagonal,
            // tip bottom-left / handle top-right), so we need +132.64 to align it with
            // the direction of travel.
            const float rotationOffset = 132.64f;
            transform.rotation = Quaternion.Euler(0f, 0f, angle + rotationOffset);
        }
    }

    // Custom throw sound, loaded from an embedded .wav the same way Stick Nade's beep was -
    // no dedicated knife/throw sound exists anywhere in the game's own assets (confirmed via
    // string search), so a bundled sound is the only reliable option.
    public static class KunaiSound
    {
        private static AudioClip _throwClip;
        private static bool _loadStarted = false;
        private static float _leadingSilenceSeconds = 0f;

        public static void EnsureLoading(MonoBehaviour runner)
        {
            if (_loadStarted) return;
            _loadStarted = true;

            string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "kunai_throw.wav");

            try
            {
                if (!System.IO.File.Exists(tempPath))
                {
                    System.Reflection.Assembly asm = System.Reflection.Assembly.GetExecutingAssembly();
                    string resourceName = "RoundsModcah.kunai_throw.wav"; // adjust if the fallback log shows a different name

                    using (System.IO.Stream stream = asm.GetManifestResourceStream(resourceName))
                    {
                        if (stream == null)
                        {
                            string allNames = string.Join(", ", asm.GetManifestResourceNames());
                            UnityEngine.Debug.LogWarning($"[RM][Kunai] Embedded resource '{resourceName}' not found. Available: {allNames}");
                            return;
                        }

                        using (System.IO.FileStream fileStream = System.IO.File.Create(tempPath))
                        {
                            stream.CopyTo(fileStream);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[RM][Kunai] Failed to extract throw sound: {e}");
                return;
            }

            runner.StartCoroutine(LoadClip(tempPath));
        }

        private static System.Collections.IEnumerator LoadClip(string path)
        {
            string url = "file:///" + path.Replace("\\", "/");
            using (UnityEngine.Networking.UnityWebRequest www =
                   UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV))
            {
                yield return www.SendWebRequest();

                if (www.isNetworkError || www.isHttpError)
                {
                    UnityEngine.Debug.LogWarning($"[RM][Kunai] Failed to load throw sound: {www.error}");
                }
                else
                {
                    _throwClip = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(www);
                    AnalyzeLeadingSilence();
                }
            }
        }

        // Scans the loaded clip for the first sample above a small volume threshold, so
        // any leading dead air baked into the source .wav can be skipped automatically
        // instead of causing a perceptible delay every time it plays.
        private static void AnalyzeLeadingSilence()
        {
            if (_throwClip == null) return;

            const float threshold = 0.02f;
            int sampleCount = _throwClip.samples * _throwClip.channels;
            float[] data = new float[sampleCount];
            _throwClip.GetData(data, 0);

            int firstNonSilentSample = 0;
            for (int i = 0; i < data.Length; i++)
            {
                if (Mathf.Abs(data[i]) > threshold)
                {
                    firstNonSilentSample = i;
                    break;
                }
            }

            int frameIndex = firstNonSilentSample / Mathf.Max(1, _throwClip.channels);
            _leadingSilenceSeconds = Mathf.Clamp(
                (float)frameIndex / _throwClip.frequency,
                0f,
                Mathf.Max(0f, _throwClip.length - 0.01f) // never skip past the very end
            );

            UnityEngine.Debug.Log($"[RM][Kunai] Detected {_leadingSilenceSeconds:0.000}s of leading silence, will skip on playback.");
        }

        public static void PlayAt(Vector3 position)
        {
            if (_throwClip == null) return;

            GameObject tempAudio = new GameObject("KunaiThrowSound");
            tempAudio.transform.position = position;
            AudioSource src = tempAudio.AddComponent<AudioSource>();
            src.clip = _throwClip;
            src.volume = 0.6f;
            src.spatialBlend = 0f;
            src.time = _leadingSilenceSeconds;
            src.Play();
            UnityEngine.Object.Destroy(tempAudio, (_throwClip.length - _leadingSilenceSeconds) + 0.1f);
        }
    }

    // Suppresses the default gunfire sound specifically for players holding Kunai, so our
    // custom throw sound (played once per bullet in the BulletInit patch below) is heard
    // instead of layering on top of the normal gun sound. Uses Traverse to reach parentGun
    // since direct access (CS1061) indicates it's likely internal, not public.
    internal static class KunaiSoundSuppressHelper
    {
        internal static bool ShouldSuppress(SoundGun instance)
        {
            try
            {
                Gun parentGun = Traverse.Create(instance).Field("parentGun").GetValue<Gun>();
                if (parentGun == null || parentGun.player == null) return false;
                if (parentGun.player.gameObject.GetComponent<KunaiCardMarker>() == null) return false;
                return true;
            }
            catch
            {
                return false; // if anything about this lookup fails, fall back to normal behavior
            }
        }
    }

    [HarmonyPatch(typeof(SoundGun), "PlaySingle")]
    class Kunai_SuppressGunSoundPatch
    {
        static bool Prefix(SoundGun __instance)
        {
            return !KunaiSoundSuppressHelper.ShouldSuppress(__instance);
        }
    }

    [HarmonyPatch(typeof(SoundGun), "PlaySingleAuto")]
    class Kunai_SuppressGunSoundAutoPatch
    {
        static bool Prefix(SoundGun __instance)
        {
            return !KunaiSoundSuppressHelper.ShouldSuppress(__instance);
        }
    }

    // Tracks a bullet's companion KunaiVisual, since it's no longer a true child (see the
    // parenting invisibility issue noted in KunaiVisual above) and needs manual tracking
    // for pooled-bullet reuse, plus explicit cleanup since Unity won't auto-destroy it
    // alongside the bullet anymore.
    public class KunaiVisualLink : MonoBehaviour
    {
        public GameObject companionVisual;

        private void OnDestroy()
        {
            if (companionVisual != null)
            {
                Destroy(companionVisual);
            }
        }
    }

    // Hides the bullet's own default appearance (the vanilla team-colored sprite) for
    // players holding Kunai, since we provide our own companion visual (KunaiVisual)
    // instead. Same patch point (Gun.ApplyProjectileStats) already proven to work for
    // Stick Nade's bullet color fix.
    [HarmonyPatch(typeof(Gun), "ApplyProjectileStats")]
    class Kunai_HideDefaultBulletPatch
    {
        static void Postfix(Gun __instance, GameObject obj)
        {
            if (__instance.player == null) return;
            if (__instance.player.gameObject.GetComponent<KunaiCardMarker>() == null) return;

            foreach (Renderer r in obj.GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = false;
            }
        }
    }

    // Attaches (or reuses, since bullets are pooled) the kunai visual on every bullet
    // fired by a player holding the card.
    [HarmonyPatch(typeof(Gun), "BulletInit")]
    class Kunai_AttachVisualPatch
    {
        static void Postfix(Gun __instance, GameObject bullet)
        {
            if (__instance.player == null) return;
            if (__instance.player.gameObject.GetComponent<KunaiCardMarker>() == null) return;

            MoveTransform moveComp = bullet.GetComponent<MoveTransform>();
            if (moveComp == null) return;

            // Reuse the existing visual if this pooled bullet already has one from a
            // previous shot, instead of stacking duplicates.
            KunaiVisualLink link = bullet.GetComponent<KunaiVisualLink>();
            if (link == null)
            {
                link = bullet.AddComponent<KunaiVisualLink>();
            }

            KunaiVisual existing;
            if (link.companionVisual == null)
            {
                GameObject visualObj = new GameObject("KunaiVisual");
                visualObj.transform.localScale = Vector3.one * 0.3f; // matches hand visual sizing, tune as needed
                SpriteRenderer sr = visualObj.AddComponent<SpriteRenderer>();
                sr.material = new Material(Shader.Find("Sprites/Default")); // same fix as the hand visual
                sr.sprite = KunaiSprite.Get();

                existing = visualObj.AddComponent<KunaiVisual>();
                existing.Setup(bullet.transform, moveComp);

                link.companionVisual = visualObj;
            }
            else
            {
                existing = link.companionVisual.GetComponent<KunaiVisual>();
            }

            // Play our custom throw sound once per shot, and make sure loading has kicked
            // off (harmless to call repeatedly - it only actually starts once)
            KunaiSound.EnsureLoading(existing);
            KunaiSound.PlayAt(bullet.transform.position);

            // Trigger the procedural hand-throw flick
            KunaiThrowAnim throwAnim = __instance.player.gameObject.GetComponent<KunaiThrowAnim>();
            if (throwAnim != null)
            {
                throwAnim.TriggerThrow();
            }
        }
    }
}