using System.Linq;
using UnityEditor.Search;
using UnityEngine;

namespace Nurture.MCP.Editor
{
    public static class SearchUtilsExtensions
    {
        public static string GetTransformPath(Transform transform, bool inIsolatedPrefab)
        {
            var path = SearchUtils.GetTransformPath(transform);

            if (inIsolatedPrefab)
            {
                path = "/" + string.Join("/", path.Split('/').Skip(2));
            }

            return path;
        }
    }
}
