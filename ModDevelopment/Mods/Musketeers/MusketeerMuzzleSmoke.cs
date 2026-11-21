using UnityEngine;

namespace BadNorth.Musketeers
{
    internal sealed class MusketeerMuzzleSmoke : MonoBehaviour
    {
        internal const float Lifetime = 1.65f;
        private static Sprite puffSprite;
        private readonly SpriteRenderer[] puffs = new SpriteRenderer[4];
        private Camera view;
        private Vector3 heading;
        private Vector3 across;
        private float emittedAt;

        internal void Play(Vector3 origin, Vector3 direction, Camera camera)
        {
            if (!puffSprite) puffSprite = MakePuff();
            for (int i = 0; i < puffs.Length; i++)
            {
                if (puffs[i]) continue;
                var puff = new GameObject("Smoke puff") { layer = gameObject.layer };
                puff.transform.SetParent(transform, false);
                puffs[i] = puff.AddComponent<SpriteRenderer>();
                puffs[i].sprite = puffSprite;
                puffs[i].sortingOrder = 2;
            }

            transform.position = origin;
            heading = direction;
            across = Vector3.Cross(Vector3.up, direction).normalized;
            view = camera;
            emittedAt = Time.time;
            gameObject.SetActive(true);
            LateUpdate();
        }

        private void LateUpdate()
        {
            float elapsed = Time.time - emittedAt;
            if (elapsed >= Lifetime)
            {
                gameObject.SetActive(false);
                return;
            }

            Quaternion facing = view ? view.transform.rotation : Quaternion.Euler(45f, 0f, 0f);
            for (int i = 0; i < puffs.Length; i++)
            {
                float start = i * 0.032f;
                float age = Mathf.Clamp01((elapsed - start) / (Lifetime - start));
                float spread = Mathf.Sqrt(age);
                float drift = 1f - (1f - age) * (1f - age);
                float side = (i - 1.5f) * 0.044f;
                var puff = puffs[i];
                puff.transform.localPosition = heading * (0.025f + 0.025f * i + (0.16f + 0.024f * i) * drift)
                    + across * (side * (0.25f + spread)) + Vector3.up * ((0.15f + 0.029f * i) * age);
                puff.transform.rotation = facing * Quaternion.AngleAxis(61f * i + 18f * age, Vector3.forward);
                puff.transform.localScale = Vector3.one * ((0.34f + 0.06f * i) + (0.78f + 0.08f * i) * spread);
                float appear = i == 0 ? 1f : Mathf.SmoothStep(0f, 1f, (elapsed - start) / 0.055f);
                float fade = 1f - Mathf.SmoothStep(0f, 1f, age);
                puff.color = new Color(0.83f, 0.81f, 0.76f, 0.60f * appear * fade);
            }
        }

        private static Sprite MakePuff()
        {
            const int size = 48;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var point = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f);
                float density = Mathf.Max(Lobe(point, new Vector2(-0.30f, -0.13f), 0.59f),
                    Lobe(point, new Vector2(0.32f, -0.14f), 0.57f));
                density = Mathf.Max(density, Lobe(point, new Vector2(-0.03f, 0.26f), 0.62f));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(density * 255));
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Musketeer smoke puff",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), Vector2.one * 0.5f, 180f);
        }

        private static float Lobe(Vector2 point, Vector2 center, float radius) =>
            1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(radius * 0.38f, radius, (point - center).magnitude));
    }
}
