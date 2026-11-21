using UnityEngine;

namespace BadNorth.Musketeers
{
    // Animate the replacement musket locally while the original Archer controller handles movement.
    internal sealed class MusketeerReloadPose : MonoBehaviour
    {
        private static Mesh gloveMesh;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private Transform glove;
        private float readyAt;

        private void Awake()
        {
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
            enabled = false;
        }

        internal void Play(float deadline, Material material, float paletteU)
        {
            if (!glove)
            {
                if (!gloveMesh) gloveMesh = MakeGlove(paletteU);
                var hand = new GameObject("Musketeer reload hand") { layer = gameObject.layer };
                glove = hand.transform;
                glove.SetParent(transform, false);
                hand.AddComponent<MeshFilter>().sharedMesh = gloveMesh;
                hand.AddComponent<MeshRenderer>().sharedMaterial = material;
                hand.SetActive(false);
            }
            readyAt = deadline;
            enabled = true;
        }

        private void LateUpdate()
        {
            if (Time.time >= readyAt)
            {
                enabled = false;
                return;
            }

            float progress = Mathf.Clamp01(1f - (readyAt - Time.time) / MusketeerCombat.ReloadSeconds);
            float lower = Ease(progress, 0.02f, 0.17f) * (1f - Ease(progress, 0.79f, 1f));
            float adjust = Gesture(progress, 0.22f, 0.59f) + 0.55f * Gesture(progress, 0.57f, 0.76f);
            transform.localPosition = restPosition + new Vector3(-0.016f, -0.043f, -0.030f) * lower;
            transform.localRotation = restRotation * Quaternion.Euler(44f * lower - 9f * adjust, -17f * lower, -14f * lower);

            bool handling = progress >= 0.13f && progress < 0.87f;
            if (glove.gameObject.activeSelf != handling) glove.gameObject.SetActive(handling);
            if (handling)
            {
                float reach = Ease(progress, 0.13f, 0.25f) * (1f - Ease(progress, 0.74f, 0.87f));
                glove.localPosition = new Vector3(-0.068f + 0.048f * reach, -0.011f + 0.025f * adjust,
                    -0.022f + 0.126f * reach - 0.052f * adjust);
                glove.localRotation = Quaternion.Euler(12f - 30f * adjust, 0f, -18f);
            }
        }

        private void OnDisable()
        {
            transform.localPosition = restPosition;
            transform.localRotation = restRotation;
            if (glove) glove.gameObject.SetActive(false);
        }

        private static float Ease(float progress, float start, float end) =>
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, progress));

        private static float Gesture(float progress, float start, float end) =>
            Mathf.Sin(Mathf.PI * Mathf.InverseLerp(start, end, progress));

        private static Mesh MakeGlove(float paletteU)
        {
            var uv = new Vector2(paletteU, 0.5f);
            var mesh = new Mesh
            {
                name = "Musketeer reload glove",
                vertices = new[]
                {
                    new Vector3(0f, 0.014f, 0f), new Vector3(0f, -0.012f, 0f),
                    new Vector3(0.018f, 0f, 0f), new Vector3(0f, 0f, 0.026f),
                    new Vector3(-0.018f, 0f, 0f), new Vector3(0f, 0f, -0.022f)
                },
                triangles = new[] { 0, 3, 2, 0, 4, 3, 0, 5, 4, 0, 2, 5, 1, 2, 3, 1, 3, 4, 1, 4, 5, 1, 5, 2 },
                uv = new[] { uv, uv, uv, uv, uv, uv }
            };
            mesh.RecalculateNormals();
            mesh.UploadMeshData(true);
            return mesh;
        }
    }
}
