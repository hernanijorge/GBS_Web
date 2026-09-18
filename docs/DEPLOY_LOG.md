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
- **VM ARM A1.Flex** — 2 OCPU / 12GB RAM, Oracle Linux 9, IP público `129.80.215.94`. Acesso SSH confirmado via usuário `opc`.

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

## Nota de segurança

Durante o processo, a senha do usuário ADMIN do Autonomous Database apareceu momentaneamente em texto puro num terminal PowerShell capturado em screenshot compartilhado. Recomendada (e presumida feita) a rotação dessa senha no console OCI logo em seguida. Lição registrada: gravar segredos em arquivo local lido programaticamente (nunca digitado/colado visível em tela), preferencialmente via `Read-Host -AsSecureString`.

## Próximos passos

- **Fase C** — conectar o GBS_Web ao Autonomous DB via TLS sem wallet (connection string `OracleCloud` em `appsettings.json`, via variável de ambiente). Bloqueios de rede já resolvidos; falta validar a aplicação em si rodando contra o banco na nuvem.
- **Fase D** — instalar Docker na VM ARM (`129.80.215.94`, usuário `opc`, SSH confirmado) e publicar a imagem `ghcr.io/hernanijorge/gbs_web:latest` já disponível no GHCR.

---
*Última atualização: 17/09/2026 — Passo 0 concluído.*
