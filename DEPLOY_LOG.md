# GBS_Web — Log de Deploy na Oracle Cloud (OCI)

Histórico técnico da migração do GBS_Web (Blazor Server) do ambiente local pra produção na Oracle Cloud Always Free. Mantido como referência — atualizar a cada fase concluída.

---

## Fase A — Autenticação real ✅

- Substituído o login fixo via variável de ambiente por autenticação real baseada em banco.
- Tabela `TBL_APP_USER`, hashing de senha via `PasswordHasher` do ASP.NET Core (PBKDF2).
- Seed de admin no primeiro run (bootstrap).

## Fase B — Docker/CI ✅

- `Dockerfile` multi-stage + workflow `.github/workflows/docker-build.yml`.
- Build e push automático pro GHCR (`ghcr.io/hernanijorge/gbs_web`) a cada push na `master`, com tags `latest` + SHA do commit.
- `docker-compose.yml` mantido só como referência/documentação (não é o caminho de validação — sem Docker Desktop local, `host.docker.internal` não resolve em engine Linux puro).
- `.env.docker` confirmado fora do controle de versão.
- Validado via GitHub Actions (run com todos os steps `success`, incluindo login no GHCR e build+push). Imagem publicada em `ghcr.io/hernanijorge/gbs_web:latest`.

## Infraestrutura OCI provisionada

- Tenancy Always Free: `hernanijorge1`, home region **US East (Ashburn)**.
- **Autonomous Database "GBS"** — Always Free, workload Transaction Processing.
- **VM ARM A1.Flex** (`gbs-web-vm`) — 2 OCPU / 12GB RAM, Oracle Linux 9, IP público `129.80.215.94`. Acesso SSH confirmado via usuário `opc`.
- VCN `vcn-20260917-2045` / Subnet `subnet-20260917-2042`, com a Default Security List associada.

## Decisão de conexão com o Autonomous DB

Optado por **TLS sem wallet** (autenticação de servidor apenas) em vez do caminho tradicional wallet-based (mTLS), já que o projeto usa `Oracle.ManagedDataAccess.Core` (suporta TLS-only a partir de ODP.NET 19.14+/21.5+). Isso evita ter que gerenciar o arquivo do wallet dentro do container Docker na Fase D.

TNS service usado: `gbs_tp` (Transaction Processing, uso geral de aplicação).

## Passo 0 — Migração de schema e dados ✅ (com um gap decidido conscientemente)

### Descoberta de drift

Os 6 scripts originais (`01_DDL_Tabelas.sql` a `06_PACK_REMESSA.sql`) estavam desatualizados em relação ao schema live: o schema em produção evoluiu por 25 scripts (patches incrementais), incluindo mudanças de coluna não refletidas nos scripts base (ex: `TBL_EQUIPAMENTO_UPGRADE` mudou de `COMPONENT_TYPE/VALUE_BEFORE/VALUE_AFTER` pra `RAM_ANTERIOR_GB/RAM_NOVA_GB/STORAGE_ANTERIOR_GB/STORAGE_NOVO_GB/TIPO_UPGRADE`).

### Abordagem adotada

Em vez de tentar reconstruir a sequência de 25 patches, o schema foi extraído **diretamente do estado live** via `DBMS_METADATA.GET_DDL`, garantindo fidelidade 100% ao que estava rodando em produção.

**Bug de driver contornado:** `Oracle.ManagedDataAccess.Core` 23.26 não consegue buscar CLOB desse Oracle 10g (nem um `TO_CLOB('x')` trivial funciona — erro de wire protocol). Workaround: extração via `DBMS_LOB.SUBSTR` em chunks de `VARCHAR2`.

### Resultado da migração de schema

| Objeto | Quantidade | Status |
|---|---|---|
| TABLE | 12 | VALID |
| SEQUENCE | 12 | VALID |
| PACKAGE | 6 | VALID |
| PACKAGE BODY | 6 | VALID |

Zero objetos inválidos.

### Bloqueios de rede resolvidos (ambos exigiram ação manual no console OCI)

1. **ACL bloqueando conexão** (`ORA-12506: TNS:listener rejected connection based on service ACL filtering`) — resolvido adicionando regra CIDR `0.0.0.0/0` na Access Control List do Autonomous DB (Network → Access Control List → IP notation type: CIDR block).
2. **mTLS obrigatório** — o campo "Mutual TLS (mTLS) authentication" estava como "Required", o que bloqueava conexão TLS-only. Resolvido trocando pra "Not required" (Network → Edit ao lado de Mutual TLS authentication).

### Migração de dados — tabelas de negócio

| Tabela | XE (origem) | Autonomous DB (destino) | Status |
|---|---|---|---|
| TBL_EQUIPAMENTO | 670 | 670 | OK |
| TBL_EQUIPAMENTO_UPGRADE | 21 | 21 | OK |
| TBL_REMESSA | 3 | 3 | OK |
| TBL_REMESSA_ITEM | 6 | 6 | OK |
| TBL_IMPORTACAO_LOG | 0 | 0 | OK |
| TBL_WHATSAPP_LOG | 0 | 0 | OK |

IDs originais preservados (sem regenerar via sequence); sequences ajustadas pro próximo valor livre após a carga.

**Teste funcional (não só contagem):** chamada de `PACK_EQUIPAMENTO.PROC_SELECT_UID` no Autonomous DB pro equipamento `SCHEMATEST001` retornou `RAM_GB=24, STORAGE_GB=2000`, batendo com o estado final dos testes de Upgrades feitos anteriormente no XE. Confirma que schema + packages + dados funcionam corretamente juntos, não só que os dados foram copiados.

### Gap decidido conscientemente

As outras 6 tabelas do schema live (`TBL_CLIENTE`, `TBL_INVOICE`, `TBL_INVOICE_ITEM`, `TBL_COMPONENT`, `TBL_BACKUP_LOG`, `TBL_APP_USER`) tiveram o **schema criado** no Autonomous DB, mas os **dados não foram migrados** — eram 13 linhas de dado de teste (cliente "ACME TEST CORP", 1 invoice, 3 componentes, histórico de backup), identificadas como dado de desenvolvimento e descartadas deliberadamente antes de apontar o GBS_Web pro banco de produção novo.

## Fase C — Conexão com Autonomous DB via TLS ✅

- `OracleConnectionFactory.cs` suporta 3 modos via variável de ambiente `GBS_ORACLE_TARGET`:
  - `LOCAL` (padrão, comportamento inalterado)
  - `ADB_TLS` (novo, **validado**) — TLS sem wallet, mesmo formato de connect string usado com sucesso na migração do Passo 0.
  - `ADB_WALLET` (documentado no código, **não testado**) — caminho alternativo caso mTLS seja reativado no futuro.
- `appsettings.json` ganhou `AdbHost` / `AdbServiceName` / `AdbTnsAlias` (endpoint/nome de serviço, não são segredos).
- Desvio consciente do plano original (que previa wallet/mTLS): como o mTLS foi desabilitado no Passo 0 pra resolver o bloqueio de conexão, o modo TLS-only (`ADB_TLS`) virou o caminho real e testado.

**Validação end-to-end** (aplicação inteira, não só uma query isolada):
- GBS_Web completo rodando com `GBS_ORACLE_TARGET=ADB_TLS` numa porta separada (5081).
- Startup limpo — seed do `TBL_APP_USER` rodou query real contra o Autonomous DB sem erro.
- Login via `curl` (independente de navegador) → sucesso.
- Dashboard retornou os dados migrados corretos: 661 em estoque, 595 em boa condição, 19 upgrades (30d) — batendo exatamente com os números do Passo 0.

Commit e push feitos (`fca789a`).

### Achados de segurança tratados durante a Fase C

1. **Chave SSH privada solta no repo**: `ssh-key-2026-09-18.key` (par de chaves da VM, gerado preparando a Fase D) estava na raiz do repositório, fora do `.gitignore`.
   - `.gitignore` corrigido pra proteger `*.key`/`*.pem`/`*.ppk`.
   - Verificação de histórico: `git log --all --full-history` pros nomes de arquivo e por extensão, em todos os 8 commits/todas as refs, incluindo `git stash list` — **nenhum arquivo de chave jamais foi commitado**. Risco confirmado como baixo (nunca exposto no remoto).
   - Mesmo assim, movido por boa prática pra `C:\Users\herna\.ssh\ssh-key-2026-09-18.key`, fora do repo. A chave tinha ACL restritiva (`Read, Synchronize` só pro usuário, sem Write/Delete — hardening equivalente a `chmod 600`) que bloqueou até o `Move-Item` do próprio usuário; resolvido via `icacls` (grant temporário de Full Control → move → reaplicação da mesma restrição de leitura no novo local).
2. **Senha do ADMIN do Autonomous DB exposta em screenshot** (ocorrido durante o Passo 0, registrado aqui por continuidade): apareceu em texto puro num terminal PowerShell capturado em print. Rotação de senha recomendada. Lição aplicada: gravar segredos em arquivo lido programaticamente via `Read-Host -AsSecureString`, nunca digitado visível em tela.

### Organização do repositório local

Identificada duplicidade de pastas: o GitHub Desktop estava acompanhando `C:\Users\herna\Desktop\GBS_Web` (clone antigo/parado), enquanto o desenvolvimento real (via Claude Code CLI) acontece em `C:\GBS\GBS_Web` — por isso os commits não apareciam no GitHub Desktop. Corrigido apontando o GitHub Desktop pra `C:\GBS\GBS_Web` (Add Existing Repository) e removendo a pasta antiga da lista (sem apagar do disco).

## Fase D — Deploy na VM (em andamento)

- **Networking liberado**: duas Ingress Rules adicionadas na Default Security List da subnet (`subnet-20260917-2042`, VCN `vcn-20260917-2045`) — `0.0.0.0/0` TCP porta 80 e `0.0.0.0/0` TCP porta 443.
- **Decisão de domínio/HTTPS**: ainda não há domínio apontando pro IP `129.80.215.94`. Como o Let's Encrypt (HTTPS automático via Caddy) exige domínio real (não emite certificado pra IP puro), decidido rodar HTTP por enquanto — a troca pra Let's Encrypt fica documentada como pendência pra quando houver um domínio registrado.
- Próximo: instalar Docker na VM ARM (`129.80.215.94`, usuário `opc`, SSH confirmado) e publicar a imagem `ghcr.io/hernanijorge/gbs_web:latest` já disponível no GHCR, com Caddy servindo HTTP.

## Notas operacionais — Always Free (custo e inatividade)

Recursos Always Free (VM e Autonomous DB) não geram custo independente de uptime. Únicos riscos são de **reclamação por inatividade**, não de cobrança:
- VM: reclamável se CPU, rede e memória ficarem todos abaixo de 20% por 7 dias seguidos.
- Autonomous DB: para automaticamente após 7 dias sem conexão (reversível, restart manual); se ficar parada por 90 dias corridos sem restart, pode ser deletada permanentemente.
- Uso ativo do projeto (como o atual, quase diário) mantém ambos fora de risco.

## Próximos passos

- **Fase D** — concluir instalação do Docker na VM + Caddy servindo o container em HTTP; migrar pra HTTPS via Let's Encrypt assim que houver domínio.

---
*Última atualização: 18/09/2026 — Fase C concluída, Fase D em andamento (networking liberado).*
