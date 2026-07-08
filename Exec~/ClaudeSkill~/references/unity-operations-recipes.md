# Unity Operations Recipes

## 1) Active scene and compile state

```csharp
return new {
    scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path,
    isCompiling = UnityEditor.EditorApplication.isCompiling,
    isPlaying = UnityEditor.EditorApplication.isPlaying
};
```

## 2) Prefab + component existence

```csharp
var path = "Assets/Resources_Addressable/Popups/UserProfileEditPopup.prefab";
var go = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(path);
if (go == null) return "MISSING_PREFAB";
return go.GetComponent<project.Common.Popup.UserProfileEditPopup>() != null ? "OK" : "MISSING_COMPONENT";
```

## 3) Private serialized field null check (reflection)

```csharp
var path = "Assets/Resources_Addressable/Popups/UserProfileEditPopup.prefab";
var go = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(path);
var comp = go.GetComponent<project.Common.Popup.UserProfileEditPopup>();
var t = comp.GetType();
var f = t.GetField("_closeButton", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
var v = f?.GetValue(comp);
return v == null ? "NULL" : "OK";
```

## 4) Scene object create/select/save (editor manipulation)

```csharp
var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
if (!scene.IsValid()) return "NO_SCENE";
var go = new UnityEngine.GameObject("__TempMarker");
UnityEditor.Selection.activeGameObject = go;
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return go.name;
```

## 5) Asset search preview before batch edits

```csharp
var guids = UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] {"Assets/Resources_Addressable/Popups"});
return new {
    count = guids.Length,
    sample = guids.Take(10).Select(g => UnityEditor.AssetDatabase.GUIDToAssetPath(g)).ToArray()
};
```

## 6) Compile status summary endpoint usage

Shell:
```bash
PORT=$(.claude/skills/unity-editor-ops/scripts/resolve-port.sh) || exit 1
TOKEN=$(cat ~/.unity-exec/auth-token)
curl -s "http://127.0.0.1:$PORT/compile" -H "X-Auth-Token: $TOKEN"
```

## 7) Logs endpoint usage

Shell:
```bash
PORT=$(.claude/skills/unity-editor-ops/scripts/resolve-port.sh) || exit 1
TOKEN=$(cat ~/.unity-exec/auth-token)
curl -s "http://127.0.0.1:$PORT/logs?count=100&level=error" -H "X-Auth-Token: $TOKEN"
```

## Notes

- For write operations, run a read-only preview first.
- Keep each `/exec` payload small and focused.
- Re-check compile state after script/asset edits.
