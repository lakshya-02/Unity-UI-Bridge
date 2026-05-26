using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

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

            var spec = UnityUiBridgeSpecJsonReader.Parse(json);
            spec.Normalize();
            return spec;
        }
    }

    internal static class UnityUiBridgeSpecJsonReader
    {
        public static UnityUiBridgeSpec Parse(string json)
        {
            if (UnityUiBridgeJson.Parse(json) is not Dictionary<string, object> root)
            {
                throw new InvalidDataException("Spec JSON root must be an object.");
            }

            return new UnityUiBridgeSpec
            {
                schemaVersion = StringValue(root, "schemaVersion"),
                document = ReadDocument(ObjectValue(root, "document")),
                assets = ReadArray(root, "assets", ReadAsset),
                styles = ReadArray(root, "styles", ReadStyle),
                nodes = ReadArray(root, "nodes", ReadNode),
                interactions = ReadArray(root, "interactions", ReadInteraction)
            };
        }

        private static UnityUiBridgeDocument ReadDocument(Dictionary<string, object> map)
        {
            if (map == null)
            {
                return null;
            }

            return new UnityUiBridgeDocument
            {
                id = StringValue(map, "id"),
                title = StringValue(map, "title"),
                source = ReadSource(ObjectValue(map, "source")),
                referenceResolution = ReadSize(ObjectValue(map, "referenceResolution")),
                target = ReadTarget(ObjectValue(map, "target"))
            };
        }

        private static UnityUiBridgeSource ReadSource(Dictionary<string, object> map)
        {
            return map == null
                ? null
                : new UnityUiBridgeSource
                {
                    type = StringValue(map, "type"),
                    uri = StringValue(map, "uri")
                };
        }

        private static UnityUiBridgeTarget ReadTarget(Dictionary<string, object> map)
        {
            return map == null
                ? null
                : new UnityUiBridgeTarget
                {
                    engine = StringValue(map, "engine"),
                    uiSystem = StringValue(map, "uiSystem"),
                    canvasMode = StringValue(map, "canvasMode")
                };
        }

        private static UnityUiBridgeAsset ReadAsset(Dictionary<string, object> map)
        {
            return new UnityUiBridgeAsset
            {
                id = StringValue(map, "id"),
                type = StringValue(map, "type"),
                uri = StringValue(map, "uri"),
                rect = ReadRect(ObjectValue(map, "rect")),
                nineSlice = ReadNineSlice(ObjectValue(map, "nineSlice")),
                sourceNodeId = StringValue(map, "sourceNodeId")
            };
        }

        private static UnityUiBridgeStyle ReadStyle(Dictionary<string, object> map)
        {
            return new UnityUiBridgeStyle
            {
                id = StringValue(map, "id"),
                name = StringValue(map, "name"),
                typography = ReadTypography(ObjectValue(map, "typography")),
                border = ReadBorder(ObjectValue(map, "border")),
                shadow = ReadShadow(ObjectValue(map, "shadow")),
                glow = ReadGlow(ObjectValue(map, "glow"))
            };
        }

        private static UnityUiBridgeInteraction ReadInteraction(Dictionary<string, object> map)
        {
            return new UnityUiBridgeInteraction
            {
                id = StringValue(map, "id"),
                nodeId = StringValue(map, "nodeId"),
                type = StringValue(map, "type"),
                action = StringValue(map, "action"),
                label = StringValue(map, "label"),
                states = StringArrayValue(map, "states")
            };
        }

        private static UnityUiBridgeNode ReadNode(Dictionary<string, object> map)
        {
            return new UnityUiBridgeNode
            {
                id = StringValue(map, "id"),
                role = StringValue(map, "role"),
                name = StringValue(map, "name"),
                rect = ReadRect(ObjectValue(map, "rect")),
                anchors = ReadAnchors(ObjectValue(map, "anchors")),
                pivot = ReadVector2(ObjectValue(map, "pivot")),
                layout = ReadLayout(ObjectValue(map, "layout")),
                styleRef = StringValue(map, "styleRef"),
                assetRef = StringValue(map, "assetRef"),
                text = ReadText(ObjectValue(map, "text")),
                interactionRef = StringValue(map, "interactionRef"),
                confidence = FloatValue(map, "confidence", 1f),
                children = ReadArray(map, "children", ReadNode)
            };
        }

        private static UnityUiBridgeText ReadText(Dictionary<string, object> map)
        {
            return map == null
                ? null
                : new UnityUiBridgeText
                {
                    content = StringValue(map, "content"),
                    language = StringValue(map, "language"),
                    isPlaceholder = BoolValue(map, "isPlaceholder"),
                    ocrConfidence = FloatValue(map, "ocrConfidence", 1f)
                };
        }

        private static UnityUiBridgeRect ReadRect(Dictionary<string, object> map)
        {
            return map == null
                ? null
                : new UnityUiBridgeRect
                {
                    x = FloatValue(map, "x"),
                    y = FloatValue(map, "y"),
                    width = FloatValue(map, "width"),
                    height = FloatValue(map, "height")
                };
        }

        private static UnityUiBridgeSize ReadSize(Dictionary<string, object> map)
        {
            return map == null
                ? null
                : new UnityUiBridgeSize
                {
                    width = IntValue(map, "width"),
                    height = IntValue(map, "height")
                };
        }

        private static UnityUiBridgeAnchors ReadAnchors(Dictionary<string, object> map)
        {
            return map == null
                ? null
                : new UnityUiBridgeAnchors
                {
                    min = ReadVector2(ObjectValue(map, "min")),
                    max = ReadVector2(ObjectValue(map, "max"))
                };
        }

        private static UnityUiBridgeVector2 ReadVector2(Dictionary<string, object> map)
        {
            return map == null
                ? null
                : new UnityUiBridgeVector2
                {
                    x = FloatValue(map, "x"),
                    y = FloatValue(map, "y")
                };
        }

        private static UnityUiBridgeLayoutHints ReadLayout(Dictionary<string, object> map)
        {
            return map == null
                ? null
                : new UnityUiBridgeLayoutHints
                {
                    mode = StringValue(map, "mode"),
                    spacing = FloatValue(map, "spacing"),
                    padding = ReadEdgeInsets(ObjectValue(map, "padding")),
                    responsivePriority = IntValue(map, "responsivePriority")
                };
        }

        private static UnityUiBridgeEdgeInsets ReadEdgeInsets(Dictionary<string, object> map)
        {
            return map == null
                ? null
                : new UnityUiBridgeEdgeInsets
                {
                    left = FloatValue(map, "left"),
                    right = FloatValue(map, "right"),
                    top = FloatValue(map, "top"),
                    bottom = FloatValue(map, "bottom")
                };
        }

        private static UnityUiBridgeTypography ReadTypography(Dictionary<string, object> map)
        {
            return map == null
                ? null
                : new UnityUiBridgeTypography
                {
                    fontFamily = StringValue(map, "fontFamily"),
                    fontSize = FloatValue(map, "fontSize"),
                    fontStyle = StringValue(map, "fontStyle"),
                    alignment = StringValue(map, "alignment")
                };
        }

        private static UnityUiBridgeBorder ReadBorder(Dictionary<string, object> map)
        {
            return map == null
                ? null
                : new UnityUiBridgeBorder
                {
                    color = StringValue(map, "color"),
                    width = FloatValue(map, "width"),
                    radius = FloatValue(map, "radius")
                };
        }

        private static UnityUiBridgeShadow ReadShadow(Dictionary<string, object> map)
        {
            return map == null
                ? null
                : new UnityUiBridgeShadow
                {
                    color = StringValue(map, "color"),
                    offset = ReadVector2(ObjectValue(map, "offset")),
                    blur = FloatValue(map, "blur")
                };
        }

        private static UnityUiBridgeGlow ReadGlow(Dictionary<string, object> map)
        {
            return map == null
                ? null
                : new UnityUiBridgeGlow
                {
                    color = StringValue(map, "color"),
                    intensity = FloatValue(map, "intensity"),
                    radius = FloatValue(map, "radius")
                };
        }

        private static UnityUiBridgeNineSlice ReadNineSlice(Dictionary<string, object> map)
        {
            return map == null
                ? null
                : new UnityUiBridgeNineSlice
                {
                    left = FloatValue(map, "left"),
                    right = FloatValue(map, "right"),
                    top = FloatValue(map, "top"),
                    bottom = FloatValue(map, "bottom")
                };
        }

        private static T[] ReadArray<T>(
            Dictionary<string, object> map,
            string key,
            Func<Dictionary<string, object>, T> reader)
        {
            if (map == null || !map.TryGetValue(key, out var value) || value is not List<object> list)
            {
                return Array.Empty<T>();
            }

            var result = new List<T>(list.Count);
            foreach (var item in list)
            {
                if (item is Dictionary<string, object> itemMap)
                {
                    result.Add(reader(itemMap));
                }
            }

            return result.ToArray();
        }

        private static string[] StringArrayValue(Dictionary<string, object> map, string key)
        {
            if (map == null || !map.TryGetValue(key, out var value) || value is not List<object> list)
            {
                return Array.Empty<string>();
            }

            var result = new List<string>(list.Count);
            foreach (var item in list)
            {
                if (item != null)
                {
                    result.Add(Convert.ToString(item, CultureInfo.InvariantCulture));
                }
            }

            return result.ToArray();
        }

        private static Dictionary<string, object> ObjectValue(Dictionary<string, object> map, string key)
        {
            return map != null && map.TryGetValue(key, out var value) ? value as Dictionary<string, object> : null;
        }

        private static string StringValue(Dictionary<string, object> map, string key)
        {
            return map != null && map.TryGetValue(key, out var value) && value != null
                ? Convert.ToString(value, CultureInfo.InvariantCulture)
                : null;
        }

        private static float FloatValue(Dictionary<string, object> map, string key, float fallback = 0f)
        {
            return map != null && map.TryGetValue(key, out var value) ? ToFloat(value, fallback) : fallback;
        }

        private static int IntValue(Dictionary<string, object> map, string key, int fallback = 0)
        {
            return map != null && map.TryGetValue(key, out var value) ? ToInt(value, fallback) : fallback;
        }

        private static bool BoolValue(Dictionary<string, object> map, string key, bool fallback = false)
        {
            return map != null && map.TryGetValue(key, out var value) ? ToBool(value, fallback) : fallback;
        }

        private static float ToFloat(object value, float fallback)
        {
            return value switch
            {
                double doubleValue => (float)doubleValue,
                float floatValue => floatValue,
                int intValue => intValue,
                long longValue => longValue,
                string stringValue when float.TryParse(
                    stringValue,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var parsed) => parsed,
                _ => fallback
            };
        }

        private static int ToInt(object value, int fallback)
        {
            return value switch
            {
                double doubleValue => (int)doubleValue,
                float floatValue => (int)floatValue,
                int intValue => intValue,
                long longValue => (int)longValue,
                string stringValue when int.TryParse(
                    stringValue,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var parsed) => parsed,
                _ => fallback
            };
        }

        private static bool ToBool(object value, bool fallback)
        {
            return value switch
            {
                bool boolValue => boolValue,
                string stringValue when bool.TryParse(stringValue, out var parsed) => parsed,
                _ => fallback
            };
        }
    }

    internal sealed class UnityUiBridgeJson
    {
        private readonly string _json;
        private int _index;

        private UnityUiBridgeJson(string json)
        {
            _json = json;
        }

        public static object Parse(string json)
        {
            var parser = new UnityUiBridgeJson(json);
            var value = parser.ParseValue();
            parser.SkipWhitespace();
            if (!parser.IsAtEnd)
            {
                throw parser.Error("Unexpected trailing JSON content.");
            }

            return value;
        }

        private bool IsAtEnd => _index >= _json.Length;

        private object ParseValue()
        {
            SkipWhitespace();
            if (IsAtEnd)
            {
                throw Error("Unexpected end of JSON.");
            }

            return Current switch
            {
                '{' => ParseObject(),
                '[' => ParseArray(),
                '"' => ParseString(),
                't' => ParseLiteral("true", true),
                'f' => ParseLiteral("false", false),
                'n' => ParseLiteral("null", null),
                '-' => ParseNumber(),
                >= '0' and <= '9' => ParseNumber(),
                _ => throw Error($"Unexpected JSON token '{Current}'.")
            };
        }

        private Dictionary<string, object> ParseObject()
        {
            Expect('{');
            var result = new Dictionary<string, object>();
            SkipWhitespace();
            if (TryConsume('}'))
            {
                return result;
            }

            while (true)
            {
                SkipWhitespace();
                var key = ParseString();
                SkipWhitespace();
                Expect(':');
                result[key] = ParseValue();
                SkipWhitespace();
                if (TryConsume('}'))
                {
                    return result;
                }

                Expect(',');
            }
        }

        private List<object> ParseArray()
        {
            Expect('[');
            var result = new List<object>();
            SkipWhitespace();
            if (TryConsume(']'))
            {
                return result;
            }

            while (true)
            {
                result.Add(ParseValue());
                SkipWhitespace();
                if (TryConsume(']'))
                {
                    return result;
                }

                Expect(',');
            }
        }

        private string ParseString()
        {
            Expect('"');
            var builder = new StringBuilder();
            while (!IsAtEnd)
            {
                var character = _json[_index++];
                if (character == '"')
                {
                    return builder.ToString();
                }

                if (character != '\\')
                {
                    builder.Append(character);
                    continue;
                }

                if (IsAtEnd)
                {
                    throw Error("Unterminated JSON string escape.");
                }

                var escaped = _json[_index++];
                builder.Append(escaped switch
                {
                    '"' => '"',
                    '\\' => '\\',
                    '/' => '/',
                    'b' => '\b',
                    'f' => '\f',
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    'u' => ParseUnicodeEscape(),
                    _ => throw Error($"Invalid JSON string escape '\\{escaped}'.")
                });
            }

            throw Error("Unterminated JSON string.");
        }

        private char ParseUnicodeEscape()
        {
            if (_index + 4 > _json.Length)
            {
                throw Error("Invalid JSON unicode escape.");
            }

            var hex = _json.Substring(_index, 4);
            _index += 4;
            return (char)int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        private object ParseNumber()
        {
            var start = _index;
            if (Current == '-')
            {
                _index++;
            }

            ConsumeDigits();
            if (!IsAtEnd && Current == '.')
            {
                _index++;
                ConsumeDigits();
            }

            if (!IsAtEnd && (Current == 'e' || Current == 'E'))
            {
                _index++;
                if (!IsAtEnd && (Current == '+' || Current == '-'))
                {
                    _index++;
                }

                ConsumeDigits();
            }

            var text = _json.Substring(start, _index - start);
            return double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private object ParseLiteral(string literal, object value)
        {
            if (_index + literal.Length > _json.Length
                || string.CompareOrdinal(_json, _index, literal, 0, literal.Length) != 0)
            {
                throw Error($"Expected JSON literal '{literal}'.");
            }

            _index += literal.Length;
            return value;
        }

        private void ConsumeDigits()
        {
            var start = _index;
            while (!IsAtEnd && Current >= '0' && Current <= '9')
            {
                _index++;
            }

            if (start == _index)
            {
                throw Error("Expected JSON number digits.");
            }
        }

        private void SkipWhitespace()
        {
            while (!IsAtEnd && char.IsWhiteSpace(Current))
            {
                _index++;
            }
        }

        private bool TryConsume(char expected)
        {
            if (IsAtEnd || Current != expected)
            {
                return false;
            }

            _index++;
            return true;
        }

        private void Expect(char expected)
        {
            if (!TryConsume(expected))
            {
                throw Error($"Expected JSON token '{expected}'.");
            }
        }

        private char Current => _json[_index];

        private InvalidDataException Error(string message)
        {
            return new InvalidDataException($"{message} Position {_index}.");
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
