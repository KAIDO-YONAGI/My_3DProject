#!/usr/bin/env bash
# push.sh — 提交并推送到远程仓库（GitHub / Gitee / 全部）
#
# 用法:
#   ./scripts/push.sh                      # 交互式选择目标远程
#   ./scripts/push.sh github               # 仅推 GitHub
#   ./scripts/push.sh gitee                # 仅推 Gitee
#   ./scripts/push.sh all                  # 推送全部远程
#   ./scripts/push.sh all "fix: 修复xxx"   # 附带 commit message，自动 commit 后再推
#
# 说明:
#   - 若工作区有改动且提供了 commit message，会先 git add -A && commit，再推送。
#   - 若有改动但没提供 message，会提示你先提交（不自动生成敷衍的 commit）。
#   - GitHub 在国内常网络不稳，脚本会对每个远程最多重试 3 次。

set -uo pipefail

# ---- 远程映射 ----
declare -A REMOTES=(
  ["github"]="github"   # https://github.com/KAIDO-YONAGI/My_3DProject.git
  ["gitee"]="origin"    # https://gitee.com/KAIDOYONAGI/my_3-dproject.git
)
BRANCH="main"
MAX_RETRY=3

cd "$(git rev-parse --show-toplevel)" || exit 1

log()  { printf '\033[1;34m[push]\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33m[warn]\033[0m %s\n' "$*" >&2; }
err()  { printf '\033[1;31m[err ]\033[0m %s\n' "$*" >&2; }
ok()   { printf '\033[1;32m[ok  ]\033[0m %s\n' "$*"; }

# ---- 可选：先提交 ----
COMMIT_MSG="${2:-}"
if [ -n "$(git status --porcelain)" ]; then
  if [ -n "$COMMIT_MSG" ]; then
    log "工作区有改动，使用提供的 message 提交: $COMMIT_MSG"
    git add -A || { err "git add 失败"; exit 1; }
    git commit -m "$COMMIT_MSG" || { err "git commit 失败"; exit 1; }
  else
    warn "工作区有未提交改动，但未提供 commit message。"
    warn "请先手动提交，或用: $0 <target> \"你的 commit 说明\""
    exit 1
  fi
else
  log "工作区干净，直接推送。"
fi

# ---- 选目标 ----
TARGET="${1:-}"
if [ -z "$TARGET" ]; then
  echo
  log "选择要推送的远程:"
  printf '  1) github   (github  → GitHub)\n'
  printf '  2) gitee    (origin  → Gitee)\n'
  printf '  3) all      (全部)\n'
  echo
  read -r -p "请输入 [1/2/3]: " CHOICE </dev/tty
  case "$CHOICE" in
    1) TARGET="github" ;;
    2) TARGET="gitee" ;;
    3) TARGET="all" ;;
    *) err "无效选择"; exit 1 ;;
  esac
fi

case "$TARGET" in
  github|gitee|all) ;;
  *) err "未知目标: $TARGET (可选: github / gitee / all)"; exit 1 ;;
esac

# ---- 推送函数（带重试）----
push_one() {
  local key="$1"
  local remote="${REMOTES[$key]}"
  log "→ 推送 [$key] (remote=$remote, branch=$BRANCH)"
  if ! git remote get-url "$remote" >/dev/null 2>&1; then
    err "远程 '$remote' 不存在，跳过。"
    return 1
  fi
  for i in $(seq 1 $MAX_RETRY); do
    if git push "$remote" "$BRANCH" 2>&1 | sed 's/^/      /'; then
      ok "[$key] 推送成功"
      return 0
    fi
    warn "[$key] 第 $i/$MAX_RETRY 次失败，2 秒后重试..."
    sleep 2
  done
  err "[$key] 重试 $MAX_RETRY 次仍失败。"
  return 1
}

# ---- 执行 ----
FAILED=0
if [ "$TARGET" = "all" ]; then
  for key in github gitee; do
    push_one "$key" || FAILED=$((FAILED+1))
  done
  if [ "$FAILED" -gt 0 ]; then
    err "有 $FAILED 个远程推送失败。"
    exit 1
  fi
  ok "全部远程推送完成。"
else
  push_one "$TARGET" || exit 1
fi
