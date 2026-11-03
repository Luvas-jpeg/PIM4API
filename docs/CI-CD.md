# CI/CD do PIM4API

O workflow `.github/workflows/dotnet.yml` valida .NET 10 com PostgreSQL 17 descartável em PRs e pushes para `main`, por execução manual e semanalmente. O workflow CodeQL verifica C# e GitHub Actions.

## Verificações

- `Backend quality`: restore com auditoria NuGet de dependências diretas/transitivas, compilação, testes reais de concorrência, TRX, cobertura e publicação de artefato.
- Vulnerabilidades altas/críticas e falhas de consulta à auditoria impedem aprovação.
- `scripts/ci/check-test-results.py` rejeita suíte vazia, falhas, testes pulados ou ausência dos seis testes PostgreSQL. `PIM4_POSTGRES_TESTS=1` é definido pela pipeline.
- `Backend container`: build da imagem de deploy e execução contra um banco exclusivo de CI, com aplicação das migrations e consulta de saúde e catálogo.
- `Backend CI gate`: exige aprovação dos dois jobs anteriores. Use esse check como obrigatório na regra da branch `main` depois da primeira execução.

Relatórios TRX/cobertura (`backend-reports`) e aplicação publicada (`backend-publish`) são guardados por sete dias. Credenciais do PostgreSQL são apenas do serviço descartável; nenhum segredo ou conexão de produção é necessário. As imagens são construídas, sem envio a registry.

Actions usam SHAs verificados, checkout sem persistência de credenciais, permissões mínimas, timeout e cancelamento de execução antiga. Dependabot propõe atualizações semanais; não há merge automático.

## CD no Render

O `render.yaml` declara `autoDeployTrigger: checksPass`. Em serviços existentes, confira **Settings → Auto-Deploy → After CI Checks Pass**, ou sincronize o Blueprint quando for gerenciado por ele. Confira também a integração GitHub e a branch do serviço.

O Render reconstrói a imagem do commit aprovado, sem consumir os artefatos do GitHub. API e frontend têm deploys independentes; mantenha contratos compatíveis durante a atualização.

Configure proteção de `main` no GitHub para exigir PR e `Backend CI gate`; ajuste checks antigos se necessário. O Code scanning precisa estar disponível/habilitado para o CodeQL. Essas configurações remotas não são ativadas apenas pelos arquivos locais.

Após publicar, confira `/health/live`, `/health`, catálogo e o fluxo com frontend. Não execute a suíte de concorrência contra o banco do Render: ela foi projetada para bancos descartáveis de teste com permissão de criação.

Rollback de deploy não reverte migrations. Mantenha backup e compatibilidade de schema antes de mudanças estruturais.

## Validação local e limites

Os workflows passaram no actionlint; a sintaxe dos scripts foi conferida e o verificador de TRX foi exercitado com cenários de aprovação/rejeição. SDK .NET e Docker não estão disponíveis neste computador; a primeira execução no GitHub precisa confirmar os testes e os containers. Nenhum deploy foi iniciado e nenhuma regra remota foi alterada.

O [guia conjunto no frontend](../../PIM4Front/docs/CI-CD.md) detalha os dois pipelines, os artefatos, a ativação no Render e a homologação. O E2E completo de compra/matrícula segue separado dos testes de container.

Referências: [Render e checks de CI](https://render.com/docs/deploys#integrating-with-ci), [Blueprint](https://render.com/docs/blueprint-spec#autodeploytrigger), [auditoria NuGet](https://learn.microsoft.com/en-us/nuget/concepts/auditing-packages).
