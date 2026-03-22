using UnityEngine;
using System.Collections.Generic;

public class CoralAgent : MonoBehaviour
{
    public CoralEcosystem.CoralType type;
    public float age        = 0f;
    public float health     = 1f;
    public float stress     = 0f;
    public float growthRate = 0.0008f;
    public Vector3 baseScale = Vector3.one;

    [HideInInspector] public float deathTime = -1f;
    [HideInInspector] public bool  isDead    = false;
    private bool skeletonRegistered = false;

    private Color[] originalColors;
    private Renderer[] childRenderers;
    private bool coloursInitialised = false;

    private float maxAgeSeconds;
    private float stressTolerance;
    private float recoveryRate;
    private float logisticK;
    private float logisticMid;

    private Vector3 currentDisplayScale;
    public  Vector3 CurrentDisplayScale => currentDisplayScale;

    void Start()
    {
        InitTypeParameters();
        CaptureOriginalColors();
        currentDisplayScale = transform.localScale;
    }

    public void Simulate(float deltaSim, CoralEcosystem eco, float boostMul = 1f)
    {
        if (isDead) return;

        age += deltaSim;

        float competition = ComputeCompetition(eco);
        float bleachStress = ComputeBleachStress(eco);

        float stressInput = competition * 0.4f + bleachStress;
        if (stressInput > 0.05f)
            stress = Mathf.MoveTowards(stress, 1f, stressInput * 0.0015f * deltaSim);
        else
            stress = Mathf.MoveTowards(stress, 0f, recoveryRate * 0.0012f * boostMul * deltaSim);

        if (stress > stressTolerance)
            health -= (stress - stressTolerance) * 0.006f * deltaSim;
        else if (health < 1f)
            health += recoveryRate * 0.0004f * boostMul * deltaSim;

        // Senescence
        if (age > maxAgeSeconds)
        {
            float overage = (age - maxAgeSeconds) / maxAgeSeconds;
            health -= 0.0002f * overage * deltaSim;
        }

        health = Mathf.Clamp01(health);

        ApplyLogisticGrowth(deltaSim, competition, boostMul);
        UpdateBleachVisuals();

        if (health <= 0f && !isDead)
            Die(eco);
    }

    private float ComputeCompetition(CoralEcosystem eco)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position,
                            eco.maxCompetitionRadius, eco.terrainLayer.value);
        float pressure = 0f;
        int   count    = 0;
        foreach (var col in hits)
        {
            var other = col.GetComponentInParent<CoralAgent>();
            if (other == null || other == this || other.isDead) continue;
            float dist = Vector3.Distance(transform.position, other.transform.position);
            pressure  += 1f - dist / eco.maxCompetitionRadius;
            count++;
        }
        return Mathf.Clamp01(pressure / Mathf.Max(1f, count * 0.7f));
    }

    private float ComputeBleachStress(CoralEcosystem eco)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, eco.bleachStressRadius);
        float total = 0f;
        foreach (var col in hits)
        {
            var other = col.GetComponentInParent<CoralAgent>();
            if (other == null || other == this) continue;
            if (other.stress > 0.5f)
                total += (other.stress - 0.5f) * eco.bleachStressAmount;
        }
        return Mathf.Clamp01(total);
    }

    private void ApplyLogisticGrowth(float deltaSim, float competition, float boostMul)
    {
        if (health < 0.15f) return;
        if (competition > 0.85f) return;

        float logistic     = 1f / (1f + Mathf.Exp(-logisticK * (age - logisticMid)));
        float healthFactor = Mathf.Lerp(0.6f, 1f, health);
        Vector3 targetScale = baseScale * logistic * healthFactor;

        if (age > maxAgeSeconds)
        {
            float shrink = Mathf.Clamp01((age - maxAgeSeconds) / (maxAgeSeconds * 0.5f));
            targetScale *= Mathf.Lerp(1f, 0.55f, shrink);
        }

        float growStep = growthRate * deltaSim * (1f - competition * 1.2f) * boostMul;
        currentDisplayScale = Vector3.MoveTowards(
            currentDisplayScale, targetScale,
            targetScale.magnitude * growStep);

        transform.localScale = currentDisplayScale;
    }

    private void UpdateBleachVisuals()
    {
        if (!coloursInitialised || childRenderers == null) return;

        Color bleachedCol = new Color(0.92f, 0.96f, 1.00f, 1f);

        for (int i = 0; i < childRenderers.Length; i++)
        {
            if (childRenderers[i] == null) continue;
            var mat = childRenderers[i].material;
            if (mat == null) continue;

            Color liveCol   = (i < originalColors.Length) ? originalColors[i] : Color.white;
            Color targetCol = Color.Lerp(liveCol, bleachedCol, stress);

            if (age > maxAgeSeconds * 0.9f)
            {
                float senescent = Mathf.Clamp01((age - maxAgeSeconds * 0.9f) / (maxAgeSeconds * 0.3f));
                targetCol = Color.Lerp(targetCol, liveCol * 0.5f, senescent * (1f - stress));
            }

            mat.SetColor("_BaseColor", targetCol);

            float emissionStrength = Mathf.Lerp(0.15f, 0f, stress) * health;
            if (emissionStrength > 0.01f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", targetCol * emissionStrength);
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
            }
        }
    }

    private void Die(CoralEcosystem eco)
    {
        isDead    = true;
        deathTime = Time.time;

        Color skeletonCol = new Color(0.90f, 0.95f, 1.00f, 1f);
        if (childRenderers != null)
        {
            foreach (var rend in childRenderers)
            {
                if (rend == null) continue;
                rend.material.SetColor("_BaseColor", skeletonCol);
                rend.material.DisableKeyword("_EMISSION");
                rend.material.SetFloat("_Surface", 1);
                rend.material.renderQueue = 3000;
            }
        }

        transform.localScale = currentDisplayScale * 0.85f;

        if (!skeletonRegistered)
        {
            skeletonRegistered = true;
            eco.RegisterSkeleton(gameObject);
        }
    }

    private void InitTypeParameters()
    {
        maxAgeSeconds   = 1800f;
        stressTolerance = 0.40f;
        recoveryRate    = 1.00f;
        logisticK       = 0.006f;
        logisticMid     = 400f;

        switch (type)
        {
            case CoralEcosystem.CoralType.Brain:
            case CoralEcosystem.CoralType.Mushroom:
                maxAgeSeconds   = 3600f;
                stressTolerance = 0.65f;
                recoveryRate    = 1.80f;
                logisticK       = 0.004f;
                logisticMid     = 600f;
                break;
            case CoralEcosystem.CoralType.Branch:
            case CoralEcosystem.CoralType.Staghorn:
                maxAgeSeconds   = 1200f;
                stressTolerance = 0.30f;
                recoveryRate    = 0.70f;
                logisticK       = 0.010f;
                logisticMid     = 250f;
                growthRate     *= 1.4f;
                break;
            case CoralEcosystem.CoralType.Fan:
                maxAgeSeconds   = 1500f;
                stressTolerance = 0.45f;
                recoveryRate    = 1.00f;
                logisticK       = 0.007f;
                logisticMid     = 350f;
                break;
            case CoralEcosystem.CoralType.Tube:
                maxAgeSeconds   = 1400f;
                stressTolerance = 0.38f;
                recoveryRate    = 0.90f;
                logisticK       = 0.008f;
                logisticMid     = 300f;
                break;
            case CoralEcosystem.CoralType.Bush:
                maxAgeSeconds   = 1600f;
                stressTolerance = 0.50f;
                recoveryRate    = 1.20f;
                logisticK       = 0.007f;
                logisticMid     = 320f;
                break;
        }
    }

    private void CaptureOriginalColors()
    {
        childRenderers = GetComponentsInChildren<Renderer>();
        originalColors = new Color[childRenderers.Length];
        for (int i = 0; i < childRenderers.Length; i++)
        {
            if (childRenderers[i] != null && childRenderers[i].material != null)
                originalColors[i] = childRenderers[i].material.GetColor("_BaseColor");
        }
        coloursInitialised = true;
    }
}