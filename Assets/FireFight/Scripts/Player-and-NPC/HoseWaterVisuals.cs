using UnityEngine;
using UnityEngine.Rendering;

/// <summary>A narrow jet or a dense pressure-fed cone, with moving water and feathered edges.</summary>
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
    private Mesh coneMesh;
    private MeshRenderer coneRenderer;
    private Material coneMaterial;
    private Texture2D coneTexture;
    private Vector3[] coneVertices;
    private Vector2[] coneUvs;
    private Color[] coneColors;
    private float nextConeUpdate;
    private bool wasSpraying;
    private readonly Vector3[] coneEdges = new Vector3[33];
    private const int ConeSegments = 32;
    private const int ConeRings = 10;
    private const int ConeSheets = 4;

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
        var splashCollision = splash.collision;
        splashCollision.enabled = false;
        var splashMain = splash.main;
        splashMain.maxParticles = 128;
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
        if (hose.Pattern == SprayPattern.Cone) BuildPressureCone();
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
        main.maxParticles = 1024;
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
        collision.quality = ParticleSystemCollisionQuality.Medium;
        collision.radiusScale = 0.2f;
        return system;
    }

    private void LateUpdate()
    {
        if (hose == null || droplets == null) return;
        bool cone = hose.Pattern == SprayPattern.Cone;
        if (cone && coneRenderer == null) BuildPressureCone();
        jet.enabled = hose.IsSpraying && !cone;
        if (coneRenderer != null) coneRenderer.enabled = hose.IsSpraying && cone;
        if (!hose.IsSpraying)
        {
            if (droplets.isEmitting) droplets.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            wasSpraying = false;
            return;
        }
        if (!wasSpraying) nextConeUpdate = 0f;
        wasSpraying = true;
        Vector3 direction = hose.Direction;
        droplets.transform.SetPositionAndRotation(hose.Origin + direction * 0.06f, Quaternion.LookRotation(direction));
        var main = droplets.main;
        float speed = cone ? 24f : 26f;
        main.startSpeed = speed;
        main.startLifetime = Mathf.Max(0.02f, (cone ? hose.SprayRange : hose.VisibleLength) / speed);
        main.startSize = cone ? new ParticleSystem.MinMaxCurve(0.05f, 0.1f) : new ParticleSystem.MinMaxCurve(0.025f, 0.045f);
        var emission = droplets.emission;
        emission.rateOverTime = cone ? 900f : 260f;
        var shape = droplets.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = cone ? hose.ConeHalfAngle : 0.6f;
        shape.radius = cone ? 0.025f : 0.012f;
        if (!droplets.isPlaying) droplets.Play();

        if (cone) UpdatePressureCone(direction);

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

    private void BuildPressureCone()
    {
        // The continuous core provides the water volume; particles add motion and soft edges.
        var obj = new GameObject("Pressurized Cone");
        obj.layer = 2;
        obj.transform.SetParent(visualRoot.transform, false);
        coneMesh = new Mesh { name = "Pressure Water Cone" };
        coneMesh.MarkDynamic();
        obj.AddComponent<MeshFilter>().sharedMesh = coneMesh;
        coneRenderer = obj.AddComponent<MeshRenderer>();
        coneTexture = new Texture2D(64, 128, TextureFormat.RGBA32, false);
        coneTexture.name = "Moving Water Streaks";
        coneTexture.wrapMode = TextureWrapMode.Repeat;
        var pixels = new Color[64 * 128];
        for (int y = 0; y < 128; y++)
        for (int x = 0; x < 64; x++)
        {
            float streak = Mathf.PerlinNoise(x * 0.35f, y * 0.04f);
            pixels[x + y * 64] = new Color(0.83f, 0.94f, 1f, Mathf.Lerp(0.25f, 0.85f, streak));
        }
        coneTexture.SetPixels(pixels);
        coneTexture.Apply();
        coneMaterial = MakeMaterial(coneTexture);
        coneRenderer.sharedMaterial = coneMaterial;
        coneRenderer.shadowCastingMode = ShadowCastingMode.Off;
        coneRenderer.receiveShadows = false;
        coneRenderer.enabled = false;

        int shellCount = ConeRings * (ConeSegments + 1);
        int count = shellCount + ConeSheets * ConeRings * 3;
        coneVertices = new Vector3[count];
        coneUvs = new Vector2[count];
        coneColors = new Color[count];
        int[] triangles = new int[(ConeRings - 1) * ConeSegments * 6 + ConeSheets * (ConeRings - 1) * 12];
        int index = 0;
        for (int ring = 0; ring < ConeRings - 1; ring++)
        for (int segment = 0; segment < ConeSegments; segment++)
        {
            int a = ring * (ConeSegments + 1) + segment;
            int b = a + ConeSegments + 1;
            triangles[index++] = a; triangles[index++] = b; triangles[index++] = a + 1;
            triangles[index++] = a + 1; triangles[index++] = b; triangles[index++] = b + 1;
        }
        for (int sheet = 0; sheet < ConeSheets; sheet++)
        for (int ring = 0; ring < ConeRings - 1; ring++)
        for (int side = 0; side < 2; side++)
        {
            int a = shellCount + sheet * ConeRings * 3 + ring * 3 + side;
            triangles[index++] = a; triangles[index++] = a + 3; triangles[index++] = a + 1;
            triangles[index++] = a + 1; triangles[index++] = a + 3; triangles[index++] = a + 4;
        }
        coneMesh.vertices = coneVertices;
        coneMesh.triangles = triangles;
        // UVs and opacity do not depend on clipping. Upload once; scroll the material instead.
        for (int ring = 0; ring < ConeRings; ring++)
        {
            float t = (float)ring / (ConeRings - 1);
            float fade = Mathf.SmoothStep(0f, 1f, t * 12f) * Mathf.SmoothStep(0f, 1f, (1f - t) * 5f);
            for (int segment = 0; segment <= ConeSegments; segment++)
            {
                int vertex = ring * (ConeSegments + 1) + segment;
                coneUvs[vertex] = new Vector2((float)segment / ConeSegments * 2f, t * 3f);
                coneColors[vertex] = new Color(0.85f, 0.95f, 1f, fade * 0.24f);
            }
            for (int sheet = 0; sheet < ConeSheets; sheet++)
            for (int side = 0; side < 3; side++)
            {
                int vertex = shellCount + sheet * ConeRings * 3 + ring * 3 + side;
                coneUvs[vertex] = new Vector2(side * 0.5f + sheet * 0.23f, t * 3f);
                coneColors[vertex] = new Color(0.85f, 0.95f, 1f, side == 1 ? fade * 0.24f : 0f);
            }
        }
        coneMesh.uv = coneUvs;
        coneMesh.colors = coneColors;
    }

    private void UpdatePressureCone(Vector3 direction)
    {
        Quaternion rotation = Quaternion.LookRotation(direction);
        coneRenderer.transform.SetPositionAndRotation(hose.Origin, rotation);
        var scroll = new Vector2(0f, -Time.time * 9f);
        coneMaterial.SetTextureOffset(coneMaterial.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex", scroll);
        if (Time.time < nextConeUpdate) return;
        nextConeUpdate = Time.time + 0.05f;
        float angle = hose.ConeHalfAngle * Mathf.Deg2Rad;
        for (int segment = 0; segment <= ConeSegments; segment++)
        {
            float theta = segment * Mathf.PI * 2f / ConeSegments;
            Vector3 ray = new Vector3(Mathf.Cos(theta) * Mathf.Sin(angle),
                Mathf.Sin(theta) * Mathf.Sin(angle), Mathf.Cos(angle));
            coneEdges[segment] = ray * hose.GetVisualReach(rotation * ray);
        }
        for (int ring = 0; ring < ConeRings; ring++)
        {
            float t = (float)ring / (ConeRings - 1);
            for (int segment = 0; segment <= ConeSegments; segment++)
            {
                int index = ring * (ConeSegments + 1) + segment;
                coneVertices[index] = coneEdges[segment] * t;
            }
            int shellCount = ConeRings * (ConeSegments + 1);
            for (int sheet = 0; sheet < ConeSheets; sheet++)
            {
                int segment = sheet * ConeSegments / (ConeSheets * 2);
                Vector3 left = coneEdges[segment] * t;
                Vector3 right = coneEdges[segment + ConeSegments / 2] * t;
                Vector3 center = Vector3.forward * Mathf.Min(hose.SprayRange, hose.VisibleLength) * t;
                int index = shellCount + sheet * ConeRings * 3 + ring * 3;
                coneVertices[index] = left;
                coneVertices[index + 1] = center;
                coneVertices[index + 2] = right;
            }
        }
        coneMesh.vertices = coneVertices;
        coneMesh.bounds = new Bounds(Vector3.zero, Vector3.one * Mathf.Max(0.1f, hose.SprayRange * 2f));
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
        if (coneMesh != null) Destroy(coneMesh);
        if (coneMaterial != null) Destroy(coneMaterial);
        if (coneTexture != null) Destroy(coneTexture);
    }
}
