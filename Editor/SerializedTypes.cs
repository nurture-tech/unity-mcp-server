#if USE_MCP

using System.ComponentModel;
using System.Linq;
using ModelContextProtocol;
using UnityEditor;
using UnityEngine;
using Unity.Behavior.GraphFramework;

namespace Nurture.MCP.Editor
{
    public record MCPSerializableGUID
    {
        public string m_Value1 { get; set; }
        public string m_Value2 { get; set; }

        public static explicit operator MCPSerializableGUID(SerializableGUID guid) =>
            new()
            {
                m_Value1 = guid.ToParts().Item1.ToString(),
                m_Value2 = guid.ToParts().Item2.ToString(),
            };

        public static explicit operator SerializableGUID(MCPSerializableGUID guid) =>
            new(ulong.Parse(guid.m_Value1), ulong.Parse(guid.m_Value2));
    }

    public record MCPVector3
    {
        public float x { get; set; }
        public float y { get; set; }
        public float z { get; set; }

        public static implicit operator Vector3(MCPVector3 vector) =>
            new()
            {
                x = vector.x,
                y = vector.y,
                z = vector.z,
            };

        public static implicit operator MCPVector3(Vector3 vector) =>
            new()
            {
                x = vector.x,
                y = vector.y,
                z = vector.z,
            };
    }

    public record MCPVector2
    {
        public float x { get; set; }
        public float y { get; set; }

        public static implicit operator Vector2(MCPVector2 vector) =>
            new() { x = vector.x, y = vector.y };

        public static implicit operator MCPVector2(Vector2 vector) =>
            new() { x = vector.x, y = vector.y };
    }

    public record MCPQuaternion
    {
        public float x { get; set; }
        public float y { get; set; }
        public float z { get; set; }
        public float w { get; set; }

        public static implicit operator Quaternion(MCPQuaternion quaternion) =>
            new()
            {
                x = quaternion.x,
                y = quaternion.y,
                z = quaternion.z,
                w = quaternion.w,
            };

        public static implicit operator MCPQuaternion(Quaternion quaternion) =>
            new()
            {
                x = quaternion.x,
                y = quaternion.y,
                z = quaternion.z,
                w = quaternion.w,
            };
    }

    public record MCPBounds
    {
        public MCPVector3 Center { get; set; }
        public MCPVector3 Extents { get; set; }

        public static implicit operator Bounds(MCPBounds bounds) =>
            new() { center = bounds.Center, extents = bounds.Extents };

        public static implicit operator MCPBounds(Bounds bounds) =>
            new()
            {
                Center = new MCPVector3()
                {
                    x = bounds.center.x,
                    y = bounds.center.y,
                    z = bounds.center.z,
                },
                Extents = new MCPVector3()
                {
                    x = bounds.extents.x,
                    y = bounds.extents.y,
                    z = bounds.extents.z,
                },
            };
    }

    public record MCPColor
    {
        public float r { get; set; }
        public float g { get; set; }
        public float b { get; set; }
        public float a { get; set; }

        public static implicit operator Color(MCPColor color) =>
            new()
            {
                r = color.r,
                g = color.g,
                b = color.b,
                a = color.a,
            };

        public static implicit operator MCPColor(Color color) =>
            new()
            {
                r = color.r,
                g = color.g,
                b = color.b,
                a = color.a,
            };
    }

    public record MCPUnityObject<T>
        where T : UnityEngine.Object
    {
        public string Guid { get; set; }

        [Description(
            "The file ID of the asset. This is a string reprentatation of a long integer."
        )]
        public string FileID { get; set; }

        public static implicit operator T(MCPUnityObject<T> obj) => obj.LoadObject();

        private T LoadObject()
        {
            if (string.IsNullOrEmpty(Guid) || string.IsNullOrEmpty(FileID))
            {
                return null;
            }

            var path = AssetDatabase.GUIDToAssetPath(Guid);

            var assets = AssetDatabase.LoadAllAssetsAtPath(path);

            var longFileID = long.Parse(FileID);

            return assets
                    .OfType<T>()
                    .FirstOrDefault(x =>
                        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(x, out _, out var fileID)
                        && fileID == longFileID
                    ) ?? throw new McpException("Failed to load GameObject from GUID");
        }
    }

    public interface IPaginated
    {
        public int NextCursor { get; set; }
        public bool HasMore { get; }
    }
}


#endif
