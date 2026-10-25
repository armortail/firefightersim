using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Continuous water droplets: a narrow jet or a genuinely widening fog cone.</summary>
[DisallowMultipleComponent]
public class HoseWaterVisuals : MonoBehaviour
{
    private HoseController hose;
    private ParticleSystem droplets;
    private ParticleSystem splash;
    private LineRenderer jet;
    private Material dropMaterial;
    private Material jetMaterial;
    private Texture2D dropTexture;
    private Texture2D jetTexture;
    private GameObject visualRoot;
    private float splashTimer;

    private void Awake()
    {
        hose = GetComponent<HoseController>();
        if (hose == null) { enabled = false; return; }
        dropTexture = MakeTexture(false);
        jetTexture = MakeTexture(true);
        dropMaterial = MakeMaterial(dropTexture);
        jetMaterial = MakeMaterial(jetTexture);
        visualRoot = new GameObject("Hose Water");
        visualRoot.layer = 2;
        visualRoot.transform.SetParent(transform, false);
        droplets = CreateParticles("Water Droplets", visualRoot.transform);
        splash = CreateParticles("Contact Splash", visualRoot.transform);
        var splashMain = splash.main;
        splashMain.loop = false;
        splashMain.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
        splashMain.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.5f);
        splashMain.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
        var splashShape = splash.shape;
        splashShape.shapeType = ParticleSystemShapeType.Hemisphere;
        splashShape.radius = 0.08f;
        var splashEmission = splash.emission;
        splashEmission.rateOverTime = 0;
        jet = new GameObject("Jet Core").AddComponent<LineRenderer>();
        jet.transform.SetParent(visualRoot.transform, false);
        jet.sharedMaterial = jetMaterial;
        jet.useWorldSpace = true;
        jet.positionCount = 12;
        jet.startWidth = 0.055f;
        jet.endWidth = 0.09f;
        jet.startColor = new Color(0.85f, 0.95f, 1f, 0.6f);
        jet.endColor = new Color(0.75f, 0.9f, 1f, 0.35f);
        jet.numCapVertices = 3;
        jet.shadowCastingMode = ShadowCastingMode.Off;
        jet.receiveShadows = false;
        jet.enabled = false;
    }

    private ParticleSystem CreateParticles(string label, Transform parent)
    {
        var obj = new GameObject(label);
        obj.layer = 2;
        obj.transform.SetParent(parent, false);
        var system = obj.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main;
        main.playOnAwake = false;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 2500;
        main.startColor = new Color(0.8f, 0.93f, 1f, 0.5f);
        var renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = dropMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 2f;
        renderer.velocityScale = 0.035f;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        var color = system.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0.45f, 0.8f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;
        var collision = system.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.collidesWith = Physics.DefaultRaycastLayers;
        collision.bounce = 0f;
        collision.dampen = 1f;
        collision.lifetimeLoss = 1f;
        collision.quality = ParticleSystemCollisionQuality.High;
        collision.radiusScale = 0.2f;
        return system;
    }

    private void LateUpdate()
    {
        if (hose == null || droplets == null) return;
        bool cone = hose.Pattern == SprayPattern.Cone;
        jet.enabled = hose.IsSpraying && !cone;
        if (!hose.IsSpraying)
        {
            if (droplets.isEmitting) droplets.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            return;
        }
        Vector3 direction = hose.Direction;
        droplets.transform.SetPositionAndRotation(hose.Origin + direction * 0.06f, Quaternion.LookRotation(direction));
        var main = droplets.main;
        float speed = cone ? 12f : 20f;
        main.startSpeed = speed;
        main.startLifetime = Mathf.Max(0.02f, (cone ? hose.SprayRange : hose.VisibleLength) / speed);
        main.startSize = cone ? new ParticleSystem.MinMaxCurve(0.025f, 0.055f) : new ParticleSystem.MinMaxCurve(0.025f, 0.045f);
        var emission = droplets.emission;
        emission.rateOverTime = cone ? 850f : 180f;
        var shape = droplets.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = cone ? hose.ConeHalfAngle : 0.6f;
        shape.radius = cone ? 0.025f : 0.012f;
        if (!droplets.isPlaying) droplets.Play();

        if (!cone)
        {
            for (int i = 0; i < jet.positionCount; i++)
            {
                float t = (float)i / (jet.positionCount - 1);
                Vector3 side = Vector3.Cross(direction, Vector3.up).normalized;
                float ripple = Mathf.Sin(t * 35f - Time.time * 24f) * 0.008f * Mathf.Sin(t * Mathf.PI);
                jet.SetPosition(i, Vector3.Lerp(hose.Origin, hose.ImpactPoint, t) + side * ripple);
            }
        }
        splashTimer -= Time.deltaTime;
        if (hose.HasImpact && splashTimer <= 0f)
        {
            splashTimer = 0.04f;
            splash.transform.SetPositionAndRotation(hose.ImpactPoint + hose.ImpactNormal * 0.02f,
                Quaternion.FromToRotation(Vector3.up, hose.ImpactNormal));
            splash.Emit(cone ? 5 : 3);
        }
    }

    private static Texture2D MakeTexture(bool strip)
    {
        int width = strip ? 2 : 32;
        var texture = new Texture2D(width, 32, TextureFormat.RGBA32, false);
        texture.name = strip ? "Water Stream Soft Edge" : "Water Drop Soft Edge";
        texture.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color[width * 32];
        for (int y = 0; y < 32; y++)
        for (int x = 0; x < width; x++)
        {
            float dy = (y + 0.5f) / 16f - 1f;
            float dx = strip ? 0f : (x + 0.5f) / 16f - 1f;
            float alpha = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy)), 1.5f);
            pixels[x + y * width] = new Color(1, 1, 1, alpha);
        }
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    private static Material MakeMaterial(Texture texture)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
        var material = new Material(shader) { name = "Hose Water", mainTexture = texture };
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0);
        if (material.HasProperty("_SrcBlend")) material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetInt("_ZWrite", 0);
        if (material.HasProperty("_Cull")) material.SetInt("_Cull", (int)CullMode.Off);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)RenderQueue.Transparent;
        return material;
    }

    private void OnDestroy()
    {
        if (visualRoot != null) Destroy(visualRoot);
        if (dropMaterial != null) Destroy(dropMaterial);
        if (jetMaterial != null) Destroy(jetMaterial);
        if (dropTexture != null) Destroy(dropTexture);
        if (jetTexture != null) Destroy(jetTexture);
    }
}
