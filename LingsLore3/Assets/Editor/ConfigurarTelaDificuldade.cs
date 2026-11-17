using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class ConfigurarTelaDificuldade
{
    private const string ScenePath = "Assets/Scenes/SelecaoDificuldade.unity";
    private const string SpritesDir = "Assets/Sprites/Dificuldade";

    static ConfigurarTelaDificuldade()
    {
        EditorApplication.delayCall += VerificarEConfigurarAutomatico;
    }

    private static void VerificarEConfigurarAutomatico()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Executar();
    }

    [MenuItem("LingsLore/Configurar Tela Dificuldade Completa")]
    public static void Executar()
    {
        AtualizarImporters();
        AtualizarCena();
    }

    private static void AtualizarImporters()
    {
        string[] sprites = {
            "Dificuldade_Facil",
            "Dificuldade_Medio",
            "Dificuldade_Dificil",
            "Dificuldade_Impossivel"
        };

        foreach (string s in sprites)
        {
            string path = $"{SpritesDir}/{s}.png";
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
        }
    }

    public static void AtualizarCena()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject listaObj = GameObject.Find("ListaDificuldades");
        if (listaObj == null)
        {
            Debug.LogError("[ConfigurarTelaDificuldade] 'ListaDificuldades' não encontrado na cena!");
            return;
        }

        string[] titulosEsperados = { "Fácil", "Médio", "Difícil", "Impossível" };
        string[] descricoesAprovadas = {
            "Inimigos menos agressivos e ampla margem para dominar os golpes com calma.",
            "A experiência equilibrada com ritmo constante de esquivas e combos.",
            "Inimigos implacáveis e dano elevado: cada erro custa caro.",
            "Um único descuido é fatal. Boa sorte, você vai precisar."
        };
        string[] nomesSprites = {
            "Dificuldade_Facil",
            "Dificuldade_Medio",
            "Dificuldade_Dificil",
            "Dificuldade_Impossivel"
        };

        for (int i = 0; i < listaObj.transform.childCount && i < 4; i++)
        {
            Transform botao = listaObj.transform.GetChild(i);

            if (botao.GetComponent<MenuButtonHoverScale>() == null)
            {
                botao.gameObject.AddComponent<MenuButtonHoverScale>();
            }

            // 1. TÍTULO NO TOPO
            Transform tituloTransform = botao.Find("Text (TMP)");
            TMP_Text tituloTMP = tituloTransform != null ? tituloTransform.GetComponent<TMP_Text>() : null;
            if (tituloTMP != null)
            {
                tituloTMP.text = titulosEsperados[i];
                RectTransform rectTitulo = tituloTMP.rectTransform;
                rectTitulo.anchorMin = new Vector2(0f, 1f);
                rectTitulo.anchorMax = new Vector2(1f, 1f);
                rectTitulo.pivot = new Vector2(0.5f, 1f);
                rectTitulo.anchoredPosition = new Vector2(0f, -18f);
                rectTitulo.sizeDelta = new Vector2(-36f, 44f);
                tituloTMP.fontSize = 33f;
                tituloTMP.alignment = TextAlignmentOptions.Center;
                tituloTMP.fontStyle = FontStyles.Bold;
            }

            // 2. ÍCONE NO CENTRO (Mais espaçoso e com proporção adequada para Ling + armas nas laterais)
            Transform iconeTransform = botao.Find("IconeDificuldade");
            GameObject iconeObj;
            if (iconeTransform == null)
            {
                iconeObj = new GameObject("IconeDificuldade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconeObj.transform.SetParent(botao, false);
            }
            else
            {
                iconeObj = iconeTransform.gameObject;
            }

            RectTransform rectIcone = iconeObj.GetComponent<RectTransform>();
            rectIcone.anchorMin = new Vector2(0.5f, 1f);
            rectIcone.anchorMax = new Vector2(0.5f, 1f);
            rectIcone.pivot = new Vector2(0.5f, 1f);
            rectIcone.anchoredPosition = new Vector2(0f, -66f);
            rectIcone.sizeDelta = new Vector2(210f, 165f);

            Image img = iconeObj.GetComponent<Image>();
            string spritePath = $"{SpritesDir}/{nomesSprites[i]}.png";
            Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sp == null)
            {
                var subAssets = AssetDatabase.LoadAllAssetsAtPath(spritePath);
                foreach (var a in subAssets)
                {
                    if (a is Sprite s) { sp = s; break; }
                }
            }

            img.sprite = sp;
            img.preserveAspect = true;
            img.raycastTarget = false;

            // 3. DESCRIÇÃO NA BASE (Maior, preenchendo o espaço inferior do card)
            Transform descTransform = botao.Find("TextoDescricao");
            GameObject descObj;
            if (descTransform == null)
            {
                descObj = new GameObject("TextoDescricao", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                descObj.transform.SetParent(botao, false);
            }
            else
            {
                descObj = descTransform.gameObject;
            }

            RectTransform rectDesc = descObj.GetComponent<RectTransform>();
            rectDesc.anchorMin = new Vector2(0f, 0f);
            rectDesc.anchorMax = new Vector2(1f, 0f);
            rectDesc.pivot = new Vector2(0.5f, 0f);
            rectDesc.anchoredPosition = new Vector2(0f, 22f);
            rectDesc.sizeDelta = new Vector2(-40f, 140f);

            TextMeshProUGUI descTMP = descObj.GetComponent<TextMeshProUGUI>();
            if (tituloTMP != null)
                descTMP.font = tituloTMP.font;

            descTMP.text = descricoesAprovadas[i];
            descTMP.fontSize = 20f;
            descTMP.fontSizeMin = 16f;
            descTMP.fontSizeMax = 22f;
            descTMP.enableAutoSizing = true;
            descTMP.color = new Color(0.92f, 0.91f, 0.94f, 1f);
            descTMP.alignment = TextAlignmentOptions.Center;
            descTMP.textWrappingMode = TextWrappingModes.Normal;
            descTMP.lineSpacing = -4f;
            descTMP.raycastTarget = false;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
