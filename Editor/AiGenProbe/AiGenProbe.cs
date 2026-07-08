// unity-ai-image-gen 스킬의 구동 하네스.
// Unity AI 의 internal `Unity.AI.Generators.Tools.AssetGenerators` 를 리플렉션으로 호출한다.
// (해당 API 는 public 이 아니며 Assistant 전용 internal — 직접 참조 불가라 리플렉션으로 우회.)
// 생성은 비동기·수십초이고 unity-exec 는 동기 30s 한계라, fire-and-forget + EditorPrefs 상태 폴링 구조로 분리한다.
using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.U2D.Sprites; // ISpriteEditorDataProvider / SpriteDataProviderFactories (Unity 6 안정 슬라이싱)
using UnityEngine;

/// <summary>internal `Unity.AI.Generators.Tools.AssetGenerators` 를 리플렉션으로 구동하는 이미지 생성 하네스.</summary>
public static class AiGenProbe
{
    const string StatusKey = "AiGenProbe.Status";
    const string ModelsKey = "AiGenProbe.Models";
    const string CostKey = "AiGenProbe.Cost";

    const BindingFlags PubStatic = BindingFlags.Public | BindingFlags.Static;

    static Type FindType(string fullName)
    {
        foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type t = null;
            try { t = a.GetType(fullName); } catch { }
            if (t != null) return t;
        }
        return null;
    }

    static Type AssetGenerators => FindType("Unity.AI.Generators.Tools.AssetGenerators");

    static void SetStatus(string s) { EditorPrefs.SetString(StatusKey, s); Debug.Log("[AiGenProbe] " + s); }

    static string Flatten(Exception e)
    {
        var s = "";
        while (e != null) { s += e.GetType().Name + ": " + e.Message + " || "; e = e.InnerException; }
        return s;
    }

    /// <summary>사용 가능한 모델 목록 조회 (포인트 비소모). 결과는 EditorPrefs("AiGenProbe.Models") 에 적재.</summary>
    public static async void ListModels()
    {
        try
        {
            SetStatus("MODELS:RUNNING");
            var m = AssetGenerators.GetMethod("GetAvailableModelsAsync", PubStatic);
            var task = (Task)m.Invoke(null, new object[] { true, CancellationToken.None });
            await task;
            var result = task.GetType().GetProperty("Result").GetValue(task);
            var list = ((System.Collections.IEnumerable)result).Cast<object>().ToArray();
            var miType = FindType("Unity.AI.Generators.Tools.ModelInfo");
            var idF = miType.GetField("ModelId");
            var dF = miType.GetField("Description");
            var lines = list.Select(o => idF.GetValue(o) + " | " + dF.GetValue(o)).ToArray();
            EditorPrefs.SetString(ModelsKey, string.Join("\n", lines));
            SetStatus("MODELS:DONE count=" + list.Length);
        }
        catch (Exception e) { SetStatus("MODELS:ERROR " + Flatten(e)); }
    }

    /// <summary>생성물(모델 고정 해상도)을 임의 크기로 리사이즈해 dstPath 에 PNG 저장. GPU blit 경로라 비-readable 텍스처도 가능.</summary>
    public static void Resize(string srcPath, string dstPath, int w, int h)
    {
        try
        {
            var src = AssetDatabase.LoadAssetAtPath<Texture2D>(srcPath);
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            var png = tex.EncodeToPNG();
            System.IO.File.WriteAllBytes(dstPath, png);
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(dstPath);
            SetStatus("RESIZE:DONE " + dstPath + " " + w + "x" + h);
        }
        catch (Exception e) { SetStatus("RESIZE:ERROR " + Flatten(e)); }
    }

    /// <summary>프롬프트(+선택적 참고이미지)로 스프라이트 생성. fire-and-forget; 상태는 EditorPrefs("AiGenProbe.Status") 폴링. (포인트 소모)</summary>
    public static async void Kick(string prompt, string savePath, string modelId, string refPath, bool removeBg)
    {
        try
        {
            SetStatus("GEN:RUNNING");
            var spriteSettingsT = FindType("Unity.AI.Generators.Tools.SpriteSettings");
            var objRefT = FindType("Unity.AI.Generators.Tools.ObjectReference");

            var settings = Activator.CreateInstance(spriteSettingsT);
            spriteSettingsT.GetField("Width").SetValue(settings, 1024);  // 일부 모델(vfx-hits/game-ui-*)은 1024-4096 강제
            spriteSettingsT.GetField("Height").SetValue(settings, 1024);
            spriteSettingsT.GetField("RemoveBackground").SetValue(settings, removeBg); // 중앙 오브젝트는 bg제거 시 통째로 사라질 수 있음 → --no-bg-removal

            var refTex = string.IsNullOrEmpty(refPath) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(refPath);
            var arr = Array.CreateInstance(objRefT, refTex == null ? 0 : 1);
            if (refTex != null)
            {
                var or = Activator.CreateInstance(objRefT);
                objRefT.GetField("Image").SetValue(or, refTex);
                objRefT.GetField("Label").SetValue(or, "ref");
                arr.SetValue(or, 0);
            }
            spriteSettingsT.GetField("ImageReferences").SetValue(settings, arr);

            var gpDef = FindType("Unity.AI.Generators.Tools.GenerationParameters`1");
            var gpT = gpDef.MakeGenericType(spriteSettingsT);
            var prms = Activator.CreateInstance(gpT);
            gpT.GetField("Prompt").SetValue(prms, prompt);
            gpT.GetField("ModelId").SetValue(prms, modelId);
            gpT.GetField("SavePath").SetValue(prms, savePath);
            gpT.GetField("AssetType").SetValue(prms, typeof(Texture2D));
            gpT.GetField("Settings").SetValue(prms, settings);
            Func<string, long, Task> perm = (label, cost) =>
            {
                Debug.Log($"[AiGenProbe] permission label={label} cost={cost}");
                return Task.CompletedTask;
            };
            gpT.GetField("PermissionCheckAsync").SetValue(prms, perm);

            var gen = AssetGenerators.GetMethod("GenerateAsync", PubStatic).MakeGenericMethod(spriteSettingsT);
            var handle = gen.Invoke(null, new object[] { prms, CancellationToken.None });
            var handleT = handle.GetType();
            var cost2 = handleT.GetProperty("PointCost").GetValue(handle);
            EditorPrefs.SetString(CostKey, cost2.ToString());
            SetStatus("GEN:STARTED cost=" + cost2);

            var genTask = (Task)handleT.GetProperty("GenerationTask").GetValue(handle);
            await genTask;
            object res = genTask.GetType().GetProperty("Result")?.GetValue(genTask);
            // 이미지 결과는 DownloadTask 로 내려온다(GenerationTask.Result 는 placeholder 일 수 있음). RunGen 과 동일 처리.
            var dlTask = handleT.GetProperty("DownloadTask")?.GetValue(handle) as Task;
            if (dlTask != null)
            {
                await dlTask;
                var dlRes = dlTask.GetType().GetProperty("Result")?.GetValue(dlTask);
                if (dlRes != null) res = dlRes;
            }
            AssetDatabase.Refresh();
            SetStatus("GEN:DONE path=" + savePath + " result=" + (res == null ? "null" : ((UnityEngine.Object)res).name));
        }
        catch (Exception e) { SetStatus("GEN:ERROR " + Flatten(e)); }
    }

    const BindingFlags AnyStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

    /// <summary>Animation/Sound/Spritesheet 공통: GenerationParameters&lt;settingsT&gt; 조립 + GenerateXAsync&lt;settingsT&gt;(prms, settings, ct) 호출 + GenerationTask 폴링.</summary>
    static async Task<UnityEngine.Object> RunGen(string methodName, Type settingsT, object settings, string prompt, string savePath, string modelId, Type assetType)
    {
        var gpDef = FindType("Unity.AI.Generators.Tools.GenerationParameters`1");
        var gpT = gpDef.MakeGenericType(settingsT);
        var prms = Activator.CreateInstance(gpT);
        gpT.GetField("Prompt").SetValue(prms, prompt);
        gpT.GetField("ModelId").SetValue(prms, modelId);
        gpT.GetField("SavePath").SetValue(prms, savePath);
        gpT.GetField("AssetType").SetValue(prms, assetType);
        gpT.GetField("Settings").SetValue(prms, settings);
        Func<string, long, Task> perm = (label, cost) =>
        {
            Debug.Log($"[AiGenProbe] permission label={label} cost={cost}");
            return Task.CompletedTask;
        };
        gpT.GetField("PermissionCheckAsync").SetValue(prms, perm);

        var gen = AssetGenerators.GetMethod(methodName, AnyStatic).MakeGenericMethod(settingsT);
        var handle = gen.Invoke(null, new object[] { prms, settings, CancellationToken.None });
        var handleT = handle.GetType();
        var cost2 = handleT.GetProperty("PointCost").GetValue(handle);
        EditorPrefs.SetString(CostKey, cost2.ToString());
        SetStatus("GEN:STARTED cost=" + cost2);

        var genTask = (Task)handleT.GetProperty("GenerationTask").GetValue(handle);
        await genTask;
        // 비디오/시트 계열은 실제 결과 에셋이 DownloadTask 로 내려온다 → 있으면 그것도 대기 후 그 결과를 우선 사용.
        object res = genTask.GetType().GetProperty("Result")?.GetValue(genTask);
        var dlTask = handleT.GetProperty("DownloadTask")?.GetValue(handle) as Task;
        if (dlTask != null)
        {
            await dlTask;
            var dlRes = dlTask.GetType().GetProperty("Result")?.GetValue(dlTask);
            if (dlRes != null) res = dlRes;
        }
        AssetDatabase.Refresh();
        return res as UnityEngine.Object;
    }

    /// <summary>효과음/오디오 생성 → AudioClip. fire-and-forget; 상태는 EditorPrefs 폴링. (포인트 소모)</summary>
    public static async void KickSound(string prompt, string savePath, string modelId, float duration, bool loop)
    {
        try
        {
            SetStatus("GEN:RUNNING");
            var sT = FindType("Unity.AI.Generators.Tools.SoundSettings");
            var s = Activator.CreateInstance(sT);
            sT.GetField("DurationInSeconds").SetValue(s, duration);
            sT.GetField("Loop").SetValue(s, loop);
            sT.GetField("VoiceName").SetValue(s, "");
            var res = await RunGen("GenerateSoundAsync", sT, s, prompt, savePath, modelId, typeof(AudioClip));
            SetStatus("GEN:DONE path=" + savePath + " result=" + (res == null ? "null" : res.name));
        }
        catch (Exception e) { SetStatus("GEN:ERROR " + Flatten(e)); }
    }

    /// <summary>텍스트(+선택적 비디오 레퍼런스)로 모션 생성 → AnimationClip. (포인트 소모)</summary>
    public static async void KickAnimation(string prompt, string savePath, string modelId, float duration, string videoRefPath)
    {
        try
        {
            SetStatus("GEN:RUNNING");
            var sT = FindType("Unity.AI.Generators.Tools.AnimationSettings");
            var s = Activator.CreateInstance(sT);
            sT.GetField("DurationInSeconds").SetValue(s, duration);
            if (!string.IsNullOrEmpty(videoRefPath))
            {
                var vc = AssetDatabase.LoadAssetAtPath<UnityEngine.Video.VideoClip>(videoRefPath);
                sT.GetField("VideoReference").SetValue(s, vc);
            }
            var res = await RunGen("GenerateAnimationAsync", sT, s, prompt, savePath, modelId, typeof(AnimationClip));
            SetStatus("GEN:DONE path=" + savePath + " result=" + (res == null ? "null" : res.name));
        }
        catch (Exception e) { SetStatus("GEN:ERROR " + Flatten(e)); }
    }

    /// <summary>스프라이트시트(애니 프레임) 생성 → 선택적으로 AnimationClip 으로 변환. (포인트 소모)</summary>
    public static async void KickSpritesheet(string prompt, string sheetPath, string clipPath, string modelId, bool loop, string refPath, int cols, int rows, float fps, int ppu, bool key)
    {
        try
        {
            SetStatus("GEN:RUNNING");
            var sT = FindType("Unity.AI.Generators.Tools.SpriteSettings");
            var objRefT = FindType("Unity.AI.Generators.Tools.ObjectReference");
            var s = Activator.CreateInstance(sT);
            sT.GetField("Width").SetValue(s, 1024);
            sT.GetField("Height").SetValue(s, 1024);
            sT.GetField("RemoveBackground").SetValue(s, false); // 시트 프레임은 bg 제거 시 내용까지 날아갈 수 있어 끔
            sT.GetField("Loop").SetValue(s, loop);
            // 영상계열 spritesheet 모델(seedance/kling)은 첫 프레임 이미지를 요구 → ImageReferences[0] 로 전달.
            var refTex = string.IsNullOrEmpty(refPath) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(refPath);
            var arr = Array.CreateInstance(objRefT, refTex == null ? 0 : 1);
            if (refTex != null)
            {
                var or = Activator.CreateInstance(objRefT);
                objRefT.GetField("Image").SetValue(or, refTex);
                objRefT.GetField("Label").SetValue(or, "ref");
                arr.SetValue(or, 0);
            }
            sT.GetField("ImageReferences").SetValue(s, arr);
            var res = await RunGen("GenerateSpritesheetAsync", sT, s, prompt, sheetPath, modelId, typeof(Texture2D));
            var sheet = (res as Texture2D) ?? AssetDatabase.LoadAssetAtPath<Texture2D>(sheetPath);
            if (sheet != null && !string.IsNullOrEmpty(clipPath))
            {
                // Unity 의 ConvertSpriteSheetToAnimationClipAsync 는 인터랙티브 UI 전용(헤드리스 비신뢰) →
                // 검증된 로컬 파이프라인으로: (영상 시트는 불투명 배경이므로) 키잉 → 그리드 슬라이스(+PPU 정규화) → sprite-swap 클립.
                int n = SliceAndClip(sheetPath, clipPath, cols, rows, fps, loop, ppu, key, 2000);
                SetStatus("GEN:DONE path=" + clipPath + " sheet=" + sheetPath + " frames=" + n + " ppu=" + ppu + " key=" + key);
            }
            else
            {
                SetStatus("GEN:DONE path=" + sheetPath + " (sheet only)");
            }
        }
        catch (Exception e) { SetStatus("GEN:ERROR " + Flatten(e)); }
    }

    /// <summary>스프라이트시트(평면 이미지)를 (선택 키잉→)cols×rows 슬라이스(+PPU 정규화) 후 sprite-swap AnimationClip 작성. AI/포인트 불필요·동기.</summary>
    public static void BuildSpriteClip(string sheetPath, string clipPath, int cols, int rows, float fps, bool loop, int ppu, bool key)
    {
        try
        {
            int n = SliceAndClip(sheetPath, clipPath, cols, rows, fps, loop, ppu, key, 2000);
            if (n == 0) { SetStatus("CLIP:ERROR no sprites after slice"); return; }
            SetStatus("CLIP:DONE frames=" + n + " len=" + (n / fps).ToString("0.00") + "s ppu=" + ppu + " key=" + key + " clip=" + clipPath);
        }
        catch (Exception e) { SetStatus("CLIP:ERROR " + Flatten(e)); }
    }

    /// <summary>
    /// 시트를 cols×rows 그리드로 슬라이스(프레임 0 = 좌상단) + PPU 정규화.
    /// Unity 6 에서 obsolete `TextureImporter.spritesheet` 는 자동슬라이스로 덮여 불안정 → `ISpriteEditorDataProvider` 사용(안정).
    /// ppu>0 이면 spritePixelsPerUnit 을 그 값으로(AI 생성물은 PPU=텍스처폭 1024 로 들어와 ~10배 작게 렌더되므로 표준값 100 등으로 정규화 필수).
    /// </summary>
    static void SliceGrid(string path, int cols, int rows, int ppu)
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Multiple;
        if (ppu > 0) ti.spritePixelsPerUnit = ppu;
        ti.SaveAndReimport(); // data provider 전에 Multiple 확정

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        int W = tex.width, H = tex.height, cw = W / cols, chh = H / rows;

        var factories = new SpriteDataProviderFactories();
        factories.Init();
        var dp = factories.GetSpriteEditorDataProviderFromObject(ti);
        dp.InitSpriteEditorDataProvider();

        var rects = new System.Collections.Generic.List<SpriteRect>();
        int i = 0;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                rects.Add(new SpriteRect
                {
                    name = "frame_" + i,
                    rect = new Rect(c * cw, H - (r + 1) * chh, cw, chh),
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    spriteID = GUID.Generate()
                });
                i++;
            }
        dp.SetSpriteRects(rects.ToArray());
        dp.Apply();
        ti.SaveAndReimport();
    }

    /// <summary>평평한 단색 배경(좌상단 코너 샘플)을 임계값 내에서 투명으로 키잉해 path 에 덮어쓴다.
    /// 영상→시트(seedance/kling)는 불투명 배경을 내므로 게임 스프라이트로 쓰려면 필요. GPU blit 경로라 비-readable 텍스처도 처리.</summary>
    public static void KeyFlatBg(string path, int threshold)
    {
        var src = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        int W = src.width, H = src.height;
        var rt = RenderTexture.GetTemporary(W, H, 0, RenderTextureFormat.ARGB32);
        var prev = RenderTexture.active;
        Graphics.Blit(src, rt);
        RenderTexture.active = rt;
        var t = new Texture2D(W, H, TextureFormat.RGBA32, false);
        t.ReadPixels(new Rect(0, 0, W, H), 0, 0); t.Apply();
        RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
        var px = t.GetPixels32();
        var bg = px[(H - 1) * W]; // 좌상단(이미지 기준) 코너 = 배경 표본
        for (int i = 0; i < px.Length; i++)
        {
            int dr = px[i].r - bg.r, dg = px[i].g - bg.g, db = px[i].b - bg.b;
            if (dr * dr + dg * dg + db * db < threshold) px[i].a = 0;
        }
        t.SetPixels32(px); t.Apply();
        System.IO.File.WriteAllBytes(path, t.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
    }

    /// <summary>(선택적 키잉 →) 슬라이스(+PPU) → SpriteRenderer.m_Sprite 스왑 AnimationClip 작성. AI/포인트 불필요·동기. 프레임 수 반환.</summary>
    static int SliceAndClip(string sheetPath, string clipPath, int cols, int rows, float fps, bool loop, int ppu, bool key, int keyThreshold)
    {
        if (key) KeyFlatBg(sheetPath, keyThreshold);
        SliceGrid(sheetPath, cols, rows, ppu);
        var sprites = AssetDatabase.LoadAllAssetsAtPath(sheetPath).OfType<Sprite>()
            .OrderBy(sp => int.Parse(sp.name.Substring(sp.name.LastIndexOf("_") + 1))).ToArray();
        if (sprites.Length == 0) return 0;
        var clip = new AnimationClip { frameRate = fps };
        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        var kfs = new ObjectReferenceKeyframe[sprites.Length];
        for (int i = 0; i < sprites.Length; i++) kfs[i] = new ObjectReferenceKeyframe { time = i / fps, value = sprites[i] };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, kfs);
        var st = AnimationUtility.GetAnimationClipSettings(clip); st.loopTime = loop; AnimationUtility.SetAnimationClipSettings(clip, st);
        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath) != null) AssetDatabase.DeleteAsset(clipPath);
        AssetDatabase.CreateAsset(clip, clipPath);
        AssetDatabase.SaveAssets();
        return sprites.Length;
    }
}
