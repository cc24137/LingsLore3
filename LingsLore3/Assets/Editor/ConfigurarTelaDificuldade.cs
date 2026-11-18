using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ConfigurarTelaDificuldade
{
    private const string MenuPath = "LingsLore/Configurar Tela Dificuldade Completa";
    private const string ScenePath = "Assets/Scenes/SelecaoDificuldade.unity";
    private const string SpritesDir = "Assets/Sprites/Dificuldade";
    private static readonly string[] NomesEmblemas = {
        "Dificuldade_Facil",
        "Dificuldade_Medio",
        "Dificuldade_Dificil",
        "Dificuldade_Impossivel"
    };

    [MenuItem(MenuPath, true)]
    private static bool PodeConfigurar()
    {
        return !EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    [MenuItem(MenuPath)]
    public static void Executar()
    {
        if (!PodeConfigurar()) return;

        AtualizarCena();
    }

    private static void AtualizarImporters()
    {
        foreach (string s in NomesEmblemas)
        {
            string path = $"{SpritesDir}/{s}.png";
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null &&
                (importer.textureType != TextureImporterType.Sprite ||
                 importer.spriteImportMode != SpriteImportMode.Single ||
                 importer.filterMode != FilterMode.Point ||
                 importer.textureCompression != TextureImporterCompression.Uncompressed ||
                 importer.mipmapEnabled || !importer.alphaIsTransparency))
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

    private static void AtualizarCena()
    {
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() || !PodeConfigurar()) return;

            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        Transform lista = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name != "ListaDificuldades") continue;
                lista = child;
                break;
            }

            if (lista != null) break;
        }

        if (lista == null)
        {
            Debug.LogError("[ConfigurarTelaDificuldade] 'ListaDificuldades' não encontrado na cena!");
            return;
        }

        AtualizarImporters();

        string[] titulosEsperados = { "Fácil", "Médio", "Difícil", "Impossível" };
        string[] descricoesAprovadas = {
            "Inimigos menos agressivos e ampla margem para dominar os golpes com calma.",
            "A experiência equilibrada com ritmo constante de esquivas e combos.",
            "Inimigos implacáveis e dano elevado: cada erro custa caro.",
            "Um único descuido é fatal. Boa sorte, você vai precisar."
        };
        for (int i = 0; i < lista.childCount && i < 4; i++)
        {
            Transform botao = lista.GetChild(i);

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

            // 2. EMBLEMA NO CENTRO
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
            // Âncoras: centro horizontal, ocupa a faixa central do card
            rectIcone.anchorMin = new Vector2(0f, 0f);
            rectIcone.anchorMax = new Vector2(1f, 1f);
            rectIcone.pivot = new Vector2(0.5f, 0.5f);
            // Margens: 20px topo (abaixo do título), 160px baixo (acima da descrição)
            rectIcone.offsetMin = new Vector2(20f, 160f);
            rectIcone.offsetMax = new Vector2(-20f, -72f);

            Image img = iconeObj.GetComponent<Image>();
            string spritePath = $"{SpritesDir}/{NomesEmblemas[i]}.png";
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
