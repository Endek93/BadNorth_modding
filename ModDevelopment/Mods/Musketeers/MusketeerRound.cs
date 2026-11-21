using System.Collections.Generic;
using UnityEngine;
using Voxels.TowerDefense;
using Voxels.TowerDefense.Ballistics;

namespace BadNorth.Musketeers
{
    internal sealed class MusketeerRound : MonoBehaviour
    {
        private static Sprite pelletSprite;
        private static Camera viewCamera;

        private readonly List<Renderer> hiddenRenderers = new List<Renderer>();
        private SpriteRenderer pellet;
        private Arrow arrow;
        private Agent reservedTarget;
        private float expiresAt;
        private bool inFlight;

        internal static MusketeerRound For(Arrow arrow)
        {
            var round = arrow.GetComponent<MusketeerRound>();
            return round ? round : arrow.gameObject.AddComponent<MusketeerRound>();
        }

        internal void HideArrow()
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == pellet || !renderer.enabled) continue;
                hiddenRenderers.Add(renderer);
                renderer.enabled = false;
            }
        }

        internal void BeginFlight(Arrow projectile, float lifetime)
        {
            HideArrow();
            if (!pellet)
            {
                var graphic = new GameObject("Musketeer pellet");
                graphic.layer = projectile.spriteRenderer ? projectile.spriteRenderer.gameObject.layer : gameObject.layer;
                graphic.transform.SetParent(transform, false);
                pellet = graphic.AddComponent<SpriteRenderer>();
                if (!pelletSprite) pelletSprite = CreatePelletSprite();
                pellet.sprite = pelletSprite;
                pellet.color = new Color32(25, 23, 22, 255);
            }

            pellet.enabled = true;
            arrow = projectile;
            expiresAt = Time.time + lifetime;
            inFlight = true;
        }

        internal void RestoreAppearance()
        {
            if (pellet) pellet.enabled = false;
            foreach (var renderer in hiddenRenderers)
                if (renderer) renderer.enabled = true;
            hiddenRenderers.Clear();
        }

        internal void ReserveTarget(Agent target)
        {
            if (!inFlight || !target || ReferenceEquals(reservedTarget, target)) return;

            ReleaseTarget();
            reservedTarget = target;
            MusketeerTargeting.ChangeInFlightCount(target, 1);
        }

        private void ReleaseTarget()
        {
            // A despawned Unity object still has a dictionary key until the shot is recycled.
            if (ReferenceEquals(reservedTarget, null)) return;
            MusketeerTargeting.ChangeInFlightCount(reservedTarget, -1);
            reservedTarget = null;
        }

        private void LateUpdate()
        {
            if (!inFlight) return;
            if (!viewCamera || !viewCamera.isActiveAndEnabled)
            {
                var levelCamera = Singleton<LevelCamera>.instance;
                viewCamera = levelCamera && levelCamera.cameraRef ? levelCamera.cameraRef : Camera.main;
            }
            // The pooled arrow rotates with velocity. The pellet's round silhouette faces the camera.
            pellet.transform.rotation = viewCamera ? viewCamera.transform.rotation : Quaternion.identity;
        }

        private void FixedUpdate()
        {
            if (inFlight && Time.time >= expiresAt && arrow.projectiling.active)
                arrow.ReturnToPool();
        }

        private void OnDisable()
        {
            ReleaseTarget();
            inFlight = false;
            arrow = null;
            RestoreAppearance();
        }

        private static Sprite CreatePelletSprite()
        {
            const int size = 16;
            const float center = (size - 1) * 0.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = new Vector2(x - center, y - center).magnitude;
                byte opacity = (byte)(255f * Mathf.Clamp01(7.5f - distance));
                pixels[y * size + x] = new Color32(255, 255, 255, opacity);
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Musketeer pellet",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), Vector2.one * 0.5f, 400f);
        }
    }
}
