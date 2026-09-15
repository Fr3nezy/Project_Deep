using UnityEngine;
using UnityEditor;

namespace Deeploration.EntitySystem.Editor
{
    /// <summary>
    /// Editor utility per creare facilmente CreatureProfile assets.
    /// </summary>
    public static class CreatureProfileCreator
    {
        [MenuItem("Assets/Create/Deeploration/Creature Profile", false, 1)]
        public static void CreateCreatureProfile()
        {
            // Crea una nuova istanza di CreatureProfile
            CreatureProfile profile = ScriptableObject.CreateInstance<CreatureProfile>();

            // Determina il path dove salvare
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(path))
            {
                path = "Assets";
            }
            else if (System.IO.Path.GetExtension(path) != "")
            {
                path = path.Replace(System.IO.Path.GetFileName(path), "");
            }

            // Crea l'asset con nome univoco
            string assetPathAndName = AssetDatabase.GenerateUniqueAssetPath(path + "/New Creature Profile.asset");
            AssetDatabase.CreateAsset(profile, assetPathAndName);

            // Salva e seleziona il nuovo asset
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = profile;
        }
    }
}
