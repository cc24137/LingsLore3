using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Checkpoint e respawn do tutorial (sem tela de fim de jogo).
/// Coloque no objeto "[ Tutorial ]" — o MESMO que vai receber o RodadaTutorial.
/// Não coloque no Ling: o script precisa continuar vivo se o jogador for desativado.
/// </summary>
public class CheckpointTutorial : MonoBehaviour
{
    public static CheckpointTutorial Instancia { get; private set; }

    [Header("Jogador")]
    [Tooltip("Deixe vazio: o script procura pela tag 'Player' ou por um componente chamado PlayerResources.")]
    [SerializeField] private Transform jogador;

    [Header("Checkpoint")]
    [Tooltip("Ponto de nascimento. Se vazio, usa a posição do jogador no Start.")]
    [SerializeField] private Transform pontoInicial;

    [Header("Queda")]
    [SerializeField] private bool detectarQueda = true;
    [Tooltip("Abaixo deste Y o jogador conta como caído. A DieZone original ficava em Y = -15.")]
    [SerializeField] private float alturaDeQueda = -12f;

    [Header("Morte")]
    [Tooltip("Detecta vida <= 0 sozinho. Se você ligou On Death -> NotificarMorte, pode desmarcar.")]
    [SerializeField] private bool monitorarMorte = true;

    [Header("Tempo")]
    [Tooltip("Espera antes de reposicionar (deixa a animação de dano/morte rodar).")]
    [SerializeField] private float atrasoRespawn = 0.6f;
    [Tooltip("Se o jogo tiver pausa, desmarque para não forçar timeScale = 1.")]
    [SerializeField] private bool garantirTimeScaleNormal = true;

    [Header("Eventos (opcional)")]
    [Tooltip("Chamado depois que o jogador volta ao checkpoint (som, partícula, resetar Animator etc.).")]
    [SerializeField] private UnityEvent aoRespawnar;

    private Vector3 posCheckpoint;
    private Quaternion rotCheckpoint;
    private Component recursos;          // PlayerResources, localizado por nome
    private bool respawnando;
    private int respawnsFalhos;

    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    // Nomes procurados por reflexão no PlayerResources.
    // Se o seu usar outros nomes, acrescente aqui (é o único lugar a editar).
    private static readonly string[] MetodosRestaurarTudo = { "Reviver", "RestaurarTudo", "RestaurarCompleto", "ResetarTudo" };
    private static readonly string[] MetodosRestaurarStamina = { "RestaurarStamina", "RecarregarStamina", "ResetarStamina" };
    private static readonly string[] NomesVidaAtual = { "VidaAtual", "Vida", "Health", "HP", "CurrentHealth", "vidaAtual", "vida", "health", "hp", "currentHealth" };
    private static readonly string[] NomesVidaMax = { "VidaMaxima", "VidaMax", "MaxVida", "MaxHealth", "MaxHP", "vidaMaxima", "vidaMax", "maxVida", "maxHealth", "maxHP" };
    private static readonly string[] NomesStaminaAtual = { "StaminaAtual", "Stamina", "CurrentStamina", "staminaAtual", "stamina", "currentStamina" };
    private static readonly string[] NomesStaminaMax = { "StaminaMaxima", "StaminaMax", "MaxStamina", "staminaMaxima", "staminaMax", "maxStamina" };
    private static readonly string[] NomesMorto = { "estaMorto", "morto", "isDead", "EstaMorto", "Morto", "IsDead" };

    // ------------------------------------------------------------------ ciclo de vida

    private void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            enabled = false;
            Destroy(this);
            return;
        }
        Instancia = this;
    }

    private void Start()
    {
        if (Instancia != this) return;

        if (jogador == null) LocalizarJogador();

        if (jogador == null)
        {
            Debug.LogError("[CheckpointTutorial] Jogador não encontrado. Arraste o Ling no campo 'Jogador'.");
            enabled = false;
            return;
        }

        LocalizarRecursos();

        if (pontoInicial != null)
        {
            posCheckpoint = pontoInicial.position;
            rotCheckpoint = pontoInicial.rotation;
        }
        else
        {
            posCheckpoint = jogador.position;
            rotCheckpoint = jogador.rotation;
        }
    }

    private void Update()
    {
        if (Instancia != this || jogador == null || respawnando) return;

        if (detectarQueda && jogador.position.y < alturaDeQueda)
        {
            IniciarRespawn("queda");
            return;
        }

        if (monitorarMorte && JogadorEstaMorto())
            IniciarRespawn("morte");
    }

    // ------------------------------------------------------------------ API pública

    /// <summary>Define o checkpoint a partir de um Transform (chame no início de cada rodada).</summary>
    public void DefinirCheckpoint(Transform ponto)
    {
        if (ponto == null) return;
        DefinirCheckpoint(ponto.position, ponto.rotation);
    }

    public void DefinirCheckpoint(Vector3 posicao, Quaternion rotacao)
    {
        posCheckpoint = posicao;
        rotCheckpoint = rotacao;
    }

    /// <summary>Ligue no On Death do PlayerResources — mais confiável que a detecção automática.</summary>
    public void NotificarMorte() => IniciarRespawn("morte");

    /// <summary>Manda o jogador de volta ao checkpoint agora (botão, teste, etc.).</summary>
    public void VoltarAoCheckpoint() => IniciarRespawn("manual");

    [ContextMenu("Testar: voltar ao checkpoint")]
    private void TesteVoltar()
    {
        if (Application.isPlaying) VoltarAoCheckpoint();
    }

    // ------------------------------------------------------------------ respawn

    private void IniciarRespawn(string motivo)
    {
        if (respawnando) return;
        respawnando = true;
        StartCoroutine(RotinaRespawn(motivo));
    }

    private IEnumerator RotinaRespawn(string motivo)
    {
        Debug.Log($"[CheckpointTutorial] Respawn ({motivo}).");

        // Realtime: funciona mesmo se o jogo estiver pausado (timeScale 0).
        if (atrasoRespawn > 0f) yield return new WaitForSecondsRealtime(atrasoRespawn);

        if (garantirTimeScaleNormal) Time.timeScale = 1f;

        ReposicionarJogador();
        RestaurarRecursos();
        aoRespawnar?.Invoke();

        // Trava de segurança: se a vida não voltou, avisa e evita loop infinito.
        if (monitorarMorte && JogadorEstaMorto())
        {
            respawnsFalhos++;
            Debug.LogWarning($"[CheckpointTutorial] O jogador continua morto após o respawn ({respawnsFalhos}). " +
                             "O script não conseguiu restaurar a vida — confira os nomes em PlayerResources.");

            if (respawnsFalhos >= 3)
            {
                monitorarMorte = false;
                Debug.LogError("[CheckpointTutorial] Detecção de morte desligada para evitar loop. " +
                               "Ligue On Death -> NotificarMorte ou ajuste os nomes no topo do script.");
            }
        }
        else
        {
            respawnsFalhos = 0;
        }

        respawnando = false;
    }

    private void ReposicionarJogador()
    {
        if (!jogador.gameObject.activeSelf) jogador.gameObject.SetActive(true);

        var cc = jogador.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        jogador.SetPositionAndRotation(posCheckpoint, rotCheckpoint);

        if (cc != null) cc.enabled = true;

        var rb = jogador.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.angularVelocity = Vector3.zero;
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = Vector3.zero;
#else
            rb.velocity = Vector3.zero;
#endif
            rb.WakeUp();
        }

        Physics.SyncTransforms();
    }

    // ------------------------------------------------------------------ restauração (via reflexão)

    private void RestaurarRecursos()
    {
        if (recursos == null) return;

        InvocarMetodo(MetodosRestaurarTudo);

        float maxVida = LerNumero(NomesVidaMax, float.NaN);
        if (!float.IsNaN(maxVida)) EscreverNumero(NomesVidaAtual, maxVida);

        float maxStamina = LerNumero(NomesStaminaMax, float.NaN);
        if (!float.IsNaN(maxStamina)) EscreverNumero(NomesStaminaAtual, maxStamina);
        else InvocarMetodo(MetodosRestaurarStamina);

        LimparFlagsDeMorte();
    }

    private bool JogadorEstaMorto()
    {
        if (recursos == null) return false;

        var t = recursos.GetType();

        foreach (var n in NomesMorto)
        {
            var f = t.GetField(n, Flags);
            if (f != null && f.FieldType == typeof(bool) && (bool)f.GetValue(recursos)) return true;

            var p = t.GetProperty(n, Flags);
            if (p != null && p.CanRead && p.PropertyType == typeof(bool) && (bool)p.GetValue(recursos)) return true;
        }

        float vida = LerNumero(NomesVidaAtual, float.NaN);
        return !float.IsNaN(vida) && vida <= 0f;
    }

    private void LimparFlagsDeMorte()
    {
        var t = recursos.GetType();

        foreach (var n in NomesMorto)
        {
            var f = t.GetField(n, Flags);
            if (f != null && f.FieldType == typeof(bool)) f.SetValue(recursos, false);

            var p = t.GetProperty(n, Flags);
            if (p != null && p.CanWrite && p.PropertyType == typeof(bool)) p.SetValue(recursos, false);
        }
    }

    private float LerNumero(string[] nomes, float padrao)
    {
        var t = recursos.GetType();

        foreach (var n in nomes)
        {
            var p = t.GetProperty(n, Flags);
            if (p != null && p.CanRead)
            {
                object v = p.GetValue(recursos);
                if (v is float f) return f;
                if (v is int i) return i;
                if (v is double d) return (float)d;
            }

            var fi = t.GetField(n, Flags);
            if (fi != null)
            {
                object v = fi.GetValue(recursos);
                if (v is float f) return f;
                if (v is int i) return i;
                if (v is double d) return (float)d;
            }
        }

        return padrao;
    }

    private bool EscreverNumero(string[] nomes, float valor)
    {
        var t = recursos.GetType();

        foreach (var n in nomes)
        {
            var p = t.GetProperty(n, Flags);
            if (p != null && p.CanWrite)
            {
                if (p.PropertyType == typeof(float)) { p.SetValue(recursos, valor); return true; }
                if (p.PropertyType == typeof(int)) { p.SetValue(recursos, Mathf.RoundToInt(valor)); return true; }
            }

            var fi = t.GetField(n, Flags);
            if (fi != null)
            {
                if (fi.FieldType == typeof(float)) { fi.SetValue(recursos, valor); return true; }
                if (fi.FieldType == typeof(int)) { fi.SetValue(recursos, Mathf.RoundToInt(valor)); return true; }
            }
        }

        return false;
    }

    private bool InvocarMetodo(string[] nomes)
    {
        var t = recursos.GetType();

        foreach (var n in nomes)
        {
            var m = t.GetMethod(n, Flags, null, Type.EmptyTypes, null);
            if (m != null) { m.Invoke(recursos, null); return true; }
        }

        return false;
    }

    // ------------------------------------------------------------------ localização automática

    private void LocalizarJogador()
    {
        var go = GameObject.FindWithTag("Player");
        if (go != null)
        {
            jogador = go.transform;
            return;
        }

#if UNITY_2023_1_OR_NEWER
        var todos = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var todos = FindObjectsOfType<MonoBehaviour>(true);
#endif

        foreach (var mb in todos)
        {
            if (mb.GetType().Name == "PlayerResources")
            {
                jogador = mb.transform;
                return;
            }
        }
    }

    private void LocalizarRecursos()
    {
        foreach (var c in jogador.GetComponentsInChildren<MonoBehaviour>(true))
            if (c.GetType().Name == "PlayerResources") { recursos = c; return; }

        foreach (var c in jogador.GetComponentsInParent<MonoBehaviour>(true))
            if (c.GetType().Name == "PlayerResources") { recursos = c; return; }

        Debug.LogWarning("[CheckpointTutorial] PlayerResources não encontrado. " +
                         "A posição volta ao checkpoint, mas vida e stamina não serão restauradas.");
    }
}