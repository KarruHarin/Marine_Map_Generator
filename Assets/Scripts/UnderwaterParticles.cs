using UnityEngine;

[ExecuteInEditMode]
public class UnderwaterParticles : MonoBehaviour
{
    [Header("Particle Settings")]
    public int maxParticles = 800;
    public float spawnRadius = 60f;
    public float spawnHeight = 40f;

    [Header("Drift")]
    public float riseSpeed  = 0.15f;
    public float driftSpeed = 0.08f;
    public float swayAmount = 0.4f;
    public float swaySpeed  = 0.2f;

    private ParticleSystem ps;
    private bool initialized = false;

    void OnEnable() => Setup();

    public void SetupParticleSystem() => Setup();

    void Setup()
    {
        ps = GetComponent<ParticleSystem>();
        if (ps == null) ps = gameObject.AddComponent<ParticleSystem>();

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main          = ps.main;
        main.loop         = true;
        main.playOnAwake  = true;
        main.duration     = 10f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 18f);
        main.startSpeed   = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
        main.startSize    = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
        main.maxParticles = maxParticles;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.015f;

        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.7f, 0.95f, 1f, 0.15f),
            new Color(1.0f, 1.00f, 1f, 0.55f));

        var em = ps.emission;
        em.enabled         = true;
        em.rateOverTime    = 40f;

        var sh = ps.shape;
        sh.enabled   = true;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale     = new Vector3(spawnRadius * 2f, spawnHeight, spawnRadius * 2f);

        var vel    = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space  = ParticleSystemSimulationSpace.World;
        vel.x      = new ParticleSystem.MinMaxCurve(-driftSpeed, driftSpeed);
        vel.y      = new ParticleSystem.MinMaxCurve(0.02f, riseSpeed);
        vel.z      = new ParticleSystem.MinMaxCurve(-driftSpeed, driftSpeed);

        var noise       = ps.noise;
        noise.enabled   = true;
        noise.strength  = swayAmount;
        noise.frequency = 0.15f;
        noise.scrollSpeed = swaySpeed;
        noise.octaveCount = 2;
        noise.damping   = true;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        var sc = new AnimationCurve();
        sc.AddKey(0f, 0f);
        sc.AddKey(0.15f, 1f);
        sc.AddKey(0.85f, 1f);
        sc.AddKey(1f, 0f);
        sol.size = new ParticleSystem.MinMaxCurve(1f, sc);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.8f, 0.97f, 1f), 0f),
                new GradientColorKey(new Color(1.0f, 1.00f, 1f), 0.5f),
                new GradientColorKey(new Color(0.7f, 0.92f, 1f), 1f),
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f,    0f),
                new GradientAlphaKey(0.6f,  0.15f),
                new GradientAlphaKey(0.6f,  0.85f),
                new GradientAlphaKey(0f,    1f),
            });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var rend          = ps.GetComponent<ParticleSystemRenderer>();
        rend.renderMode   = ParticleSystemRenderMode.Billboard;
        rend.sortingOrder = 2;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.material     = BuildParticleMat();

        ps.Play();
        initialized = true;
    }

    Material BuildParticleMat()
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend",   0f);
        mat.SetFloat("_ZWrite",  0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetColor("_BaseColor", new Color(0.8f, 0.97f, 1f, 0.5f));
        mat.renderQueue = 3200;
        return mat;
    }
}