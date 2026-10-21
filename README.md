# 🌾 Farm & Friends

Uma aplicação dedicada ao gerenciamento/simulação de atividades agrícolas e colaboração. Este projeto possui uma arquitetura dividida entre uma API robusta em .NET e um ecossistema de Frontend dinâmico.

---

## 📂 Estrutura do Projeto

O repositório está organizado da seguinte forma:

*   **`FarmAndFriends.Api/`**: Backend desenvolvido em .NET (Backend API C# + .NET 8 + Entity Framework), responsável pelas regras de negócio, persistência de dados e disponibilização dos endpoints da API.
*   **`FarmAndFriends.Frontend/`**: Interface do usuário (Client-side) que consome a API para exibição e gerenciamento dos recursos.
*   **`infra/`**: Configurações de infraestrutura, bancos de dados, containers ou scripts de deploy.
*   **`imgs/`**: Imagens e recursos visuais utilizados na documentação e no projeto.

---

## 🛠️ Tecnologias Utilizadas

*   **Backend:** .NET / C# (ASP.NET Core Web API)
*   **Frontend:** Phaser + React + Vite + Tailwind CSS
*   **Automação de Scripts:** PowerShell (Windows) e Bash (macOS/Linux)

---

## 🚀 Como Executar o Projeto

### Pré-requisitos

* [.NET SDK 8](https://dotnet.microsoft.com/download/dotnet/8.0).
* [Node.js](https://nodejs.org/) 22.12+ (ou 24), com npm, para o frontend.
* [Docker Desktop](https://www.docker.com/products/docker-desktop/) instalado e aberto, com Docker Compose disponível. 
* Entity Framework CLI compatível com o projeto:

  ```bash
  dotnet tool install --global dotnet-ef --version 8.0.0
  ```

  Se a ferramenta já estiver instalada, verifique com `dotnet ef --version`.
  No macOS, se o comando não for encontrado, adicione esta linha ao `~/.zshrc`
  e abra um novo terminal:

  ```bash
  export PATH="$PATH:$HOME/.dotnet/tools"
  ```

### macOS / Linux

No Terminal, abra a pasta do repositório. Execute:

```bash
./run_api.sh
```

O script inicia o PostgreSQL no Docker, aguarda sua disponibilidade, aplica as
migrations existentes e inicia a API com o perfil HTTP de desenvolvimento.
A documentação fica em [Swagger](http://localhost:5204/swagger).
Não é necessário configurar certificado HTTPS para esse perfil.

Em uma segunda aba do Terminal, abra a pasta do repositório. Execute:

```bash
./run_frontend.sh
```

O script executa `npm ci` quando `node_modules` ainda não existe e inicia o Vite.
Abra o endereço mostrado no terminal, normalmente
[http://localhost:5173](http://localhost:5173).
O Vite encaminha as chamadas de API para a porta 5204.
Se trouxe `node_modules` do Windows ou alterou o lockfile, execute `npm ci`
na pasta `FarmAndFriends.Frontend` para reinstalar as dependências nesta estação.

Os scripts resolvem os caminhos a partir de sua própria localização.
Se o sistema informar falta de permissão para executá-los, use `bash run_api.sh`
e `bash run_frontend.sh`, ou execute `chmod +x run_api.sh run_frontend.sh`.

Para parar API e frontend, pressione `Ctrl+C` nos respectivos terminais.
O banco continua rodando e mantém os dados. Para pará-lo:

```bash
docker compose --file infra/database/docker-compose.yml stop
```

### Windows / PowerShell

Os scripts existentes continuam disponíveis. Na raiz do repositório, execute
cada um em um terminal PowerShell separado:

```powershell
./run_api.ps1
./run_frontend.ps1
```

Antes da primeira execução do frontend, rode `npm ci` na pasta
`FarmAndFriends.Frontend`.

### Posso usar os arquivos `.ps1` no Mac?

Sim, com PowerShell para macOS (`pwsh`) instalado. Porém, os scripts existentes
usam caminhos no estilo Windows e o script do frontend depende do diretório
atual. Os arquivos `.sh` acima são a opção preparada para este ambiente e
dispensam instalar PowerShell. Não execute arquivos `.ps1` com Bash ou zsh.

---

## ⚙️ Infraestrutura 
O reset não é necessário para a inicialização normal. Pare a API com `Ctrl+C`
antes de executá-lo e mantenha o Docker Desktop aberto.

(Opcional) Para apagar o volume local descartável, recriar o PostgreSQL e
reaplicar as migrations versionadas, execute na raiz do projeto:

macOS / Linux:

```bash
./infra/database/reset-database.sh
```

Windows / PowerShell:

```powershell
./infra/database/reset-database.ps1
```

Esse comando remove todos os dados locais. Ele não apaga nem regenera arquivos
de migration e não inicia a API; use `./run_api.sh` no Mac ou `./run_api.ps1`
no Windows depois do reset. O script Bash verifica as ferramentas antes da
remoção, aguarda o PostgreSQL e reaplica as migrations versionadas.

---

📄 Licença
Este projeto está sob a licença [Insira a licença aqui, ex: MIT]. Consulte o arquivo LICENSE para mais informações.
