using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// Setup: attach to Main Camera, assign ReefCenter and CoralParent in the Inspector.
public class ReefCinematicCamera : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Empty GameObject placed at the center of your reef")]
    public Transform reefCenter;

    [Tooltip("The parent GameObject containing all coral objects (LivingCorals)")]
    public GameObject coralParent;

    [Header("Phase Durations (seconds)")]
    public float seabedPanDuration   = 8f;
    public float riseRevealDuration  = 5f;
    public float wideOrbitDuration   = 12f;
    public float closeupDuration     = 4f;
    public float transitionDuration  = 2f;

    [Header("Seabed Pan")]
    public float seabedHeight        = 2f;
    public float seabedPanDistance   = 30f;
    public float seabedTiltAngle     = 15f;

    [Header("Wide Orbit")]
    public float orbitRadius         = 45f;
    public float orbitHeight         = 18f;
    public float orbitSpeed          = 12f;
    public float heightBobAmount     = 1.5f;
    public float heightBobSpeed      = 0.15f;

    [Header("Coral Close-ups")]
    public float closeupDistance     = 4f;
    public float closeupHeight       = 2f;
    [Tooltip("Field of view during close-ups (narrower = more cinematic)")]
    public float closeupFOV          = 40f;
    [Tooltip("Field of view during wide shots")]
    public float wideFOV             = 60f;

    [Header("Water Surface")]
    [Tooltip("Y position of your water surface — camera will never go above this")]
    public float waterSurfaceY       = 10f;
    [Tooltip("Safety margin — keeps camera this far below the surface")]
    public float belowSurfaceMargin  = 2f;

    private enum Phase { SeabedPan, RiseReveal, WideOrbit, CoralCloseup, FinalPullback }

    private Phase   currentPhase;
    private float   phaseTimer  = 0f;
    private float   orbitAngle  = 0f;
    private Camera  cam;

    private Vector3    positionTarget;
    private Quaternion rotationTarget;
    private float      fovTarget;

    private List<Transform> coralTargets = new List<Transform>();
    private int             closeupIndex = 0;

    private Vector3 seabedStartPos;
    private Vector3 seabedEndPos;
    private Vector3 riseStartPos;
    private Vector3 riseEndPos;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;

        if (reefCenter == null)
        {
            GameObject found = GameObject.Find("ReefTarget");
            if (found) reefCenter = found.transform;
            else
            {
                GameObject go = new GameObject("ReefTarget");
                reefCenter = go.transform;
            }
        }

        CollectCoralTargets();
        StartPhase(Phase.SeabedPan);
    }

    void Update()
    {
        phaseTimer += Time.deltaTime;

        switch (currentPhase)
        {
            case Phase.SeabedPan:    UpdateSeabedPan();    break;
            case Phase.RiseReveal:   UpdateRiseReveal();   break;
            case Phase.WideOrbit:    UpdateWideOrbit();    break;
            case Phase.CoralCloseup: UpdateCoralCloseup(); break;
            case Phase.FinalPullback:UpdateFinalPullback();break;
        }
    }

    private void StartPhase(Phase phase)
    {
        currentPhase = phase;
        phaseTimer   = 0f;

        switch (phase)
        {
            case Phase.SeabedPan:
                Vector3 center = reefCenter != null ? reefCenter.position : Vector3.zero;
                seabedStartPos = center + new Vector3(-seabedPanDistance, seabedHeight, -seabedPanDistance * 0.5f);
                seabedEndPos   = center + new Vector3( seabedPanDistance, seabedHeight,  seabedPanDistance * 0.5f);
                transform.position = seabedStartPos;
                SetFOV(wideFOV);
                break;

            case Phase.RiseReveal:
                riseStartPos = transform.position;
                riseEndPos   = reefCenter.position + new Vector3(
                    Mathf.Sin(orbitAngle * Mathf.Deg2Rad) * orbitRadius,
                    orbitHeight,
                    Mathf.Cos(orbitAngle * Mathf.Deg2Rad) * orbitRadius);
                break;

            case Phase.WideOrbit:
                orbitAngle = 0f;
                break;

            case Phase.CoralCloseup:
                closeupIndex = 0;
                if (coralTargets.Count == 0) StartPhase(Phase.WideOrbit);
                break;

            case Phase.FinalPullback:
                orbitAngle = 0f;
                break;
        }
    }

    private void UpdateSeabedPan()
    {
        float t = Mathf.SmoothStep(0f, 1f, phaseTimer / seabedPanDuration);
        transform.position = Vector3.Lerp(seabedStartPos, seabedEndPos, t);

        Vector3 lookDir = (seabedEndPos - seabedStartPos).normalized;
        lookDir.y       = -Mathf.Sin(seabedTiltAngle * Mathf.Deg2Rad);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(lookDir.normalized),
            Time.deltaTime * 3f);

        if (phaseTimer >= seabedPanDuration)
            StartPhase(Phase.RiseReveal);
    }

    private void UpdateRiseReveal()
    {
        float t = Mathf.SmoothStep(0f, 1f, phaseTimer / riseRevealDuration);
        transform.position = Vector3.Lerp(riseStartPos, riseEndPos, t);

        if (reefCenter != null)
        {
            Quaternion targetRot = Quaternion.LookRotation(
                (reefCenter.position - transform.position).normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 2f);
        }

        if (phaseTimer >= riseRevealDuration)
            StartPhase(Phase.WideOrbit);
    }

    private void UpdateWideOrbit()
    {
        orbitAngle += orbitSpeed * Time.deltaTime;
        float rad   = orbitAngle * Mathf.Deg2Rad;
        float bob   = Mathf.Sin(Time.time * heightBobSpeed * Mathf.PI * 2f) * heightBobAmount;

        Vector3 orbitPos = reefCenter.position + new Vector3(
            Mathf.Sin(rad) * orbitRadius,
            orbitHeight + bob,
            Mathf.Cos(rad) * orbitRadius);
        transform.position = ClampBelowWater(orbitPos);

        Quaternion targetRot = Quaternion.LookRotation(
            (reefCenter.position - transform.position + Vector3.up * 2f).normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 2f);

        SmoothFOV(wideFOV);

        if (phaseTimer >= wideOrbitDuration)
            StartPhase(Phase.CoralCloseup);
    }

    private float closeupPhaseTimer = 0f;
    private bool  movingToCloseup   = false;
    private Vector3    closeupStartPos;
    private Quaternion closeupStartRot;

    private void UpdateCoralCloseup()
    {
        if (coralTargets.Count == 0 || closeupIndex >= coralTargets.Count)
        {
            StartPhase(Phase.FinalPullback);
            return;
        }

        Transform coral = coralTargets[closeupIndex];
        if (coral == null)
        {
            closeupIndex++;
            return;
        }

        Vector3 offset       = new Vector3(closeupDistance, closeupHeight, closeupDistance * 0.5f);
        Vector3 targetPos    = coral.position + offset;
        Quaternion targetRot = Quaternion.LookRotation((coral.position - targetPos + Vector3.up * 0.5f).normalized);

        closeupPhaseTimer += Time.deltaTime;

        if (closeupPhaseTimer < transitionDuration)
        {
            float t = Mathf.SmoothStep(0f, 1f, closeupPhaseTimer / transitionDuration);
            transform.position = Vector3.Lerp(closeupStartPos, targetPos, t);
            transform.rotation = Quaternion.Slerp(closeupStartRot, targetRot, t);
            SmoothFOV(closeupFOV);
        }
        else if (closeupPhaseTimer < transitionDuration + closeupDuration)
        {
            float driftTime = closeupPhaseTimer - transitionDuration;
            float drift     = Mathf.Sin(driftTime * 0.5f) * 0.3f;
            transform.position = targetPos + new Vector3(drift, drift * 0.5f, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 1.5f);
            SmoothFOV(closeupFOV);
        }
        else
        {
            closeupStartPos   = transform.position;
            closeupStartRot   = transform.rotation;
            closeupPhaseTimer = 0f;
            closeupIndex++;
        }

        if (closeupIndex >= coralTargets.Count)
            StartPhase(Phase.FinalPullback);
    }

    private void UpdateFinalPullback()
    {
        SmoothFOV(wideFOV);
        orbitAngle += (orbitSpeed * 0.5f) * Time.deltaTime;
        float rad   = orbitAngle * Mathf.Deg2Rad;
        float bob   = Mathf.Sin(Time.time * heightBobSpeed * Mathf.PI * 2f) * heightBobAmount;

        Vector3 targetPos = reefCenter.position + new Vector3(
            Mathf.Sin(rad) * (orbitRadius * 1.3f),
            orbitHeight * 1.2f + bob,
            Mathf.Cos(rad) * (orbitRadius * 1.3f));

        transform.position = Vector3.Lerp(transform.position, ClampBelowWater(targetPos), Time.deltaTime * 0.8f);

        Quaternion targetRot = Quaternion.LookRotation(
            (reefCenter.position - transform.position + Vector3.up).normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 1.5f);

        if (phaseTimer >= (360f / (orbitSpeed * 0.5f)))
        {
            CollectCoralTargets();
            StartPhase(Phase.WideOrbit);
        }
    }

    private void CollectCoralTargets()
    {
        coralTargets.Clear();

        if (coralParent == null)
        {
            coralParent = GameObject.Find("LivingCorals");
            if (coralParent == null) coralParent = GameObject.Find("Corals");
        }

        if (coralParent == null) return;

        // Pick the largest healthy coral of each type for the close-up tour
        Dictionary<CoralEcosystem.CoralType, Transform> bestPerType = new Dictionary<CoralEcosystem.CoralType, Transform>();
        Dictionary<CoralEcosystem.CoralType, float>     bestScale   = new Dictionary<CoralEcosystem.CoralType, float>();

        foreach (Transform child in coralParent.transform)
        {
            var agent = child.GetComponent<CoralAgent>();
            if (agent == null || agent.isDead || agent.health < 0.5f) continue;

            float size = child.localScale.magnitude;
            if (!bestPerType.ContainsKey(agent.type) || size > bestScale[agent.type])
            {
                bestPerType[agent.type] = child;
                bestScale[agent.type]   = size;
            }
        }

        foreach (var kvp in bestPerType)
            coralTargets.Add(kvp.Value);

        // Shuffle tour order
        for (int i = coralTargets.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (coralTargets[i], coralTargets[j]) = (coralTargets[j], coralTargets[i]);
        }

        closeupIndex      = 0;
        closeupPhaseTimer = 0f;
        if (coralTargets.Count > 0)
        {
            closeupStartPos = transform.position;
            closeupStartRot = transform.rotation;
        }
    }

    private void SmoothFOV(float target)
    {
        if (cam != null)
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, target, Time.deltaTime * 2f);
    }

    private void SetFOV(float value)
    {
        if (cam != null) cam.fieldOfView = value;
    }

    private Vector3 ClampBelowWater(Vector3 pos)
    {
        pos.y = Mathf.Min(pos.y, waterSurfaceY - belowSurfaceMargin);
        return pos;
    }
}