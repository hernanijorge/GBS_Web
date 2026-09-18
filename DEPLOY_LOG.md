# GBS_Web — Log de Deploy na Oracle Cloud (OCI)

Histórico técnico da migração do GBS_Web (Blazor Server) do ambiente local pra produção na Oracle Cloud Always Free. Mantido como referência — atualizar a cada fase concluída.

**Nota de processo:** este arquivo é mantido pela sessão Cowork (nuvem), com base nos relatos da sessão local (Claude Code CLI em `C:\GBS\GBS_Web`, que executa de fato o trabalho técnico via SSH/terminal). Evita duas sessões commitando neste mesmo arquivo ao mesmo tempo.

---

## Status atual: 🟢 GBS_Web em produção

**http://129.80.215.94/** — login `admin`, senha em arquivo local (nunca impressa em chat), gerada na correção do item 5 da Fase D abaixo.

---

## Fase A — Autenticação real ✅

- Substituído o login fixo via variável de ambiente por autenticação real baseada em banco.
- Tabela `TBL_APP_USER`, hashing de senha via `PasswordHasher` do ASP.NET Core (PBKDF2).
- Seed de admin no primeiro run (bootstrap).

## Fase B — Docker/CI ✅

- `Dockerfile` multi-stage + workflow `.github/workflows/docker-build.yml`.
- Build e push automático pro GHCR (`ghcr.io/hernanijorge/gbs_web`) a cada push na `master`, com tags `latest` + SHA do commit.
- `.env.docker` confirmado fora do controle de versão.
- Validado via GitHub Actions. Imagem publicada em `ghcr.io/hernanijorge/gbs_web:latest`.
- **Correção posterior (Fase D):** o workflow só buildava `linux/amd64`; a VM é `arm64` e o `docker pull` falhava com "no matching manifest". Corrigido adicionando QEMU + build multi-arch no workflow.

## Infraestrutura OCI provisionada

- Tenancy Always Free: `hernanijorge1`, home region **US East (Ashburn)**.
- **Autonomous Database "GBS"** — Always Free, workload Transaction Processing.
- **VM ARM A1.Flex** (`gbs-web-vm`) — 2 OCPU / 12GB RAM, Oracle Linux 9, IP público `129.80.215.94`. Acesso SSH confirmado via usuário `opc`.
- VCN `vcn-20260917-2045` / Subnet `subnet-20260917-2042`, Default Security List com ingress liberado pra TCP 80 e 443 (`0.0.0.0/0`).

## Decisão de conexão com o Autonomous DB

Optado por **TLS sem wallet** (autenticação de servidor apenas) em vez do caminho tradicional wallet-based (mTLS), já que o projeto usa `Oracle.ManagedDataAccess.Core` (suporta TLS-only a partir de ODP.NET 19.14+/21.5+).

TNS service usado: `gbs_tp` (Transaction Processing, uso geral de aplicação).

## Passo 0 — Migração de schema e dados ✅ (com um gap decidido conscientemente)

### Descoberta de drift

Os 6 scripts originais (`01_DDL_Tabelas.sql` a `06_PACK_REMESSA.sql`) estavam desatualizados em relação ao schema live: o schema em produção evoluiu por 25 scripts (patches incrementais), incluindo mudanças de coluna não refletidas nos scripts base.

### Abordagem adotada

Schema extraído **diretamente do estado live** via `DBMS_METADATA.GET_DDL`, garantindo fidelidade 100% ao que estava rodando em produção. Bug de driver contornado: `Oracle.ManagedDataAccess.Core` 23.26 não busca CLOB desse Oracle 10g — workaround via `DBMS_LOB.SUBSTR` em chunks de `VARCHAR2`.

### Resultado da migração de schema

| Objeto | Quantidade | Status |
|---|---|---|
| TABLE | 12 | VALID |
| SEQUENCE | 12 | VALID |
| PACKAGE | 6 | VALID |
| PACKAGE BODY | 6 | VALID |

Zero objetos inválidos.

### Bloqueios de rede resolvidos (ACL + mTLS)

1. **ACL bloqueando conexão** (`ORA-12506`) — resolvido com regra CIDR `0.0.0.0/0` na Access Control List do Autonomous DB.
2. **mTLS obrigatório** — trocado pra "Not required" (Network → Mutual TLS authentication) pra permitir TLS-only.

### Migração de dados — tabelas de negócio

| Tabela | XE (origem) | Autonomous DB (destino) | Status |
|---|---|---|---|
| TBL_EQUIPAMENTO | 670 | 670 | OK |
| TBL_EQUIPAMENTO_UPGRADE | 21 | 21 | OK |
| TBL_REMESSA | 3 | 3 | OK |
| TBL_REMESSA_ITEM | 6 | 6 | OK |
| TBL_IMPORTACAO_LOG | 0 | 0 | OK |
| TBL_WHATSAPP_LOG | 0 | 0 | OK |

IDs preservados; sequences ajustadas. Teste funcional (`PACK_EQUIPAMENTO.PROC_SELECT_UID`) confirmou schema + packages + dados funcionando juntos.

### Gap decidido conscientemente

`TBL_CLIENTE`, `TBL_INVOICE`, `TBL_INVOICE_ITEM`, `TBL_COMPONENT`, `TBL_BACKUP_LOG`, `TBL_APP_USER` — schema criado, dados de teste (13 linhas, "ACME TEST CORP") descartados deliberadamente.

## Fase C — Conexão com Autonomous DB via TLS ✅

- `OracleConnectionFactory.cs` com 3 modos via `GBS_ORACLE_TARGET`: `LOCAL` (padrão), `ADB_TLS` (validado), `ADB_WALLET` (documentado, não testado).
- Validação end-to-end: app inteira rodando com `GBS_ORACLE_TARGET=ADB_TLS`, seed rodando query real, login via curl, Dashboard batendo com os números do Passo 0.

### Achados de segurança tratados na Fase C

1. **Chave SSH privada solta no repo** (`ssh-key-2026-09-18.key`) — confirmado via `git log --all --full-history` que nunca foi commitada. Movida pra `C:\Users\herna\.ssh\`, ACL restritiva (read-only) preservada via `icacls`. `.gitignore` corrigido pra `*.key`/`*.pem`/`*.ppk`.
2. **Senha do ADMIN do Autonomous DB exposta em screenshot** — rotação recomendada. Lição aplicada: segredos via `Read-Host -AsSecureString`, nunca digitados visíveis em tela.

### Organização do repositório local

GitHub Desktop estava acompanhando um clone antigo (`C:\Users\herna\Desktop\GBS_Web`) em vez do repo real (`C:\GBS\GBS_Web`). Corrigido apontando pro repo certo.

## Fase D — Deploy na VM ✅ Concluída (GBS_Web em produção)

1. **Docker instalado**: Docker CE 29.8.1, ARM64, via repositório oficial. Testado com `hello-world`.
2. **Firewall liberado**: `firewalld` (http+https) na VM + Security List da OCI — validado com teste externo real (não só de dentro da VM).
3. **Bug de build corrigido**: o workflow do GitHub Actions só buildava `linux/amd64`; a VM é `arm64` (`docker pull` falhava com "no matching manifest"). Adicionado QEMU + build multi-arch; rebuild confirmado.
4. **Container em produção**: `ghcr.io/hernanijorge/gbs_web:latest`, secrets em `/etc/gbs_web/gbs_web.env` (`root:root`, `600`, fora do repo), porta exposta só em `127.0.0.1:8080` (não exposta direto na rede — só via Caddy).
5. **Bug de dado corrigido**: `TBL_APP_USER` já tinha uma linha `admin` seedada durante os testes da Fase C (senha de dev `gbs_web_dev`). Como a app só semeia se a tabela estiver vazia, a tentativa de senha de produção nova falhava silenciosamente no login. Corrigido atualizando o hash direto no banco pra uma senha de produção nova (gerada, guardada em arquivo local, nunca impressa em chat).
6. **Caddy instalado** como reverse proxy: `:80 → localhost:8080` (HTTP puro — sem domínio ainda, Let's Encrypt não pode emitir certificado pra IP). Documentado: trocar pra HTTPS automático é só substituir `:80` pelo domínio no `Caddyfile` assim que houver um registrado.
7. **Validação externa completa** (de fora da VM, não só localhost): login via curl + Dashboard retornando os dados corretos em `http://129.80.215.94/`.
8. **`deploy.sh` testado de verdade**: rodado em produção, fez pull + restart do container, site confirmado no ar depois.

## Notas operacionais — Always Free (custo e inatividade)

Recursos Always Free (VM e Autonomous DB) não geram custo independente de uptime. Riscos são só de **reclamação por inatividade** (não cobrança): VM reclamável se CPU/rede/memória ficarem abaixo de 20% por 7 dias seguidos; Autonomous DB para sozinha após 7 dias sem conexão (reversível) e pode ser deletada após 90 dias corridos parada. Uso ativo do projeto mantém ambos fora de risco.

## Próximos passos

- Registrar um domínio e apontar (registro DNS tipo A) pro IP `129.80.215.94`.
- Trocar o `Caddyfile` de `:80` pro domínio — Let's Encrypt emite certificado automaticamente, sem mais nenhuma configuração manual.
- (Opcional/limpeza) confirmar se existe duplicidade de arquivo `DEPLOY_LOG.md` na raiz do repo vs. em `docs/`, e consolidar num só caminho.

---
*Última atualização: 18/09/2026 — Fase D concluída. GBS_Web em produção em http://129.80.215.94/.*
