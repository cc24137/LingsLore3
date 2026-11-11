using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ContadorCreditos : MonoBehaviour
{
    [System.Serializable]
    private class RodadaTutorial
    {
        public GameObject grupoCreditos;
        [Min(0f)] public float tempoLimite;
        public Transform pontoInicial;
        public EnemyHealth alvoParaDerrotar;
        public bool exigeComboLeve;
        public bool exigeComboCarregado;
        public bool exigeAtaqueAereoGiratorio;
        public bool exigeArremessoHashi;
    }

    [SerializeField] private TMP_Text textoContador;
    [SerializeField, Min(1)] private int totalCreditos = 5;

    [Header("Rodadas opcionais - o total sera contado automaticamente")]
    [SerializeField] private RodadaTutorial[] rodadas = new RodadaTutorial[0];

    [Header("Cronometro e jogador")]
    [SerializeField] private TMP_Text textoTempo;
    [SerializeField] private PlayerResources jogador;
    private PlayerMotor motorJogador;
    private PlayerMelee meleeJogador;
    private PlayerHashi hashiJogador;

    [Header("Reinicio apos morte")]
    [SerializeField, Min(0f)] private float atrasoRespawn = 0.35f;
    [SerializeField, Min(0f)] private float tempoInvulnerabilidade = 2.5f;

    private int creditosColetados;
    private int rodadaAtual = -1;
    private bool coletaAutomatica;
    private bool tempoAtivo;
    private bool tutorialConcluido;
    private bool reiniciandoPorMorte;
    private float tempoRestante;
    private MoedaColetavel[][] notasPorRodada;
    private Coroutine coroutineReinicio;
    private EnemyHealth alvoDaRodada;
    private int progressoComboLeve;
    private bool comboLeveCompleto;
    private int progressoComboCarregado;
    private bool comboCarregadoCompleto;
    private bool ataqueAereoGiratorioCompleto;
    private bool arremessoHashiCompleto;

    private readonly HashSet<MoedaColetavel> notasRegistradas =
        new HashSet<MoedaColetavel>();
    private readonly HashSet<MoedaColetavel> notasDaRodadaAtual =
        new HashSet<MoedaColetavel>();
    private readonly HashSet<MoedaColetavel> notasContadas =
        new HashSet<MoedaColetavel>();
    private bool creditosLiberadosPeloAlvo;

    private void Awake()
    {
        if (textoContador == null)
        {
            Debug.LogError("Atribua o texto do contador no Inspector.", this);
            enabled = false;
            return;
        }

        AtualizarTexto();
        if (textoTempo != null)
            textoTempo.gameObject.SetActive(false);
    }

    private void Start()
    {
        ConfigurarRodadaDoManequim();

        if (rodadas == null || rodadas.Length == 0)
            return;

        // Valida tudo antes de alterar a cena.
        notasPorRodada = new MoedaColetavel[rodadas.Length][];
        for (int i = 0; i < rodadas.Length; i++)
        {
            if (rodadas[i] == null)
            {
                Debug.LogError($"Rodada {i + 1}: configure os dados da rodada no Inspector.", this);
                enabled = false;
                return;
            }

            GameObject grupo = rodadas[i].grupoCreditos;
            if (grupo == null || transform.IsChildOf(grupo.transform) ||
                grupo.GetComponentsInChildren<MoedaColetavel>(true).Length == 0)
            {
                Debug.LogError($"Rodada {i + 1}: atribua um grupo com notas, separado do Canvas.", this);
                enabled = false;
                return;
            }

            notasPorRodada[i] = grupo.GetComponentsInChildren<MoedaColetavel>(true);

            if (rodadas[i].pontoInicial != null &&
                rodadas[i].pontoInicial.IsChildOf(grupo.transform))
            {
                Debug.LogError($"Rodada {i + 1}: coloque o ponto inicial fora do grupo de creditos.", this);
                enabled = false;
                return;
            }

            for (int j = 0; j < i; j++)
            {
                GameObject anterior = rodadas[j].grupoCreditos;
                if (grupo == anterior)
                    continue;

                if (grupo.transform.IsChildOf(anterior.transform) ||
                    anterior.transform.IsChildOf(grupo.transform))
                {
                    Debug.LogError("Use grupos separados para cada rodada, sem colocar um dentro do outro.", this);
                    enabled = false;
                    return;
                }
            }
        }

        if (jogador == null)
            jogador = FindFirstObjectByType<PlayerResources>();

        if (jogador == null)
        {
            Debug.LogError("ContadorCreditos: nao foi encontrado um PlayerResources para controlar a morte e o respawn.", this);
            enabled = false;
            return;
        }

        jogador.Died += AoJogadorMorrer;
        motorJogador = jogador.GetComponent<PlayerMotor>();
        meleeJogador = jogador.GetComponent<PlayerMelee>();
        hashiJogador = jogador.GetComponent<PlayerHashi>();
        bool acompanhaMelee = Array.Exists(rodadas,
            rodada => rodada != null &&
                (rodada.exigeComboLeve || rodada.exigeComboCarregado ||
                 rodada.exigeAtaqueAereoGiratorio));
        if (acompanhaMelee && meleeJogador == null)
        {
            Debug.LogError("ContadorCreditos: o jogador precisa de PlayerMelee para validar o combo.", this);
            enabled = false;
            return;
        }

        bool acompanhaHashi = Array.Exists(rodadas,
            rodada => rodada != null && rodada.exigeArremessoHashi);
        if (acompanhaHashi && hashiJogador == null)
        {
            Debug.LogError("ContadorCreditos: o jogador precisa de PlayerHashi para validar o arremesso.", this);
            enabled = false;
            return;
        }

        if (meleeJogador != null)
            meleeJogador.HitLanded += AoJogadorAtingirAlvo;
        if (hashiJogador != null)
            hashiJogador.TargetHit += AoHashiAtingirAlvo;

        for (int i = 0; i < rodadas.Length; i++)
        {
            GameObject grupo = rodadas[i].grupoCreditos;
            grupo.SetActive(false);

            foreach (MoedaColetavel nota in notasPorRodada[i])
            {
                if (notasRegistradas.Add(nota))
                    nota.Coletada += RegistrarColetaAutomatica;
            }
        }

        if (alvoDaRodada != null)
        {
            alvoDaRodada.Died -= AoAlvoDaRodadaDerrotado;
            alvoDaRodada.Died += AoAlvoDaRodadaDerrotado;
            alvoDaRodada.RestoreHealth();
            alvoDaRodada.gameObject.SetActive(false);
        }

        coletaAutomatica = true;
        IniciarRodada(0);
    }

    private void Update()
    {
        if (!tempoAtivo || tutorialConcluido || reiniciandoPorMorte ||
            (jogador != null && jogador.IsDead))
            return;

        tempoRestante = Mathf.Max(0f, tempoRestante - Time.deltaTime);
        AtualizarTempo();

        // Ao acabar o tempo, a propria rodada reinicia com todos os creditos.
        if (tempoRestante <= 0f)
            IniciarRodada(rodadaAtual);
    }

    private void AoJogadorMorrer()
    {
        if (!coletaAutomatica || tutorialConcluido || rodadaAtual < 0 ||
            reiniciandoPorMorte)
            return;

        // Congela o cronometro durante a pequena espera do respawn.
        tempoAtivo = false;
        reiniciandoPorMorte = true;

        if (coroutineReinicio != null)
            StopCoroutine(coroutineReinicio);

        coroutineReinicio = StartCoroutine(ReiniciarRodadaAposMorte());
    }

    private IEnumerator ReiniciarRodadaAposMorte()
    {
        if (atrasoRespawn > 0f)
            yield return new WaitForSecondsRealtime(atrasoRespawn);

        if (this == null || !isActiveAndEnabled || jogador == null ||
            rodadaAtual < 0 || tutorialConcluido)
        {
            reiniciandoPorMorte = false;
            coroutineReinicio = null;
            yield break;
        }

        // IniciarRodada restaura notas, contador, cronometro e ponto inicial.
        IniciarRodada(rodadaAtual);

        // Reanima o jogador e protege contra dano imediato ao reaparecer.
        jogador.SetRespawnInvulnerability(tempoInvulnerabilidade);
        jogador.RestoreFullResources();

        reiniciandoPorMorte = false;
        coroutineReinicio = null;
    }

    // Compatibilidade com o contador antigo, sem rodadas configuradas.
    // No modo automatico, ignora ligacoes antigas para nao contar duas vezes.
    public void RegistrarColeta()
    {
        if (coletaAutomatica)
            return;

        ContabilizarCredito();
    }

    private void RegistrarColetaAutomatica(MoedaColetavel nota)
    {
        if (!isActiveAndEnabled || !coletaAutomatica ||
            !notasDaRodadaAtual.Contains(nota) || !notasContadas.Add(nota))
            return;

        ContabilizarCredito();
    }

    private void ConfigurarRodadaDoManequim()
    {
        if (rodadas == null || rodadas.Length == 0)
            return;

        EnemyHealth manequim = null;
        foreach (RodadaTutorial rodada in rodadas)
        {
            if (rodada != null && rodada.alvoParaDerrotar != null)
            {
                manequim = rodada.alvoParaDerrotar;
                break;
            }
        }

        if (manequim == null)
        {
#if UNITY_2023_1_OR_NEWER
            EnemyHealth[] alvos = FindObjectsByType<EnemyHealth>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            EnemyHealth[] alvos = FindObjectsOfType<EnemyHealth>(true);
#endif
            foreach (EnemyHealth alvo in alvos)
            {
                if (alvo != null &&
                    (alvo.name.IndexOf("dummy", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     alvo.name.IndexOf("manequim", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    manequim = alvo;
                    break;
                }
            }
        }

        if (manequim == null)
            return;

        alvoDaRodada = manequim;
        GameObject grupoRodada5 = GameObject.Find("CreditosRodada5");
        GameObject grupoRodada6 = GameObject.Find("CreditosRodada6");
        GameObject grupoRodada7 = GameObject.Find("CreditosRodada7");
        GameObject grupoRodada8 = GameObject.Find("CreditosRodada8");
        GameObject grupoRodada9 = GameObject.Find("CreditosRodada9");
        bool rodadaDoDummyConfigurada = false;
        foreach (RodadaTutorial rodada in rodadas)
        {
            if (rodada == null)
                continue;

            if ((grupoRodada5 != null && rodada.grupoCreditos == grupoRodada5) ||
                (grupoRodada6 != null && rodada.grupoCreditos == grupoRodada6) ||
                (grupoRodada7 != null && rodada.grupoCreditos == grupoRodada7) ||
                (grupoRodada8 != null && rodada.grupoCreditos == grupoRodada8) ||
                (grupoRodada9 != null && rodada.grupoCreditos == grupoRodada9))
            {
                rodada.alvoParaDerrotar = manequim;
                rodadaDoDummyConfigurada = true;
            }
        }

        if (rodadaDoDummyConfigurada)
            return;

        if (grupoRodada5 == null ||
            grupoRodada5.GetComponentsInChildren<MoedaColetavel>(true).Length == 0)
        {
            grupoRodada5 = rodadas[rodadas.Length - 1].grupoCreditos;
        }

        RodadaTutorial[] comRodadaDoManequim = new RodadaTutorial[rodadas.Length + 1];
        Array.Copy(rodadas, comRodadaDoManequim, rodadas.Length);
        comRodadaDoManequim[rodadas.Length] = new RodadaTutorial
        {
            // CreditosRodada5 e separado do grupo que contem as zonas da rodada 4.
            grupoCreditos = grupoRodada5,
            tempoLimite = 0f,
            alvoParaDerrotar = manequim
        };
        rodadas = comRodadaDoManequim;
    }

    private void AoJogadorAtingirAlvo(EnemyHealth alvo, PlayerMelee.AttackType ataque)
    {
        if (!isActiveAndEnabled || tutorialConcluido || rodadaAtual < 0 ||
            rodadaAtual >= rodadas.Length)
            return;

        RodadaTutorial rodada = rodadas[rodadaAtual];
        if (rodada.alvoParaDerrotar != alvo || creditosLiberadosPeloAlvo)
            return;

        if (rodada.exigeComboLeve)
        {
            if (ataque == PlayerMelee.AttackType.Light1)
                progressoComboLeve = 1;
            else if (ataque == PlayerMelee.AttackType.Light2 && progressoComboLeve == 1)
                progressoComboLeve = 2;
            else if (ataque == PlayerMelee.AttackType.Light3 && progressoComboLeve == 2)
                progressoComboLeve = 3;
            else
                progressoComboLeve = 0;

            if (progressoComboLeve == 3)
            {
                comboLeveCompleto = true;
                LiberarCreditosPeloAlvo(alvo);
                return;
            }
        }
        else if (rodada.exigeComboCarregado)
        {
            if (ataque == PlayerMelee.AttackType.ChargedUp)
                progressoComboCarregado = 1;
            else if (ataque == PlayerMelee.AttackType.ChargedDown &&
                progressoComboCarregado == 1)
                progressoComboCarregado = 2;
            else
                progressoComboCarregado = 0;

            if (progressoComboCarregado == 2)
            {
                comboCarregadoCompleto = true;
                LiberarCreditosPeloAlvo(alvo);
                return;
            }
        }
        else if (rodada.exigeAtaqueAereoGiratorio &&
            ataque == PlayerMelee.AttackType.AirSpin)
        {
            ataqueAereoGiratorioCompleto = true;
            LiberarCreditosPeloAlvo(alvo);
            return;
        }

        AtualizarTexto();
    }

    private void AoHashiAtingirAlvo(EnemyHealth alvo)
    {
        if (!isActiveAndEnabled || tutorialConcluido || rodadaAtual < 0 ||
            rodadaAtual >= rodadas.Length)
            return;

        RodadaTutorial rodada = rodadas[rodadaAtual];
        if (!rodada.exigeArremessoHashi || rodada.alvoParaDerrotar != alvo ||
            creditosLiberadosPeloAlvo)
            return;

        arremessoHashiCompleto = true;
        if (hashiJogador != null)
            hashiJogador.SetAimIndicatorVisible(false);
        LiberarCreditosPeloAlvo(alvo);
    }

    private void AoAlvoDaRodadaDerrotado(EnemyHealth alvo)
    {
        if (!isActiveAndEnabled || tutorialConcluido || rodadaAtual < 0 ||
            rodadaAtual >= rodadas.Length)
            return;

        RodadaTutorial rodada = rodadas[rodadaAtual];
        if (rodada.alvoParaDerrotar != alvo || creditosLiberadosPeloAlvo)
            return;

        if ((rodada.exigeComboLeve && !comboLeveCompleto) ||
            (rodada.exigeComboCarregado && !comboCarregadoCompleto) ||
            (rodada.exigeAtaqueAereoGiratorio && !ataqueAereoGiratorioCompleto) ||
            (rodada.exigeArremessoHashi && !arremessoHashiCompleto))
        {
            // O dummy pode receber varios golpes enquanto o jogador aprende a sequencia.
            alvo.RestoreHealth();
            alvo.gameObject.SetActive(true);
            Physics.SyncTransforms();
            return;
        }

        LiberarCreditosPeloAlvo(alvo);
    }

    private void LiberarCreditosPeloAlvo(EnemyHealth alvo)
    {
        if (!isActiveAndEnabled || tutorialConcluido || rodadaAtual < 0 ||
            rodadaAtual >= rodadas.Length || creditosLiberadosPeloAlvo)
            return;

        RodadaTutorial rodada = rodadas[rodadaAtual];
        if (rodada.alvoParaDerrotar != alvo)
            return;

        MoedaColetavel[] notas = notasPorRodada[rodadaAtual];
        float raio = Mathf.Max(0.65f, notas.Length * 0.18f);
        Vector3 centro = alvo.transform.position;
        for (int i = 0; i < notas.Length; i++)
        {
            float angulo = i * Mathf.PI * 2f / Mathf.Max(1, notas.Length);
            notas[i].transform.SetPositionAndRotation(
                centro + new Vector3(Mathf.Cos(angulo) * raio, 1f, Mathf.Sin(angulo) * raio),
                Quaternion.identity);
            notas[i].ReiniciarColeta();
        }

        creditosLiberadosPeloAlvo = true;
        rodada.grupoCreditos.SetActive(true);
        AtualizarTexto();
        alvo.gameObject.SetActive(false);
    }

    private void ContabilizarCredito()
    {
        if (!isActiveAndEnabled || creditosColetados >= totalCreditos)
            return;

        creditosColetados++;
        AtualizarTexto();

        if (creditosColetados == totalCreditos && rodadaAtual >= 0)
        {
            tempoAtivo = false;
            rodadas[rodadaAtual].grupoCreditos.SetActive(false);

            if (rodadaAtual + 1 < rodadas.Length)
                IniciarRodada(rodadaAtual + 1);
            else
            {
                tutorialConcluido = true;
                if (motorJogador != null)
                    motorJogador.SetAttackLessonMode(PlayerMotor.AttackLessonMode.All);

                if (textoTempo != null)
                    textoTempo.gameObject.SetActive(false);
            }
        }
    }

    private void IniciarRodada(int indice)
    {
        if (indice < 0 || indice >= rodadas.Length)
            return;

        rodadaAtual = indice;
        RodadaTutorial rodada = rodadas[indice];
        progressoComboLeve = 0;
        comboLeveCompleto = false;
        progressoComboCarregado = 0;
        comboCarregadoCompleto = false;
        ataqueAereoGiratorioCompleto = false;
        arremessoHashiCompleto = false;
        if (motorJogador != null)
        {
            PlayerMotor.AttackLessonMode modo = rodada.alvoParaDerrotar == null
                ? PlayerMotor.AttackLessonMode.All
                : rodada.exigeComboCarregado
                    ? PlayerMotor.AttackLessonMode.ChargedComboOnly
                    : rodada.exigeAtaqueAereoGiratorio
                        ? PlayerMotor.AttackLessonMode.AirSpinOnly
                        : rodada.exigeArremessoHashi
                            ? PlayerMotor.AttackLessonMode.HashiOnly
                            : PlayerMotor.AttackLessonMode.LightComboOnly;
            motorJogador.SetAttackLessonMode(modo);
        }

        if (rodada.alvoParaDerrotar != null)
            rodada.alvoParaDerrotar.SetBasicAttackOnly(
                !rodada.exigeComboCarregado && !rodada.exigeAtaqueAereoGiratorio &&
                !rodada.exigeArremessoHashi);
        if (hashiJogador != null)
            hashiJogador.SetAimIndicatorVisible(rodada.exigeArremessoHashi);

        tempoAtivo = false;
        rodada.grupoCreditos.SetActive(false);
        notasDaRodadaAtual.Clear();
        notasContadas.Clear();
        totalCreditos = notasPorRodada[indice].Length;
        creditosColetados = 0;

        foreach (MoedaColetavel nota in notasPorRodada[indice])
        {
            notasDaRodadaAtual.Add(nota);
            nota.ReiniciarColeta();
        }

        creditosLiberadosPeloAlvo = rodada.alvoParaDerrotar == null;
        if (rodada.alvoParaDerrotar != null)
        {
            rodada.alvoParaDerrotar.RestoreHealth();
            rodada.alvoParaDerrotar.gameObject.SetActive(true);
            ReposicionarJogadorDianteDoAlvo(rodada.alvoParaDerrotar.transform);
        }
        else
        {
            ReposicionarJogador(rodada.pontoInicial);
        }

        AtualizarTexto();
        rodada.grupoCreditos.SetActive(creditosLiberadosPeloAlvo);

        tempoRestante = Mathf.Max(0f, rodada.tempoLimite);
        tempoAtivo = tempoRestante > 0f;
        if (textoTempo != null)
        {
            textoTempo.gameObject.SetActive(tempoAtivo);
            AtualizarTempo();
        }
    }

    private void ReposicionarJogadorDianteDoAlvo(Transform alvo)
    {
        if (alvo == null || jogador == null)
            return;

        Vector3 direcao = Vector3.ProjectOnPlane(alvo.forward, Vector3.up).normalized;
        if (direcao.sqrMagnitude < 0.001f)
            direcao = Vector3.forward;

        Vector3 posicao = alvo.position - direcao * 1.8f;
        Quaternion rotacao = Quaternion.LookRotation(direcao, Vector3.up);
        Rigidbody corpo = jogador.GetComponent<Rigidbody>();
        if (corpo != null)
        {
            corpo.position = posicao;
            corpo.rotation = rotacao;
            corpo.linearVelocity = Vector3.zero;
            corpo.angularVelocity = Vector3.zero;
            corpo.WakeUp();
            Physics.SyncTransforms();
        }
        else
        {
            jogador.transform.SetPositionAndRotation(posicao, rotacao);
        }
    }

    private void ReposicionarJogador(Transform ponto)
    {
        if (ponto == null || jogador == null)
            return;

        Rigidbody corpo = jogador.GetComponent<Rigidbody>();
        if (corpo != null)
        {
            corpo.position = ponto.position;
            corpo.rotation = ponto.rotation;
            corpo.linearVelocity = Vector3.zero;
            corpo.angularVelocity = Vector3.zero;
            corpo.WakeUp();
            Physics.SyncTransforms();
        }
        else
        {
            jogador.transform.SetPositionAndRotation(ponto.position, ponto.rotation);
        }
    }

    private void AtualizarTempo()
    {
        if (textoTempo != null)
            textoTempo.text = $"Tempo: {Mathf.CeilToInt(tempoRestante)}s";
    }

    private void AtualizarTexto()
    {
        if (rodadaAtual >= 0 && rodadaAtual < rodadas.Length &&
            rodadas[rodadaAtual].alvoParaDerrotar != null && !creditosLiberadosPeloAlvo)
        {
            if (rodadas[rodadaAtual].exigeComboLeve)
                textoContador.text = $"Combo no dummy: {progressoComboLeve}/3 (Light1 → Light2 → Light3)";
            else if (rodadas[rodadaAtual].exigeComboCarregado)
                textoContador.text = $"Segure ataque; durante o ascendente, solte e segure de novo para o descendente: {progressoComboCarregado}/2";
            else if (rodadas[rodadaAtual].exigeAtaqueAereoGiratorio)
                textoContador.text = "Pule e acerte o dummy com o ataque aéreo giratório";
            else if (rodadas[rodadaAtual].exigeArremessoHashi)
                textoContador.text = "Mire no dummy pelo centro da tela e clique com o botão direito";
            else
                textoContador.text = "Golpeie o dummy com o ataque básico";
            return;
        }

        textoContador.text = $"Creditos sociais: {creditosColetados}/{totalCreditos}";
    }

    private void OnDestroy()
    {
        if (jogador != null)
            jogador.Died -= AoJogadorMorrer;

        if (motorJogador != null)
            motorJogador.SetAttackLessonMode(PlayerMotor.AttackLessonMode.All);

        if (meleeJogador != null)
            meleeJogador.HitLanded -= AoJogadorAtingirAlvo;
        if (hashiJogador != null)
        {
            hashiJogador.SetAimIndicatorVisible(false);
            hashiJogador.TargetHit -= AoHashiAtingirAlvo;
        }

        if (alvoDaRodada != null)
            alvoDaRodada.Died -= AoAlvoDaRodadaDerrotado;

        foreach (MoedaColetavel nota in notasRegistradas)
        {
            if (nota != null)
                nota.Coletada -= RegistrarColetaAutomatica;
        }
    }
}
