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

## Fase E — Portação de relatórios (ReportService.vb → GBS_Web) ✅ Concluída e em produção

Portados todos os relatórios de `Utils/ReportService.vb` (GBS_Inventory desktop) pro GBS_Web, um de cada vez, com `dotnet build` (0 erros) depois de cada item. Além do `ReportService.vb`, também portados os relatórios de `Views/frmHistoricoEquipamento.vb` (histórico do equipamento) e `Views/frmImportacao.vb` (qualidade da importação), conforme pedido. Padrão seguido em todos: PDF via itext7 (paleta de cores igual ao `InvoicePdfGenerator.cs`), Excel via ClosedXML, endpoint `GET /{rota}/{id}/pdf` + `/excel` com `.RequireAuthorization()`, botão de download na página correspondente.

**Deploy (19/09/2026):** commit `dcc33f6`, push pro `master`, GitHub Actions (`docker-build.yml`, run #12) build multi-arch com sucesso, `deploy.sh` rodado contra a VM — pull da imagem nova, container `gbs_web` recriado, health check local (`HTTP 200` em `/login` de dentro da VM) e externo (`HTTP 200` em `http://129.80.215.94/login`) confirmados. Não foi possível clicar nos botões novos em produção nesta sessão (sem a senha de produção, guardada só no arquivo local do usuário) — recomendado um clique manual de conferência (ver "Pendência" no fim desta seção).

### Item 0 — Shipment Report (`Shipments.razor`)

- Novo: `Services/ShipmentReportGenerator.cs`. Endpoints `/shipments/{remessaRef}/pdf` e `/excel`.
- `Data/RemessaRepository.GetItemsForReportAsync` — join direto `TBL_REMESSA_ITEM → TBL_REMESSA → TBL_EQUIPAMENTO` (SQL simples, sem tocar o `PACK_REMESSA`), porque `PROC_SELECT_ITENS` não retorna `SERIAL_NUMBER`/`CONDITION_STATUS`.
- `Models/ItemRemessa` ganhou `SerialNumber` e `ConditionStatus` (só preenchidos pelo relatório, não fazem parte do fluxo de criação de remessa).
- Botão "Track" por linha usando `Remessa.UrlRastreio` (já existia), abre em nova aba, oculto quando vazio.
- **Discrepância encontrada e corrigida:** o prompt original pedia 9 colunas incluindo "Notes", mas o `ReportService.vb` real (`GerarExcelRemessa`/`GerarPdfRemessa`) só tem 8 colunas — sem Notes. Implementado fiel à fonte real (8 colunas), não ao prompt.
- **Testado de ponta a ponta**: PDF e Excel verificados contra o Oracle local (conteúdo de célula/página conferido, não só HTTP 200).

### Item 1 — Inventory Report (`Inventory.razor`)

- Novo: `Services/InventoryReportGenerator.cs`. Endpoints `/inventory/report/pdf` e `/excel`, aceitam `?search=&status=`.
- Usa `EquipamentoRepository.GetAllAsync` — já retornava todos os campos necessários, sem mudança de schema/model.
- **Decisão de escopo:** o desktop tem 3 modos de seleção pro relatório (`ColetarItensRelatorio`: lista customizada via Ctrl+L/scanner → checkboxes marcados no grid → fallback "filtro atual"). Só o modo "filtro atual" foi portado (via querystring `search`/`status`, o mesmo filtro já ativo na página) — os outros dois exigem paradigmas de UI que não existem no GBS_Web e não foram pedidos explicitamente.
- **Testado de ponta a ponta**: PDF verificado visualmente (555 itens filtrados, 9 colunas, texto livre de Notes correto). Excel retornou HTTP 200 e tipo de arquivo válido, mas o conteúdo de célula não foi conferido linha a linha (diferente do Item 0).

### Item 2 — Components Report (`Components.razor`)

- Novo: `Services/ComponentReportGenerator.cs`. Endpoints `/components/report/pdf` e `/excel`.
- `Models/Component` e `ComponentRepository.GetAllAsync` já tinham as 12 colunas — nenhuma mudança de model/schema.

### Item 3 — Components Summary (`Components.razor`)

- Novo: `Services/ComponentSummaryReportGenerator.cs` + `Models/ComponentSummary` + `ComponentRepository.GetSummaryAsync`. Endpoints `/components/summary/pdf` e `/excel`.
- Query de agregação nova (SQL simples, `GROUP BY` tipo/capacidade/geração/velocidade/CPU/storage com `COUNT`/`SUM CASE`) — porta exata de `clsReadComponent.selectSummary()`, sem proc equivalente no pacote.
- Cabeçalho verde (RGB 16,185,129) nas 5 colunas numéricas, igual ao desktop.

### Item 4 — Upgrades por cliente (`Upgrades.razor`)

- Novo: `Services/UpgradeClientReportGenerator.cs` (**só Excel** — o desktop não tem versão PDF desse relatório) + `Models/UpgradeReportRow` + `UpgradeRepository.GetUpgradesForClientReportAsync`. Endpoint `/upgrades/report/excel?days=N`.
- **Achado importante:** "CUSTOMER" não é um campo próprio do upgrade nem do equipamento — é derivado via `LEFT JOIN` até `TBL_REMESSA.DESTINATARIO` (o destinatário da remessa mais recente daquele equipamento). Réplica exata da query do desktop (`clsLeituraUpgrade.selecionarUpgradesComCliente`).
- Uma aba Excel por cliente (nome sanitizado, máx. 31 caracteres), "Unassigned" quando não há remessa associada.

### Item 5 — Equipment History (novo `History.razor`)

- Lido `Views/frmHistoricoEquipamento.vb` e `classes/clsLeituraHistorico.vb` por completo. Optado por uma página nova (`History.razor`, rota `/inventory/{id}/history`) em vez de embutir no `Inventory.razor` — o desktop já trata isso como uma tela própria (`frmHistoricoEquipamento`), com 3 seções (Arrival/Upgrades/Shipments) e totais.
- Novos: `Data/HistoryRepository.cs`, `Models/EquipmentHistory.cs` (`HistoryUpgradeRow`/`HistoryShipmentRow`), `Services/HistoryReportGenerator.cs`, `EquipamentoRepository.GetByIdAsync`. Endpoints `/inventory/{id}/history/pdf` e `/excel`. Link "History" adicionado em cada linha do `Inventory.razor`.
- **Mudança de formato deliberada:** o desktop gera esse relatório como HTML salvo com extensão `.doc` (aberto via `Process.Start`) — aqui virou PDF real (itext7) / Excel real (ClosedXML), consistente com todos os outros relatórios desta fase.
- **Correção de drift do próprio desktop:** `clsLeituraHistorico.selecionarShipmentEquipamento` tenta (e falha, caindo no fallback) colunas `RECIPIENT_NAME`/`ESTIMATED_DELIVERY`/`ACTUAL_DELIVERY` em `TBL_REMESSA` — essas colunas não existem; as reais já confirmadas em uso no GBS_Web são `DESTINATARIO`/`DATA_ENVIO`/`DATA_ENTREGA`. Usei as reais direto, sem replicar a cascata de tentativa/erro do desktop.
- **Gap documentado:** o desktop tem um segundo relatório separado nessa tela, orientado ao cliente (`btnUpgradeReport`/"Hardware Upgrade Report", 6 colunas, sem custo/fonte/nº de série da peça). Não foi portado como download separado — a seção "2. Upgrades" do relatório de histórico já cobre um superconjunto dessas colunas (9 vs. 6).
- **Testado de ponta a ponta** (ver "Verificação" abaixo).

### Item 6 — Import Quality Report (`Import.razor`)

- Lido `classes/clsImportacaoExcel.vb` e `Controllers/ImportacaoController.vb` por completo — nenhum dos dois gera o relatório em si; a lógica real está em `Views/frmImportacao.vb` (`CarregarGridAnalise`/`MontarHtmlRelatorioQualidade`), que só foi encontrada depois de rastrear onde `ImportacaoController` é consumido.
- Novos: `Models/ImportQuality.cs` (`ImportAnalysisRow`), `EquipamentoRepository.GetForImportAnalysisAsync`, `Services/ImportQualityReportGenerator.cs`. Endpoints `/import/quality/pdf` e `/excel`, recebem `?uids=...&file=...` (lista de UIDs recém-importados, já que esse relatório depende do resultado de uma importação específica, não de um filtro estável).
- `Import.razor` agora guarda a lista de UIDs inseridos/atualizados a cada importação, busca a análise de qualidade (bateria FAIR/POOR, status IN_REPAIR, observação não vazia → "problema"), e mostra uma grade interativa com checkbox "Problem?" editável, filtro "Show issues only" e botões de download — réplica do grid do desktop.
- **Gap documentado:** o override manual do checkbox "Problem?" na tela só afeta a exibição em tela; o PDF/Excel exportado sempre usa o flag calculado pelo servidor (mesma limitação existiria de qualquer forma, já que o export é via endpoint HTTP stateless, não via estado do circuito Blazor).
- **Testado de ponta a ponta**, incluindo upload real de planilha via navegador (ver "Verificação" abaixo).

### Verificação — todos os itens testados ✅ (19/09/2026)

Servidor de dev local rodado pelo usuário com `GBS_ORACLE_PASSWORD` real; todos os 7 itens da Fase E testados de ponta a ponta (curl autenticado + inspeção de conteúdo de PDF/Excel célula-a-célula via `openpyxl`, mais um teste real de upload/import via navegador pro Item 6). Resultado: **todos passaram**, sem bugs na lógica nova desta fase.

| Item | Resultado | Como foi testado |
|---|---|---|
| 0 — Shipments | ✅ Passou (sessão anterior) | PDF/Excel conferidos célula a célula |
| 1 — Inventory | ✅ Passou (sessão anterior) | PDF conferido visualmente; Excel só HTTP 200 |
| 2 — Components | ✅ Passou | PDF+Excel conferidos, cruzados com a página `/components` (3 itens, 12 colunas batendo) |
| 3 — Components Summary | ✅ Passou | PDF+Excel conferidos, agregação (3 grupos, Total/In Stock corretos) batendo com o Item 2 |
| 4 — Upgrades por cliente | ✅ Passou | Excel com 2 abas ("Claude Code Test Recipient", "Unassigned"); cruzado o join CUSTOMER↔DESTINATARIO contra a página `/shipments` |
| 5 — Equipment History | ✅ Passou | Página + PDF + Excel do equipamento WEBTEST001 (id 668), as 3 seções (Arrival/Upgrades/Shipments) batendo entre si e com `/shipments`/`/upgrades` |
| 6 — Import Quality | ✅ Passou | Upload real de uma planilha de 2 linhas via navegador (Chrome), import executado, grid de análise conferido, botão "Generate Report (PDF)" clicado e conteúdo conferido |

**Achado (não é bug da Fase E, é gap pré-existente do pacote PL/SQL original):** a coluna "Batch" do relatório de Import Quality sempre aparece vazia pra equipamento inserido via importação de planilha, mesmo quando `ExcelImportService.cs` calcula corretamente `SourceBatch = nome da aba` antes de gravar. Causa raiz: `EquipamentoRepository.UpsertAsync` (usado só no fluxo de import em massa) chama `PACK_EQUIPAMENTO.PROC_UPSERT_EQUIPAMENTO`, e essa procedure — **desde o `03_PACK_EQUIPAMENTO_SPEC.sql`/`04_PACK_EQUIPAMENTO_BODY.sql` originais do desktop** — nunca teve parâmetro `P_SOURCE_BATCH` nem grava essa coluna no `INSERT`. Ou seja: isso nunca funcionou nem no desktop, pra equipamento inserido via importação de planilha (o form manual "+ Add Equipment"/`PROC_INSERT` grava `SOURCE_BATCH` normalmente — só o caminho de import em massa é afetado). Fase E não introduziu nem piorou isso; só ficou mais visível porque o novo relatório de qualidade tem uma coluna dedicada pra esse campo. **Não corrigido** — reportando conforme pedido, decisão de estender `PROC_UPSERT_EQUIPAMENTO` fica pra quando for explicitamente priorizado.

Dados de teste deixados no Oracle XE local (equipamento `QATEST-IMPORT-001`/`QATEST-IMPORT-002`, mesmo padrão dos dados de teste já existentes tipo `WEBTEST001`/`SCHEMATEST001`) — não removidos, por não ser prática estabelecida neste projeto limpar dados de teste do banco de dev local.

### Pendência — conferência manual em produção

Todo o teste de ponta a ponta acima foi feito contra o **Oracle XE local**. Em produção, o deploy foi validado só até o nível de infraestrutura (container no ar, health check HTTP 200) — ninguém clicou nos botões novos (Report PDF/Excel, Summary, History, Generate Report) em `http://129.80.215.94/` ainda, porque a sessão não tem a senha de admin de produção (fica só no arquivo local do usuário). Recomendo um clique de conferência rápido lá antes de considerar a Fase E 100% fechada.

### Novos endpoints (Fase E)

| Rota | Método | Página |
|---|---|---|
| `/shipments/{remessaRef}/pdf`, `/excel` | GET | Shipments |
| `/inventory/report/pdf`, `/excel` | GET | Inventory |
| `/components/report/pdf`, `/excel` | GET | Components |
| `/components/summary/pdf`, `/excel` | GET | Components |
| `/upgrades/report/excel` | GET | Upgrades |
| `/inventory/{id}/history` | página | Inventory → History |
| `/inventory/{id}/history/pdf`, `/excel` | GET | History |
| `/import/quality/pdf`, `/excel` | GET | Import |

Todos com `.RequireAuthorization()`.

## Notas operacionais — Always Free (custo e inatividade)

Recursos Always Free (VM e Autonomous DB) não geram custo independente de uptime. Riscos são só de **reclamação por inatividade** (não cobrança): VM reclamável se CPU/rede/memória ficarem abaixo de 20% por 7 dias seguidos; Autonomous DB para sozinha após 7 dias sem conexão (reversível) e pode ser deletada após 90 dias corridos parada. Uso ativo do projeto mantém ambos fora de risco.

## Próximos passos

- Registrar um domínio e apontar (registro DNS tipo A) pro IP `129.80.215.94`.
- Trocar o `Caddyfile` de `:80` pro domínio — Let's Encrypt emite certificado automaticamente, sem mais nenhuma configuração manual.
- (Opcional/limpeza) confirmar se existe duplicidade de arquivo `DEPLOY_LOG.md` na raiz do repo vs. em `docs/`, e consolidar num só caminho.
- **Fase E:** conferência manual dos botões novos em produção (ver "Pendência" acima) — infraestrutura validada, cliques ainda não.
- (Opcional, separado da Fase E) avaliar se vale estender `PACK_EQUIPAMENTO.PROC_UPSERT_EQUIPAMENTO` com `P_SOURCE_BATCH`, já que hoje toda importação em massa perde essa informação — tanto no desktop quanto no GBS_Web.

---
*Última atualização: 19/09/2026 — Fase E (portação de relatórios) testada de ponta a ponta localmente, commitada (`dcc33f6`), pushed, e deployada em produção em http://129.80.215.94/ (build multi-arch #12, container recriado, health check OK).*
