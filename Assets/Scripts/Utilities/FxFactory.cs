using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace LastShift.Utilities
{
    /// <summary>
    /// Runtime effect helpers: soft shadows, glows, URP 2D lights and lightweight
    /// particle systems (steam, cold fog, sparks). All visuals stay readable and
    /// never carry gameplay logic.
    /// </summary>
    public static class FxFactory
    {
        static Material particleMaterial;

        static Material ParticleMaterial
        {
            get
            {
                if (particleMaterial == null)
                {
                    Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                    if (s == null) s = Shader.Find("Sprites/Default");
                    particleMaterial = new Material(s);
                    particleMaterial.mainTexture = TextureFactory.SoftCircle().texture;
                }
                return particleMaterial;
            }
        }

        // ---------------- sprites ----------------

        /// <summary>Soft elliptical drop shadow under an object.</summary>
        public static SpriteRenderer Shadow(Transform parent, Vector2 size, float alpha = 0.4f, int order = 1)
        {
            var go = new GameObject("Shadow");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0.05f, -0.12f, 0f);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = TextureFactory.SoftCircle();
            sr.sharedMaterial = SpriteFactory.UnlitMaterial;
            sr.color = new Color(0f, 0f, 0f, alpha);
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>Soft colored glow (unlit, always visible).</summary>
        public static SpriteRenderer Glow(Transform parent, Color color, Vector2 size, int order)
        {
            var go = new GameObject("Glow");
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = TextureFactory.SoftCircle();
            sr.sharedMaterial = SpriteFactory.UnlitMaterial;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        // ---------------- URP 2D lights ----------------

        public static Light2D GlobalLight(Color color, float intensity)
        {
            var go = new GameObject("GlobalLight2D");
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.color = color;
            light.intensity = intensity;
            return light;
        }

        public static Light2D PointLight(Transform parent, Vector2 localPos, Color color,
            float radius, float intensity)
        {
            var go = new GameObject("PointLight2D");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(localPos.x, localPos.y, 0f);
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.pointLightInnerRadius = radius * 0.2f;
            light.pointLightOuterRadius = radius;
            light.falloffIntensity = 0.6f;
            return light;
        }

        // ---------------- particles ----------------

        /// <summary>
        /// Emission rate for an area effect, budgeted by fill rate rather than by area.
        /// A soft particle costs its whole quad every frame, so the price of a plume is
        /// (live particles x particle area) — i.e. how many translucent layers pile up
        /// over the zone. `layers` is that stack depth; density lost to a lower rate is
        /// paid back with per-particle alpha, which is free to draw.
        /// </summary>
        static float RateForCoverage(Vector2 areaSize, float avgParticleSize, float avgLifetime,
            float layers, float minRate, float maxRate)
        {
            float zone = Mathf.Max(1f, areaSize.x * areaSize.y);
            float particleArea = avgParticleSize * avgParticleSize;
            float rate = zone * layers / (particleArea * avgLifetime);
            return Mathf.Clamp(rate, minRate, maxRate);
        }

        static ParticleSystem BaseParticles(Transform parent, string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = ParticleMaterial;
            renderer.sortingOrder = order;
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            return ps;
        }

        /// <summary>Warm white-grey steam plume rising from a rect area.</summary>
        public static ParticleSystem Steam(Transform parent, Vector2 areaSize, int order = 7)
        {
            var ps = BaseParticles(parent, "SteamFX", order);
            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.95f, 0.92f, 0.88f, 0.35f), new Color(0.85f, 0.85f, 0.85f, 0.2f));
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            main.maxParticles = 48;
            var emission = ps.emission;
            // ~2.2 layers, which is what the old linear rate already gave the shipped
            // steam rects — the clamp only guards against an oversized zone.
            emission.rateOverTime = RateForCoverage(areaSize, 0.85f, 1.25f, 2.2f, 6f, 30f);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(areaSize.x, areaSize.y, 0.1f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.y = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.5f, 0.25f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            return ps;
        }

        /// <summary>Low rolling blue-white cold fog over a rect area.</summary>
        public static ParticleSystem ColdFog(Transform parent, Vector2 areaSize, int order = 7)
        {
            var ps = BaseParticles(parent, "ColdFogFX", order);
            var main = ps.main;
            // Alpha raised ~1.8x against the pre-budget values (0.22/0.14) to pay back
            // the density lost with the lower emission rate below; still low enough
            // that routes and the engineer read through the fog.
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.75f, 0.87f, 1f, 0.40f), new Color(0.85f, 0.95f, 1f, 0.25f));
            main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            main.maxParticles = 64;
            var emission = ps.emission;
            // The cold rooms carry the biggest zones in the game (6 x 7.5 units), where
            // the old linear rate stacked ~9 layers of 2.4-unit blob — by far the
            // heaviest fill in any room. 3.5 layers still reads as solid fog.
            emission.rateOverTime = RateForCoverage(areaSize, 1.8f, 2.2f, 3.5f, 4f, 24f);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(areaSize.x, areaSize.y, 0.1f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.6f, 0.3f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            return ps;
        }

        /// <summary>Short repair-spark burst emitter (played while repairing).</summary>
        public static ParticleSystem Sparks(Transform parent, int order = 9)
        {
            var ps = BaseParticles(parent, "SparksFX", order);
            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.85f, 0.4f, 1f), new Color(1f, 0.6f, 0.2f, 1f));
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
            main.gravityModifier = 1.4f;
            main.maxParticles = 24; // sparks are tiny, but every objective owns an emitter
            var emission = ps.emission;
            emission.rateOverTime = 14f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f;
            return ps;
        }
    }
}
