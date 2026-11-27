using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class PreviaSkinsLing : MonoBehaviour
{
    [SerializeField] private Transform modeloLing;
    [SerializeField] private UnityEngine.UI.RawImage painel;

    private const string NomeCamada = "PreviaSkin";
    private readonly List<RenderTexture> texturas = new();
    private Camera cameraPrevia;
    private Transform modelo;
    private GameObject adereco;
    private int camada;

    public void Preparar()
    {
        camada = LayerMask.NameToLayer(NomeCamada);
        var palco = new GameObject("PalcoPreviaLing").transform;
        palco.SetParent(transform, false);
        palco.position = new Vector3(0f, -64f, 0f);

        modelo = Instantiate(modeloLing, palco, false);
        MoverParaCamada(modelo);

        var objetoCamera = new GameObject("CameraPreviaLing");
        objetoCamera.transform.SetParent(palco, false);
        objetoCamera.transform.localPosition = new Vector3(0.48f, 0.41f, 2.8f);
        objetoCamera.transform.LookAt(palco.TransformPoint(new Vector3(0f, 0.16f, 0f)));

        var texturaPrevia = CriarTextura(512, "Prévia do Ling");
        cameraPrevia = objetoCamera.AddComponent<Camera>();
        cameraPrevia.depth = -10f;
        cameraPrevia.clearFlags = CameraClearFlags.SolidColor;
        cameraPrevia.backgroundColor = new Color32(34, 31, 41, 255);
        cameraPrevia.orthographic = true;
        cameraPrevia.orthographicSize = 0.83f;
        cameraPrevia.nearClipPlane = 0.1f;
        cameraPrevia.farClipPlane = 8f;
        cameraPrevia.cullingMask = 1 << camada;
        cameraPrevia.allowHDR = false;
        cameraPrevia.targetTexture = texturaPrevia;
        cameraPrevia.GetUniversalAdditionalCameraData().renderShadows = false;

        var preenchimento = new GameObject("LuzPreviaLing").AddComponent<Light>();
        preenchimento.transform.SetParent(palco, false);
        preenchimento.transform.localPosition = new Vector3(-1.2f, 1.6f, 2.4f);
        preenchimento.type = LightType.Point;
        preenchimento.range = 8f;
        preenchimento.intensity = 2f;
        preenchimento.cullingMask = 1 << camada;

        painel.texture = texturaPrevia;
        painel.enabled = true;
    }

    public void Mostrar(CatalogoSkinsLing.Skin skin)
    {
        if (adereco != null)
        {
            adereco.SetActive(false);
            Destroy(adereco);
        }

        adereco = skin.Equipar(modelo);
        if (adereco != null)
            MoverParaCamada(adereco.transform);
    }

    public RenderTexture RenderizarMiniatura()
    {
        var textura = CriarTextura(192, "Miniatura do Ling");
        var pedido = new UniversalRenderPipeline.SingleCameraRequest { destination = textura };
        RenderPipeline.SubmitRenderRequest(cameraPrevia, pedido);
        return textura;
    }

    private void MoverParaCamada(Transform raiz)
    {
        foreach (Transform parte in raiz.GetComponentsInChildren<Transform>(true))
            parte.gameObject.layer = camada;
    }

    private RenderTexture CriarTextura(int tamanho, string nome)
    {
        var textura = new RenderTexture(tamanho, tamanho, 24) { name = nome };
        texturas.Add(textura);
        return textura;
    }

    private void OnDestroy()
    {
        if (cameraPrevia != null)
            cameraPrevia.targetTexture = null;

        foreach (RenderTexture textura in texturas)
            Destroy(textura);
    }
}
