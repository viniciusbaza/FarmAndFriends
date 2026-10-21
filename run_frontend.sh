#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
if ! command -v npm >/dev/null 2>&1; then
    echo "npm não encontrado. Instale o Node.js conforme o README.md." >&2
    exit 1
fi

cd "$repository_root/FarmAndFriends.Frontend"
if [[ ! -d node_modules ]]; then
    npm ci
fi
exec npm run dev -- "$@"
