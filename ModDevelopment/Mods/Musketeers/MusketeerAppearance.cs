using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using Voxels.TowerDefense;
using Voxels.TowerDefense.Ballistics;
using Voxels.TowerDefense.SpriteMagic;

namespace BadNorth.Musketeers
{
    internal sealed class MusketeerAppearance : MonoBehaviour
    {
        private const string ArtNamespace = "BadNorth.Musketeers.";
        private const int PartsWidth = 64;
        private const int PartsHeight = 126;
        private const int PartsOffsetY = 128;
        private const float RaiseSeconds = 0.32f;
        private const float LowerSeconds = 0.18f;
        private const float CarryLift = 0.065f;
        private const float CarrySide = -0.13f;
        private const float CarryForward = 0.055f;
        // In the weapon mesh, +Z points toward the muzzle and -Y toward the trigger guard.
        private static readonly Quaternion ShoulderRotation = Quaternion.LookRotation(
            new Vector3(0.035f, 1f, -0.21f), Vector3.left);

        private static Sprite uniform;
        private static Mesh musketMesh;
        private static Mesh fieldKitMesh;
        private static Texture2D equipmentPalette;
        private static Material equipmentMaterial;
        private static Vector3 localMuzzle;
        private static float glovePaletteU;
        private static MaterialPropertyBlock equipmentProperties;
        private static readonly int HoverProperty = Shader.PropertyToID("_Hover");

        private SpriteAnimator body;
        private Sprite previousUniform;
        private MeshFilter oldBowGeometry;
        private MeshRenderer oldBowRenderer;
        private Mesh oldBowMesh;
        private bool oldBowVisible;
        private Archery archer;
        private LineOfSight scout;
        private Transform bowPose;
        private Vector3 bowGrip;
        private Transform weaponMount;
        private MeshRenderer musketRenderer;
        private MusketeerReloadPose reloadPose;
        private MeshRenderer fieldKitRenderer;
        private float carry;
        private Vector3 nextOrigin;
        private bool originPrepared;

        internal static void Initialize(ManualLogSource logger)
        {
            try
            {
                LoadEquipment();
                uniform = LoadUniform();
                logger.LogInfo("Loaded the native musketeer uniform, musket, and field kit.");
            }
            catch (Exception error)
            {
                logger.LogError("Could not load the native musketeer art: " + error);
            }
        }

        private static Sprite LoadUniform()
        {
            var tile = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            using (var stream = typeof(MusketeerAppearance).Assembly.GetManifestResourceStream(ArtNamespace + "PartTex_Musketeer.png"))
            using (var data = new BinaryReader(stream))
            {
                if (!ImageConversion.LoadImage(tile, data.ReadBytes((int)stream.Length))
                    || tile.width != PartsWidth || tile.height != PartsHeight)
                    throw new InvalidOperationException("Unexpected dimensions for the musketeer parts texture.");
            }

            // Match the stock parts atlas dimensions and the placement of its English tile.
            var atlas = new Texture2D(512, 256, TextureFormat.RGBA32, false, true)
            {
                name = "Musketeer parts atlas",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            atlas.SetPixels32(new Color32[atlas.width * atlas.height]);
            atlas.SetPixels32(0, PartsOffsetY, PartsWidth, PartsHeight, tile.GetPixels32());
            atlas.Apply(false, true);
            Destroy(tile);
            var sprite = Sprite.Create(atlas, new Rect(0, PartsOffsetY, PartsWidth, PartsHeight),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = "PartTex_Musketeer";
            return sprite;
        }

        private static void LoadEquipment()
        {
            using (var stream = typeof(MusketeerAppearance).Assembly.GetManifestResourceStream(ArtNamespace + "MusketeerMusket.bytes"))
            using (var data = new BinaryReader(stream))
            {
                localMuzzle = new Vector3(data.ReadSingle(), data.ReadSingle(), data.ReadSingle());
                int colorCount = data.ReadInt32();
                int vertexCount = data.ReadInt32();
                if (colorCount < 1 || colorCount > 256 || vertexCount < 3 || vertexCount > 60000 || vertexCount % 3 != 0)
                    throw new InvalidOperationException("Malformed musket mesh resource.");

                var colors = new Color32[colorCount + 3];
                for (int i = 0; i < colorCount; i++)
                    colors[i] = new Color32(data.ReadByte(), data.ReadByte(), data.ReadByte(), 255);
                colors[colorCount] = new Color32(87, 110, 122, 255);
                colors[colorCount + 1] = new Color32(91, 72, 55, 255);
                colors[colorCount + 2] = new Color32(130, 101, 72, 255);
                glovePaletteU = (colorCount + 0.5f) / colors.Length;
                equipmentPalette = new Texture2D(colors.Length, 1, TextureFormat.RGBA32, false)
                {
                    name = "Musketeer equipment palette",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Point
                };
                equipmentPalette.SetPixels32(colors);
                equipmentPalette.Apply(false, true);

                var positions = new Vector3[vertexCount];
                var uv = new Vector2[vertexCount];
                var triangles = new int[vertexCount];
                for (int i = 0; i < vertexCount; i++)
                {
                    positions[i] = new Vector3(data.ReadSingle(), data.ReadSingle(), data.ReadSingle());
                    uv[i] = new Vector2((data.ReadByte() + 0.5f) / colors.Length, 0.5f);
                    triangles[i] = i;
                }

                musketMesh = new Mesh { name = "Line infantry musket", vertices = positions, uv = uv, triangles = triangles };
                musketMesh.RecalculateNormals();
                musketMesh.UploadMeshData(true);
                fieldKitMesh = CreateFieldKit(colorCount, colors.Length);
            }
        }

        private static Mesh CreateFieldKit(int strapColor, int paletteSize)
        {
            var vertices = new List<Vector3>();
            var paletteUv = new List<Vector2>();
            var triangles = new List<int>();

            void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int color)
            {
                int first = vertices.Count;
                vertices.AddRange(new[] { a, b, c, d });
                triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
                var sample = new Vector2((color + 0.5f) / paletteSize, 0.5f);
                paletteUv.AddRange(new[] { sample, sample, sample, sample });
            }

            var strap = new[]
            {
                new Vector3(-0.071f, 0.179f, 0f), new Vector3(-0.053f, 0.173f, -0.031f),
                new Vector3(0f, 0.156f, -0.044f), new Vector3(0.053f, 0.139f, -0.031f),
                new Vector3(0.071f, 0.133f, 0f), new Vector3(0.053f, 0.139f, 0.031f),
                new Vector3(0f, 0.156f, 0.044f), new Vector3(-0.053f, 0.173f, 0.031f)
            };
            var halfWidth = Vector3.up * 0.008f;
            for (int i = 0; i < strap.Length; i++)
            {
                var a = strap[i];
                var b = strap[(i + 1) % strap.Length];
                AddQuad(a + halfWidth, b + halfWidth, b - halfWidth, a - halfWidth, strapColor);
            }

            var pouchLeft = new Vector3(0.038f, 0.137f, -0.056f);
            var pouchRight = new Vector3(0.078f, 0.137f, -0.048f);
            var height = Vector3.up * 0.026f;
            var depth = new Vector3(-0.006f, 0f, 0.021f);
            var backLeft = pouchLeft + depth;
            var backRight = pouchRight + depth;
            var flapRise = Vector3.up * 0.008f;
            int leather = strapColor + 1;
            AddQuad(pouchLeft, pouchRight, pouchRight - height, pouchLeft - height, leather);
            AddQuad(pouchRight, backRight + flapRise, backRight - height, pouchRight - height, leather);
            AddQuad(backLeft + flapRise, pouchLeft, pouchLeft - height, backLeft - height, leather);
            AddQuad(backRight + flapRise, backLeft + flapRise, backLeft - height, backRight - height, leather);
            AddQuad(pouchLeft - height, pouchRight - height, backRight - height, backLeft - height, leather);
            AddQuad(backLeft + flapRise, backRight + flapRise, pouchRight, pouchLeft, strapColor + 2);

            var mesh = new Mesh { name = "Musketeer strap and pouch" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, paletteUv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.UploadMeshData(true);
            return mesh;
        }

        internal static void Attach(Archery archer, Transform bowAimer)
        {
            var soldier = archer.agent;
            if (!uniform || !musketMesh || !fieldKitMesh || !MusketeersPlugin.IsMusketeer(soldier) || !soldier.spriteAnimator
                || !bowAimer || soldier.GetComponent<MusketeerAppearance>()) return;

            var bowPose = bowAimer.Find("BowAnim");
            var bow = bowPose ? bowPose.Find("Bow") : null;
            var geometry = bow ? bow.GetComponent<MeshFilter>() : null;
            var renderer = bow ? bow.GetComponent<MeshRenderer>() : null;
            if (!geometry || !renderer || !renderer.sharedMaterial) return;

            var appearance = soldier.gameObject.AddComponent<MusketeerAppearance>();
            appearance.body = soldier.spriteAnimator;
            appearance.previousUniform = soldier.spriteAnimator.sprite2;
            appearance.oldBowGeometry = geometry;
            appearance.oldBowRenderer = renderer;
            appearance.oldBowMesh = geometry.sharedMesh;
            appearance.oldBowVisible = renderer.enabled;
            appearance.archer = archer;
            appearance.scout = archer.GetComponent<LineOfSight>();
            appearance.bowPose = bowPose;
            appearance.bowGrip = bow.localPosition;

            if (!equipmentMaterial)
                equipmentMaterial = new Material(renderer.sharedMaterial)
                    { name = "Musketeer flat palette", mainTexture = equipmentPalette };

            // BowAnim stretches and squashes the stock bow during the draw and release.
            var mount = new GameObject("Musketeer weapon mount");
            appearance.weaponMount = mount.transform;
            appearance.weaponMount.SetParent(soldier.transform, false);
            appearance.SyncWeaponMount();

            var weapon = new GameObject("Musketeer musket") { layer = bow.gameObject.layer };
            weapon.transform.SetParent(appearance.weaponMount, false);
            weapon.AddComponent<MeshFilter>().sharedMesh = musketMesh;
            appearance.musketRenderer = weapon.AddComponent<MeshRenderer>();
            appearance.musketRenderer.sharedMaterial = equipmentMaterial;
            appearance.reloadPose = weapon.AddComponent<MusketeerReloadPose>();

            var fieldKit = new GameObject("Musketeer strap and pouch") { layer = bow.gameObject.layer };
            var bodyMount = appearance.body.aimer ? appearance.body.aimer.parent : null;
            fieldKit.transform.SetParent(bodyMount ? bodyMount : soldier.transform, false);
            fieldKit.AddComponent<MeshFilter>().sharedMesh = fieldKitMesh;
            appearance.fieldKitRenderer = fieldKit.AddComponent<MeshRenderer>();
            appearance.fieldKitRenderer.sharedMaterial = equipmentMaterial;

            geometry.sharedMesh = null;
            renderer.enabled = false;
            appearance.body.SetSprite2(uniform);
        }

        [HarmonyPatch(typeof(AgentSelected), "UpdateHover")]
        private static class KeepEquipmentRigidOnHover
        {
            private static void Postfix(AgentSelected __instance)
            {
                var soldier = __instance.agent;
                var appearance = soldier ? soldier.GetComponent<MusketeerAppearance>() : null;
                if (!appearance) return;

                // The stock shader offsets vertices by their face normals on hover.
                ClearHover(appearance.musketRenderer);
                ClearHover(appearance.fieldKitRenderer);
            }

            private static void ClearHover(MeshRenderer renderer)
            {
                if (!renderer) return;
                if (equipmentProperties == null) equipmentProperties = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(equipmentProperties);
                equipmentProperties.SetFloat(HoverProperty, 0f);
                renderer.SetPropertyBlock(equipmentProperties);
            }
        }

        private void LateUpdate()
        {
            if (!archer || !bowPose || !weaponMount) return;

            bool raise = MusketeerTargeting.IsAvailable(archer.agent)
                && archer.aiming?.active != true && archer.shooting?.active != true
                && !(reloadPose && reloadPose.isActiveAndEnabled)
                && !MusketeerTargeting.HasSpottedEnemyInRange(scout, bowPose.parent.position);
            float seconds = raise ? RaiseSeconds : LowerSeconds;
            carry = Mathf.MoveTowards(carry, raise ? 1f : 0f, Time.deltaTime / seconds);
            SyncWeaponMount(carry);
        }

        private bool SyncWeaponMount(float raised = 0f)
        {
            if (!bowPose || !weaponMount) return false;

            Vector3 position = bowPose.TransformPoint(bowGrip);
            Quaternion rotation = bowPose.rotation;
            if (raised > 0f)
            {
                float blend = Mathf.SmoothStep(0f, 1f, raised);
                float gripHeight = transform.InverseTransformPoint(position).y;
                var shoulderGrip = transform.TransformPoint(new Vector3(CarrySide, gripHeight + CarryLift, CarryForward));
                position = Vector3.Lerp(position, shoulderGrip, blend);
                rotation = Quaternion.Slerp(rotation, transform.rotation * ShoulderRotation, blend);
            }
            weaponMount.SetPositionAndRotation(position, rotation);
            bool visible = bowPose.gameObject.activeInHierarchy;
            if (weaponMount.gameObject.activeSelf != visible) weaponMount.gameObject.SetActive(visible);
            return true;
        }

        [HarmonyPatch(typeof(SpriteAnimator), nameof(SpriteAnimator.SetSprite2))]
        private static class RetainMusketeerUniform
        {
            private static void Prefix(SpriteAnimator __instance, ref Sprite newSprite)
            {
                if (!uniform) return;
                var appearance = __instance.gameObject.GetComponentInParentIncludingInactive<MusketeerAppearance>();
                if (appearance && appearance.body == __instance)
                    newSprite = uniform;
            }
        }

        internal void BeginReload(float readyAt)
        {
            if (reloadPose) reloadPose.Play(readyAt, equipmentMaterial, glovePaletteU);
        }

        internal void PrepareOrigin(Vector3 position)
        {
            nextOrigin = position;
            originPrepared = true;
        }

        internal bool TakeOrigin(out Vector3 position)
        {
            position = nextOrigin;
            bool prepared = originPrepared;
            originPrepared = false;
            return prepared;
        }

        internal static bool TryGetMuzzle(Agent soldier, out Vector3 muzzle)
        {
            muzzle = default;
            var appearance = soldier ? soldier.GetComponent<MusketeerAppearance>() : null;
            if (!appearance || !appearance.musketRenderer || !appearance.SyncWeaponMount()) return false;

            // A firing event can arrive before the next LateUpdate finishes lowering the carry pose.
            appearance.carry = 0f;
            muzzle = appearance.musketRenderer.transform.TransformPoint(localMuzzle);
            return true;
        }

        private void OnDestroy()
        {
            if (oldBowGeometry) oldBowGeometry.sharedMesh = oldBowMesh;
            if (oldBowRenderer) oldBowRenderer.enabled = oldBowVisible;
            if (weaponMount) Destroy(weaponMount.gameObject);
            if (fieldKitRenderer) Destroy(fieldKitRenderer.gameObject);

            var spriteAnimator = body;
            body = null;
            if (spriteAnimator && previousUniform && spriteAnimator.gameObject.activeInHierarchy)
                spriteAnimator.SetSprite2(previousUniform);
        }
    }
}
