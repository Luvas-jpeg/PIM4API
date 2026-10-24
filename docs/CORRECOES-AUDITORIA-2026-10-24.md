# Correções da auditoria — 24/10/2026

Este documento atualiza [a revisão de 17/10](AUDITORIA-COMPLETA-2026-10-17.md). A integração do provedor de pagamento permanece fora desta etapa, conforme solicitado. Funcionalidades futuras da seção 14, como EAD comercial e checkout nativo, não foram incorporadas ao escopo presencial.

## Regra de compra confirmada pelo usuário

O aluno pode comprar vários cursos diferentes no mesmo pedido, com **uma vaga por curso para ele próprio**. Não há compra para terceiros nesta versão.

- Carrinho e API aplicam limite de uma vaga.
- Interface informa uma vaga para o próprio aluno e permite adicionar outros cursos.
- Pedido pendente ou matrícula não cancelada na mesma turma/curso bloqueia nova compra duplicada.
- Cancelamento/expiração libera a possibilidade de nova compra; matrícula reembolsada não é reativada administrativamente.
- Dados antigos com matrículas múltiplas são preservados. As ações do admin enviam enrollmentId; chamadas antigas ambíguas são rejeitadas.

## Products: resultado do rastreamento

Não foram encontradas entidades, DbSets, endpoints ou clientes ativos de Products no backend, Angular, Flutter ou WPF. O vínculo atual é **CourseClasses.CourseId → Courses.Id**. Nenhuma dessas aplicações acessa diretamente o banco, exceto a API. A única coluna encontrada com Product no nome é __EFMigrationsHistory.ProductVersion, metadado da versão do EF Core, sem relação com produtos.

Referências históricas em migrations, documentação e teste de rejeição de carrinho antigo permanecem intencionalmente: não representam ligação ativa. As classes CSS product-card/product-grid foram renomeadas para course-card/course-grid. Não é necessário recriar Products.

A verificação se limita aos quatro projetos locais e ao PostgreSQL configurado neste ambiente. Não comprova a inexistência de um sistema externo desconhecido ou de outra instalação antiga em outro servidor.

## Correções implementadas

| Achado original | Resultado desta etapa |
| --- | --- |
| 13.1 Gateway | Adiado a pedido do usuário; sem cobrança/estorno externo |
| 13.2 Status geral de aluno e vagas | EnrollmentStateService compartilhado por status da turma, aluno geral e cancelamento no reembolso |
| 13.3 Transferência + reembolso | Liberação pela turma atual da matrícula; matrícula já cancelada não devolve vaga duas vezes |
| 13.4 Transições financeiras concorrentes | MutationTransaction com lock transacional PostgreSQL antes de ler estado de pedido/vaga/cupom |
| 13.5 Última vaga | Transferência, reativação e alteração de capacidade coordenadas com checkout e reembolso |
| 13.6 Idempotência/cupom | Consulta de chave dentro da seção serializada; cupom vinculado por ID e uso liberado no encerramento |
| 13.7 Proxy/rate limit | Forwarded headers antes de autenticação/limitador; apenas IPs/redes configurados são confiáveis |
| 13.8 Refresh concorrente | Uma renovação compartilhada no web; exclusão/versão de sessão no desktop; proteção contra resposta após logout |
| 13.9 Logout web | Revogação no servidor e limpeza local mesmo na falha; clientes novos informam refresh da sessão atual |
| 13.10 Quantidade/participantes | Uma vaga por curso e identificação explícita da matrícula no admin |
| 13.11 IDs de auditoria | AddCreatedAsync persiste entidade e auditoria na mesma transação, usando ID definitivo |
| 13.12 Dependências | Angular 21.2.25, CLI/build 21.2.28 e patch Undici; auditoria npm sem vulnerabilidades conhecidas na consulta final |
| 13.13 Homologação externa | Testes de cliente desktop adicionados; teste físico Android/outro Windows continua pendente |
| 13.14 Coluna de vagas | Apenas AvailableSeats persistida; alias antigo é somente compatibilidade em memória |
| 13.14 Coleção EAD | ValueComparer detecta mudanças internas em CompletedLessons |
| 13.14 Backup | Scripts de backup/restauração e ensaio em banco separado concluído |
| 13.14 Mobile | Git local inicializado, ignore de segredos e CI Flutter preparado; remoto/primeiro commit não publicados |

### Transações e concorrência

MutationTransaction usa pg_advisory_xact_lock no PostgreSQL para coordenar as mutações curtas de pedido, vaga e cupom. A proteção funciona entre instâncias da aplicação conectadas ao mesmo banco e é liberada no commit/rollback. Consultas permanecem concorrentes.

A escolha prioriza consistência nesta implantação. Ela serializa mutações comerciais entre si, reduzindo o paralelismo de escrita; não deve envolver chamadas externas de rede/gateway. Se o volume crescer, substituir por locks por recurso/ordem estável e testes equivalentes. SQLite dos testes comuns não simula essa proteção; os testes específicos PostgreSQL exercitam o banco real.

### Política de cupom

Para pedidos novos, UsageCount representa pedidos pendentes ou pagos que consomem a promoção. Cancelamento, expiração, recusa e reembolso liberam o uso uma vez. Edição administrativa não sobrescreve o contador com valor antigo enviado pelo navegador.

Orders.PromoCodeId preserva a identidade mesmo se o código textual mudar. Cupom referenciado não é excluído: deve ser desativado. Pedidos anteriores à migration mantêm o histórico textual e não recebem vínculo inferido, evitando alterar contadores legados por adivinhação.

### Logout

Os três clientes enviam o refresh token da sessão encerrada para POST /api/Auth/revoke. O próprio token opaco autentica apenas sua revogação, inclusive quando o JWT já expirou, sem expor se o token existia. A API revoga somente a sessão correspondente quando ele é informado; requisições antigas sem esse dado mantêm o comportamento de revogação geral por compatibilidade. JWT de acesso continua sujeito ao próprio prazo; não foi criada uma blacklist de acesso em tempo real.

## Banco e migration

Migration: **20261024135217_HardenEnrollmentAndReservations**.

- Remove a coluna duplicada VafasDisponiveis e conserva AvailableSeats.
- Adiciona Orders.PromoCodeId nullable, índice e FK com exclusão restrita.
- Não remove alunos, cursos, pedidos ou matrículas.
- Down recria a coluna legada e copia os valores de AvailableSeats.

Backup anterior à aplicação: `.local-backups/pim4-20261024-105725-de9c8197.dump`.
SHA256: `C90E43023E8FF8255C790607E1B5F322839F340B7E4C5F6CB875588327014648`.

O backup foi restaurado em banco temporário isolado. Foram conferidas as contagens de tabelas, cursos, pedidos, matrículas e migrations; o banco temporário foi removido. O banco original foi preservado.

~~~powershell
cd D:/Projetos/PIM4API
$backup = ./scripts/Backup-Database.ps1
./scripts/Test-DatabaseRestore.ps1 -BackupPath $backup
~~~

O segundo comando cria somente um banco com prefixo pim4_restore_test_, confere e remove esse banco de teste. Nunca restaura por cima do banco principal. Backups contêm dados pessoais e devem ter acesso restrito e cópia externa protegida. Agendamento/retenção na hospedagem definitiva ainda dependem da infraestrutura escolhida.

## Infraestrutura e distribuição

- PostgreSQL publicado somente em 127.0.0.1 no Compose local.
- Rede Compose definida em 172.31.245.0/24 para configurar confiança de proxy explicitamente.
- Frontend Nginx recebe API_UPSTREAM; padrão local http://api:8080, produção deve receber a URL HTTPS raiz da API.
- TLS do upstream HTTPS é verificado, com SNI habilitado.
- Front possui render.yaml próprio; API/banco mantêm blueprint no PIM4API.
- API possui /health para prontidão do banco e /health/live para processo; produção usa logs JSON e X-Request-ID.
- Configuração ReverseProxy:KnownProxies/KnownNetworks precisa refletir os proxies reais da hospedagem. Não usar 0.0.0.0/0; configuração rejeita rede universal.
- Node CI web alinhado à família 24 do Docker.
- CI API inclui PostgreSQL 17 e os testes concorrentes; desktop inclui os testes de cliente HTTP; mobile possui análise/testes/build debug.
- Desktop passou a aceitar horário local HH:mm e envia início convertido para UTC; release exige HTTPS público.
- Android possui key.properties.example e signing config própria; release sem chave configurada falha em vez de usar assinatura debug.

Os arquivos de configuração estão preparados; não houve deploy, criação de domínio, publicação na loja ou envio de código para remoto. Criar chave de assinatura e guardar backup dela é parte da distribuição Android.

## Limites e pendências externas

1. Provedor de pagamento, credenciais, cobranças e estornos reais continuam pendentes por decisão do usuário.
2. Domínios/hospedagem/TLS/segredos de produção e confiança nos proxies dependem dos endereços reais.
3. Notificação, boot e deep link precisam de Android físico/emulador; a prévia web não reproduz alarmes nativos.
4. WPF precisa de homologação no Windows do cliente.
5. Repositório remoto/mobile e cópia externa dos backups precisam de destino/autorização de publicação.
6. Alertas, retenção e agendamento de backup devem ser ativados no servidor definitivo.
7. Recuperação de senha/e-mail, certificado presencial e demais recursos futuros da auditoria continuam funcionalidades a planejar; não são implementações existentes escondidas por estas correções.

## Referências de configuração

- [ASP.NET Core: proxies confiáveis](https://learn.microsoft.com/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0).
- [Angular: atualizações](https://angular.dev/cli/update).
- [Flutter: assinatura Android](https://docs.flutter.dev/deployment/android).
## Resultados finais de 24/10/2026

| Verificação | Resultado |
| --- | --- |
| API, com PostgreSQL concorrente habilitado | **99 testes aprovados, nenhum pulado** |
| Frontend | **56 testes aprovados** |
| Cliente HTTP desktop | **2 testes aprovados** |
| Desktop Release | Compilação sem warnings/erros |
| Flutter analyze | Sem problemas |
| Flutter | **20 testes aprovados**, 1 LIVE_API opcional pulado |
| Flutter web profile | Compilado com código final |
| Android APK debug | Compilado com código final e configuração de assinatura separada |
| Docker API + frontend | Imagens compiladas e containers atualizados |
| E2E com atraso controlado | **1 cenário completo aprovado** (8,0 s de runner) |
| npm audit completo | **0 vulnerabilidades conhecidas** na consulta |
| npm audit omitindo dev | **0 vulnerabilidades conhecidas** na consulta |
| Banco Products | Ausente; endpoint antigo retorna 404 |
| CourseClasses.CourseId | FK Courses.Id, ON DELETE RESTRICT |
| Coluna duplicada de vagas | Ausente no schema final |
| Contadores de vagas fora de 0..capacidade | Nenhum no banco local conferido |
| API /health e /health/live | Respostas saudáveis |
| Nginx /api/courses/catalog | HTTP 200 |

O E2E utiliza uma conta administrativa fictícia; o curso de teste foi arquivado e a conta foi despromovida/revogou refresh ao finalizar. Não movimentou dinheiro nem modificou permissões de usuários reais.

No primeiro E2E desta etapa, o teste ainda procurava a classe CSS antiga product-card. O seletor foi atualizado para course-card e o cenário completo foi repetido com sucesso. Esse ajuste não exigiu recuperar Products.

Os builds web ainda emitem avisos de orçamento de CSS de componentes já existentes. O build Android emitiu aviso de versões de metadados do SDK, sem impedir compilação. Não foram tratados como falhas funcionais ou ocultados aumentando arbitrariamente os limites.

### Evidências atuais

- [Inventário técnico atualizado](INVENTARIO-TECNICO-2026-10-24.md) e [JSON](INVENTARIO-TECNICO-2026-10-24.json): **64 operações HTTP, 22 tabelas incluindo histórico EF e 168 colunas**.
- [Verificação do legado](VERIFICACAO-LEGADO-2026-10-24.json).
- [Auditoria npm completa](AUDITORIA-DEPENDENCIAS-WEB-2026-10-24.json) e [produção](AUDITORIA-DEPENDENCIAS-PRODUCAO-WEB-2026-10-24.json).
- Backup do schema/dados finais: .local-backups/pim4-20261024-111307-c4c19e64.dump, SHA256 9EA38DDAE33ABC4E15930F516A39A6850214249B6CD5D462E7BA57E50376D5ED.

### Reproduzir a concorrência no PostgreSQL

A suíte normal pula seis testes que exigem PostgreSQL. Para executá-los, configurar ConnectionStrings__DefaultConnection com acesso local de desenvolvimento capaz de criar bancos de teste e definir PIM4_POSTGRES_TESTS=1, depois executar dotnet test backend.Tests/backend.Tests.csproj a partir de PIM4API.

Esses testes criam bancos com nome único pim4_audit_test_ e removem somente os bancos criados por cada fixture. Não executam EnsureDeleted no banco principal. A pipeline CI usa PostgreSQL isolado e credencial exclusiva de teste.
O backup final também foi restaurado e conferido em banco isolado (24 migrations, 15 cursos, 9 pedidos e 9 matrículas). A revogação com header JWT inválido e refresh desconhecido retornou 204, sem exigir renovação do JWT nem revelar existência de sessão.
