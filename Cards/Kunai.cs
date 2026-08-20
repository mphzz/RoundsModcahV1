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
            Transform anchor = (holding != null) ? holding.handPos : data.hand;
            UnityEngine.Debug.Log($"[RM][Kunai] Holding found: {holding != null}. Using anchor: {(anchor != null ? anchor.name : "null")}");

            KunaiThrowAnim throwAnim = player.gameObject.AddComponent<KunaiThrowAnim>();
            throwAnim.Setup(anchor);

            KunaiHandVisual handVisual = player.gameObject.AddComponent<KunaiHandVisual>();
            handVisual.Setup(gun, anchor);
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
    public class KunaiCardMarker : MonoBehaviour { }

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
        // TEMPORARY DEBUG helper - a giant solid magenta square, impossible to miss if
        // anything is rendering at all. Remove once the real visibility issue is found.
        private static Sprite _debugSprite;
        private static Sprite GetDebugTestSprite()
        {
            if (_debugSprite != null) return _debugSprite;

            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.magenta;
            tex.SetPixels(pixels);
            tex.Apply();

            _debugSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _debugSprite;
        }
        private readonly List<Renderer> _hiddenRenderers = new List<Renderer>();
        private GameObject _staticKunaiObj;

        public void Setup(Gun gun, Transform hand)
        {
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
                _staticKunaiObj = new GameObject("KunaiHandSprite");
                _staticKunaiObj.transform.SetParent(hand, false);
                _staticKunaiObj.transform.localPosition = Vector3.zero;
                _staticKunaiObj.transform.localRotation = Quaternion.identity;
                _staticKunaiObj.transform.localScale = Vector3.one * 3f; // our sprite is tiny in world units, scale up as a starting guess

                SpriteRenderer sr = _staticKunaiObj.AddComponent<SpriteRenderer>();

                // TEMPORARY DEBUG: swapped to a huge, unmistakable solid magenta square
                // instead of the real kunai sprite, to isolate whether the problem is
                // rendering/parenting in general, or something specific to our kunai
                // texture. Revert to KunaiSprite.Get() once this is confirmed visible.
                sr.sprite = GetDebugTestSprite();
                sr.color = Color.white;
                sr.enabled = true;

                // Try to match sorting layer/order from a nearby existing renderer (e.g. one
                // of the gun renderers we just hid) rather than assuming "Default" - 2D games
                // often use named sorting LAYERS, and being on the wrong layer entirely would
                // make sortingOrder irrelevant.
                if (_hiddenRenderers.Count > 0 && _hiddenRenderers[0] != null)
                {
                    sr.sortingLayerID = _hiddenRenderers[0].sortingLayerID;
                    sr.sortingOrder = _hiddenRenderers[0].sortingOrder + 1;
                }
                else
                {
                    sr.sortingOrder = 10;
                }

                UnityEngine.Debug.Log($"[RM][Kunai] Hand sprite created. worldPos={_staticKunaiObj.transform.position}, " +
                    $"localScale={_staticKunaiObj.transform.localScale}, lossyScale={_staticKunaiObj.transform.lossyScale}, " +
                    $"sortingLayerID={sr.sortingLayerID}, sortingOrder={sr.sortingOrder}, spriteNull={sr.sprite == null}, " +
                    $"handWorldPos={hand.position}");
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

    // Builds a small procedural kunai-blade sprite once (blade tapering to a point +
    // guard + handle), the same way card art was built - drawing pixels into a Texture2D
    // and wrapping it in a Sprite, since we have no way to import real art assets.
    public static class KunaiSprite
    {
        private static Sprite _sprite;
        private static bool _built = false;

        public static Sprite Get()
        {
            if (_built) return _sprite;
            _built = true;

            const int width = 24;
            const int height = 48;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);

            Color clear = new Color(0f, 0f, 0f, 0f);
            Color bladeFill = new Color(0.85f, 0.87f, 0.9f, 1f);
            Color bladeEdge = new Color(0.55f, 0.58f, 0.62f, 1f);
            Color guardColor = new Color(0.3f, 0.3f, 0.33f, 1f);
            Color handleColor = new Color(0.12f, 0.08f, 0.06f, 1f);

            const float centerX = width / 2f;
            const float bladeBaseY = 16f;   // where the blade meets the guard
            const float guardY = 14f;       // guard occupies bladeBaseY down to guardY
            const float bladeHalfWidthBase = 6f;
            const float guardHalfWidth = bladeHalfWidthBase + 1.5f;
            const float handleHalfWidth = 2.5f;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color c = clear;
                    float dist = Mathf.Abs(x - centerX);

                    if (y >= bladeBaseY)
                    {
                        // Blade: tapers linearly from full width at the base to a point at the tip
                        float t = (y - bladeBaseY) / (height - 1f - bladeBaseY);
                        float halfWidth = Mathf.Lerp(bladeHalfWidthBase, 0f, t);
                        if (dist <= halfWidth)
                        {
                            c = dist > halfWidth - 1.2f ? bladeEdge : bladeFill;
                        }
                    }
                    else if (y >= guardY)
                    {
                        if (dist <= guardHalfWidth)
                        {
                            c = guardColor;
                        }
                    }
                    else
                    {
                        if (dist <= handleHalfWidth)
                        {
                            c = handleColor;
                        }
                    }

                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();

            _sprite = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
            return _sprite;
        }
    }

    // Follows a bullet and rotates to face its direction of travel, giving the flying
    // knife visual instead of the default round bullet sprite. Parented to the bullet so
    // position is inherited automatically - only rotation needs per-frame updating.
    public class KunaiVisual : MonoBehaviour
    {
        private MoveTransform _moveComp;

        public void Setup(Transform bulletTransform, MoveTransform moveComp)
        {
            transform.SetParent(bulletTransform, false);
            transform.localPosition = Vector3.zero;
            _moveComp = moveComp;
        }

        private void LateUpdate()
        {
            if (_moveComp == null) return;

            Vector2 dir = ((Vector2)_moveComp.velocity);
            if (dir.sqrMagnitude < 0.0001f) return; // keep last facing if not moving (e.g. briefly at rest)

            dir.Normalize();
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            // Sprite is drawn tip-up (+Y), so offset by -90 to align "up" with travel direction
            transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
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
            KunaiVisual existing = bullet.GetComponentInChildren<KunaiVisual>();
            if (existing == null)
            {
                GameObject visualObj = new GameObject("KunaiVisual");
                SpriteRenderer sr = visualObj.AddComponent<SpriteRenderer>();
                sr.sprite = KunaiSprite.Get();

                existing = visualObj.AddComponent<KunaiVisual>();
                existing.Setup(bullet.transform, moveComp);
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