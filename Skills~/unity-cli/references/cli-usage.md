# Unity CLI 사용 레퍼런스

`unity command <name> [args] --project-path . --format json` 형태로 실행 중인 에디터를 구동한다.
정본 목록은 항상 `unity command --project-path . --format json`(인자 없이) 로 실측. 아래는 도메인별 요약(설치 버전 1.0.0-beta.x 기준, ~140개).

## 명령 카탈로그 (도메인별)

- **임의 실행**: `eval`(코드 인자), `eval_file`(파일). C# 문장 + `return expr;`.
- **컴파일/콘솔**: `recompile`, `recompile_status`, `get_console_logs`(--severity/--limit), `console`, `clear_console`.
- **에디터 제어**: `editor_status`, `editor_play`, `editor_pause`, `editor_stop`, `editor_focus`, `save_all`.
- **조회(inspect)**: `get_serialized_fields`, `get_component_properties`, `get_scene_hierarchy`, `get_selection`, `find_gameobjects`, `find_assets`, `list_open_scenes`, `list_shaders`, `search`, `read_text_file`, `get_performance_stats`, `get_*_settings`(time/physics/quality/player/graphics/audio/lighting/navmesh/import/build/input), `get_tags_layers`, `get_authoring_root`, `get_animation_clip`, `get_animator_controller`, `get_timeline`, `get_material_properties`, `get_shader_properties`.
- **GameObject/컴포넌트**: `create_gameobject(s)`, `delete_gameobject`, `rename_gameobject`, `set_active`, `set_tag`, `set_layer`, `set_parent`, `set_transform`, `add_component`, `remove_component`, `set_component_properties`, `set_serialized_field`, `set_selection`, `attach_script`.
- **prefab**: `create_prefab`, `create_prefab_variant`, `instantiate_prefab`, `apply_prefab_overrides`, `revert_prefab_overrides`, `save_prefab_contents`, `unpack_prefab`.
- **scene**: `create_scene`, `open_scene`, `save_scene`, `set_active_scene`, `add_scene_to_build`, `remove_scene_from_build`.
- **asset**: `create_asset`, `create_folder`, `create_script`, `copy_asset`, `move_asset`, `rename_asset`, `delete_asset`, `import_asset`, `write_text_file`, `reload_file`, `reload_file_override`.
- **animator/animation/timeline**: `create_animator_controller`, `add_animator_layer/parameter/state/transition`, `create_animation_clip`, `set_animation_curve`, `remove_animation_curve`, `create_timeline`, `add_timeline_track/clip`, `set_material_properties`.
- **lighting/navmesh/occlusion** (대개 async — `*_status` 폴링): `bake_lighting`/`clear_baked_lighting`/`lighting_bake_status`/`cancel_lighting_bake`, `bake_navmesh`/`bake_navmesh_surfaces`/`clear_navmesh`/`navmesh_bake_status`/`cancel_navmesh_bake`/`set_navmesh_settings`, `bake_occlusion_culling`/`clear_occlusion_culling`/`occlusion_bake_status`/`cancel_occlusion_bake`.
- **settings 변경**(대개 `--confirm true`, 일부 `--dry_run`): `set_*_settings`, `set_tags_layers`, `set_autotick`, `set_authoring_root`.
- **package**: `package_add`, `package_remove`, `package_list`, `package_search`, `package_resolve`, `package_status`.
- **build/test**: `build`(APK/AAB/서명/CI), `build_status`, `switch_build_target`+`switch_build_target_status`, `list_build_targets`, `list_build_profiles`, `run_tests`/`list_tests`/`test_status`/`cancel_tests`.
- **menu**: 에디터 메뉴 항목 실행.

> 규약: 이름에 `_status` 가 있으면 대응 트리거 명령은 **즉시 반환 + 상태 폴링**(비동기). `--confirm true` 필요 명령은 파괴적. `--dry_run` 지원 시 먼저 미리보기.

## eval 레시피 (first-class 명령이 없을 때의 탈출구)

```bash
U="$HOME/.unity/bin/unity"; PP=(--project-path .)
ev(){ "$U" command eval "${PP[@]}" --format json "$1" | jq -r '.data.result.result // empty'; }

# 씬/컴파일 상태
ev 'return UnityEditor.EditorUtility.scriptCompilationFailed;'
# private [SerializeField] null 체크 (리플렉션) — 대상이 first-class로 안 잡힐 때
ev 'var go=UnityEngine.GameObject.Find("Foo"); var c=go.GetComponent<Bar>(); var f=typeof(Bar).GetField("_ref", System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic); return f.GetValue(c)==null?"NULL":"OK";'
# internal/hidden 타입 접근
ev 'var t=System.AppDomain.CurrentDomain.GetAssemblies().SelectMany(a=>{try{return a.GetTypes();}catch{return new System.Type[0];}}).FirstOrDefault(x=>x.Name=="SomeInternal"); return t==null?"NO":"YES";'
```

## 도메인 리로드 관통 폴링 (필수 패턴)

`recompile`/스크립트 리로드/Play 진입은 도메인 리로드를 유발하고, 그 창(~3–5초) 동안 `command` 가 일시 실패한다. **에러를 흡수하며 폴링**한다:

```bash
U="$HOME/.unity/bin/unity"; PP=(--project-path .)
"$U" command recompile "${PP[@]}" >/dev/null 2>&1 || true
for i in $(seq 1 45); do
  rs="$("$U" command recompile_status "${PP[@]}" --format json 2>/dev/null | jq -r '.data.result.status // "?"' 2>/dev/null || echo '?')"
  case "$rs" in completed|up_to_date|idle) break;; esac   # 그 외(triggered/compiling/?): 계속
  sleep 3
done
"$U" command recompile_status "${PP[@]}" --format json | jq '.data.result | {status, failed, errors}'
```

## 장기 async 작업 (fire-and-forget + EditorPrefs 폴링)

`eval` 은 동기(~30s, `--timeout` 조정)라 수십초+ 작업은 Task 를 await 하지 않는다. `EditorPrefs` 에 상태를 쓰고 별도 eval 호출로 폴링한다(호출 간 에디터 프로세스 동일 → 상태 유지). `unity-ai-image-gen` 의 `uai.sh`(AiGenProbe) 가 이 패턴을 사용한다.

## 오류 진단

- **`CS****` (eval)**: 컴파일 실패 → `.data.result.diagnostics[]` 확인. bare 식이면 `return` 누락(CS1002/CS0126), 짧은 타입명이면 완전 한정 필요.
- **"No Unity Editor instances found with reachable Pipeline servers"**: 에디터 미실행/Pipeline 미설치 → `unity pipeline list`, `unity status`, `unity pipeline install`, `unity open <proj>`.
- **command 미인식**(root help 로 폴백): 해당 명령이 이 버전에 없음 → `unity command`(목록)으로 실명 확인(베타라 이름 변동). 예: 구버전 top-level `unity eval` → 현재 `unity command eval`.
- **타임아웃**: `--timeout` 상향 또는 코드 축소; 장기 작업은 위 async 패턴.
- **capture 400 (path outside project root)**: `capture_game_view` 는 프로젝트 내부만 → 프로젝트 밖 저장은 `screenshot --output` 사용.
