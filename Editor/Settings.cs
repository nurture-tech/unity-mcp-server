using UnityEditor;
using UnityEngine;

namespace Nurture.MCP.Editor
{
    [CreateAssetMenu(
        fileName = "Assets/Settings/UnityMCPSettings.asset",
        menuName = "MCP/Settings"
    )]
    public class Settings : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Additional assemblies to always include when compiling generated scripts.")]
        private string[] alwaysIncludedAssemblies;

        public string[] AlwaysIncludedAssemblies => alwaysIncludedAssemblies;

        public static Settings Instance
        {
            get
            {
                var settings = AssetDatabase.LoadAssetAtPath<Settings>(
                    "Assets/Settings/UnityMCPSettings.asset"
                );
                if (settings == null)
                {
                    settings = CreateInstance<Settings>();
                    AssetDatabase.CreateAsset(settings, "Assets/Settings/UnityMCPSettings.asset");
                }
                return settings;
            }
        }
    }
}
