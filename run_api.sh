#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
compose_file="$repository_root/infra/database/docker-compose.yml"

for dependency in docker dotnet; do
    if ! command -v "$dependency" >/dev/null 2>&1; then
        echo "Comando ausente: $dependency. Consulte os pré-requisitos no README.md." >&2
        exit 1
    fi
done

docker compose version >/dev/null
if ! docker info >/dev/null 2>&1; then
    echo "Docker indisponível. Abra o Docker Desktop e aguarde o mecanismo iniciar." >&2
    exit 1
fi

cd "$repository_root/FarmAndFriends.Api"
if ! dotnet ef --version >/dev/null 2>&1; then
    echo 'Instale o Entity Framework CLI: dotnet tool install --global dotnet-ef --version 8.0.0' >&2
    echo 'Inclua no PATH: export PATH="$PATH:$HOME/.dotnet/tools"' >&2
    exit 1
fi

docker compose --file "$compose_file" up --detach
postgres_ready=false
for ((attempt = 1; attempt <= 60; attempt++)); do
    if docker compose --file "$compose_file" exec --no-TTY postgres \
        pg_isready --username farm_user --dbname farmandfriends >/dev/null 2>&1; then
        postgres_ready=true
        break
    fi
    sleep 1
done

if [[ "$postgres_ready" != true ]]; then
    echo "PostgreSQL não ficou pronto em 60 segundos. Verifique: docker compose --file \"$compose_file\" logs postgres" >&2
    exit 1
fi

dotnet ef database update
exec dotnet run --launch-profile http
