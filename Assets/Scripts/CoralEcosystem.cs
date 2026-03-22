using UnityEngine;
using System.Collections.Generic;

public class CoralEcosystem : MonoBehaviour
{
    [Header("Simulation Settings")]
    public float simulationTickInterval = 4f;
    public float timeScale              = 60f;
    public int   maxCorals              = 500;

    [Header("Placement Settings")]
    public int   seed              = 42;
    public int   initialCoralCount = 200;
    public float raycastHeight     = 200f;
    public float spawnAreaSize     = 230f;

    [Header("Steepness Filter")]
    [Range(0f,90f)] public float maxSlopeAngle = 25f;

    [Header("Clustering")]
    [Range(0f,1f)] public float clusterStrength = 0.85f;
    public int   clusterCount  = 12;
    public float clusterRadius = 15f;

    [Header("Coral Visuals (initial size)")]
    public float minHeight = 0.5f;
    public float maxHeight = 4f;
    public float minRadius = 0.1f;
    public float maxRadius = 0.6f;

    [Header("Coral Type Mix")]
    [Range(0f,1f)] public float branchCoralChance   = 0.20f;
    [Range(0f,1f)] public float fanCoralChance       = 0.15f;
    [Range(0f,1f)] public float brainCoralChance     = 0.15f;
    [Range(0f,1f)] public float staghornChance       = 0.15f;
    [Range(0f,1f)] public float tubeCoralChance      = 0.15f;
    [Range(0f,1f)] public float mushroomCoralChance  = 0.10f;
    [Range(0f,1f)] public float bushCoralChance      = 0.10f;

    [Header("Ecosystem Dynamics")]
    public float baseGrowthRate            = 0.0008f;
    public float maxCompetitionRadius      = 3.5f;
    public float reproductionChancePerTick = 0.15f;
    public float babyScale                 = 0.12f;

    [Header("Bleaching & Stress")]
    [Tooltip("Radius within which a bleaching coral stresses its neighbours")]
    public float bleachStressRadius = 5f;
    [Tooltip("How much each bleaching neighbour raises local stress per tick")]
    public float bleachStressAmount = 0.04f;

    [Header("Season / Bloom")]
    [Tooltip("Real-time seconds between nutrient blooms")]
    public float bloomIntervalSeconds = 60f;
    [Tooltip("Multiplier on growth & reproduction chance during a bloom")]
    public float bloomBoostMultiplier = 2.5f;
    [Tooltip("How many real seconds the bloom lasts")]
    public float bloomDurationSeconds = 8f;

    [Header("Pulse (polyp breathing)")]
    [Tooltip("Amplitude of the scale pulse (0 = off, ~0.04 looks natural)")]
    public float pulseAmplitude = 0.04f;
    [Tooltip("Cycles per real second")]
    public float pulseFrequency = 0.35f;

    [Header("References")]
    public LayerMask terrainLayer;

    private static readonly Color[] BaseColors =
    {
        new Color(1.00f,0.20f,0.10f), new Color(1.00f,0.55f,0.00f),
        new Color(1.00f,0.85f,0.10f), new Color(0.90f,0.15f,0.55f),
        new Color(0.55f,0.10f,0.90f), new Color(0.10f,0.75f,0.90f),
        new Color(0.10f,0.90f,0.45f), new Color(1.00f,0.40f,0.70f),
        new Color(0.20f,0.40f,1.00f), new Color(0.95f,0.60f,0.80f),
    };
    private static readonly Color[] TipColors =
    {
        new Color(1.00f,0.95f,0.60f), new Color(1.00f,1.00f,1.00f),
        new Color(0.50f,1.00f,0.85f), new Color(1.00f,0.70f,0.30f),
        new Color(0.80f,0.30f,1.00f),
    };

    private List<GameObject> corals      = new List<GameObject>();
    private List<GameObject> skeletons   = new List<GameObject>();
    private GameObject coralParent;
    private float nextTickTime;
    private float nextBloomTime;
    private float bloomEndTime = -1f;
    public  bool  BloomActive => Time.time < bloomEndTime;

    public enum CoralType { Branch, Fan, Brain, Staghorn, Tube, Mushroom, Bush }

    void Start() => InitializeEcosystem();

    void Update()
    {
        if (Time.time >= nextBloomTime)
        {
            bloomEndTime  = Time.time + bloomDurationSeconds;
            nextBloomTime = Time.time + bloomIntervalSeconds;
            Debug.Log("[CoralEcosystem] 🌊 Nutrient bloom started!");
        }

        if (Time.time >= nextTickTime)
        {
            SimulateStep();
            nextTickTime = Time.time + simulationTickInterval;
        }

        PulseLivingCorals();
        UpdateSkeletons();
    }

    public void InitializeEcosystem()
    {
        ClearAllCorals();
        coralParent = new GameObject("LivingCorals");
        System.Random rng = new System.Random(seed);

        Vector2[]   clusterCenters = new Vector2[clusterCount];
        CoralType[] clusterTypes   = new CoralType[clusterCount];
        Color[]     clusterBase    = new Color[clusterCount];
        Color[]     clusterTip     = new Color[clusterCount];

        for (int i = 0; i < clusterCount; i++)
        {
            clusterCenters[i] = new Vector2(
                (float)(rng.NextDouble()*spawnAreaSize - spawnAreaSize/2f),
                (float)(rng.NextDouble()*spawnAreaSize - spawnAreaSize/2f));
            clusterTypes[i] = PickCoralType(rng);
            clusterBase[i]  = BaseColors[rng.Next(BaseColors.Length)];
            clusterTip[i]   = TipColors [rng.Next(TipColors.Length)];
        }

        int placed=0, attempts=0, maxAttempts=initialCoralCount*20;
        while (placed < initialCoralCount && attempts < maxAttempts)
        {
            attempts++;
            Vector2 candidate = GetCandidatePosition(rng, clusterCenters, out int ci);
            Ray ray = new Ray(new Vector3(candidate.x, raycastHeight, candidate.y), Vector3.down);
            if (!Physics.Raycast(ray, out RaycastHit hit, raycastHeight*2f, terrainLayer)) continue;
            if (Vector3.Angle(hit.normal, Vector3.up) > maxSlopeAngle) continue;

            CoralType type = (ci>=0)?clusterTypes[ci]:PickCoralType(rng);
            Color b = (ci>=0)?clusterBase[ci]:BaseColors[rng.Next(BaseColors.Length)];
            Color t = (ci>=0)?clusterTip[ci] :TipColors [rng.Next(TipColors.Length)];

            // Scatter initial ages so the reef looks established
            float initialAge = (float)(rng.NextDouble() * 800f);
            SpawnCoral(hit.point, hit.normal, rng, type, b, t, initialAge);
            placed++;
        }

        Debug.Log($"[CoralEcosystem] Initial placement: {placed} corals ({attempts} attempts)");
        nextTickTime  = Time.time + simulationTickInterval;
        nextBloomTime = Time.time + bloomIntervalSeconds;
    }

    public void ClearAllCorals()
    {
        if (coralParent != null) DestroyImmediate(coralParent);
        corals.Clear();
        foreach (var s in skeletons) if (s) DestroyImmediate(s);
        skeletons.Clear();
    }

    private void SimulateStep()
    {
        float deltaSim = simulationTickInterval * timeScale;
        float boostMul = BloomActive ? bloomBoostMultiplier : 1f;

        for (int i = corals.Count-1; i >= 0; i--)
        {
            if (corals[i] == null) { corals.RemoveAt(i); continue; }
            var agent = corals[i].GetComponent<CoralAgent>();
            if (agent != null) agent.Simulate(deltaSim, this, boostMul);
        }

        TryReproduce(boostMul);

        Debug.Log($"[Reef] Living: {corals.Count} | Skeletons: {skeletons.Count} | Bloom: {BloomActive}");
    }

    private void TryReproduce(float boostMul)
    {
        if (corals.Count >= maxCorals) return;
        float chance = reproductionChancePerTick * boostMul;
        if (Random.value > chance) return;

        var parent = corals[Random.Range(0, corals.Count)];
        var parentAgent = parent.GetComponent<CoralAgent>();
        if (parentAgent == null || parentAgent.health < 0.5f) return;

        float driftRadius = Random.Range(2f, 12f);
        Vector3 offset    = Random.insideUnitSphere * driftRadius;
        offset.y          = Mathf.Abs(offset.y) + 1f;
        Vector3 origin    = parent.transform.position + offset + Vector3.up * 10f;
        Ray ray           = new Ray(origin, Vector3.down);

        if (!Physics.Raycast(ray, out RaycastHit hit, 40f, terrainLayer)) return;
        if (Vector3.Angle(hit.normal, Vector3.up) > maxSlopeAngle)       return;
        if (Physics.OverlapSphere(hit.point, 1.0f).Length > 1)            return;

        System.Random rng = new System.Random();
        // Juveniles share parent type 80% of the time
        CoralType type = (rng.NextDouble() < 0.8)
            ? parentAgent.type
            : PickCoralType(rng);
        Color b = BaseColors[rng.Next(BaseColors.Length)];
        Color t = TipColors [rng.Next(TipColors.Length)];

        SpawnCoral(hit.point, hit.normal, rng, type, b, t, 0f);

        var newCoral = corals[^1];
        if (newCoral != null)
        {
            newCoral.transform.localScale *= babyScale;
            var ag = newCoral.GetComponent<CoralAgent>();
            if (ag != null) { ag.health = 0.70f; ag.baseScale = newCoral.transform.localScale; }
        }
    }

    public void RegisterSkeleton(GameObject go)
    {
        skeletons.Add(go);
        corals.Remove(go);
    }

    private void PulseLivingCorals()
    {
        if (pulseAmplitude <= 0f) return;
        float pulse = 1f + Mathf.Sin(Time.time * pulseFrequency * Mathf.PI * 2f) * pulseAmplitude;
        foreach (var c in corals)
        {
            if (c == null) continue;
            var agent = c.GetComponent<CoralAgent>();
            if (agent == null || agent.health < 0.1f) continue;
            c.transform.localScale = agent.CurrentDisplayScale * pulse;
        }
    }

    private void UpdateSkeletons()
    {
        for (int i = skeletons.Count-1; i >= 0; i--)
        {
            var s = skeletons[i];
            if (s == null) { skeletons.RemoveAt(i); continue; }

            var agent = s.GetComponent<CoralAgent>();
            float elapsed = agent != null ? Time.time - agent.deathTime : 999f;
            float fade    = Mathf.Clamp01(elapsed / 20f);

            foreach (var rend in s.GetComponentsInChildren<Renderer>())
            {
                if (rend.material == null) continue;
                Color c = rend.material.color;
                c.a = 1f - fade;
                rend.material.color = c;
                rend.material.SetFloat("_Surface", 1);
                rend.material.renderQueue = 3000;
            }

            if (fade >= 1f)
            {
                skeletons.RemoveAt(i);
                Destroy(s);
            }
        }
    }

    private void SpawnCoral(Vector3 pos, Vector3 normal, System.Random rng,
                            CoralType type, Color baseCol, Color tipCol, float initialAge)
    {
        GameObject root = new GameObject($"Coral_{type}");
        root.transform.position = pos;
        root.transform.up       = normal;
        root.transform.Rotate(Vector3.up, (float)(rng.NextDouble()*360f), Space.Self);

        switch (type)
        {
            case CoralType.Branch:   BuildBranchCoral  (root,rng,baseCol,tipCol); break;
            case CoralType.Fan:      BuildFanCoral     (root,rng,baseCol,tipCol); break;
            case CoralType.Brain:    BuildBrainCoral   (root,rng,baseCol);        break;
            case CoralType.Staghorn: BuildStaghornCoral(root,rng,baseCol,tipCol); break;
            case CoralType.Tube:     BuildTubeCoral    (root,rng,baseCol,tipCol); break;
            case CoralType.Mushroom: BuildMushroomCoral(root,rng,baseCol,tipCol); break;
            case CoralType.Bush:     BuildBushCoral    (root,rng,baseCol,tipCol); break;
        }

        root.transform.SetParent(coralParent.transform);

        var agent        = root.AddComponent<CoralAgent>();
        agent.type       = type;
        agent.age        = initialAge;
        agent.health     = 1f;
        agent.growthRate = baseGrowthRate * Random.Range(0.75f, 1.35f);
        agent.baseScale  = root.transform.localScale;

        corals.Add(root);
    }

    void BuildBranchCoral(GameObject root, System.Random rng, Color baseCol, Color tipCol)
    {
        float h = Lerp(rng, minHeight, maxHeight);
        float r = Lerp(rng, minRadius, minRadius*2.5f);
        var stem = Prim(PrimitiveType.Capsule, root);
        stem.transform.localPosition = new Vector3(0, h*0.5f, 0);
        stem.transform.localScale    = new Vector3(r, h*0.5f, r);
        ApplyMat(stem, baseCol);
        var tip = Prim(PrimitiveType.Sphere, root);
        tip.transform.localPosition = new Vector3(0, h+r, 0);
        tip.transform.localScale    = Vector3.one*r*2f;
        ApplyMat(tip, tipCol, emission:0.4f);
    }

    void BuildFanCoral(GameObject root, System.Random rng, Color baseCol, Color tipCol)
    {
        int   spokes    = (int)Lerp(rng, 7f, 12f);
        float fanWidth  = Lerp(rng, 40f, 80f);
        float spokeH    = Lerp(rng, 0.8f, 2.0f);
        float spokeR    = Lerp(rng, 0.03f, 0.07f);
        var stalk = Prim(PrimitiveType.Cylinder, root);
        stalk.transform.localPosition = new Vector3(0,0.2f,0);
        stalk.transform.localScale    = new Vector3(0.08f,0.2f,0.08f);
        ApplyMat(stalk, baseCol);
        for (int i=0;i<spokes;i++)
        {
            float t     = spokes==1?0.5f:i/(float)(spokes-1);
            float angle = Mathf.Lerp(-fanWidth*0.5f,fanWidth*0.5f,t);
            var spoke   = Prim(PrimitiveType.Capsule, root);
            spoke.transform.localPosition = new Vector3(0,0.4f,0);
            spoke.transform.localRotation = Quaternion.Euler(0f,0f,angle);
            spoke.transform.localScale    = new Vector3(spokeR,spokeH*0.5f,spokeR);
            float ct = Mathf.Abs(t-0.5f)*2f;
            ApplyMat(spoke, Color.Lerp(baseCol,tipCol,ct), emission:ct*0.2f);
            Vector3 dir = spoke.transform.localRotation*Vector3.up;
            var tip = Prim(PrimitiveType.Sphere,root);
            tip.transform.localPosition = new Vector3(0,0.4f,0)+dir*spokeH;
            tip.transform.localScale    = Vector3.one*spokeR*3f;
            ApplyMat(tip, tipCol, emission:0.3f);
        }
    }

    void BuildBrainCoral(GameObject root, System.Random rng, Color baseCol)
    {
        float r = Lerp(rng, 0.8f, 2f);
        var dome = Prim(PrimitiveType.Sphere, root);
        dome.transform.localPosition = new Vector3(0,r*0.6f,0);
        dome.transform.localScale    = new Vector3(r*2f,r*1.2f,r*2f);
        ApplyMat(dome, baseCol);
        for (int i=0;i<8;i++)
        {
            float angle = (i/8f)*Mathf.PI*2f;
            var bump = Prim(PrimitiveType.Sphere,root);
            bump.transform.localPosition = new Vector3(Mathf.Cos(angle)*r*0.7f,r*0.8f,Mathf.Sin(angle)*r*0.7f);
            bump.transform.localScale    = Vector3.one*r*0.25f;
            ApplyMat(bump, baseCol*0.75f);
        }
    }

    void BuildStaghornCoral(GameObject root, System.Random rng, Color baseCol, Color tipCol)
    {
        float trunkH = Lerp(rng, minHeight, maxHeight*0.6f);
        float r      = Lerp(rng, minRadius, minRadius*2f);
        var trunk = Prim(PrimitiveType.Cylinder, root);
        trunk.transform.localPosition = new Vector3(0,trunkH*0.5f,0);
        trunk.transform.localScale    = new Vector3(r,trunkH*0.5f,r);
        ApplyMat(trunk, baseCol);
        int branches = (int)Lerp(rng,3f,6f);
        for (int i=0;i<branches;i++)
        {
            float spread = 25f+(float)(rng.NextDouble()*35f);
            float rotY   = (i/(float)branches)*360f;
            float bH     = Lerp(rng,trunkH*0.4f,trunkH*0.8f);
            float br     = r*0.6f;
            var branch   = Prim(PrimitiveType.Cylinder,root);
            branch.transform.localPosition = new Vector3(0,trunkH*0.7f,0);
            branch.transform.localRotation = Quaternion.Euler(spread,rotY,0f);
            branch.transform.localScale    = new Vector3(br,bH*0.5f,br);
            ApplyMat(branch, Color.Lerp(baseCol,tipCol,0.5f));
            Vector3 dir = branch.transform.localRotation*Vector3.up;
            var tip = Prim(PrimitiveType.Sphere,root);
            tip.transform.localPosition = new Vector3(0,trunkH*0.7f,0)+dir*bH;
            tip.transform.localScale    = Vector3.one*br*2f;
            ApplyMat(tip, tipCol, emission:0.3f);
        }
    }

    void BuildTubeCoral(GameObject root, System.Random rng, Color baseCol, Color tipCol)
    {
        int tubes = (int)Lerp(rng,3f,7f);
        for (int i=0;i<tubes;i++)
        {
            float angle = (float)(rng.NextDouble()*Mathf.PI*2f);
            float dist  = (float)(rng.NextDouble()*0.4f);
            float h     = Lerp(rng,minHeight*0.8f,maxHeight*0.7f);
            float r     = Lerp(rng,minRadius*0.8f,minRadius*2f);
            float ox    = Mathf.Cos(angle)*dist, oz=Mathf.Sin(angle)*dist;
            var outer = Prim(PrimitiveType.Cylinder,root);
            outer.transform.localPosition = new Vector3(ox,h*0.5f,oz);
            outer.transform.localScale    = new Vector3(r,h*0.5f,r);
            ApplyMat(outer, baseCol);
            var inner = Prim(PrimitiveType.Cylinder,root);
            inner.transform.localPosition = new Vector3(ox,h*0.5f+0.01f,oz);
            inner.transform.localScale    = new Vector3(r*0.5f,h*0.52f,r*0.5f);
            ApplyMat(inner, baseCol*0.2f);
            var rim = Prim(PrimitiveType.Sphere,root);
            rim.transform.localPosition   = new Vector3(ox,h,oz);
            rim.transform.localScale      = new Vector3(r*2.2f,r*0.5f,r*2.2f);
            ApplyMat(rim, tipCol, emission:0.2f);
        }
    }

    void BuildMushroomCoral(GameObject root, System.Random rng, Color baseCol, Color tipCol)
    {
        float stalkH = Lerp(rng,0.3f,0.7f);
        float capR   = Lerp(rng,0.5f,1.2f);
        float stalkR = capR*0.18f;
        var stalk = Prim(PrimitiveType.Cylinder,root);
        stalk.transform.localPosition = new Vector3(0,stalkH*0.5f,0);
        stalk.transform.localScale    = new Vector3(stalkR,stalkH*0.5f,stalkR);
        ApplyMat(stalk, baseCol*0.8f);
        var capBot = Prim(PrimitiveType.Sphere,root);
        capBot.transform.localPosition = new Vector3(0,stalkH,0);
        capBot.transform.localScale    = new Vector3(capR*2f,capR*0.35f,capR*2f);
        ApplyMat(capBot, baseCol);
        var capTop = Prim(PrimitiveType.Sphere,root);
        capTop.transform.localPosition = new Vector3(0,stalkH+capR*0.28f,0);
        capTop.transform.localScale    = new Vector3(capR*1.6f,capR*0.45f,capR*1.6f);
        ApplyMat(capTop, Color.Lerp(baseCol,tipCol,0.4f));
        for (int i=0;i<10;i++)
        {
            float a  = (i/10f)*Mathf.PI*2f;
            var rim  = Prim(PrimitiveType.Sphere,root);
            rim.transform.localPosition = new Vector3(Mathf.Cos(a)*capR*0.9f,stalkH+capR*0.05f,Mathf.Sin(a)*capR*0.9f);
            rim.transform.localScale    = Vector3.one*capR*0.18f;
            ApplyMat(rim, tipCol, emission:0.4f);
        }
        for (int i=0;i<8;i++)
        {
            float a  = (i/8f)*Mathf.PI*2f;
            float sr = capR*0.12f;
            var sep  = Prim(PrimitiveType.Sphere,root);
            sep.transform.localPosition = new Vector3(Mathf.Cos(a)*capR*0.55f,stalkH-sr*0.5f,Mathf.Sin(a)*capR*0.55f);
            sep.transform.localScale    = new Vector3(sr,sr*0.4f,sr);
            ApplyMat(sep, baseCol*0.6f);
        }
    }

    void BuildBushCoral(GameObject root, System.Random rng, Color baseCol, Color tipCol)
    {
        int   balls = (int)Lerp(rng,8f,18f);
        float mound = Lerp(rng,0.5f,1.5f);
        for (int i=0;i<balls;i++)
        {
            float angle     = (float)(rng.NextDouble()*Mathf.PI*2f);
            float elevation = (float)(rng.NextDouble()*Mathf.PI*0.5f);
            float dist      = Lerp(rng,0f,mound);
            float ox = Mathf.Cos(angle)*Mathf.Cos(elevation)*dist;
            float oy = Mathf.Sin(elevation)*dist*0.7f;
            float oz = Mathf.Sin(angle)*Mathf.Cos(elevation)*dist;
            float br = Lerp(rng,0.15f,0.45f);
            var ball = Prim(PrimitiveType.Sphere,root);
            ball.transform.localPosition = new Vector3(ox,oy+br,oz);
            ball.transform.localScale    = Vector3.one*br*2f;
            float t = dist/mound;
            ApplyMat(ball, Color.Lerp(baseCol,tipCol,t), emission:t*0.2f);
        }
    }

    GameObject Prim(PrimitiveType type, GameObject parent)
    {
        var go  = GameObject.CreatePrimitive(type);
        var col = go.GetComponent<Collider>();
        if (col != null) DestroyImmediate(col);
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    void ApplyMat(GameObject go, Color color, float emission=0.15f)
    {
        var rend = go.GetComponent<Renderer>();
        if (rend == null) return;
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", color);
        if (emission > 0f)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color*emission);
        }
        rend.material = mat;
    }

    float Lerp(System.Random rng, float a, float b) =>
        Mathf.Lerp(a, b, (float)rng.NextDouble());

    private CoralType PickCoralType(System.Random rng)
    {
        double roll=rng.NextDouble(), acc=0;
        acc+=branchCoralChance;   if(roll<acc) return CoralType.Branch;
        acc+=fanCoralChance;      if(roll<acc) return CoralType.Fan;
        acc+=brainCoralChance;    if(roll<acc) return CoralType.Brain;
        acc+=staghornChance;      if(roll<acc) return CoralType.Staghorn;
        acc+=tubeCoralChance;     if(roll<acc) return CoralType.Tube;
        acc+=mushroomCoralChance; if(roll<acc) return CoralType.Mushroom;
        return CoralType.Bush;
    }

    private Vector2 GetCandidatePosition(System.Random rng, Vector2[] centers, out int clusterIndex)
    {
        if (rng.NextDouble() < clusterStrength && centers.Length > 0)
        {
            clusterIndex = rng.Next(centers.Length);
            double a = rng.NextDouble()*Mathf.PI*2;
            double r = Mathf.Sqrt((float)rng.NextDouble())*clusterRadius;
            return new Vector2(
                centers[clusterIndex].x+(float)(Mathf.Cos((float)a)*r),
                centers[clusterIndex].y+(float)(Mathf.Sin((float)a)*r));
        }
        clusterIndex=-1;
        return new Vector2(
            (float)(rng.NextDouble()*spawnAreaSize-spawnAreaSize/2f),
            (float)(rng.NextDouble()*spawnAreaSize-spawnAreaSize/2f));
    }
}