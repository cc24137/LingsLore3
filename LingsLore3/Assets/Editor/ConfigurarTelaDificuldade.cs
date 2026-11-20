using UnityEditor;
using UnityEngine;

public static class ConfigurarTelaDificuldade
{
    private const string MenuPath = "LingsLore/Atualizar Sprites de Dificuldade";
    private const string SpritesDir = "Assets/Sprites/Dificuldade";
    private static readonly string[] NomesSprites = {
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

        AtualizarImporters();
    }

    private static void AtualizarImporters()
    {
        foreach (string s in NomesSprites)
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
}
