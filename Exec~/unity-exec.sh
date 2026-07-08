#!/usr/bin/env bash
# unity-exec — Secure C# execution in Unity Editor
# Usage:
#   unity-exec "Application.dataPath"
#   unity-exec --usings "Unity.Entities" "World.All.Count"
#   unity-exec status
#   unity-exec security
#   unity-exec --port 8091 "Time.time"

set -euo pipefail

UNITY_EXEC_DIR="$HOME/.unity-exec"
TOKEN_FILE="$UNITY_EXEC_DIR/auth-token"
INSTANCES_FILE="$UNITY_EXEC_DIR/instances.json"

# Defaults
PORT=""
USINGS=""
TIMEOUT=30
PROJECT=""

# Colors (disabled if not a terminal)
if [ -t 1 ]; then
    RED='\033[0;31m'
    GREEN='\033[0;32m'
    YELLOW='\033[0;33m'
    CYAN='\033[0;36m'
    NC='\033[0m'
else
    RED='' GREEN='' YELLOW='' CYAN='' NC=''
fi

usage() {
    cat <<EOF
${CYAN}unity-exec${NC} — Secure C# execution in Unity Editor

${YELLOW}Usage:${NC}
  unity-exec [options] <code>       Execute C# code
  unity-exec status                 Show Unity instance status
  unity-exec security               Show security policy info
  unity-exec logs [opts]            Show recent console logs
  unity-exec compile [opts]         Show compile status / trigger recompile
  unity-exec help                   Show this help

${YELLOW}Options:${NC}
  --port <N>          Override Unity instance port (default: auto-discover)
  --project <path>    Prefer Unity instance matching this project path
  --usings <ns>       Additional using directives (comma-separated)
  --timeout <sec>     Request timeout in seconds (default: 30)

${YELLOW}logs options:${NC}
  --count <N>         Max log entries (1-500, default: 50)
  --level <lvl>       Filter: all|error|warning|log|exception|assert

${YELLOW}compile options:${NC}
  --trigger           Trigger recompile (returns immediately; poll 'compile' for result)
  --full              Force full recompile (implies --trigger)
  (no option)         Show current compile status only
  Note: for trigger + wait-for-result, use the skill's ucompile.sh.

${YELLOW}Examples:${NC}
  unity-exec "Application.dataPath"
  unity-exec "GameObject.FindObjectsOfType<Camera>().Length"
  unity-exec --usings "UnityEditor.SceneManagement" "EditorSceneManager.GetActiveScene().name"
  unity-exec "var go = new GameObject(\"Test\"); return go.name;"
  unity-exec logs --level error --count 20
  unity-exec compile
  unity-exec compile --full

${YELLOW}Security:${NC}
  Auth token: ~/.unity-exec/auth-token
  Audit log:  ~/.unity-exec/audit.log
EOF
}

die() {
    echo -e "${RED}Error:${NC} $1" >&2
    exit 1
}

# Read auth token
read_token() {
    if [ ! -f "$TOKEN_FILE" ]; then
        die "Auth token not found at $TOKEN_FILE. Is Unity running with unity-exec package?"
    fi
    cat "$TOKEN_FILE"
}

# Auto-discover port from instances.json
discover_port() {
    if [ -n "$PORT" ]; then
        echo "$PORT"
        return
    fi

    if [ ! -f "$INSTANCES_FILE" ]; then
        die "No Unity instances found. Is Unity running with unity-exec package?"
    fi

    # Prefer matching project instance (PROJECT option or current working directory)
    local port
    if command -v python3 &>/dev/null; then
        port=$(python3 -c "
import json, os, sys
target = os.path.realpath(sys.argv[1]) if len(sys.argv) > 1 and sys.argv[1] else ''
cwd = os.path.realpath(os.getcwd())
if not target:
    target = cwd

def to_project_root(instance_project_value: str) -> str:
    p = os.path.realpath(instance_project_value or '')
    if p.endswith('/Assets'):
        return p[:-7]
    return p

def is_alive(pid) -> bool:
    # pid 가 살아있는 프로세스인지 확인 (죽은 인스턴스의 stale 포트 회피)
    try:
        os.kill(int(pid), 0)
        return True
    except (OSError, ValueError, TypeError):
        return False

try:
    with open('$INSTANCES_FILE') as f:
        instances = json.load(f)
    if not instances:
        sys.exit(1)

    # 살아있는 인스턴스만 후보로 사용
    alive = [inst for inst in instances if is_alive(inst.get('pid'))]
    if not alive:
        sys.exit(1)

    best = None
    # Search from latest to oldest
    for inst in reversed(alive):
        project = to_project_root(inst.get('project', ''))
        assets = os.path.realpath(inst.get('project', ''))
        if not project:
            continue

        exact = (target == project or target == assets)
        nested = (target.startswith(project + os.sep) or project.startswith(target + os.sep))
        if exact or nested:
            best = inst
            break

    if best is None:
        best = alive[-1]

    print(best['port'])
except SystemExit:
    raise
except:
    sys.exit(1)
" "$PROJECT" 2>/dev/null) || die "Failed to read instances.json"
    elif command -v jq &>/dev/null; then
        # jq fallback: latest instance only
        port=$(jq -r '.[-1].port // empty' "$INSTANCES_FILE" 2>/dev/null) || die "Failed to read instances.json"
    else
        # Fallback: grep for port
        port=$(grep -o '"port":[[:space:]]*[0-9]*' "$INSTANCES_FILE" | tail -1 | grep -o '[0-9]*')
    fi

    if [ -z "$port" ]; then
        die "No Unity instance port found in instances.json"
    fi
    echo "$port"
}

# Make HTTP request
http_request() {
    local method="$1"
    local path="$2"
    local data="${3:-}"
    local port
    port=$(discover_port)

    local url="http://127.0.0.1:${port}${path}"
    local curl_args=(
        -s -S
        --max-time "$TIMEOUT"
        -H "Content-Type: application/json"
    )

    # /status is public. Other endpoints require token.
    if [ "$path" != "/status" ]; then
        local token
        token=$(read_token)
        curl_args+=(-H "X-Auth-Token: $token")
    fi

    if [ "$method" = "POST" ] && [ -n "$data" ]; then
        curl_args+=(-X POST -d "$data")
    fi

    local response
    local http_code

    # Get response with HTTP status code
    response=$(curl -w "\n%{http_code}" "${curl_args[@]}" "$url" 2>&1) || {
        die "Failed to connect to Unity at $url. Is the editor running?"
    }

    # Split response body and status code
    http_code=$(echo "$response" | tail -1)
    local body
    body=$(echo "$response" | sed '$d')

    # Format output
    if [ "$http_code" -ge 200 ] && [ "$http_code" -lt 300 ]; then
        format_response "$body"
    else
        echo -e "${RED}HTTP $http_code${NC}" >&2
        format_response "$body" >&2
        exit 1
    fi
}

# Format JSON response for terminal
format_response() {
    local body="$1"

    if command -v python3 &>/dev/null; then
        python3 -c "
import json, sys
try:
    r = json.loads(sys.argv[1])
    if r.get('success'):
        data = r.get('data')
        if data is not None:
            if isinstance(data, (dict, list)):
                print(json.dumps(data, indent=2, ensure_ascii=False))
            else:
                print(data)
        else:
            print(r.get('message', 'OK'))
    else:
        print('Error:', r.get('error', 'Unknown error'), file=sys.stderr)
        sys.exit(1)
except json.JSONDecodeError:
    print(sys.argv[1])
except Exception as e:
    print(sys.argv[1])
" "$body"
    elif command -v jq &>/dev/null; then
        echo "$body" | jq -r 'if .success then (.data // .message) else .error end'
    else
        echo "$body"
    fi
}

# Parse arguments
POSITIONAL=()
while [[ $# -gt 0 ]]; do
    case "$1" in
        --port)
            PORT="$2"
            shift 2
            ;;
        --project)
            PROJECT="$2"
            shift 2
            ;;
        --usings)
            USINGS="$2"
            shift 2
            ;;
        --timeout)
            TIMEOUT="$2"
            shift 2
            ;;
        -h|--help|help)
            usage
            exit 0
            ;;
        status)
            http_request GET /status
            exit 0
            ;;
        security)
            http_request GET /security
            exit 0
            ;;
        logs)
            shift
            LOGS_COUNT=""
            LOGS_LEVEL=""
            while [[ $# -gt 0 ]]; do
                case "$1" in
                    --count) LOGS_COUNT="$2"; shift 2 ;;
                    --level) LOGS_LEVEL="$2"; shift 2 ;;
                    *) die "Unknown logs option: $1" ;;
                esac
            done
            LOGS_PATH="/logs"
            LOGS_QUERY=""
            [ -n "$LOGS_COUNT" ] && LOGS_QUERY="count=$LOGS_COUNT"
            [ -n "$LOGS_LEVEL" ] && LOGS_QUERY="${LOGS_QUERY:+$LOGS_QUERY&}level=$LOGS_LEVEL"
            [ -n "$LOGS_QUERY" ] && LOGS_PATH="/logs?$LOGS_QUERY"
            http_request GET "$LOGS_PATH"
            exit 0
            ;;
        compile)
            shift
            COMPILE_TRIGGER=false
            COMPILE_FULL=false
            while [[ $# -gt 0 ]]; do
                case "$1" in
                    --trigger) COMPILE_TRIGGER=true; shift ;;
                    --full) COMPILE_FULL=true; COMPILE_TRIGGER=true; shift ;;
                    *) die "Unknown compile option: $1" ;;
                esac
            done
            if [ "$COMPILE_TRIGGER" = true ]; then
                COMPILE_BODY='{}'
                [ "$COMPILE_FULL" = true ] && COMPILE_BODY='{"full":true}'
                http_request POST /compile "$COMPILE_BODY"
            else
                # 트리거 없이 현재 컴파일 상태만 조회 (완료 대기는 ucompile.sh 사용)
                http_request GET /compile
            fi
            exit 0
            ;;
        -*)
            die "Unknown option: $1"
            ;;
        *)
            POSITIONAL+=("$1")
            shift
            ;;
    esac
done

# Need code to execute
if [ ${#POSITIONAL[@]} -eq 0 ]; then
    usage
    exit 1
fi

CODE="${POSITIONAL[0]}"

# Build JSON payload
if [ -n "$USINGS" ]; then
    # Convert comma-separated usings to JSON array
    USINGS_JSON=$(echo "$USINGS" | python3 -c "
import json, sys
usings = [u.strip() for u in sys.stdin.read().split(',') if u.strip()]
print(json.dumps(usings))
" 2>/dev/null || echo "[]")
    PAYLOAD=$(python3 -c "
import json, sys
print(json.dumps({'code': sys.argv[1], 'usings': json.loads(sys.argv[2])}))
" "$CODE" "$USINGS_JSON" 2>/dev/null) || die "Failed to build request payload"
else
    PAYLOAD=$(python3 -c "
import json, sys
print(json.dumps({'code': sys.argv[1]}))
" "$CODE" 2>/dev/null) || {
        # Fallback without python3: manual JSON escaping (basic)
        ESCAPED_CODE=$(echo "$CODE" | sed 's/\\/\\\\/g; s/"/\\"/g; s/\n/\\n/g')
        PAYLOAD="{\"code\":\"$ESCAPED_CODE\"}"
    }
fi

http_request POST /exec "$PAYLOAD"
