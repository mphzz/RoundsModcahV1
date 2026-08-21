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
    // Tags our from-scratch decoy so the Harmony patch below knows to intervene in its
    // CharacterData.Start(). Unlike our earlier catastrophic attempts, this decoy NEVER
    // has a PhotonView at all (never created, never cloned), so there's no ViewID
    // collision risk - this patch only needs to prevent a null-reference, nothing more.
    public class DecoyMarker : MonoBehaviour { }

    // Confirmed via decompiling CharacterData.Start():
    //   this.groundMask = LayerMask.GetMask(new string[] { "Default" });
    //   if (!this.view.IsMine) { PlayerManager.RegisterPlayer(this.player); }
    // Our decoy has no PhotonView, so "view" is null - we replicate the safe first line
    // ourselves and skip the rest.
    [HarmonyPatch(typeof(CharacterData), "Start")]
    class Decoy_SafeCharacterDataStartPatch
    {
        static bool Prefix(CharacterData __instance)
        {
            if (__instance.GetComponent<DecoyMarker>() == null) return true;

            try
            {
                Traverse.Create(__instance).Field("groundMask").SetValue(LayerMask.GetMask(new string[] { "Default" }));
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[RM][Decoy] Failed to set groundMask on decoy: {e}");
            }

            return false;
        }
    }

    // FALLING-THROUGH-THE-MAP FIX. Confirmed via decompiling PlayerVelocity.FixedUpdate():
    //   this.velocity += Vector2.down * Time.fixedDeltaTime * TimeHandler.timeScale * 20f;
    //   base.transform.position += Time.fixedDeltaTime * TimeHandler.timeScale * this.velocity;
    // Gravity is added UNCONDITIONALLY every FixedUpdate with nothing to stop it - the
    // real player presumably gets ground-stopping from CharacterData.TouchGround(),
    // called somewhere in PlayerMovement.FixedUpdate() (undecompiled, and tangled up
    // with components we deliberately skipped). Since Rigidbody2D/Collider2D alone
    // don't stop this either (same "transform overwritten directly, solver never runs"
    // reason as the wall-clipping fix above), this postfixes PlayerVelocity.FixedUpdate()
    // itself - same private-method-patch pattern already used for Gun.DoAttack/
    // ApplyProjectileStats elsewhere in this mod - and manually clamps decoy-tagged
    // objects to the ground. Gated on DecoyMarker so this can never affect real players.
    [HarmonyPatch(typeof(PlayerVelocity), "FixedUpdate")]
    class Decoy_GroundClampPatch
    {
        private const float GroundCheckDistance = 0.6f; // a bit past DecoyRealAI.ColliderRadius (0.5f)

        static void Postfix(PlayerVelocity __instance)
        {
            if (__instance.GetComponent<DecoyMarker>() == null) return;

            Transform t = __instance.transform;
            RaycastHit2D hit = Physics2D.Raycast(t.position, Vector2.down, GroundCheckDistance, DecoyRealAI.WallMask);
            if (hit.collider == null) return; // still airborne, nothing to clamp yet

            Vector3 pos = t.position;
            pos.y = hit.point.y + DecoyRealAI.ColliderRadius;
            t.position = pos;

            try
            {
                Traverse.Create(__instance).Field("velocity").SetValue(Vector2.zero);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[RM][Decoy] Failed to zero velocity on ground clamp: {e}");
            }
        }
    }

    // Feeds fake input into the real PlayerMovement/PlayerVelocity system so the decoy
    // walks using genuine physics instead of tweened position.
    //
    // WALL COLLISION NOTE (confirmed via decompiling PlayerVelocity.FixedUpdate()):
    // real player movement is entirely transform-driven, not physics-driven -
    // PlayerVelocity keeps its own internal `velocity` field and writes
    // `base.transform.position` directly every FixedUpdate; it never touches
    // Rigidbody2D at all (not even for gravity, which it hand-rolls itself). This
    // means the Rigidbody2D/CircleCollider2D added in SpawnDecoy_RealMovement() do NOT
    // stop the decoy walking through walls - Unity's physics solver never gets a
    // chance to run collision response against a body whose transform is being
    // overwritten wholesale each frame. So instead of chasing the real player's
    // collision components (CollisionChecker/PlayerCollision/WallRayCaster -
    // undecompiled, likely tangled up with the ~20 sibling components we
    // deliberately skipped), we reuse the same manual Physics2D.Linecast
    // wall-avoidance already proven in DecoyMovement (the safe fallback) and clamp
    // the fake direction before it's ever written into data.input.direction.
    public class DecoyRealAI : MonoBehaviour
    {
        private CharacterData _data;
        private Vector2 _currentDirection = Vector2.zero;
        private float _directionTimer = 0f;

        private const float MinDirectionTime = 0.8f;
        private const float MaxDirectionTime = 2.2f;

        // Static so Decoy_GroundClampPatch below (which runs from a static Harmony
        // postfix, not an instance) can reuse the same mask - walls and ground are the
        // same "Default" map layer, so one lookup covers both.
        public static int WallMask = ~0; // fallback: everything, in case wall lookup fails
        public const float ColliderRadius = 0.5f; // matches CircleCollider2D.radius in SpawnDecoy_RealMovement
        private const float LookAheadDistance = 0.6f; // a bit past ColliderRadius

        public void Setup(CharacterData data)
        {
            _data = data;
            DetermineWallMask();
        }

        private void DetermineWallMask()
        {
            try
            {
                Collider2D[] mapColliders = MapManager.instance.currentMap.Map.GetComponentsInChildren<Collider2D>();
                if (mapColliders.Length > 0)
                {
                    WallMask = 1 << mapColliders[0].gameObject.layer;
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[RM][Decoy] Failed to determine wall layer for real-movement decoy, wandering may clip through walls: {e}");
            }
        }

        private void Update()
        {
            if (_data == null || _data.input == null) return;

            _directionTimer -= Time.deltaTime;
            if (_directionTimer <= 0f)
            {
                PickNewDirection();
            }

            Vector2 safeDirection = ClampToAvoidWalls(_currentDirection);

            try
            {
                Traverse.Create(_data.input).Field("direction").SetValue(safeDirection);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[RM][Decoy] Failed to set input direction: {e}");
            }
        }

        // Checks a short distance ahead independently on each axis and zeroes out any
        // component that would cross a wall collider this frame, so e.g. a wall to the
        // right doesn't also cancel vertical movement. Mirrors DecoyMovement's approach.
        private Vector2 ClampToAvoidWalls(Vector2 direction)
        {
            if (direction == Vector2.zero) return direction;

            Vector2 result = direction;
            Vector3 origin = transform.position;

            if (Mathf.Abs(direction.x) > 0.01f)
            {
                Vector3 xTarget = origin + new Vector3(Mathf.Sign(direction.x) * LookAheadDistance, 0f, 0f);
                if (Physics2D.Linecast(origin, xTarget, WallMask).collider != null)
                {
                    result.x = 0f;
                }
            }

            if (Mathf.Abs(direction.y) > 0.01f)
            {
                Vector3 yTarget = origin + new Vector3(0f, Mathf.Sign(direction.y) * LookAheadDistance, 0f);
                if (Physics2D.Linecast(origin, yTarget, WallMask).collider != null)
                {
                    result.y = 0f;
                }
            }

            // If a wall blocked movement entirely, force an early retarget next frame
            // instead of leaving the decoy stuck pressing into the wall until the timer
            // runs out.
            if (result == Vector2.zero && direction != Vector2.zero)
            {
                _directionTimer = 0f;
            }

            return result;
        }

        private void PickNewDirection()
        {
            float choice = UnityEngine.Random.value;
            if (choice < 0.4f) _currentDirection = Vector2.left;
            else if (choice < 0.8f) _currentDirection = Vector2.right;
            else _currentDirection = Vector2.zero;

            _directionTimer = UnityEngine.Random.Range(MinDirectionTime, MaxDirectionTime);
        }
    }

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
            // NOTE: previous SAFE visual-only version (wandering sprite, no real
            // movement) is preserved below in SpawnDecoy_SafeVisualOnly() as an easy
            // revert target. This method now attempts the from-scratch reconstruction
            // instead - building a brand NEW, independent GameObject and manually
            // adding only specific real gameplay components (never cloning the player
            // root, never creating a PhotonView), so any failure here is contained to
            // our own decoy object and can NEVER affect the real player.
            try
            {
                SpawnDecoy_RealMovement();
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[RM][Decoy] Real-movement decoy construction failed, falling back to safe visual-only: {e}");
                SpawnDecoy_SafeVisualOnly();
            }
        }

        private void SpawnDecoy_RealMovement()
        {
            Vector3 spawnPos = _player.transform.position;
            spawnPos.z = 0f;

            UnityEngine.Debug.Log("[RM][Decoy] Stage: creating root object");
            GameObject decoyObj = new GameObject("DecoyReconstructed");
            decoyObj.transform.position = spawnPos;
            decoyObj.transform.rotation = _player.transform.rotation;
            decoyObj.transform.localScale = _player.transform.localScale;

            UnityEngine.Debug.Log("[RM][Decoy] Stage: adding visual sub-objects (Art, Limbs, PlayerSkin)");
            string[] visualChildNames = { "Art", "Limbs", "PlayerSkin", "Particles", "Effects" };
            foreach (string childName in visualChildNames)
            {
                Transform sourceChild = _player.transform.Find(childName);
                if (sourceChild == null) continue;

                GameObject clonedChild = UnityEngine.Object.Instantiate(sourceChild.gameObject, decoyObj.transform);
                clonedChild.name = childName;
                clonedChild.transform.localPosition = sourceChild.localPosition;
                clonedChild.transform.localRotation = sourceChild.localRotation;
                clonedChild.transform.localScale = sourceChild.localScale;

                // These visual sub-objects may contain their own colliders/scripts from
                // the original player - strip colliders (we'll add our own single one
                // for real physics) and disable their scripts (IK/animation scripts will
                // be re-enabled selectively once we confirm the base movement works)
                foreach (Collider2D col in clonedChild.GetComponentsInChildren<Collider2D>(true))
                {
                    UnityEngine.Object.Destroy(col);
                }
                foreach (MonoBehaviour mb in clonedChild.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null) continue;
                    mb.enabled = false;
                }
            }

            UnityEngine.Debug.Log("[RM][Decoy] Stage: adding required stub children (HandPos, PlayerWobblePosition)");
            // CharacterData.Awake() does:
            //   this.wobblePos = base.GetComponentInChildren<PlayerWobblePosition>().transform;
            //   this.hand = base.GetComponentInChildren<HandPos>().transform;
            // Both crash immediately if no child has these components. Simple stub
            // children satisfy this without needing anything else.
            GameObject handStub = new GameObject("HandPosStub");
            handStub.transform.SetParent(decoyObj.transform, false);
            handStub.AddComponent<HandPos>();

            GameObject wobbleStub = new GameObject("WobblePosStub");
            wobbleStub.transform.SetParent(decoyObj.transform, false);
            wobbleStub.AddComponent<PlayerWobblePosition>();

            UnityEngine.Debug.Log("[RM][Decoy] Stage: adding Rigidbody2D + Collider2D (for CharacterData.TouchGround/hit detection, NOT wall collision)");
            // NOTE (updated after decompiling PlayerVelocity.FixedUpdate()): this rigidbody
            // does NOT give us wall collision - PlayerVelocity writes transform.position
            // directly every FixedUpdate rather than going through the physics solver, so
            // Unity never runs collision response against it. Wall-avoidance is instead
            // handled manually in DecoyRealAI via Physics2D.Linecast, same as DecoyMovement.
            // Still kept here because CharacterData.TouchGround(Vector3, Vector3,
            // Rigidbody2D, Transform) takes a Rigidbody2D parameter directly, and the
            // CircleCollider2D lets real bullets' RayCastTrail queries hit the decoy.
            // Kinematic since nothing ever applies physics forces to it - avoids the decoy
            // being shoved around unpredictably by incidental collisions with other bodies.
            Rigidbody2D rb = decoyObj.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.freezeRotation = true; // typical for platformer characters, avoid tipping over

            CircleCollider2D col2d = decoyObj.AddComponent<CircleCollider2D>();
            col2d.radius = DecoyRealAI.ColliderRadius; // starting guess, tune to match real player collider size

            UnityEngine.Debug.Log("[RM][Decoy] Stage: adding minimal component set for movement");
            // Deliberately minimal - PlayerMovement.FixedUpdate() (confirmed via
            // decompile) only touches data.input, data.playerVel, data.isWallGrab,
            // data.wallDistance - NOT weaponHandler/block/healthHandler/etc. Skipping
            // those avoids their own Start()-crash landmines (e.g. HealthHandler.Start()
            // needs a serialized hpSprite reference we can't provide) until we actually
            // need them.
            decoyObj.AddComponent<CharacterStatModifiers>();
            GeneralInput input = decoyObj.AddComponent<GeneralInput>();
            decoyObj.AddComponent<PlayerVelocity>();
            decoyObj.AddComponent<PlayerMovement>();

            UnityEngine.Debug.Log("[RM][Decoy] Stage: tagging as decoy, adding CharacterData");
            decoyObj.AddComponent<DecoyMarker>();
            CharacterData data = decoyObj.AddComponent<CharacterData>();

            UnityEngine.Debug.Log("[RM][Decoy] Stage: disabling GeneralInput's own Update (stops it reading real hardware input)");
            input.enabled = false;

            UnityEngine.Debug.Log("[RM][Decoy] Stage: setting isPlaying via Traverse (field access, may be internal)");
            try
            {
                Traverse.Create(data).Field("isPlaying").SetValue(true);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[RM][Decoy] Failed to set isPlaying: {e}");
            }

            UnityEngine.Debug.Log("[RM][Decoy] Stage: attaching AI and lifetime");
            DecoyRealAI ai = decoyObj.AddComponent<DecoyRealAI>();
            ai.Setup(data);

            DecoyLifetime lifetime = decoyObj.AddComponent<DecoyLifetime>();
            lifetime.duration = DecoyDuration;

            UnityEngine.Debug.Log($"[RM][Decoy] Real-movement decoy fully constructed at {spawnPos} for player {_player.playerID}.");
        }

        // SAFE FALLBACK - preserved from the confirmed-working version. Never touches
        // the player root or PhotonView. Used automatically if the real-movement
        // reconstruction above throws at any point.
        private void SpawnDecoy_SafeVisualOnly()
        {
            Vector3 spawnPos = _player.transform.position;
            spawnPos.z = 0f;

            GameObject decoyObj = new GameObject("DecoyClone");
            decoyObj.transform.position = spawnPos;
            decoyObj.transform.rotation = _player.transform.rotation;
            decoyObj.transform.localScale = _player.transform.localScale;

            string[] visualChildNames = { "Art", "Limbs", "PlayerSkin", "Particles", "Effects" };
            foreach (string childName in visualChildNames)
            {
                Transform sourceChild = _player.transform.Find(childName);
                if (sourceChild == null) continue;

                GameObject clonedChild = UnityEngine.Object.Instantiate(sourceChild.gameObject, decoyObj.transform);
                clonedChild.name = childName;
                clonedChild.transform.localPosition = sourceChild.localPosition;
                clonedChild.transform.localRotation = sourceChild.localRotation;
                clonedChild.transform.localScale = sourceChild.localScale;

                foreach (Collider2D col in clonedChild.GetComponentsInChildren<Collider2D>(true))
                {
                    UnityEngine.Object.Destroy(col);
                }
                foreach (MonoBehaviour mb in clonedChild.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null) continue;
                    mb.enabled = false;
                }
            }

            DecoyLifetime lifetime = decoyObj.AddComponent<DecoyLifetime>();
            lifetime.duration = DecoyDuration;

            decoyObj.AddComponent<DecoyMovement>();

            UnityEngine.Debug.Log($"[RM][Decoy] Spawned safe visual-only decoy (fallback) at {spawnPos} for player {_player.playerID}.");
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
    // Wanders the decoy around near its spawn point with random hops and occasional
    // vertical "jump" bobs. NOTE: this manually tweens position rather than using real
    // PlayerMovement - an attempt to drive real movement via a full player-root clone
    // caused a serious bug (the REAL player teleported/froze/slid through objects),
    // likely from toggling SetActive() on the live networked player object. Reverted to
    // this safer approach; revisiting real animation needs a more careful, incremental
    // plan that never touches the real player's active state or clones its root.
    public class DecoyMovement : MonoBehaviour
    {
        private Vector3 _spawnPos;
        private Vector3 _targetPos;
        private Vector3 _basePos; // current position before any jump-bob offset is applied

        private const float WanderRadius = 2.5f;
        private const float WalkSpeed = 2.2f;
        private const float MinRetargetTime = 1.2f;
        private const float MaxRetargetTime = 2.8f;
        private const float JumpChance = 0.3f;
        private const float JumpHeight = 0.5f;
        private const float JumpDuration = 0.4f;
        private const int MaxTargetAttempts = 8;

        private float _retargetTimer = 0f;
        private bool _jumping = false;
        private float _jumpElapsed = 0f;

        private int _wallMask = ~0; // fallback: everything, in case wall lookup fails

        private void Start()
        {
            _spawnPos = transform.position;
            _basePos = _spawnPos;

            try
            {
                Collider2D[] mapColliders = MapManager.instance.currentMap.Map.GetComponentsInChildren<Collider2D>();
                if (mapColliders.Length > 0)
                {
                    _wallMask = 1 << mapColliders[0].gameObject.layer;
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[RM][Decoy] Failed to determine wall layer, wandering may clip through walls: {e}");
            }

            PickNewTarget();
        }

        private void PickNewTarget()
        {
            for (int attempt = 0; attempt < MaxTargetAttempts; attempt++)
            {
                Vector2 offset = UnityEngine.Random.insideUnitCircle * WanderRadius;
                Vector3 candidate = _spawnPos + new Vector3(offset.x, offset.y, 0f);

                RaycastHit2D hit = Physics2D.Linecast(_basePos, candidate, _wallMask);
                if (hit.collider == null)
                {
                    _targetPos = candidate;
                    _retargetTimer = UnityEngine.Random.Range(MinRetargetTime, MaxRetargetTime);

                    if (UnityEngine.Random.value < JumpChance)
                    {
                        _jumping = true;
                        _jumpElapsed = 0f;
                    }
                    return;
                }
            }

            _targetPos = _basePos;
            _retargetTimer = UnityEngine.Random.Range(MinRetargetTime, MaxRetargetTime);
        }

        private void Update()
        {
            Vector3 nextStep = Vector3.MoveTowards(_basePos, _targetPos, WalkSpeed * Time.deltaTime);

            RaycastHit2D stepHit = Physics2D.Linecast(_basePos, nextStep, _wallMask);
            if (stepHit.collider != null)
            {
                PickNewTarget();
            }
            else
            {
                _basePos = nextStep;
            }

            _retargetTimer -= Time.deltaTime;
            if (_retargetTimer <= 0f || Vector3.Distance(_basePos, _targetPos) < 0.05f)
            {
                PickNewTarget();
            }

            Vector3 finalPos = _basePos;
            if (_jumping)
            {
                _jumpElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(_jumpElapsed / JumpDuration);
                float arc = Mathf.Sin(t * Mathf.PI);
                finalPos.y += arc * JumpHeight;

                if (t >= 1f)
                {
                    _jumping = false;
                }
            }

            transform.position = finalPos;
        }
    }

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