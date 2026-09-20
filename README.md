# GBS_Web

Blazor Server (.NET 10) para gestão interna de inventário — reescrita web do sistema desktop GBS Inventory (VB.NET/WinForms). Autenticado (login obrigatório), uso interno da equipe, não é a vitrine pública (essa é o `GBS_App`, projeto e repositório separados, mesmo banco Oracle).

Em produção: `http://129.80.215.94/` — ver [`DEPLOY_LOG.md`](DEPLOY_LOG.md) pro histórico completo da migração e [`docs/DEPLOY.md`](docs/DEPLOY.md) pro passo a passo de infraestrutura.

## Stack

- **Blazor Server** (.NET 10, `net10.0`), render mode `InteractiveServer`.
- **Oracle** via `Oracle.ManagedDataAccess.Core` — Autonomous Database em produção (TLS sem wallet, ver `Data/OracleConnectionFactory.cs`), Oracle XE local em dev.
- **Autenticação**: cookie-based via `Microsoft.AspNetCore.Authentication.Cookies`, credenciais em `TBL_APP_USER` (hash PBKDF2 via `PasswordHasher`).
- **Relatórios**: PDF via `itext7`, Excel via `ClosedXML`.
- **Deploy**: Docker (multi-stage, multi-arch `amd64`+`arm64`) → GHCR via GitHub Actions → VM Oracle Cloud (Always Free) atrás de Caddy (reverse proxy).

## Estrutura

```
GBS_Web/
├── Components/
│   ├── Pages/            # uma página Razor por tela (rota via @page, ver tabela abaixo)
│   └── Layout/            # MainLayout, NavMenu
├── Data/                  # um *Repository.cs por tabela/domínio — SQL puro ou PACK_* (PL/SQL do desktop)
├── Services/               # geradores de relatório (PDF/Excel) e importação de planilha
├── Models/                 # POCOs — Equipamento, Cliente, Invoice, Component, Upgrade, Remessa, etc.
├── docs/DEPLOY.md          # passo a passo de infra (VM, Caddy, Docker, secrets)
├── DEPLOY_LOG.md           # histórico de decisões da migração pra produção
├── Dockerfile
└── docker-compose.yml      # referência/documentação — não é o caminho de validação (ver comentário no arquivo)
```

## Como rodar localmente

```powershell
cd C:\GBS\GBS_Web
$env:GBS_ORACLE_TARGET="LOCAL"          # ou omitir — LOCAL é o default
$env:GBS_ORACLE_PASSWORD="<senha do gbs_owner no Oracle XE local>"
$env:GBS_APP_PASSWORD="<senha do usuário admin — só usada no seed inicial>"
dotnet run
```

Login em `/login`. `GBS_APP_PASSWORD` só é lida se `TBL_APP_USER` estiver vazia (seed do usuário `admin` no primeiro startup) — depois disso a senha vive só no hash salvo no banco, trocar exige `UPDATE` direto na tabela (sem fluxo de "esqueci a senha" ainda).

Variáveis de ambiente (`Data/OracleConnectionFactory.cs`):
- `GBS_ORACLE_TARGET` — `LOCAL` (default) | `ADB_TLS` | `ADB_WALLET`.
- `GBS_ORACLE_PASSWORD` — senha do `gbs_owner`, obrigatória.
- `GBS_ORACLE_CONNECTION_STRING` — opcional, sobrescreve a connection string inteira.
- `GBS_APP_PASSWORD` — senha do usuário admin, só usada no seed inicial.

## Páginas principais

| Rota | Página | O que é |
|---|---|---|
| `/` | Dashboard | Métricas gerais (total de unidades, em estoque, boas condições, breakdowns) — cards clicáveis levam pra página de origem dos dados. |
| `/inventory` | Inventory | CRUD de equipamentos, busca/filtro por status, contador de unidades quando filtrado, import de planilha, relatórios PDF/Excel. |
| `/inventory/{id}/history` | History | Histórico de um equipamento — chegada, upgrades e envios, com totais. |
| `/shipments` | Shipments | Gestão de remessas (`TBL_REMESSA`), status, tracking, relatórios PDF/Excel. |
| `/upgrades` | Upgrades | Registro de upgrades de hardware por equipamento, relatório Excel por cliente. |
| `/clients` | Clients | CRUD de clientes (`TBL_CLIENTE`). |
| `/invoices` | Invoices | Faturas, status, download em PDF. |
| `/components` | Components | Estoque de componentes avulsos, relatório PDF/Excel (individual e resumo). |
| `/import` | Import | Importação de planilha Excel de equipamentos, com grid de análise de qualidade e relatórios PDF/Excel. |
| `/backup` | Backup | Disparo manual de backup e histórico de execuções (`TBL_BACKUP_LOG`). |
| `/login` | Login | Autenticação. |

Todas as páginas (exceto `/login`) exigem `[Authorize]`.

## Endpoints de relatório (PDF/Excel)

| Rota | Método | Origem |
|---|---|---|
| `/invoices/{id}/pdf` | GET | Invoices |
| `/shipments/{remessaRef}/pdf`, `/excel` | GET | Shipments |
| `/inventory/report/pdf`, `/excel` | GET | Inventory (respeita `search`/`status` ativos) |
| `/components/report/pdf`, `/excel` | GET | Components |
| `/components/summary/pdf`, `/excel` | GET | Components (agregado) |
| `/upgrades/report/excel` | GET | Upgrades (parâmetro `days`, default 90) |
| `/inventory/{id}/history/pdf`, `/excel` | GET | History |
| `/import/quality/pdf`, `/excel` | GET | Import (parâmetros `uids`, `file`) |

Todos com `.RequireAuthorization()`.

## Deploy

Ver [`docs/DEPLOY.md`](docs/DEPLOY.md) para o passo a passo completo (VM, Caddy, Docker, secrets) e [`DEPLOY_LOG.md`](DEPLOY_LOG.md) para o histórico de decisões e verificações de cada fase da migração.
