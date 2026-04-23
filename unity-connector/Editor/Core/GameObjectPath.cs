using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace UnityCliConnector
{
    public sealed class GameObjectPath
    {
        static readonly Regex SegmentPattern = new(@"^(?<name>[^\[\]]+?)(?:\[(?<index>\d+)\])?$");

        public sealed class Segment
        {
            public string Name { get; }
            public int? Index { get; }
            public bool HasExplicitIndex => Index.HasValue;

            public Segment(string name, int? index)
            {
                Name = name;
                Index = index;
            }
        }

        public string SceneName { get; }
        public IReadOnlyList<Segment> Segments { get; }

        GameObjectPath(string sceneName, IReadOnlyList<Segment> segments)
        {
            SceneName = sceneName;
            Segments = segments;
        }

        public static GameObjectPath Parse(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                throw new ArgumentException("path is required", nameof(raw));

            var split = raw.Split(new[] { "::" }, StringSplitOptions.None);
            if (split.Length > 2)
                throw new ArgumentException("path may contain at most one scene qualifier", nameof(raw));

            var sceneName = split.Length == 2 ? split[0] : null;
            var hierarchy = split.Length == 2 ? split[1] : split[0];

            if (split.Length == 2 && string.IsNullOrWhiteSpace(sceneName))
                throw new ArgumentException("scene qualifier is required when using ::", nameof(raw));

            var segments = new List<Segment>();
            foreach (var part in hierarchy.Split('/'))
            {
                var match = SegmentPattern.Match(part);
                if (!match.Success)
                    throw new ArgumentException($"invalid path segment: {part}", nameof(raw));

                var name = match.Groups["name"].Value;
                var indexGroup = match.Groups["index"];
                int? index = indexGroup.Success ? int.Parse(indexGroup.Value) : null;
                segments.Add(new Segment(name, index));
            }

            return new GameObjectPath(sceneName, segments);
        }
    }
}
