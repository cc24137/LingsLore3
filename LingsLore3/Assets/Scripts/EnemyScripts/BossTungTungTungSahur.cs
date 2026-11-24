using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth), typeof(Rigidbody), typeof(CapsuleCollider))]
public sealed class BossTungTungTungSahur : MonoBehaviour
{
    private enum Estado { Aproximando, Preparando, Atacando, Recuperando, Encerrado }

    [Header("Arena")]
    [SerializeField] private PlayerResources ling;
    [SerializeField] private Collider piso;
    [SerializeField] private LayerMask obstaculos;
    [SerializeField] private Shader shaderDosAvisos;
    [SerializeField] private bool mostrarHudDeTeste;

    [Header("Perseguição")]
    [SerializeField, Min(0.1f)] private float velocidade = 3.5f;
    [SerializeField, Min(0.1f)] private float distanciaParaGolpear = 2.65f;

    [Header("Golpe frontal")]
    [SerializeField, Min(0.1f)] private float alcanceDoGolpe = 3.4f;
    [SerializeField, Range(1f, 179f)] private float semiAnguloDoGolpe = 55f;
    [SerializeField, Min(0.1f)] private float preparacao = 0.75f;
    [SerializeField, Min(0.1f)] private float recuperacao = 1.15f;
    [SerializeField, Min(0f)] private float danoDoGolpe = 16f;
    [SerializeField, Min(0.1f)] private float alturaDoGolpe = 1.55f;

    [Header("Estado atual")]
    [SerializeField] private Estado estado;

    private const float FolgaDeColisao = 0.04f;
    private const float DuracaoDoIndicadorDeImpacto = 0.12f;
    private static readonly int CorBase = Shader.PropertyToID("_BaseColor");
    private static readonly Color Amarelo = new Color(1f, 0.72f, 0.12f);
    private static readonly Color Vermelho = new Color(1f, 0.22f, 0.06f);

    private EnemyHealth vida;
    private Rigidbody corpo;
    private CapsuleCollider colisor;
    private CapsuleCollider colisorDoLing;
    private LineRenderer aviso;
    private Material materialDoAviso;
    private Vector3 centroDoGolpe;
    private Vector3 frenteDoGolpe;
    private float fimDoEstado;

    private float RaioDoCorpo => colisor.radius *
        Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));

    private void Awake()
    {
        corpo = GetComponent<Rigidbody>();
        vida = GetComponent<EnemyHealth>();
        colisor = GetComponent<CapsuleCollider>();
        if (ling == null || !ling.TryGetComponent(out colisorDoLing) || piso == null || shaderDosAvisos == null)
        {
            Debug.LogError("Configure Ling, o piso e o shader de aviso do Tung Tung Tung Sahur.", this);
            enabled = false;
            return;
        }

        materialDoAviso = new Material(shaderDosAvisos);
        GameObject objetoDoAviso = new GameObject("Aviso do golpe frontal");
        objetoDoAviso.transform.SetParent(transform, false);
        aviso = objetoDoAviso.AddComponent<LineRenderer>();
        aviso.sharedMaterial = materialDoAviso;
        aviso.useWorldSpace = true;
        aviso.loop = true;
        aviso.positionCount = 33;
        aviso.shadowCastingMode = ShadowCastingMode.Off;
        aviso.receiveShadows = false;
        aviso.enabled = false;
    }

    private void FixedUpdate()
    {
        if (vida.IsDead || ling == null || ling.IsDead)
        {
            estado = Estado.Encerrado;
            aviso.enabled = false;
            colisor.enabled = !vida.IsDead;
            return;
        }

        if (!ling.isActiveAndEnabled)
        {
            estado = Estado.Aproximando;
            aviso.enabled = false;
            return;
        }

        if (estado == Estado.Encerrado)
        {
            colisor.enabled = true;
            estado = Estado.Aproximando;
        }

        switch (estado)
        {
            case Estado.Aproximando:
                AproximarDoLing();
                break;
            case Estado.Preparando when Time.fixedTime >= fimDoEstado:
                ExecutarGolpe();
                break;
            case Estado.Atacando when Time.fixedTime >= fimDoEstado:
                aviso.enabled = false;
                estado = Estado.Recuperando;
                fimDoEstado = Time.fixedTime + recuperacao;
                break;
            case Estado.Recuperando when Time.fixedTime >= fimDoEstado:
                estado = Estado.Aproximando;
                break;
        }
    }

    private void AproximarDoLing()
    {
        Bounds arena = piso.bounds;
        Vector3 centro = corpo.position;
        centro.y = arena.max.y;
        Vector3 ateLing = Horizontal(colisorDoLing.bounds.center - centro);
        float distancia = ateLing.magnitude;
        Vector3 frente = distancia > 0.001f ? ateLing / distancia : corpo.rotation * Vector3.forward;
        float distanciaDoGolpe = Mathf.Min(distanciaParaGolpear, alcanceDoGolpe);
        if (distancia <= distanciaDoGolpe && !Physics.Linecast(centro + Vector3.up * 0.75f,
                colisorDoLing.bounds.center, obstaculos, QueryTriggerInteraction.Ignore))
        {
            PrepararGolpe(centro, frente);
            return;
        }

        float passo = Mathf.Min(velocidade * Time.fixedDeltaTime, distancia);
        Vector3 destino = LimitarAoPiso(corpo.position + frente * passo, arena);
        Vector3 deslocamento = destino - corpo.position;
        if (deslocamento.sqrMagnitude < 0.000001f)
            return;

        int mascara = obstaculos.value | (1 << ling.gameObject.layer);
        if (VarrerCorpo(deslocamento, mascara, out RaycastHit hit))
        {
            Vector3 normal = Horizontal(hit.normal).normalized;
            Vector3 lado = Vector3.Cross(Vector3.up, normal);
            if (Vector3.Dot(lado, frente) < 0f)
                lado = -lado;

            destino = LimitarAoPiso(corpo.position + (lado + normal * 0.15f).normalized * passo, arena);
            deslocamento = destino - corpo.position;
            if (deslocamento.sqrMagnitude < 0.000001f)
                return;

            if (VarrerCorpo(deslocamento, mascara, out hit))
                deslocamento = deslocamento.normalized * Mathf.Max(0f, hit.distance - FolgaDeColisao);
        }

        if (deslocamento.sqrMagnitude < 0.000001f)
            return;

        corpo.MoveRotation(Quaternion.LookRotation(deslocamento));
        corpo.MovePosition(corpo.position + deslocamento);
    }

    private bool VarrerCorpo(Vector3 deslocamento, int mascara, out RaycastHit hit)
    {
        Vector3 escala = transform.lossyScale;
        Vector3 centro = corpo.position + corpo.rotation * Vector3.Scale(colisor.center, escala);
        float raio = RaioDoCorpo;
        float meiaAltura = Mathf.Max(0f, colisor.height * Mathf.Abs(escala.y) * 0.5f - raio);
        Vector3 eixo = Vector3.up * meiaAltura;
        return Physics.CapsuleCast(centro - eixo, centro + eixo, raio, deslocamento.normalized, out hit,
            deslocamento.magnitude + FolgaDeColisao, mascara, QueryTriggerInteraction.Ignore);
    }

    private Vector3 LimitarAoPiso(Vector3 destino, Bounds arena)
    {
        float raio = Mathf.Max(0f, Mathf.Min(arena.extents.x, arena.extents.z) - RaioDoCorpo - FolgaDeColisao);
        Vector3 noPiso = arena.center + Vector3.ClampMagnitude(Horizontal(destino - arena.center), raio);
        return new Vector3(noPiso.x, destino.y, noPiso.z);
    }

    private void PrepararGolpe(Vector3 centro, Vector3 frente)
    {
        centroDoGolpe = centro;
        frenteDoGolpe = frente;
        corpo.rotation = Quaternion.LookRotation(frente);
        estado = Estado.Preparando;
        fimDoEstado = Time.fixedTime + preparacao;

        Vector3 noChao = centro + Vector3.up * 0.05f;
        aviso.SetPosition(0, noChao);
        for (int i = 1; i < aviso.positionCount; i++)
        {
            float angulo = Mathf.Lerp(-semiAnguloDoGolpe, semiAnguloDoGolpe, (i - 1f) / (aviso.positionCount - 2));
            Vector3 borda = Quaternion.AngleAxis(angulo, Vector3.up) * frente * alcanceDoGolpe;
            aviso.SetPosition(i, noChao + borda);
        }

        materialDoAviso.SetColor(CorBase, Amarelo);
        aviso.widthMultiplier = 0.1f;
        aviso.enabled = true;
    }

    private void ExecutarGolpe()
    {
        estado = Estado.Atacando;
        fimDoEstado = Time.fixedTime + DuracaoDoIndicadorDeImpacto;
        materialDoAviso.SetColor(CorBase, Vermelho);
        aviso.widthMultiplier = 0.2f;

        Vector3 origem = centroDoGolpe + Vector3.up * (alturaDoGolpe * 0.5f);
        Vector3 alvo = colisorDoLing.ClosestPoint(origem);
        Vector3 delta = Horizontal(alvo - centroDoGolpe);
        if (delta.sqrMagnitude <= alcanceDoGolpe * alcanceDoGolpe &&
            Vector3.Angle(frenteDoGolpe, delta) <= semiAnguloDoGolpe &&
            alvo.y >= centroDoGolpe.y && alvo.y <= centroDoGolpe.y + alturaDoGolpe &&
            !Physics.Linecast(origem, alvo, obstaculos, QueryTriggerInteraction.Ignore))
        {
            ling.TakeDamage(danoDoGolpe, centroDoGolpe);
        }
    }

    private static Vector3 Horizontal(Vector3 vetor) => new Vector3(vetor.x, 0f, vetor.z);

    private void OnDisable()
    {
        if (aviso != null)
            aviso.enabled = false;
        if (estado != Estado.Encerrado)
            estado = Estado.Aproximando;
    }

    private void OnDestroy()
    {
        Destroy(materialDoAviso);
        if (aviso != null)
            Destroy(aviso.gameObject);
    }

    private void OnGUI()
    {
        if (!mostrarHudDeTeste || ling == null)
            return;

        string textoBoss = vida.IsDead ? "Boss derrotado" :
            $"Sahur   HP {Mathf.CeilToInt(vida.CurrentHealth)} / {Mathf.CeilToInt(vida.MaxHealth)}";
        string textoLing = ling.IsDead ? "Ling caiu" : $"Ling   HP {Mathf.CeilToInt(ling.CurrentHealth)}";
        GUI.Box(new Rect(16f, 16f, 225f, 58f), GUIContent.none);
        GUI.Label(new Rect(25f, 22f, 207f, 20f), textoBoss);
        GUI.Label(new Rect(25f, 43f, 207f, 20f), textoLing);
    }
}
