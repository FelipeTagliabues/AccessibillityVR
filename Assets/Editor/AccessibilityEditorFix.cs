#if UNITY_EDITOR
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Accessibility.EditorTools
{
    public static class AccessibilityEditorFix
    {
        [MenuItem("Tools/Accessibility/Fix URP PostProcessData")]
        public static void FixPostProcessData()
        {
            var ppdGuids = AssetDatabase.FindAssets("t:PostProcessData");
            if (ppdGuids.Length == 0)
            {
                Debug.LogError("[A11y Fix] Nenhum PostProcessData encontrado no projeto. Verifique se o package URP está instalado.");
                return;
            }
            var ppdPath = AssetDatabase.GUIDToAssetPath(ppdGuids[0]);
            var ppd = AssetDatabase.LoadAssetAtPath<PostProcessData>(ppdPath);
            Debug.Log($"[A11y Fix] Usando PostProcessData: {ppdPath}");

            var rendererGuids = AssetDatabase.FindAssets("t:UniversalRendererData");
            int patched = 0;
            foreach (var g in rendererGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (renderer == null) continue;

                var field = typeof(UniversalRendererData).GetField("postProcessData",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                {
                    field.SetValue(renderer, ppd);
                    EditorUtility.SetDirty(renderer);
                    Debug.Log($"[A11y Fix] PostProcessData atribuído em {path}");
                    patched++;
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[A11y Fix] {patched} renderer(s) corrigido(s). Saia e reentre em Play Mode para ver o efeito.");
        }
    }
}
#endif
