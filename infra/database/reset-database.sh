#!/usr/bin/env bash
set -euo pipefail

database_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "$database_directory/../.." && pwd)"
compose_file="$database_directory/docker-compose.yml"

for dependency in docker dotnet; do
    if ! command -v "$dependency" >/dev/null 2>&1; then
        echo "Comando ausente: $dependency. Consulte os pré-requisitos no README.md." >&2
        exit 1
    fi
done

[[ -f "$compose_file" ]] || { echo "Compose não encontrado: $compose_file" >&2; exit 1; }
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

echo 'Apagando os dados locais do PostgreSQL e recriando o banco...'
# Limita a remoção aos recursos deste Compose; preserva as migrations versionadas.
docker compose --file "$compose_file" down --volumes
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
echo 'Volume recriado e migrations existentes aplicadas. Para iniciar a API, execute ./run_api.sh na raiz do projeto.'
