using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UnityUIBridge.Runtime.Spec
{
    public static class UnityUiBridgeSpecParser
    {
        public static UnityUiBridgeSpec LoadFromFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Spec path is required.", nameof(path));
            }

            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Unity UI Bridge spec file was not found.", path);
            }

            var json = File.ReadAllText(path);
            return FromJson(json);
        }

        public static UnityUiBridgeSpec FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("Spec JSON is required.", nameof(json));
            }

            var spec = JsonUtility.FromJson<UnityUiBridgeSpec>(json);
            if (spec == null)
            {
                throw new InvalidDataException("Spec JSON could not be parsed.");
            }

            spec.Normalize();
            return spec;
        }
    }

    [Serializable]
    public sealed class UnityUiBridgeSpec
    {
        public string schemaVersion;
        public UnityUiBridgeDocument document;
        public UnityUiBridgeAsset[] assets;
        public UnityUiBridgeStyle[] styles;
        public UnityUiBridgeNode[] nodes;
        public UnityUiBridgeInteraction[] interactions;

        private Dictionary<string, UnityUiBridgeAsset> _assetsById;
        private Dictionary<string, UnityUiBridgeStyle> _stylesById;
        private Dictionary<string, UnityUiBridgeInteraction> _interactionsById;
        private Dictionary<string, UnityUiBridgeNode> _nodesById;

        public void Normalize()
        {
            assets ??= Array.Empty<UnityUiBridgeAsset>();
            styles ??= Array.Empty<UnityUiBridgeStyle>();
            nodes ??= Array.Empty<UnityUiBridgeNode>();
            interactions ??= Array.Empty<UnityUiBridgeInteraction>();

            _assetsById = BuildMap(assets);
            _stylesById = BuildMap(styles);
            _interactionsById = BuildMap(interactions);
            _nodesById = new Dictionary<string, UnityUiBridgeNode>();

            foreach (var node in nodes)
            {
                RegisterNode(node);
            }
        }

        public UnityUiBridgeNode FindNode(string id)
        {
            EnsureIndexes();
            return id != null && _nodesById.TryGetValue(id, out var node) ? node : null;
        }

        public UnityUiBridgeStyle FindStyle(string id)
        {
            EnsureIndexes();
            return id != null && _stylesById.TryGetValue(id, out var style) ? style : null;
        }

        public UnityUiBridgeAsset FindAsset(string id)
        {
            EnsureIndexes();
            return id != null && _assetsById.TryGetValue(id, out var asset) ? asset : null;
        }

        public UnityUiBridgeInteraction FindInteraction(string id)
        {
            EnsureIndexes();
            return id != null && _interactionsById.TryGetValue(id, out var interaction) ? interaction : null;
        }

        private void EnsureIndexes()
        {
            if (_nodesById == null)
            {
                Normalize();
            }
        }

        private void RegisterNode(UnityUiBridgeNode node)
        {
            if (node == null)
            {
                return;
            }

            node.children ??= Array.Empty<UnityUiBridgeNode>();

            if (!string.IsNullOrWhiteSpace(node.id))
            {
                _nodesById[node.id] = node;
            }

            foreach (var child in node.children)
            {
                RegisterNode(child);
            }
        }

        private static Dictionary<string, T> BuildMap<T>(IEnumerable<T> items) where T : IUnityUiBridgeIdentified
        {
            var map = new Dictionary<string, T>();
            foreach (var item in items)
            {
                if (item != null && !string.IsNullOrWhiteSpace(item.Id))
                {
                    map[item.Id] = item;
                }
            }

            return map;
        }
    }

    public interface IUnityUiBridgeIdentified
    {
        string Id { get; }
    }

    [Serializable]
    public sealed class UnityUiBridgeDocument
    {
        public string id;
        public string title;
        public UnityUiBridgeSource source;
        public UnityUiBridgeSize referenceResolution;
        public UnityUiBridgeTarget target;
    }

    [Serializable]
    public sealed class UnityUiBridgeSource
    {
        public string type;
        public string uri;
    }

    [Serializable]
    public sealed class UnityUiBridgeTarget
    {
        public string engine;
        public string uiSystem;
        public string canvasMode;
    }

    [Serializable]
    public sealed class UnityUiBridgeAsset : IUnityUiBridgeIdentified
    {
        public string id;
        public string type;
        public string uri;
        public UnityUiBridgeRect rect;
        public UnityUiBridgeNineSlice nineSlice;
        public string sourceNodeId;

        public string Id => id;
    }

    [Serializable]
    public sealed class UnityUiBridgeStyle : IUnityUiBridgeIdentified
    {
        public string id;
        public string name;
        public UnityUiBridgeTypography typography;
        public UnityUiBridgeBorder border;
        public UnityUiBridgeShadow shadow;
        public UnityUiBridgeGlow glow;

        public string Id => id;
    }

    [Serializable]
    public sealed class UnityUiBridgeInteraction : IUnityUiBridgeIdentified
    {
        public string id;
        public string nodeId;
        public string type;
        public string action;
        public string label;
        public string[] states;

        public string Id => id;
    }

    [Serializable]
    public sealed class UnityUiBridgeNode
    {
        public string id;
        public string role;
        public string name;
        public UnityUiBridgeRect rect;
        public UnityUiBridgeAnchors anchors;
        public UnityUiBridgeVector2 pivot;
        public UnityUiBridgeLayoutHints layout;
        public string styleRef;
        public string assetRef;
        public UnityUiBridgeText text;
        public string interactionRef;
        public float confidence = 1f;
        [SerializeReference]
        public UnityUiBridgeNode[] children;

        public string DisplayName => string.IsNullOrWhiteSpace(name) ? id : name;
    }

    [Serializable]
    public sealed class UnityUiBridgeText
    {
        public string content;
        public string language;
        public bool isPlaceholder;
        public float ocrConfidence = 1f;
    }

    [Serializable]
    public sealed class UnityUiBridgeRect
    {
        public float x;
        public float y;
        public float width;
        public float height;
    }

    [Serializable]
    public sealed class UnityUiBridgeSize
    {
        public int width;
        public int height;
    }

    [Serializable]
    public sealed class UnityUiBridgeAnchors
    {
        public UnityUiBridgeVector2 min;
        public UnityUiBridgeVector2 max;
    }

    [Serializable]
    public sealed class UnityUiBridgeVector2
    {
        public float x;
        public float y;
    }

    [Serializable]
    public sealed class UnityUiBridgeLayoutHints
    {
        public string mode;
        public float spacing;
        public UnityUiBridgeEdgeInsets padding;
        public int responsivePriority;
    }

    [Serializable]
    public sealed class UnityUiBridgeEdgeInsets
    {
        public float left;
        public float right;
        public float top;
        public float bottom;
    }

    [Serializable]
    public sealed class UnityUiBridgeTypography
    {
        public string fontFamily;
        public float fontSize;
        public string fontStyle;
        public string alignment;
    }

    [Serializable]
    public sealed class UnityUiBridgeBorder
    {
        public string color;
        public float width;
        public float radius;
    }

    [Serializable]
    public sealed class UnityUiBridgeShadow
    {
        public string color;
        public UnityUiBridgeVector2 offset;
        public float blur;
    }

    [Serializable]
    public sealed class UnityUiBridgeGlow
    {
        public string color;
        public float intensity;
        public float radius;
    }

    [Serializable]
    public sealed class UnityUiBridgeNineSlice
    {
        public float left;
        public float right;
        public float top;
        public float bottom;
    }
}
