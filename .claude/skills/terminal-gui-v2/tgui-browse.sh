#!/usr/bin/env bash
# Browse Terminal.Gui v2 source files via GitHub API.
#
# Usage:
#   tgui-browse.sh list <Dir>            — list files in Terminal.Gui/<Dir>
#   tgui-browse.sh read <Dir> <File>.cs  — print source of a file
#
# All paths are relative to the Terminal.Gui/ directory in the v2_develop branch.

set -euo pipefail

REPO="gui-cs/Terminal.Gui"
REF="develop"
BASE="Terminal.Gui"

cmd="${1:-}"
shift || true

case "$cmd" in
  list)
    dir="${1:?Usage: tgui-browse.sh list <Dir>}"
    gh api "repos/${REPO}/contents/${BASE}/${dir}?ref=${REF}" \
      | python3 -c "import json,sys; [print(d['name']) for d in json.load(sys.stdin)]"
    ;;
  read)
    dir="${1:?Usage: tgui-browse.sh read <Dir> <File>.cs}"
    file="${2:?Usage: tgui-browse.sh read <Dir> <File>.cs}"
    gh api "repos/${REPO}/contents/${BASE}/${dir}/${file}?ref=${REF}" \
      | python3 -c "import json,sys,base64; print(base64.b64decode(json.load(sys.stdin)['content']).decode())"
    ;;
  *)
    echo "Usage:"
    echo "  tgui-browse.sh list <Dir>"
    echo "  tgui-browse.sh read <Dir> <File>.cs"
    exit 1
    ;;
esac
