using System;
using UnityEngine;

[CreateAssetMenu(menuName = "LingsLore/Catálogo de skins do Ling")]
public sealed class CatalogoSkinsLing : ScriptableObject
{
    [Serializable]
    public sealed class Skin
    {
        [SerializeField] private string id;
        [SerializeField] private string nome;
        [SerializeField] private string descricao;
        [SerializeField] private GameObject adereco;

        public string Id => id;
        public string Nome => nome;
        public string Descricao => descricao;

        public GameObject Equipar(Transform encaixe)
        {
            return adereco == null ? null : UnityEngine.Object.Instantiate(adereco, encaixe, false);
        }
    }

    private const string ChavePreferencia = "Ling.Skin";
    private const string IdOriginal = "original";

    [SerializeField] private Skin[] skins;

    public Skin Obter(int indice) => skins[indice];

    public int LerIndiceSalvo()
    {
        string id = PlayerPrefs.GetString(ChavePreferencia, IdOriginal);
        int indice = Array.FindIndex(skins, skin => skin.Id == id);
        return indice >= 0 ? indice : Array.FindIndex(skins, skin => skin.Id == IdOriginal);
    }

    public void Salvar(Skin skin)
    {
        PlayerPrefs.SetString(ChavePreferencia, skin.Id);
        PlayerPrefs.Save();
    }
}
