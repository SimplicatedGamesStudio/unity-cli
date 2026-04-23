# unity-cli Missing Commands Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add grouped `ui`, `scene`, and `gameobject` CLI commands to `unity-cli`, backed by exact-path Unity handlers for canvas capture, scene-object capture, object info, and bounded hierarchy listing.

**Architecture:** Keep Go responsible for public command parsing, help text, and parameter mapping. Keep Unity-side code responsible for exact path parsing, scene and prefab resolution, structured serialization, and capture behavior. Build one shared resolver/serializer layer in the connector first, then wire grouped Go commands to the new internal transport names.

**Tech Stack:** Go CLI, Unity Editor C# package, Newtonsoft JSON, Unity Test Framework, Go `testing`

---

## Verification Host Setup

This repo is not itself a Unity project. Use `/Users/ertugrulkara/Desktop/sweep-and-dice` as the local verification host while developing the package.

- Point the host project at the local package path during development:

```json
"com.simplicatedgamesstudio.unity-cli-connector": "file:/Users/ertugrulkara/Desktop/unity-cli/unity-connector"
```

- Rebuild the local CLI after Go changes:

```bash
cd /Users/ertugrulkara/Desktop/unity-cli
GOBIN="$HOME/.local/bin" go install .
```

- Keep Unity open on `/Users/ertugrulkara/Desktop/sweep-and-dice` while running connector tests.
- Do not commit host-project manifest changes unless explicitly asked.

## File Map

### Go CLI

- Create: `/Users/ertugrulkara/Desktop/unity-cli/cmd/ui.go`
- Create: `/Users/ertugrulkara/Desktop/unity-cli/cmd/ui_test.go`
- Create: `/Users/ertugrulkara/Desktop/unity-cli/cmd/scene.go`
- Create: `/Users/ertugrulkara/Desktop/unity-cli/cmd/scene_test.go`
- Create: `/Users/ertugrulkara/Desktop/unity-cli/cmd/gameobject.go`
- Create: `/Users/ertugrulkara/Desktop/unity-cli/cmd/gameobject_test.go`
- Modify: `/Users/ertugrulkara/Desktop/unity-cli/cmd/root.go`

### Unity connector shared infrastructure

- Create: `/Users/ertugrulkara/Desktop/unity-cli/unity-connector/Editor/Core/GameObjectPath.cs`
- Create: `/Users/ertugrulkara/Desktop/unity-cli/unity-connector/Editor/Core/GameObjectResolver.cs`
- Create: `/Users/ertugrulkara/Desktop/unity-cli/unity-connector/Editor/Core/GameObjectInfoSerializer.cs`
- Create: `/Users/ertugrulkara/Desktop/unity-cli/unity-connector/Editor/Core/CaptureUtility.cs`
- Modify: `/Users/ertugrulkara/Desktop/unity-cli/unity-connector/Editor/Tools/EditorScreenshot.cs`

### Unity connector commands

- Create: `/Users/ertugrulkara/Desktop/unity-cli/unity-connector/Editor/Tools/CaptureUiCanvas.cs`
- Create: `/Users/ertugrulkara/Desktop/unity-cli/unity-connector/Editor/Tools/CaptureSceneObject.cs`
- Create: `/Users/ertugrulkara/Desktop/unity-cli/unity-connector/Editor/Tools/GetGameObjectInfo.cs`
- Create: `/Users/ertugrulkara/Desktop/unity-cli/unity-connector/Editor/Tools/ListGameObjectsInHierarchy.cs`

### Unity package tests

- Create: `/Users/ertugrulkara/Desktop/unity-cli/unity-connector/Editor/Tests/UnityCliConnector.EditorTests.asmdef`
- Create: `/Users/ertugrulkara/Desktop/unity-cli/unity-connector/Editor/Tests/GameObjectPathTests.cs`
- Create: `/Users/ertugrulkara/Desktop/unity-cli/unity-connector/Editor/Tests/GameObjectResolverTests.cs`
- Create: `/Users/ertugrulkara/Desktop/unity-cli/unity-connector/Editor/Tests/GameObjectCommandTests.cs`
- Create: `/Users/ertugrulkara/Desktop/unity-cli/unity-connector/Editor/Tests/CaptureCommandTests.cs`

### Docs

- Modify: `/Users/ertugrulkara/Desktop/unity-cli/README.md`
- Modify: `/Users/ertugrulkara/Desktop/unity-cli/README.ko.md`

Unity will generate matching `.meta` files when the package is opened in the host project. Do not hand-author `.meta` files in advance.

### Task 1: Add the `ui` CLI group

**Files:**
- Create: `/Users/ertugrulkara/Desktop/unity-cli/cmd/ui.go`
- Create: `/Users/ertugrulkara/Desktop/unity-cli/cmd/ui_test.go`
- Modify: `/Users/ertugrulkara/Desktop/unity-cli/cmd/root.go`

- [ ] **Step 1: Write the failing Go test**

```go
package cmd

import "testing"

func TestUICmd_CaptureCanvas(t *testing.T) {
	send, params := mockSend("capture_ui_canvas", t)

	_, err := uiCmd([]string{"capture-canvas", "--path", "HUD/MainCanvas[0]", "--width", "2560"}, send)
	if err != nil {
		t.Fatalf("uiCmd returned error: %v", err)
	}

	if (*params)["path"] != "HUD/MainCanvas[0]" {
		t.Fatalf("expected path to be forwarded")
	}
	if (*params)["width"] != 2560 {
		t.Fatalf("expected width to be parsed as int")
	}
}

func TestUICmd_MissingSubcommand(t *testing.T) {
	send, _ := mockSend("capture_ui_canvas", t)

	if _, err := uiCmd(nil, send); err == nil {
		t.Fatal("expected usage error for missing subcommand")
	}
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```bash
cd /Users/ertugrulkara/Desktop/unity-cli
GOCACHE=/tmp/unity-cli-gocache GOMODCACHE=/tmp/unity-cli-gomodcache go test ./cmd -run 'TestUICmd' -v
```

Expected: FAIL because `uiCmd` does not exist yet.

- [ ] **Step 3: Write the minimal implementation**

Create `/Users/ertugrulkara/Desktop/unity-cli/cmd/ui.go`:

```go
package cmd

import (
	"fmt"

	"github.com/SimplicatedGamesStudio/unity-cli/internal/client"
)

func uiCmd(args []string, send sendFn) (*client.CommandResponse, error) {
	if len(args) == 0 {
		return nil, fmt.Errorf("usage: unity-cli ui <capture-canvas>")
	}

	switch args[0] {
	case "capture-canvas":
		params, err := buildParams(args[1:], nil)
		if err != nil {
			return nil, err
		}
		return send("capture_ui_canvas", params)
	default:
		return nil, fmt.Errorf("unknown ui action: %s\nAvailable: capture-canvas", args[0])
	}
}
```

Add the new category to `/Users/ertugrulkara/Desktop/unity-cli/cmd/root.go`:

```go
	case "ui":
		resp, err = uiCmd(subArgs, send)
```

- [ ] **Step 4: Run the test to verify it passes**

Run:

```bash
cd /Users/ertugrulkara/Desktop/unity-cli
GOCACHE=/tmp/unity-cli-gocache GOMODCACHE=/tmp/unity-cli-gomodcache go test ./cmd -run 'TestUICmd' -v
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
cd /Users/ertugrulkara/Desktop/unity-cli
git add cmd/ui.go cmd/ui_test.go cmd/root.go
git commit -m "feat: add ui command group"
```

### Task 2: Add the `scene` and `gameobject` CLI groups

**Files:**
- Create: `/Users/ertugrulkara/Desktop/unity-cli/cmd/scene.go`
- Create: `/Users/ertugrulkara/Desktop/unity-cli/cmd/scene_test.go`
- Create: `/Users/ertugrulkara/Desktop/unity-cli/cmd/gameobject.go`
- Create: `/Users/ertugrulkara/Desktop/unity-cli/cmd/gameobject_test.go`
- Modify: `/Users/ertugrulkara/Desktop/unity-cli/cmd/root.go`

- [ ] **Step 1: Write the failing Go tests**

Create `/Users/ertugrulkara/Desktop/unity-cli/cmd/scene_test.go`:

```go
package cmd

import "testing"

func TestSceneCmd_CaptureObject(t *testing.T) {
	send, params := mockSend("capture_scene_object", t)

	_, err := sceneCmd([]string{"capture-object", "--path", "BossScene::Units/BossRoot[0]"}, send)
	if err != nil {
		t.Fatalf("sceneCmd returned error: %v", err)
	}

	if (*params)["path"] != "BossScene::Units/BossRoot[0]" {
		t.Fatalf("expected path to be forwarded")
	}
}
```

Create `/Users/ertugrulkara/Desktop/unity-cli/cmd/gameobject_test.go`:

```go
package cmd

import "testing"

func TestGameObjectCmd_Info(t *testing.T) {
	send, params := mockSend("get_game_object_info", t)

	_, err := gameObjectCmd([]string{"info", "--prefab", "Assets/Prefabs/UI/MainHUD.prefab", "--path", "Root/MainCanvas[0]"}, send)
	if err != nil {
		t.Fatalf("gameObjectCmd returned error: %v", err)
	}

	if (*params)["prefab"] != "Assets/Prefabs/UI/MainHUD.prefab" {
		t.Fatalf("expected prefab to be forwarded")
	}
}

func TestGameObjectCmd_ListRecursive(t *testing.T) {
	send, params := mockSend("list_game_objects_in_hierarchy", t)

	_, err := gameObjectCmd([]string{"list", "--path", "HUD/MainCanvas[0]", "--recursive"}, send)
	if err != nil {
		t.Fatalf("gameObjectCmd returned error: %v", err)
	}

	if (*params)["recursive"] != true {
		t.Fatalf("expected recursive flag to be forwarded as bool")
	}
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:

```bash
cd /Users/ertugrulkara/Desktop/unity-cli
GOCACHE=/tmp/unity-cli-gocache GOMODCACHE=/tmp/unity-cli-gomodcache go test ./cmd -run 'TestSceneCmd|TestGameObjectCmd' -v
```

Expected: FAIL because `sceneCmd` and `gameObjectCmd` do not exist yet.

- [ ] **Step 3: Write the minimal implementation**

Create `/Users/ertugrulkara/Desktop/unity-cli/cmd/scene.go`:

```go
package cmd

import (
	"fmt"

	"github.com/SimplicatedGamesStudio/unity-cli/internal/client"
)

func sceneCmd(args []string, send sendFn) (*client.CommandResponse, error) {
	if len(args) == 0 {
		return nil, fmt.Errorf("usage: unity-cli scene <capture-object>")
	}

	switch args[0] {
	case "capture-object":
		params, err := buildParams(args[1:], nil)
		if err != nil {
			return nil, err
		}
		return send("capture_scene_object", params)
	default:
		return nil, fmt.Errorf("unknown scene action: %s\nAvailable: capture-object", args[0])
	}
}
```

Create `/Users/ertugrulkara/Desktop/unity-cli/cmd/gameobject.go`:

```go
package cmd

import (
	"fmt"

	"github.com/SimplicatedGamesStudio/unity-cli/internal/client"
)

func gameObjectCmd(args []string, send sendFn) (*client.CommandResponse, error) {
	if len(args) == 0 {
		return nil, fmt.Errorf("usage: unity-cli gameobject <info|list>")
	}

	switch args[0] {
	case "info":
		params, err := buildParams(args[1:], nil)
		if err != nil {
			return nil, err
		}
		return send("get_game_object_info", params)
	case "list":
		params, err := buildParams(args[1:], nil)
		if err != nil {
			return nil, err
		}
		return send("list_game_objects_in_hierarchy", params)
	default:
		return nil, fmt.Errorf("unknown gameobject action: %s\nAvailable: info, list", args[0])
	}
}
```

Add both categories to `/Users/ertugrulkara/Desktop/unity-cli/cmd/root.go`:

```go
	case "scene":
		resp, err = sceneCmd(subArgs, send)
	case "gameobject":
		resp, err = gameObjectCmd(subArgs, send)
```

- [ ] **Step 4: Run the tests to verify they pass**

Run:

```bash
cd /Users/ertugrulkara/Desktop/unity-cli
GOCACHE=/tmp/unity-cli-gocache GOMODCACHE=/tmp/unity-cli-gomodcache go test ./cmd -run 'TestSceneCmd|TestGameObjectCmd' -v
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
cd /Users/ertugrulkara/Desktop/unity-cli
git add cmd/scene.go cmd/scene_test.go cmd/gameobject.go cmd/gameobject_test.go cmd/root.go
git commit -m "feat: add scene and gameobject command groups"
```

### Task 3: Add the Unity test assembly and path parser

**Files:**
- Create: `/Users/ertugrulkara/Desktop/unity-cli/unity-connector/Editor/Tests/UnityCliConnector.EditorTests.asmdef`
- Create: `/Users/ertugrulkara/Desktop/unity-cli/unity-connector/Editor/Tests/GameObjectPathTests.cs`
- Create: `/Users/ertugrulkara/Desktop/unity-cli/unity-connector/Editor/Core/GameObjectPath.cs`

- [ ] **Step 1: Write the failing Unity tests**

Create `/Users/ertugrulkara/Desktop/unity-cli/unity-connector/Editor/Tests/UnityCliConnector.EditorTests.asmdef`:

```json
{
  "name": "UnityCliConnector.EditorTests",
  "rootNamespace": "UnityCliConnector.EditorTests",
  "references": [
    "UnityCliConnector.Editor",
    "UnityEditor.TestRunner"
  ],
  "includePlatforms": ["Editor"],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [
    {
      "name": "com.unity.test-framework",
      "expression": "1.0.0",
      "define": "UNITY_TEST_FRAMEWORK"
    }
  ],
  "noEngineReferences": false
}
```

Create `/Users/ertugrulkara/Desktop/unity-cli/unity-connector/Editor/Tests/GameObjectPathTests.cs`:

```csharp
using NUnit.Framework;

namespace UnityCliConnector.EditorTests
{
    public class GameObjectPathTests
    {
        [Test]
        public void Parse_SceneQualifiedPath_SplitsSceneAndSegments()
        {
            var parsed = GameObjectPath.Parse("BattleScene::HUD/MainCanvas[0]");

            Assert.That(parsed.SceneName, Is.EqualTo("BattleScene"));
            Assert.That(parsed.Segments.Count, Is.EqualTo(2));
            Assert.That(parsed.Segments[1].Name, Is.EqualTo("MainCanvas"));
            Assert.That(parsed.Segments[1].Index, Is.EqualTo(0));
        }

        [Test]
        public void Parse_DuplicateSiblingWithoutIndex_IsMarkedAmbiguous()
        {
            var parsed = GameObjectPath.Parse("HUD/MainCanvas");
            Assert.That(parsed.Segments[1].HasExplicitIndex, Is.False);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:

```bash
cd /Users/ertugrulkara/Desktop/unity-cli
GOBIN="$HOME/.local/bin" go install .
cd /Users/ertugrulkara/Desktop/sweep-and-dice
unity-cli --project /Users/ertugrulkara/Desktop/sweep-and-dice test --mode EditMode --filter UnityCliConnector.EditorTests.GameObjectPathTests
```

Expected: FAIL or compile error because `GameObjectPath` does not exist yet.

- [ ] **Step 3: Write the minimal implementation**

Create `/Users/ertugrulkara/Desktop/unity-cli/unity-connector/Editor/Core/GameObjectPath.cs`:

```csharp
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
            var sceneName = split.Length == 2 ? split[0] : null;
            var hierarchy = split.Length == 2 ? split[1] : split[0];

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
```

- [ ] **Step 4: Run the tests to verify they pass**

Run:

```bash
cd /Users/ertugrulkara/Desktop/sweep-and-dice
unity-cli --project /Users/ertugrulkara/Desktop/sweep-and-dice test --mode EditMode --filter UnityCliConnector.EditorTests.GameObjectPathTests
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
cd /Users/ertugrulkara/Desktop/unity-cli
git add unity-connector/Editor/Tests/UnityCliConnector.EditorTests.asmdef unity-connector/Editor/Tests/GameObjectPathTests.cs unity-connector/Editor/Core/GameObjectPath.cs
git commit -m "test: add path parser coverage for connector commands"
```

### Task 4: Add scene and prefab resolution helpers

**Files:**
- Create: `/Users/ertugrulkara/Desktop/unity-cli/unity-connector/Editor/Core/GameObjectResolver.cs`
- Create: `/Users/ertugrulkara/Desktop/unity-cli/unity-connector/Editor/Tests/GameObjectResolverTests.cs`

- [ ] **Step 1: Write the failing Unity tests**

Create `/Users/ertugrulkara/Desktop/unity-cli/unity-connector/Editor/Tests/GameObjectResolverTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

namespace UnityCliConnector.EditorTests
{
    public class GameObjectResolverTests
    {
        [Test]
        public void ResolveSceneObject_DuplicateSiblingWithoutIndex_Throws()
        {
            var root = new GameObject("HUD");
            var first = new GameObject("Panel");
            var second = new GameObject("Panel");
            first.transform.SetParent(root.transform);
            second.transform.SetParent(root.transform);

            Assert.Throws<System.InvalidOperationException>(() =>
                GameObjectResolver.ResolveSceneObject("HUD/Panel"));

            Object.DestroyImmediate(root);
        }

        [Test]
        public void ResolveSceneObject_ExplicitIndex_ReturnsExpectedChild()
        {
            var root = new GameObject("HUD");
            var first = new GameObject("Panel");
            var second = new GameObject("Panel");
            first.transform.SetParent(root.transform);
            second.transform.SetParent(root.transform);

            var resolved = GameObjectResolver.ResolveSceneObject("HUD/Panel[1]");

            Assert.That(resolved, Is.EqualTo(second));
            Object.DestroyImmediate(root);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:

```bash
cd /Users/ertugrulkara/Desktop/sweep-and-dice
unity-cli --project /Users/ertugrulkara/Desktop/sweep-and-dice test --mode EditMode --filter UnityCliConnector.EditorTests.GameObjectResolverTests
```

Expected: FAIL because `GameObjectResolver` does not exist yet.

- [ ] **Step 3: Write the minimal implementation**

Create `/Users/ertugrulkara/Desktop/unity-cli/unity-connector/Editor/Core/GameObjectResolver.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityCliConnector
{
    public static class GameObjectResolver
    {
        public static GameObject ResolveSceneObject(string rawPath)
        {
            var path = GameObjectPath.Parse(rawPath);
            if (SceneManager.sceneCount > 1 && string.IsNullOrEmpty(path.SceneName))
                throw new InvalidOperationException("scene-qualified path required when multiple scenes are loaded");

            var scene = string.IsNullOrEmpty(path.SceneName)
                ? SceneManager.GetActiveScene()
                : SceneManager.GetSceneByName(path.SceneName);

            if (!scene.IsValid())
                throw new InvalidOperationException($"scene not found: {path.SceneName}");

            return ResolveParsedPath(path, scene.GetRootGameObjects());
        }

        public static GameObject LoadPrefabRoot(string prefabPath)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (!root)
                throw new InvalidOperationException($"prefab not found: {prefabPath}");
            return root;
        }

        public static GameObject ResolvePrefabObject(GameObject prefabRoot, string rawPath)
        {
            var path = GameObjectPath.Parse(rawPath);
            return ResolveParsedPath(path, new[] { prefabRoot });
        }

        static GameObject ResolveParsedPath(GameObjectPath path, IEnumerable<GameObject> startingNodes)
        {
            IEnumerable<GameObject> current = startingNodes;
            GameObject resolved = null;

            foreach (var segment in path.Segments)
            {
                var matches = current.Where(go => go.name == segment.Name).ToList();
                if (matches.Count == 0)
                    throw new InvalidOperationException($"object not found: {segment.Name}");

                if (segment.Index.HasValue)
                {
                    if (segment.Index.Value < 0 || segment.Index.Value >= matches.Count)
                        throw new InvalidOperationException($"object index out of range: {segment.Name}[{segment.Index.Value}]");
                    resolved = matches[segment.Index.Value];
                }
                else
                {
                    if (matches.Count > 1)
                        throw new InvalidOperationException($"ambiguous path segment: {segment.Name}");
                    resolved = matches[0];
                }

                current = resolved.transform.Cast<Transform>().Select(t => t.gameObject);
            }

            return resolved;
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run:

```bash
cd /Users/ertugrulkara/Desktop/sweep-and-dice
unity-cli --project /Users/ertugrulkara/Desktop/sweep-and-dice test --mode EditMode --filter UnityCliConnector.EditorTests.GameObjectResolverTests
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
cd /Users/ertugrulkara/Desktop/unity-cli
git add unity-connector/Editor/Core/GameObjectResolver.cs unity-connector/Editor/Tests/GameObjectResolverTests.cs
git commit -m "feat: add exact scene and prefab resolution helpers"
```

### Task 5: Add `gameobject info` and `gameobject list`

**Files:**
- Create: `/Users/ertugrulkara/Desktop/unity-cli/unity-connector/Editor/Core/GameObjectInfoSerializer.cs`
- Create: `/Users/ertugrulkara/Desktop/unity-cli/unity-connector/Editor/Tools/GetGameObjectInfo.cs`
- Create: `/Users/ertugrulkara/Desktop/unity-cli/unity-connector/Editor/Tools/ListGameObjectsInHierarchy.cs`
- Create: `/Users/ertugrulkara/Desktop/unity-cli/unity-connector/Editor/Tests/GameObjectCommandTests.cs`

- [ ] **Step 1: Write the failing Unity tests**

Create `/Users/ertugrulkara/Desktop/unity-cli/unity-connector/Editor/Tests/GameObjectCommandTests.cs`:

```csharp
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityCliConnector.Tools;

namespace UnityCliConnector.EditorTests
{
    public class GameObjectCommandTests
    {
        [Test]
        public void GetGameObjectInfo_ReturnsCoreSchema()
        {
            var root = new GameObject("HUD");
            var canvas = root.AddComponent<Canvas>();

            var response = (SuccessResponse)GetGameObjectInfo.HandleCommand(new JObject
            {
                ["path"] = "HUD"
            });

            var data = JObject.FromObject(response.data);
            Assert.That(data["name"]?.ToString(), Is.EqualTo("HUD"));
            Assert.That(data["componentTypes"]?.ToObject<string[]>(), Does.Contain(nameof(Canvas)));

            Object.DestroyImmediate(root);
        }

        [Test]
        public void ListGameObjectsInHierarchy_DefaultsToDepthOne()
        {
            var root = new GameObject("HUD");
            var child = new GameObject("Panel");
            var grandChild = new GameObject("Label");
            child.transform.SetParent(root.transform);
            grandChild.transform.SetParent(child.transform);

            var response = (SuccessResponse)ListGameObjectsInHierarchy.HandleCommand(new JObject
            {
                ["path"] = "HUD"
            });

            var data = JObject.FromObject(response.data);
            Assert.That(data["children"]?[0]?["children"], Is.Null);

            Object.DestroyImmediate(root);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:

```bash
cd /Users/ertugrulkara/Desktop/sweep-and-dice
unity-cli --project /Users/ertugrulkara/Desktop/sweep-and-dice test --mode EditMode --filter UnityCliConnector.EditorTests.GameObjectCommandTests
```

Expected: FAIL because the serializer and handlers do not exist yet.

- [ ] **Step 3: Write the minimal implementation**

Create `/Users/ertugrulkara/Desktop/unity-cli/unity-connector/Editor/Core/GameObjectInfoSerializer.cs`:

```csharp
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityCliConnector
{
    public static class GameObjectInfoSerializer
    {
        public static object SerializeInfo(GameObject target, string resolvedPath, string source)
        {
            var rect = target.GetComponent<RectTransform>();
            return new
            {
                name = target.name,
                resolvedPath,
                source,
                activeSelf = target.activeSelf,
                layer = target.layer,
                tag = target.tag,
                childCount = target.transform.childCount,
                componentTypes = target.GetComponents<Component>().Where(c => c).Select(c => c.GetType().Name).ToArray(),
                transform = new
                {
                    localPosition = target.transform.localPosition,
                    localRotation = target.transform.localRotation.eulerAngles,
                    localScale = target.transform.localScale,
                },
                rectTransform = rect == null ? null : new
                {
                    anchoredPosition = rect.anchoredPosition,
                    sizeDelta = rect.sizeDelta,
                    anchorMin = rect.anchorMin,
                    anchorMax = rect.anchorMax,
                },
                prefab = PrefabUtility.GetPrefabInstanceStatus(target).ToString(),
            };
        }
    }
}
```

Create `/Users/ertugrulkara/Desktop/unity-cli/unity-connector/Editor/Tools/GetGameObjectInfo.cs`:

```csharp
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityCliConnector.Tools
{
    [UnityCliTool(Name = "get_game_object_info", Description = "Return a stable core schema for an exact scene or prefab object.")]
    public static class GetGameObjectInfo
    {
        public static object HandleCommand(JObject parameters)
        {
            var p = new ToolParams(parameters);
            var prefab = p.Get("prefab");
            var path = p.GetRequired("path");
            if (!path.IsSuccess)
                return new ErrorResponse(path.ErrorMessage);

            GameObject target = null;
            GameObject prefabRoot = null;
            try
            {
                if (string.IsNullOrEmpty(prefab))
                {
                    target = GameObjectResolver.ResolveSceneObject(path.Value);
                }
                else
                {
                    prefabRoot = GameObjectResolver.LoadPrefabRoot(prefab);
                    target = GameObjectResolver.ResolvePrefabObject(prefabRoot, path.Value);
                }

                return new SuccessResponse("GameObject info", GameObjectInfoSerializer.SerializeInfo(
                    target, path.Value, string.IsNullOrEmpty(prefab) ? "scene" : "prefab"));
            }
            catch (System.Exception ex)
            {
                return new ErrorResponse(ex.Message);
            }
            finally
            {
                if (prefabRoot)
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
    }
}
```

Create `/Users/ertugrulkara/Desktop/unity-cli/unity-connector/Editor/Tools/ListGameObjectsInHierarchy.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityCliConnector.Tools
{
    [UnityCliTool(Name = "list_game_objects_in_hierarchy", Description = "List an exact scene or prefab hierarchy with bounded depth.")]
    public static class ListGameObjectsInHierarchy
    {
        public static object HandleCommand(JObject parameters)
        {
            var p = new ToolParams(parameters);
            var depth = p.GetInt("depth", 1).Value;
            var recursive = p.GetBool("recursive", false);
            if (recursive) depth = -1;

            var prefab = p.Get("prefab");
            var path = p.Get("path");
            GameObject prefabRoot = null;

            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    if (string.IsNullOrEmpty(prefab))
                    {
                        var roots = SceneManager.GetActiveScene().GetRootGameObjects();
                        var data = roots.Select((root, index) =>
                            SerializeNode(root, $"{root.name}[{index}]", depth)).ToArray();
                        return new SuccessResponse("Hierarchy listing", data);
                    }

                    prefabRoot = GameObjectResolver.LoadPrefabRoot(prefab);
                    return new SuccessResponse("Hierarchy listing",
                        SerializeNode(prefabRoot, $"{prefabRoot.name}[0]", depth));
                }

                var target = string.IsNullOrEmpty(prefab)
                    ? GameObjectResolver.ResolveSceneObject(path)
                    : GameObjectResolver.ResolvePrefabObject(
                        prefabRoot = GameObjectResolver.LoadPrefabRoot(prefab), path);

                return new SuccessResponse("Hierarchy listing", SerializeNode(target, path, depth));
            }
            finally
            {
                if (prefabRoot)
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        static object SerializeNode(GameObject target, string path, int depth)
        {
            var includeChildren = depth != 0;
            return new
            {
                name = target.name,
                path,
                activeSelf = target.activeSelf,
                childCount = target.transform.childCount,
                componentTypes = target.GetComponents<Component>().Where(c => c).Select(c => c.GetType().Name).ToArray(),
                children = includeChildren
                    ? target.transform.Cast<Transform>().Select((child, index) =>
                        SerializeNode(child.gameObject, $"{path}/{child.name}[{index}]", depth < 0 ? -1 : depth - 1)).ToArray()
                    : null
            };
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run:

```bash
cd /Users/ertugrulkara/Desktop/sweep-and-dice
unity-cli --project /Users/ertugrulkara/Desktop/sweep-and-dice test --mode EditMode --filter UnityCliConnector.EditorTests.GameObjectCommandTests
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
cd /Users/ertugrulkara/Desktop/unity-cli
git add unity-connector/Editor/Core/GameObjectInfoSerializer.cs unity-connector/Editor/Tools/GetGameObjectInfo.cs unity-connector/Editor/Tools/ListGameObjectsInHierarchy.cs unity-connector/Editor/Tests/GameObjectCommandTests.cs
git commit -m "feat: add gameobject info and list commands"
```

### Task 6: Refactor screenshot helpers and add `ui capture-canvas`

**Files:**
- Create: `/Users/ertugrulkara/Desktop/unity-cli/unity-connector/Editor/Core/CaptureUtility.cs`
- Create: `/Users/ertugrulkara/Desktop/unity-cli/unity-connector/Editor/Tools/CaptureUiCanvas.cs`
- Modify: `/Users/ertugrulkara/Desktop/unity-cli/unity-connector/Editor/Tools/EditorScreenshot.cs`
- Create: `/Users/ertugrulkara/Desktop/unity-cli/unity-connector/Editor/Tests/CaptureCommandTests.cs`

- [ ] **Step 1: Write the failing Unity tests**

Create `/Users/ertugrulkara/Desktop/unity-cli/unity-connector/Editor/Tests/CaptureCommandTests.cs`:

```csharp
using System.IO;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityCliConnector.Tools;

namespace UnityCliConnector.EditorTests
{
    public class CaptureCommandTests
    {
        [Test]
        public void CaptureUiCanvas_RejectsNonCanvasTarget()
        {
            var go = new GameObject("HUD");
            var result = CaptureUiCanvas.HandleCommand(new JObject { ["path"] = "HUD" });

            Assert.That(result, Is.TypeOf<ErrorResponse>());
            Object.DestroyImmediate(go);
        }

        [Test]
        public void CaptureUiCanvas_WritesFile()
        {
            var root = new GameObject("HUD");
            root.AddComponent<Canvas>();
            var output = "Screenshots/test-ui-capture.png";

            var result = (SuccessResponse)CaptureUiCanvas.HandleCommand(new JObject
            {
                ["path"] = "HUD",
                ["output_path"] = output,
                ["width"] = 512,
                ["height"] = 512,
            });

            var data = JObject.FromObject(result.data);
            Assert.That(File.Exists(data["path"]!.ToString()), Is.True);
            Object.DestroyImmediate(root);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:

```bash
cd /Users/ertugrulkara/Desktop/sweep-and-dice
unity-cli --project /Users/ertugrulkara/Desktop/sweep-and-dice test --mode EditMode --filter UnityCliConnector.EditorTests.CaptureCommandTests
```

Expected: FAIL because `CaptureUiCanvas` and the shared capture helper do not exist yet.

- [ ] **Step 3: Write the minimal implementation**

Create `/Users/ertugrulkara/Desktop/unity-cli/unity-connector/Editor/Core/CaptureUtility.cs`:

```csharp
using System;
using System.IO;
using UnityEngine;

namespace UnityCliConnector
{
    public static class CaptureUtility
    {
        public static string ResolveOutputPath(string userPath, string prefix)
        {
            if (string.IsNullOrEmpty(userPath))
                userPath = $"Screenshots/{prefix}-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}.png";

            if (Path.IsPathRooted(userPath))
                return Path.GetFullPath(userPath);

            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.GetFullPath(Path.Combine(projectRoot, userPath));
        }

        public static object CaptureCamera(Camera camera, int width, int height, string outputPath, object data)
        {
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var previousRT = camera.targetTexture;
            RenderTexture rt = null;
            Texture2D tex = null;
            try
            {
                rt = new RenderTexture(width, height, 24);
                camera.targetTexture = rt;
                camera.Render();
                RenderTexture.active = rt;
                tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                File.WriteAllBytes(outputPath, tex.EncodeToPNG());
                return new SuccessResponse($"Screenshot saved to {outputPath}", data);
            }
            finally
            {
                camera.targetTexture = previousRT;
                RenderTexture.active = null;
                if (rt) UnityEngine.Object.DestroyImmediate(rt);
                if (tex) UnityEngine.Object.DestroyImmediate(tex);
            }
        }
    }
}
```

Create `/Users/ertugrulkara/Desktop/unity-cli/unity-connector/Editor/Tools/CaptureUiCanvas.cs`:

```csharp
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace UnityCliConnector.Tools
{
    [UnityCliTool(Name = "capture_ui_canvas", Description = "Capture an exact Canvas object from the loaded scene.")]
    public static class CaptureUiCanvas
    {
        public static object HandleCommand(JObject parameters)
        {
            var p = new ToolParams(parameters);
            var path = p.GetRequired("path");
            if (!path.IsSuccess)
                return new ErrorResponse(path.ErrorMessage);

            var target = GameObjectResolver.ResolveSceneObject(path.Value);
            var canvas = target.GetComponent<Canvas>();
            if (!canvas)
                return new ErrorResponse("invalid_target_type");

            var width = p.GetInt("width", 1920).Value;
            var height = p.GetInt("height", 1080).Value;
            var outputPath = CaptureUtility.ResolveOutputPath(p.Get("output_path"), "ui-capture");
            var camera = canvas.worldCamera ? canvas.worldCamera : Camera.main;
            if (!camera)
                return new ErrorResponse("capture_failed");

            return CaptureUtility.CaptureCamera(camera, width, height, outputPath, new
            {
                path = outputPath,
                width,
                height,
                resolvedPath = path.Value,
                mode = Application.isPlaying ? "play" : "edit",
                source = "scene",
            });
        }
    }
}
```

Refactor `/Users/ertugrulkara/Desktop/unity-cli/unity-connector/Editor/Tools/EditorScreenshot.cs` to call `CaptureUtility.ResolveOutputPath(...)` and `CaptureUtility.CaptureCamera(...)` instead of duplicating output-path and render-texture code.

- [ ] **Step 4: Run the tests to verify they pass**

Run:

```bash
cd /Users/ertugrulkara/Desktop/sweep-and-dice
unity-cli --project /Users/ertugrulkara/Desktop/sweep-and-dice test --mode EditMode --filter UnityCliConnector.EditorTests.CaptureCommandTests
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
cd /Users/ertugrulkara/Desktop/unity-cli
git add unity-connector/Editor/Core/CaptureUtility.cs unity-connector/Editor/Tools/CaptureUiCanvas.cs unity-connector/Editor/Tools/EditorScreenshot.cs unity-connector/Editor/Tests/CaptureCommandTests.cs
git commit -m "feat: add canvas capture command"
```

### Task 7: Add `scene capture-object` subtree isolation

**Files:**
- Create: `/Users/ertugrulkara/Desktop/unity-cli/unity-connector/Editor/Tools/CaptureSceneObject.cs`
- Modify: `/Users/ertugrulkara/Desktop/unity-cli/unity-connector/Editor/Tests/CaptureCommandTests.cs`

- [ ] **Step 1: Write the failing Unity test**

Append to `/Users/ertugrulkara/Desktop/unity-cli/unity-connector/Editor/Tests/CaptureCommandTests.cs`:

```csharp
[Test]
public void CaptureSceneObject_RestoresHiddenSiblings()
{
    var root = new GameObject("SceneRoot");
    var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
    var sibling = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    target.name = "Target";
    sibling.name = "Sibling";
    target.transform.SetParent(root.transform);
    sibling.transform.SetParent(root.transform);

    var result = CaptureSceneObject.HandleCommand(new JObject
    {
        ["path"] = "SceneRoot/Target[0]",
        ["output_path"] = "Screenshots/test-scene-capture.png"
    });

    Assert.That(result, Is.TypeOf<SuccessResponse>());
    Assert.That(sibling.GetComponent<Renderer>().enabled, Is.True);

    Object.DestroyImmediate(root);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```bash
cd /Users/ertugrulkara/Desktop/sweep-and-dice
unity-cli --project /Users/ertugrulkara/Desktop/sweep-and-dice test --mode EditMode --filter UnityCliConnector.EditorTests.CaptureCommandTests.CaptureSceneObject_RestoresHiddenSiblings
```

Expected: FAIL because `CaptureSceneObject` does not exist yet.

- [ ] **Step 3: Write the minimal implementation**

Create `/Users/ertugrulkara/Desktop/unity-cli/unity-connector/Editor/Tools/CaptureSceneObject.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace UnityCliConnector.Tools
{
    [UnityCliTool(Name = "capture_scene_object", Description = "Capture an exact scene object subtree while hiding unrelated renderers.")]
    public static class CaptureSceneObject
    {
        public static object HandleCommand(JObject parameters)
        {
            var p = new ToolParams(parameters);
            var path = p.GetRequired("path");
            if (!path.IsSuccess)
                return new ErrorResponse(path.ErrorMessage);

            var target = GameObjectResolver.ResolveSceneObject(path.Value);
            var width = p.GetInt("width", 1920).Value;
            var height = p.GetInt("height", 1080).Value;
            var outputPath = CaptureUtility.ResolveOutputPath(p.Get("output_path"), "scene-capture");
            var camera = Camera.main ? Camera.main : Object.FindFirstObjectByType<Camera>();
            if (!camera)
                return new ErrorResponse("capture_failed");

            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            var keep = new HashSet<Renderer>(target.GetComponentsInChildren<Renderer>(true));
            var hidden = renderers.Where(r => r && !keep.Contains(r) && r.enabled).ToList();

            try
            {
                foreach (var renderer in hidden)
                    renderer.enabled = false;

                return CaptureUtility.CaptureCamera(camera, width, height, outputPath, new
                {
                    path = outputPath,
                    width,
                    height,
                    resolvedPath = path.Value,
                    mode = Application.isPlaying ? "play" : "edit",
                    source = "scene",
                });
            }
            finally
            {
                foreach (var renderer in hidden)
                    if (renderer) renderer.enabled = true;
            }
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run:

```bash
cd /Users/ertugrulkara/Desktop/sweep-and-dice
unity-cli --project /Users/ertugrulkara/Desktop/sweep-and-dice test --mode EditMode --filter UnityCliConnector.EditorTests.CaptureCommandTests.CaptureSceneObject_RestoresHiddenSiblings
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
cd /Users/ertugrulkara/Desktop/unity-cli
git add unity-connector/Editor/Tools/CaptureSceneObject.cs unity-connector/Editor/Tests/CaptureCommandTests.cs
git commit -m "feat: add scene object capture command"
```

### Task 8: Wire help text, README docs, and run full verification

**Files:**
- Modify: `/Users/ertugrulkara/Desktop/unity-cli/cmd/root.go`
- Modify: `/Users/ertugrulkara/Desktop/unity-cli/README.md`
- Modify: `/Users/ertugrulkara/Desktop/unity-cli/README.ko.md`

- [ ] **Step 1: Write the failing documentation and help checks**

Add Go tests to `/Users/ertugrulkara/Desktop/unity-cli/cmd/root_test.go`:

```go
import (
	"bytes"
	"io"
	"os"
	"strings"
)

func captureOutput(t *testing.T, fn func()) string {
	t.Helper()
	old := os.Stdout
	r, w, err := os.Pipe()
	if err != nil {
		t.Fatalf("os.Pipe: %v", err)
	}
	os.Stdout = w
	defer func() { os.Stdout = old }()

	fn()

	_ = w.Close()
	var buf bytes.Buffer
	if _, err := io.Copy(&buf, r); err != nil {
		t.Fatalf("io.Copy: %v", err)
	}
	return buf.String()
}

func TestPrintTopicHelp_UI(t *testing.T) {
	out := captureOutput(t, func() { printTopicHelp("ui") })
	if !strings.Contains(out, "capture-canvas") {
		t.Fatalf("expected ui help to mention capture-canvas, got %q", out)
	}
}

func TestPrintTopicHelp_GameObject(t *testing.T) {
	out := captureOutput(t, func() { printTopicHelp("gameobject") })
	if !strings.Contains(out, "gameobject info") {
		t.Fatalf("expected gameobject help to mention info command, got %q", out)
	}
}
```

Add README examples for the new commands:

```markdown
unity-cli ui capture-canvas --path HUD/MainCanvas[0]
unity-cli scene capture-object --path Units/BossRoot[0]
unity-cli gameobject info --path HUD/MainCanvas[0]/Panel[1]
unity-cli gameobject list --path HUD/MainCanvas[0] --depth 2
```

- [ ] **Step 2: Run the checks to verify they fail**

Run:

```bash
cd /Users/ertugrulkara/Desktop/unity-cli
GOCACHE=/tmp/unity-cli-gocache GOMODCACHE=/tmp/unity-cli-gomodcache go test ./cmd -run 'TestPrintTopicHelp_UI|TestPrintTopicHelp_GameObject' -v
```

Expected: FAIL or incomplete help output because topic help has not been updated yet.

- [ ] **Step 3: Write the minimal implementation**

Update `/Users/ertugrulkara/Desktop/unity-cli/cmd/root.go` help text to include:

```text
UI:
  ui capture-canvas --path <exact-path> [--output <path>] [--width N] [--height N]

Scene:
  scene capture-object --path <exact-path> [--output <path>] [--width N] [--height N]

GameObject:
  gameobject info --path <exact-path> [--prefab <asset-path>]
  gameobject list [--path <exact-path>] [--prefab <asset-path>] [--depth N|--recursive]
```

Update `/Users/ertugrulkara/Desktop/unity-cli/README.md` and `/Users/ertugrulkara/Desktop/unity-cli/README.ko.md` with matching examples and the exact-path rules:

```markdown
- exact paths only
- duplicate siblings require `[index]`
- scene-qualified paths required when multiple scenes are loaded
- prefab support is available for `gameobject info` and `gameobject list`
```

- [ ] **Step 4: Run full verification**

Run:

```bash
cd /Users/ertugrulkara/Desktop/unity-cli
GOCACHE=/tmp/unity-cli-gocache GOMODCACHE=/tmp/unity-cli-gomodcache go test ./...
GOCACHE=/tmp/unity-cli-gocache GOMODCACHE=/tmp/unity-cli-gomodcache GOBIN=/tmp/unity-cli-bin go install . && /tmp/unity-cli-bin/unity-cli version

cd /Users/ertugrulkara/Desktop/sweep-and-dice
unity-cli --project /Users/ertugrulkara/Desktop/sweep-and-dice test --mode EditMode --filter UnityCliConnector.EditorTests
unity-cli --project /Users/ertugrulkara/Desktop/sweep-and-dice gameobject info --path HUD/MainCanvas[0]
unity-cli --project /Users/ertugrulkara/Desktop/sweep-and-dice gameobject list --path HUD/MainCanvas[0] --depth 2
unity-cli --project /Users/ertugrulkara/Desktop/sweep-and-dice ui capture-canvas --path HUD/MainCanvas[0]
```

Expected:

- all Go tests PASS
- local CLI install prints `unity-cli <version>`
- Unity edit-mode tests PASS
- smoke commands return JSON output without transport errors

- [ ] **Step 5: Commit**

```bash
cd /Users/ertugrulkara/Desktop/unity-cli
git add cmd/root.go cmd/root_test.go README.md README.ko.md
git commit -m "docs: add help and usage for missing commands"
```
