using System.Collections;
using UnityEngine;

internal sealed class EncenacaoFinal : MonoBehaviour
{
    private PlayerMotor motor;
    private Rigidbody corpo;
    private Animator animador;
    private ThirdPersonCamera controleDaCamera;
    private Camera cameraPrincipal;
    private GameObject hud;
    private Transform garfo;
    private Vector3 posicaoOriginalDoGarfo;
    private Quaternion rotacaoOriginalDoGarfo;
    private Vector3 pontaDoGarfo;
    private Mesh malhaDoYahu;
    private MeshRenderer yahuCaido;
    private Light luzDaCena;
    private float escalaDoTempo;
    private float campoDeVisao;
    private Vector3 posicaoDaCamera;
    private Quaternion rotacaoDaCamera;
    private CursorLockMode modoDoCursor;
    private bool cursorVisivel;
    private bool motorAtivo;
    private bool corpoCinematico;
    private bool animadorAtivo;
    private bool hudAtivo;
    private bool cameraControlada;
    private bool emCena;

    internal IEnumerator Apresentar(PlayerResources jogador, BossNetanyahu chefe)
    {
        Bounds piso = chefe.LimitesDoPiso;
        Vector3 centro = new Vector3(piso.center.x, piso.max.y, piso.center.z);
        motor = jogador.GetComponent<PlayerMotor>();
        corpo = jogador.GetComponent<Rigidbody>();
        foreach (Animator candidato in jogador.GetComponentsInChildren<Animator>())
        {
            Transform arma = candidato.transform.Find("PivotGarfo/Garfo");
            if (arma == null)
                continue;

            animador = candidato;
            garfo = arma;
            break;
        }
        cameraPrincipal = Camera.main;
        controleDaCamera = cameraPrincipal != null ? cameraPrincipal.GetComponent<ThirdPersonCamera>() : null;
        var combate = FindFirstObjectByType<HudCombate>();
        hud = combate != null ? combate.gameObject : null;

        emCena = true;
        escalaDoTempo = Time.timeScale;
        modoDoCursor = Cursor.lockState;
        cursorVisivel = Cursor.visible;
        Time.timeScale = 0f;
        motorAtivo = motor.enabled;
        motor.enabled = false;
        corpoCinematico = corpo.isKinematic;
        if (!corpoCinematico)
        {
            corpo.linearVelocity = Vector3.zero;
            corpo.angularVelocity = Vector3.zero;
            corpo.isKinematic = true;
        }
        if (animador != null)
        {
            animadorAtivo = animador.enabled;
            animador.enabled = false;
        }
        if (garfo != null)
        {
            posicaoOriginalDoGarfo = garfo.localPosition;
            rotacaoOriginalDoGarfo = garfo.localRotation;
        }
        if (hud != null)
        {
            hudAtivo = hud.activeSelf;
            hud.SetActive(false);
        }
        if (cameraPrincipal != null)
        {
            campoDeVisao = cameraPrincipal.fieldOfView;
            posicaoDaCamera = cameraPrincipal.transform.position;
            rotacaoDaCamera = cameraPrincipal.transform.rotation;
        }
        if (controleDaCamera != null)
        {
            cameraControlada = controleDaCamera.enabled;
            controleDaCamera.enabled = false;
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = false;

        Vector3 origem = centro + new Vector3(-1.25f, 0f, 0f);
        Vector3 posicaoDoLing = centro + new Vector3(1.35f, 0.04f, 0.6f);
        Quaternion rotacaoDoLing = Quaternion.LookRotation(Vector3.ProjectOnPlane(origem - posicaoDoLing, Vector3.up));
        jogador.transform.SetPositionAndRotation(posicaoDoLing, rotacaoDoLing);
        corpo.position = posicaoDoLing;
        corpo.rotation = rotacaoDoLing;

        yahuCaido = CriarCorpoDoYahu(chefe);

        var iluminacao = new GameObject("Luz da cena final");
        iluminacao.transform.SetParent(transform, false);
        iluminacao.transform.position = centro + new Vector3(1.5f, 5f, -3f);
        luzDaCena = iluminacao.AddComponent<Light>();
        luzDaCena.type = LightType.Point;
        luzDaCena.color = new Color(1f, 0.80f, 0.60f);
        luzDaCena.intensity = 14f;
        luzDaCena.range = 15f;

        Quaternion frenteDoYahu = yahuCaido != null ? yahuCaido.transform.rotation : Quaternion.identity;
        yield return Animar(1.4f, t =>
        {
            if (yahuCaido != null)
            {
                Transform corpoDoYahu = yahuCaido.transform;
                Quaternion queda = Quaternion.Euler(0f, 90f, 0f) * Quaternion.Euler(-90f * t, 0f, 0f);
                corpoDoYahu.SetPositionAndRotation(origem, queda * frenteDoYahu);
                Bounds limites = yahuCaido.bounds;
                Vector3 baseDoCorpo = new Vector3(limites.center.x, limites.min.y, limites.center.z);
                corpoDoYahu.position += origem - baseDoCorpo + Vector3.up * 0.025f;
            }
            if (garfo != null)
                garfo.localRotation = Quaternion.Slerp(rotacaoOriginalDoGarfo, Quaternion.Euler(-84f, 0f, -12f), t);
            Enquadrar(centro, t);
        });
        if (garfo != null)
            pontaDoGarfo = ObterPontaDoGarfo();
        yield return new WaitForSecondsRealtime(0.55f);
        Cursor.visible = true;
    }

    private MeshRenderer CriarCorpoDoYahu(BossNetanyahu chefe)
    {
        SkinnedMeshRenderer modeloNormal = chefe.ModeloNormal;
        if (modeloNormal == null || modeloNormal.sharedMesh == null)
            return null;

        // Copia apenas a malha posada. O chefe original continua desativado.
        malhaDoYahu = new Mesh { name = "Malha do Yahu na cutscene" };
        modeloNormal.BakeMesh(malhaDoYahu);
        var visual = new GameObject("Yahu incapacitado");
        visual.transform.SetParent(transform, false);
        visual.transform.rotation = Quaternion.Inverse(chefe.transform.rotation) * modeloNormal.transform.rotation;
        Vector3 escala = modeloNormal.transform.lossyScale;
        Vector3 eixoVertical = Quaternion.Inverse(visual.transform.rotation) * Vector3.up;
        eixoVertical = new Vector3(Mathf.Abs(eixoVertical.x), Mathf.Abs(eixoVertical.y), Mathf.Abs(eixoVertical.z));
        float altura = Vector3.Dot(eixoVertical, Vector3.Scale(malhaDoYahu.bounds.size, escala));
        visual.transform.localScale = escala * (2.1f / Mathf.Max(2.1f, altura));
        visual.AddComponent<MeshFilter>().sharedMesh = malhaDoYahu;
        var renderizador = visual.AddComponent<MeshRenderer>();
        renderizador.sharedMaterials = modeloNormal.sharedMaterials;
        return renderizador;
    }

    internal IEnumerator Resolver(bool executar)
    {
        Cursor.visible = false;
        if (garfo == null)
        {
            if (executar)
                OcultarCorpoDoYahu();
            yield break;
        }

        Vector3 inicio = garfo.position;
        Quaternion pose = garfo.rotation;
        if (executar)
        {
            Vector3 alto = inicio + Vector3.up * 0.76f;
            Vector3 alvo = yahuCaido != null
                ? yahuCaido.bounds.center + Vector3.up * yahuCaido.bounds.extents.y
                : inicio + corpo.transform.forward;
            Vector3 direcao = (alvo - alto).normalized;
            Quaternion rotacaoDoGolpe = Quaternion.FromToRotation(garfo.TransformVector(pontaDoGarfo), direcao) * pose;
            Vector3 impacto = alvo - rotacaoDoGolpe * Vector3.Scale(pontaDoGarfo, garfo.lossyScale);
            yield return Animar(0.56f, t => garfo.SetPositionAndRotation(Vector3.Lerp(inicio, alto, t),
                Quaternion.Slerp(pose, rotacaoDoGolpe, t)));
            yield return Animar(0.2f, t => garfo.position = Vector3.Lerp(alto, impacto, t));
            OcultarCorpoDoYahu();
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Animar(0.48f, t => garfo.SetPositionAndRotation(Vector3.Lerp(impacto, inicio, t),
                Quaternion.Slerp(rotacaoDoGolpe, pose, t)));
        }
        else
        {
            yield return Animar(0.8f, t => garfo.SetPositionAndRotation(inicio - Vector3.up * (0.34f * t),
                pose * Quaternion.Euler(0f, 0f, 24f * t)));
        }
    }

    private void OcultarCorpoDoYahu()
    {
        if (yahuCaido != null)
            yahuCaido.gameObject.SetActive(false);
    }

    private Vector3 ObterPontaDoGarfo()
    {
        foreach (MeshFilter filtro in garfo.GetComponentsInChildren<MeshFilter>())
        {
            if (filtro.transform == garfo || filtro.sharedMesh == null)
                continue;

            Bounds limites = filtro.sharedMesh.bounds;
            Vector3 eixo = Vector3.right * limites.extents.x;
            Vector3 ponta = filtro.transform.TransformPoint(limites.center + eixo);
            Vector3 cabo = filtro.transform.TransformPoint(limites.center - eixo);
            return garfo.InverseTransformPoint(ponta.y > cabo.y ? ponta : cabo);
        }

        return new Vector3(0f, 0f, 0.65f);
    }

    private void Enquadrar(Vector3 centro, float progresso)
    {
        if (cameraPrincipal == null)
            return;

        Vector3 posicao = centro + Vector3.Lerp(new Vector3(3.1f, 4.1f, -8.2f),
            new Vector3(2.4f, 3.5f, -6.7f), progresso);
        Vector3 foco = centro + new Vector3(-0.4f, 0.9f, 0.35f);
        cameraPrincipal.fieldOfView = 46f;
        cameraPrincipal.transform.SetPositionAndRotation(posicao, Quaternion.LookRotation(foco - posicao));
    }

    private static IEnumerator Animar(float duracao, System.Action<float> aplicar)
    {
        for (float tempo = 0f; tempo < duracao; tempo += Time.unscaledDeltaTime)
        {
            aplicar(Mathf.SmoothStep(0f, 1f, tempo / duracao));
            yield return null;
        }
        aplicar(1f);
    }

    internal void Encerrar()
    {
        if (!emCena)
            return;

        emCena = false;
        Time.timeScale = escalaDoTempo;
        if (garfo != null)
            garfo.SetLocalPositionAndRotation(posicaoOriginalDoGarfo, rotacaoOriginalDoGarfo);
        if (animador != null)
            animador.enabled = animadorAtivo;
        if (corpo != null)
            corpo.isKinematic = corpoCinematico;
        if (motor != null)
            motor.enabled = motorAtivo;
        if (cameraPrincipal != null)
        {
            cameraPrincipal.fieldOfView = campoDeVisao;
            cameraPrincipal.transform.SetPositionAndRotation(posicaoDaCamera, rotacaoDaCamera);
        }
        if (controleDaCamera != null)
            controleDaCamera.enabled = cameraControlada;
        if (hud != null)
            hud.SetActive(hudAtivo);
        if (luzDaCena != null)
            Destroy(luzDaCena.gameObject);
        Cursor.lockState = modoDoCursor;
        Cursor.visible = cursorVisivel;
    }

    private void OnDestroy()
    {
        Encerrar();
        if (malhaDoYahu != null)
            Destroy(malhaDoYahu);
    }
}
