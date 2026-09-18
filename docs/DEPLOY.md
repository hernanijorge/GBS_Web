# Deploy — Oracle Cloud VM (Fase D)

Passos manuais pra reproduzir (ou refazer do zero) o deploy do GBS_Web na VM Always Free da Oracle Cloud. Estado atual documentado em [`DEPLOY_LOG.md`](DEPLOY_LOG.md).

## Infraestrutura

- VM ARM A1.Flex, Oracle Linux 9, IP público `129.80.215.94`, usuário `opc`.
- Chave SSH em `~/.ssh/ssh-key-2026-09-18.key` (permissão read-only reaplicada via `icacls` após qualquer manuseio — nunca deixar essa chave dentro do repo).
- Imagem publicada em `ghcr.io/hernanijorge/gbs_web:latest` via CI (`.github/workflows/docker-build.yml`), multi-arch (`linux/amd64` + `linux/arm64` — a VM é arm64, então isso é obrigatório).

## 1. Abrir portas na Security List da OCI

No console: VM → Attached VNIC → Subnet → Security Lists → a lista associada → Add Ingress Rules:

- Source CIDR `0.0.0.0/0`, TCP, porta `80`
- Source CIDR `0.0.0.0/0`, TCP, porta `443`

(Porta `22` já vinha liberada por padrão.)

## 2. Instalar Docker na VM

```bash
sudo dnf config-manager --add-repo https://download.docker.com/linux/centos/docker-ce.repo
sudo dnf install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
sudo systemctl enable --now docker
sudo usermod -aG docker opc
```

## 3. Abrir portas no firewall do próprio Linux (firewalld)

```bash
sudo firewall-cmd --permanent --add-service=http
sudo firewall-cmd --permanent --add-service=https
sudo firewall-cmd --reload
```

## 4. Instalar Caddy (reverse proxy)

```bash
sudo dnf install -y 'dnf-command(copr)'
sudo dnf copr enable -y @caddy/caddy
sudo dnf install -y caddy
```

`/etc/caddy/Caddyfile`:

```caddyfile
:80 {
	reverse_proxy localhost:8080
}
```

```bash
sudo systemctl enable --now caddy
sudo systemctl reload caddy
```

### Trocar pra HTTPS automático (Let's Encrypt) quando houver domínio

Sem domínio ainda — rodando em HTTP puro no IP. Assim que um domínio tiver um registro DNS tipo A apontando pra `129.80.215.94`, troque a primeira linha do Caddyfile de `:80` pro domínio (ex: `gbs.exemplo.com`) e recarregue:

```bash
sudo systemctl reload caddy
```

Caddy cuida sozinho de emitir e renovar o certificado via ACME — nenhuma outra mudança é necessária.

## 5. Secrets — nunca no repositório

Arquivo `/etc/gbs_web/gbs_web.env` (root:root, permissão `600`), fora do controle de versão:

```
GBS_ORACLE_PASSWORD=<senha do usuário GBS_OWNER no Autonomous DB>
GBS_ORACLE_TARGET=ADB_TLS
GBS_APP_PASSWORD=<senha de login da app — só é usada pra seed inicial, se TBL_APP_USER já tiver linha isso é ignorado>
```

## 6. Rodar o container

```bash
sudo docker run -d --name gbs_web \
  --restart unless-stopped \
  --env-file /etc/gbs_web/gbs_web.env \
  -p 127.0.0.1:8080:8080 \
  ghcr.io/hernanijorge/gbs_web:latest
```

Porta do container exposta só em `127.0.0.1` — quem recebe tráfego externo é o Caddy na `:80`/`:443`, nunca o container diretamente.

## Redeploy (nova versão da imagem)

Ver [`deploy.sh`](../deploy.sh) — builda/aguarda a imagem no GHCR (via push na `master`, que já dispara o workflow) e depois faz pull + restart na VM via SSH.

## Nota sobre `TBL_APP_USER` e re-seed

A app só popula `TBL_APP_USER` automaticamente se a tabela estiver **vazia** no primeiro startup. Se quiser resetar a senha de login sem recriar o container do zero, é preciso fazer um `UPDATE` direto na tabela (não existe fluxo de "esqueci a senha" ainda — ver limitação já registrada na Fase A).
