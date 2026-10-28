
using UnityEngine;

public class PlayerHashi : MonoBehaviour
{
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

    public bool Fire(Transform owner)
    {
        if (!isActiveAndEnabled || !CanFire || owner == null ||
            projectilePrefab == null || firePoint == null || aimCamera == null)
            return false;

        Ray ray = aimCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f));

        Vector3 target = ray.GetPoint(aimDistance);
        float closest = aimDistance;

        foreach (RaycastHit hit in Physics.RaycastAll(
            ray, aimDistance, hitMask, QueryTriggerInteraction.Collide))
        {
            if (hit.collider.transform.IsChildOf(owner) ||
                hit.distance >= closest)
                continue;

            closest = hit.distance;
            target = hit.point;
        }

        Vector3 direction = target - firePoint.position;

        if (direction.sqrMagnitude < 0.0001f)
            direction = ray.direction;

        if (Vector3.Dot(direction, ray.direction) <= 0f)
            direction = ray.direction;

        int usedSlot = System.Array.FindIndex(
            slotLoaded, loaded => loaded);

        if (usedSlot < 0)
            return false;

        HashiProjectile projectile = Instantiate(
            projectilePrefab,
            firePoint.position,
            Quaternion.LookRotation(direction.normalized)
        );

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
