using UnityEngine;
using Voxels.TowerDefense;

namespace BadNorth.Musketeers
{
    internal sealed class MusketeerShotEffects : MonoBehaviour
    {
        private const float FlashSeconds = 0.075f;
        private static Sprite flashSprite;
        private static Camera battleCamera;

        private SpriteRenderer flash;
        private MusketeerMuzzleSmoke smoke;
        private Vector3 position;
        private Vector3 direction;
        private float firedAt;

        internal static void Play(Agent shooter, Vector3 origin, Vector3 velocity, int layer)
        {
            if (!shooter || !shooter.gameObject.activeInHierarchy) return;

            var effects = shooter.GetComponent<MusketeerShotEffects>();
            if (!effects) effects = shooter.gameObject.AddComponent<MusketeerShotEffects>();
            if (MusketeerAppearance.TryGetMuzzle(shooter, out var muzzle)) origin = muzzle;
            effects.Fire(origin, velocity.normalized, layer);
        }

        private void Fire(Vector3 origin, Vector3 heading, int layer)
        {
            if (!flash)
            {
                var emitter = new GameObject("Musketeer shot effects") { layer = layer };
                emitter.transform.SetParent(transform, false);
                flash = emitter.AddComponent<SpriteRenderer>();
                if (!flashSprite) flashSprite = CreateFlashSprite();
                flash.sprite = flashSprite;
                flash.sortingOrder = 1;
            }

            position = origin + heading * 0.07f;
            direction = heading;
            firedAt = Time.unscaledTime;
            flash.enabled = true;
            enabled = true;
            LateUpdate();

            if (!smoke)
            {
                var plume = new GameObject("Musketeer muzzle smoke") { layer = layer };
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(plume, gameObject.scene);
                smoke = plume.AddComponent<MusketeerMuzzleSmoke>();
            }
            smoke.Play(origin, heading, battleCamera);
            MusketeerShotAudio.Play(position, battleCamera);
        }

        private void OnDestroy()
        {
            if (smoke)
                Destroy(smoke.gameObject, smoke.gameObject.activeSelf ? MusketeerMuzzleSmoke.Lifetime : 0f);
        }

        private void LateUpdate()
        {
            float progress = (Time.unscaledTime - firedAt) / FlashSeconds;
            if (progress >= 1f)
            {
                flash.enabled = false;
                enabled = false;
                return;
            }

            flash.transform.position = position;
            flash.transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.65f, progress);
            flash.color = new Color(1f, 1f, 1f, 1f - progress * progress);
            if (!battleCamera || !battleCamera.isActiveAndEnabled)
            {
                var levelCamera = Singleton<LevelCamera>.instance;
                battleCamera = levelCamera && levelCamera.cameraRef ? levelCamera.cameraRef : Camera.main;
            }
            if (battleCamera)
            {
                Transform cameraPose = battleCamera.transform;
                float angle = Mathf.Atan2(Vector3.Dot(direction, cameraPose.up),
                    Vector3.Dot(direction, cameraPose.right)) * Mathf.Rad2Deg;
                flash.transform.rotation = cameraPose.rotation * Quaternion.AngleAxis(angle, Vector3.forward);
            }
        }

        private static Sprite CreateFlashSprite()
        {
            const int width = 48;
            const int height = 32;
            const float centerX = 14f;
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var point = new Vector2((x + 0.5f - centerX) / 20f, (y + 0.5f - height * 0.5f) / 14f);
                float center = Mathf.Clamp01(1.25f - point.magnitude * 2.8f);
                float forward = RayIntensity(point, new Vector2(1.6f, 0f), 0.20f);
                float upper = RayIntensity(point, new Vector2(0.72f, 0.64f), 0.14f);
                float lower = RayIntensity(point, new Vector2(0.58f, -0.55f), 0.14f);
                float intensity = Mathf.Max(center, Mathf.Max(forward, Mathf.Max(upper, lower)));
                var color = Color.Lerp(new Color(1f, 0.32f, 0.055f), new Color(1f, 0.96f, 0.74f), intensity);
                color.a = Mathf.Clamp01(intensity * 1.6f);
                pixels[y * width + x] = color;
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Musketeer muzzle flash",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(centerX / width, 0.5f), 110f);
        }

        private static float RayIntensity(Vector2 point, Vector2 tip, float width)
        {
            float progress = Vector2.Dot(point, tip) / tip.sqrMagnitude;
            if (progress < 0f || progress >= 1f) return 0f;

            float remaining = 1f - progress;
            float distance = (point - tip * progress).magnitude;
            return Mathf.Clamp01(1f - distance / (width * remaining)) * Mathf.Sqrt(remaining);
        }
    }
}
