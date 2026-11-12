
using System;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(100)]
public class PlayerHashi : MonoBehaviour
{
    public event Action<EnemyHealth> TargetHit;

    [Header("Referencias")]
    [SerializeField] private HashiProjectile projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private Camera aimCamera;
    [SerializeField] private GameObject[] backHashis = new GameObject[2];

    [Header("Disparo")]
    [SerializeField, Min(0f)] private float damage = 20f;
    [SerializeField, Min(0.01f)] private float shotInterval = 0.25f;
    [SerializeField, Min(0.01f)] private float reloadTime = 10f;
    [SerializeField, Min(1f)] private float aimDistance = 80f;

    [Tooltip("Inclua inimigos e obstaculos.")]
    [SerializeField] private LayerMask hitMask = ~0;

    private Renderer[][] backRenderers;
    private bool[][] originalVisibility;
    private bool[] slotLoaded = { true, true };

    private bool ready;
    private float nextShotTime;
    private float nextReloadTime = -1f;
    private bool tutorialAimIndicatorVisible;
    private bool aimIndicatorVisible;
    private GameObject aimIndicatorRoot;
    private LineRenderer aimLine;
    private Transform aimMarker;
    private Material aimIndicatorMaterial;
    private readonly RaycastHit[] aimHits = new RaycastHit[32];

    private const float AimPreviewDistance = 12f;
    private static readonly Color AimIndicatorColor = new Color(0.1f, 0.95f, 1f, 0.9f);

    public int CurrentAmmo { get; private set; } = 2;
    public int MaxAmmo => 2;

    public bool CanFire =>
        ready && CurrentAmmo > 0 && Time.time >= nextShotTime;

    private void Awake()
    {
        if (projectilePrefab == null || firePoint == null || aimCamera == null ||
            backHashis == null || backHashis.Length != 2 ||
            backHashis[0] == null || backHashis[1] == null ||
            backHashis[0] == backHashis[1])
        {
            Debug.LogError("PlayerHashi: referencias invalidas.", this);
            return;
        }

        backRenderers = new Renderer[2][];
        originalVisibility = new bool[2][];

        for (int i = 0; i < 2; i++)
        {
            backRenderers[i] = backHashis[i].GetComponentsInChildren<Renderer>(true);

            if (backRenderers[i].Length == 0)
            {
                Debug.LogError("PlayerHashi: hashi sem Renderer.", this);
                return;
            }

            originalVisibility[i] = new bool[backRenderers[i].Length];

            for (int j = 0; j < backRenderers[i].Length; j++)
                originalVisibility[i][j] = backRenderers[i][j].enabled;
        }

        ready = true;
    }

    private void Update()
    {
        bool aimAssistHeld = Keyboard.current != null &&
            Keyboard.current.qKey.isPressed;
        SetAimIndicatorState(tutorialAimIndicatorVisible || aimAssistHeld);

        if (!ready || CurrentAmmo >= MaxAmmo || nextReloadTime < 0f)
            return;

        // Recupera uma municao a cada reloadTime segundos.
        while (CurrentAmmo < MaxAmmo && Time.time >= nextReloadTime)
        {
            ReloadOneAmmo();

            if (CurrentAmmo < MaxAmmo)
                nextReloadTime += reloadTime;
            else
                nextReloadTime = -1f;
        }
    }

    private void LateUpdate()
    {
        if (!aimIndicatorVisible || !ready || aimLine == null || aimMarker == null)
            return;

        CalculateAim(transform, out Vector3 target, out Vector3 direction, out _);
        aimLine.SetPosition(0, firePoint.position);
        aimLine.SetPosition(1, firePoint.position + direction *
            Mathf.Min(aimDistance, AimPreviewDistance));
        aimMarker.position = target;
    }

    private void OnGUI()
    {
        if (!aimIndicatorVisible || !ready || aimCamera == null)
            return;

        Rect viewport = aimCamera.pixelRect;
        float centerX = viewport.x + viewport.width * 0.5f;
        float centerY = Screen.height - (viewport.y + viewport.height * 0.5f);
        Color previousColor = GUI.color;
        DrawAimReticle(centerX, centerY, Color.black, 4f);
        DrawAimReticle(centerX, centerY, AimIndicatorColor, 2f);
        GUI.color = previousColor;
    }

    private static void DrawAimReticle(float centerX, float centerY, Color color, float thickness)
    {
        const float armLength = 7f;
        const float gap = 4f;
        GUI.color = color;
        GUI.DrawTexture(new Rect(centerX - thickness * 0.5f, centerY - gap - armLength,
            thickness, armLength), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(centerX - thickness * 0.5f, centerY + gap,
            thickness, armLength), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(centerX - gap - armLength, centerY - thickness * 0.5f,
            armLength, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(centerX + gap, centerY - thickness * 0.5f,
            armLength, thickness), Texture2D.whiteTexture);
    }

    public void SetAimIndicatorVisible(bool visible)
    {
        tutorialAimIndicatorVisible = visible;
        bool aimAssistHeld = Keyboard.current != null &&
            Keyboard.current.qKey.isPressed;
        SetAimIndicatorState(tutorialAimIndicatorVisible || aimAssistHeld);
    }

    private void SetAimIndicatorState(bool visible)
    {
        if (aimIndicatorVisible == visible)
            return;

        aimIndicatorVisible = visible;
        if (visible && ready)
            CreateAimIndicator();

        if (aimLine != null)
            aimLine.enabled = visible;
        if (aimMarker != null)
            aimMarker.gameObject.SetActive(visible);
    }

    public bool Fire(Transform owner)
    {
        if (!isActiveAndEnabled || !CanFire || owner == null ||
            projectilePrefab == null || firePoint == null || aimCamera == null)
            return false;

        CalculateAim(owner, out _, out Vector3 direction, out _);

        int usedSlot = System.Array.FindIndex(
            slotLoaded, loaded => loaded);

        if (usedSlot < 0)
            return false;

        HashiProjectile projectile = Instantiate(
            projectilePrefab,
            firePoint.position,
            Quaternion.LookRotation(direction.normalized)
        );

        projectile.HitEnemy += RelayEnemyHit;
        projectile.gameObject.SetActive(true);

        CurrentAmmo--;
        slotLoaded[usedSlot] = false;

        nextShotTime = Time.time + shotInterval;

        SetSlotVisible(usedSlot, false);

        // Inicia a recarga se ainda nao houver uma em andamento.
        if (nextReloadTime < 0f)
            nextReloadTime = Time.time + reloadTime;

        projectile.Launch(owner, direction, damage, hitMask);

        return true;
    }

    private void CalculateAim(
        Transform owner,
        out Vector3 target,
        out Vector3 direction,
        out bool hitSomething)
    {
        Ray ray = aimCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f));

        target = ray.GetPoint(aimDistance);
        float closest = aimDistance;
        hitSomething = false;

        int hitCount = Physics.RaycastNonAlloc(
            ray, aimHits, aimDistance, hitMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = aimHits[i];
            if (hit.collider.transform == owner ||
                hit.collider.transform.IsChildOf(owner) ||
                hit.distance >= closest)
                continue;

            closest = hit.distance;
            target = hit.point;
            hitSomething = true;
        }

        direction = target - firePoint.position;

        if (direction.sqrMagnitude < 0.0001f ||
            Vector3.Dot(direction, ray.direction) <= 0f)
        {
            direction = ray.direction;
            target = firePoint.position + direction.normalized * aimDistance;
        }

        direction.Normalize();
    }

    private void CreateAimIndicator()
    {
        if (aimIndicatorRoot != null)
            return;

        aimIndicatorRoot = new GameObject("HashiAimIndicator");
        aimIndicatorRoot.layer = 2;
        aimIndicatorRoot.transform.SetParent(transform, false);

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        if (shader != null)
        {
            aimIndicatorMaterial = new Material(shader);
            if (aimIndicatorMaterial.HasProperty("_BaseColor"))
                aimIndicatorMaterial.SetColor("_BaseColor", AimIndicatorColor);
            if (aimIndicatorMaterial.HasProperty("_Color"))
                aimIndicatorMaterial.SetColor("_Color", AimIndicatorColor);
        }
        else
        {
            Renderer sourceRenderer = projectilePrefab.GetComponentInChildren<Renderer>();
            if (sourceRenderer != null)
                aimIndicatorMaterial = new Material(sourceRenderer.sharedMaterial);
        }

        aimLine = aimIndicatorRoot.AddComponent<LineRenderer>();
        aimLine.useWorldSpace = true;
        aimLine.positionCount = 2;
        aimLine.startWidth = 0.025f;
        aimLine.endWidth = 0.012f;
        aimLine.startColor = AimIndicatorColor;
        aimLine.endColor = AimIndicatorColor;
        aimLine.numCapVertices = 4;
        if (aimIndicatorMaterial != null)
            aimLine.sharedMaterial = aimIndicatorMaterial;

        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = "HashiAimPoint";
        marker.layer = 2;
        marker.transform.SetParent(aimIndicatorRoot.transform, false);
        marker.transform.localScale = Vector3.one * 0.12f;
        Collider markerCollider = marker.GetComponent<Collider>();
        if (markerCollider != null)
            markerCollider.enabled = false;

        Renderer markerRenderer = marker.GetComponent<Renderer>();
        if (markerRenderer != null && aimIndicatorMaterial != null)
            markerRenderer.sharedMaterial = aimIndicatorMaterial;

        aimMarker = marker.transform;
        aimLine.enabled = aimIndicatorVisible;
        marker.SetActive(aimIndicatorVisible);
    }

    private void RelayEnemyHit(EnemyHealth enemy)
    {
        TargetHit?.Invoke(enemy);
    }

    private void OnDestroy()
    {
        if (aimIndicatorRoot != null)
            Destroy(aimIndicatorRoot);
        if (aimIndicatorMaterial != null)
            Destroy(aimIndicatorMaterial);
    }

    private void ReloadOneAmmo()
    {
        if (CurrentAmmo >= MaxAmmo)
            return;

        int slot = System.Array.FindIndex(
            slotLoaded, loaded => !loaded);

        if (slot < 0)
            return;

        slotLoaded[slot] = true;
        CurrentAmmo++;

        SetSlotVisible(slot, true);
    }

    private void SetSlotVisible(int slot, bool visible)
    {
        for (int j = 0; j < backRenderers[slot].Length; j++)
        {
            Renderer renderer = backRenderers[slot][j];

            if (renderer != null)
                renderer.enabled = visible && originalVisibility[slot][j];
        }
    }

    public void RefillAmmo()
    {
        if (!ready)
            return;

        CurrentAmmo = MaxAmmo;
        nextShotTime = Time.time;
        nextReloadTime = -1f;

        for (int i = 0; i < MaxAmmo; i++)
        {
            slotLoaded[i] = true;
            SetSlotVisible(i, true);
        }
    }

    [ContextMenu("Teste/Repor dois hashis")]
    private void TestRefill()
    {
        if (Application.isPlaying)
            RefillAmmo();
    }
}
