# Contexto da primeira etapa - Plataforma de cursos

## Objetivo do projeto

O projeto e uma plataforma de cursos presenciais para web e mobile, com:

- Backend ASP.NET Core/.NET 10;
- Entity Framework Core 10;
- PostgreSQL 17;
- Frontend Angular 21;
- Docker Compose para PostgreSQL, API e frontend.

O sistema deixou de ser um ecommerce de equipamentos medicos. O escopo atual e somente cursos presenciais.

## Caminhos dos projetos

```text
Backend:  E:\Projetos\PIM4API
Frontend: E:\Projetos\PIM4Front
```

O antigo caminho `E:\Projetos\e-commerce` nao existe mais. Uma sessao antiga do terminal tentou iniciar nesse diretorio e apresentou `os error 267`. Esse erro significa que o diretorio de trabalho inicial e invalido; nao indica problema no Docker Engine.

## Arquitetura de dominio desejada

```text
Course
  |
  +-- CourseClass
        |
        +-- OrderItem
              |
              +-- Enrollment
                    +-- Student
                    +-- Order
```

Um curso pode possuir varias turmas. Cada turma possui data, horario, local, instrutor, capacidade e vagas disponiveis. O comprador deve selecionar uma turma antes de concluir o pedido.

## O que ja foi implementado no backend

### Entidades

- `backend\Models\Course.cs`
  - Entidade propria de curso.
  - Possui nome, descricao, preco, imagem, categoria, status ativo e `LegacyProductId`.
  - Mantem compatibilidade temporaria com `Product`.

- `backend\Models\CourseClass.cs`
  - Possui `CourseId` como relacao principal.
  - `ProdutoId` permanece nullable para compatibilidade com turmas legadas.
  - Possui data, data final, local, instrutor, capacidade, vagas disponiveis e status.

- `backend\Models\Enrollment.cs`
  - Relaciona explicitamente aluno, turma e pedido.
  - Possui `EnrolledAt` e `Status`.

- `backend\Models\OrderItem.cs`
  - Possui `TurmaId` nullable.

### Banco e Entity Framework

- `backend\Data\AppDbContext.cs`
  - Registra `DbSet<Course>`.
  - Configura relacoes entre cursos, turmas, produtos legados, matriculas, alunos e pedidos.
  - Mantem `Product` temporariamente.

- Migrations relevantes:

```text
20260828115348_AddOrderIdempotencyAndUniqueIndexes
20260828150000_DeleteEquipmentMockData
20260902125852_IntroduceCoursesAndExplicitClasses
20260904090000_AllowCourseClassesWithoutLegacyProduct
```

- `20260902125852_IntroduceCoursesAndExplicitClasses`
  - Cria a tabela `Courses`.
  - Migra produtos cujo `TipoProduto` e `course` para `Courses`.
  - Mantem os IDs dos produtos legados.
  - Cria `CourseId` em `CourseClasses`.
  - Cria campos de capacidade, vagas, data final e status.

- `20260904090000_AllowCourseClassesWithoutLegacyProduct`
  - Permite que uma turma exista sem `ProdutoId`.

- `20260828150000_DeleteEquipmentMockData`
  - Remove equipamentos mockados somente quando nao existem referencias em pedidos ou turmas.
  - O metodo `Down` e vazio por seguranca e nao recria os equipamentos.

## Endpoints de cursos implementados

Controller:

```text
backend\Controllers\CoursesController.cs
```

Endpoints:

```http
GET    /api/courses
GET    /api/courses/{id}
POST   /api/courses
PUT    /api/courses/{id}

GET    /api/courses/{courseId}/classes
GET    /api/courses/{courseId}/classes/{classId}
POST   /api/courses/{courseId}/classes
PUT    /api/courses/{courseId}/classes/{classId}

GET    /api/courses/{courseId}/classes/{classId}/students
```

Criacao e edicao exigem a role `Admin`. A consulta de alunos da turma tambem exige `Admin`.

O servico principal esta em:

```text
backend\Services\CourseService.cs
```

Ele valida:

- Nome e preco do curso;
- Data inicial e final da turma;
- Local e instrutor;
- Capacidade maior que zero;
- Nao reduzir capacidade abaixo das vagas ja reservadas.

## Fluxo atual de pedido, pagamento e matricula

1. O frontend cria um pedido com status `pending`.
2. Cada item envia `produtoId`, `turmaId` e `quantidade`.
3. Quando `turmaId` existe, o `InventoryService` valida se:
   - O produto e um curso;
   - A turma pertence ao curso;
   - A turma nao esta cancelada;
   - Existem vagas suficientes.
4. As vagas da turma sao reservadas durante a criacao do pedido.
5. O webhook de pagamento aprovado cria as matriculas.
6. Webhooks repetidos nao devem criar matriculas duplicadas.
7. Pagamentos recusados ou cancelados liberam as vagas.
8. Reembolsos cancelam as matriculas.

Arquivos principais:

```text
backend\Services\OrderService.cs
backend\Services\InventoryService.cs
backend\Services\EnrollmentService.cs
```

Ainda existe compatibilidade com o fluxo legado baseado em `Product`. A migracao completa para `Course` e `CourseClass` ainda nao terminou.

## O que ja foi implementado no frontend

### Curso e turma

- `src\app\core\models\course.models.ts`
  - Interfaces `Course` e `CourseClass`.

- `src\app\core\services\course.service.ts`
  - Consome os endpoints de cursos e turmas.

- `src\app\features\course-detail\course-detail.ts`
- `src\app\features\course-detail\course-detail.html`
  - A tela de detalhes consulta as turmas.
  - Exibe data, horario, local, instrutor e vagas.
  - Obriga a selecao de uma turma.
  - Impede inscricao quando nao existem turmas com vagas.

### Carrinho

- `src\app\core\services\cart.service.ts`
  - `CartItem` agora pode armazenar `courseClass`.
  - Cursos de turmas diferentes nao sao agrupados.
  - A quantidade respeita as vagas da turma.

- `src\app\features\cart\cart.ts`
- `src\app\features\cart\cart.html`
  - Mostram a turma selecionada.
  - Usam `turmaId` no item do pedido.

### Checkout

- `src\app\features\checkout\checkout.ts`
- `src\app\features\checkout\checkout.html`
  - Envia `turmaId` em cada item.
  - Bloqueia a finalizacao se algum curso estiver sem turma.
  - Continua aceitando `pix`, `debit_card` e `credit_card`.
  - Envia o header `Idempotency-Key`.

### Compatibilidade legada

O catalogo, o painel administrativo e parte dos contratos ainda usam `Product`:

```text
src\app\core\models\product.models.ts
src\app\core\services\product.service.ts
src\app\features\home\home.ts
src\app\features\admin\admin.ts
```

O backend filtra produtos para aceitar somente `TipoProduto = course`. O catalogo publico e os detalhes de curso ja usam `CourseService`; carrinho, checkout e painel continuam com compatibilidade gradual para `Product`.

## Retomada em 04/09/2026

- Build do backend validado com `dotnet build .\backend\backend.csproj --no-restore --nologo /p:UseSharedCompilation=false`.
- Testes do backend aprovados: 11 testes.
- Build do frontend aprovado com `npm run build`.
- Catalogo publico migrado de `ProductService` para `CourseService`.
- Detalhes publicos do curso agora carregam `Course` e suas turmas diretamente.
- Reserva de vagas ajustada para atualizacao condicional atomica no PostgreSQL, evitando decremento concorrente da mesma vaga.
- O Docker nao estava disponivel nesta maquina durante a validacao; nenhum volume foi alterado.
- Painel administrativo migrado para carregar e editar cursos pelo `CourseService`.
- Painel administrativo agora exibe uma entrada por turma real, com data, local, instrutor, capacidade e vagas.
- Consulta de alunos do painel migrada para `GET /api/courses/{courseId}/classes/{classId}/students`.
- Exclusao destrutiva de cursos foi bloqueada no frontend e o endpoint legado de produtos agora rejeita exclusao quando existe turma, curso ou historico de pedido.
- Painel administrativo agora permite criar e editar turmas usando os endpoints de classes do `CourseService`.
- O formulario de turma envia data inicial/final, local, instrutor, capacidade e status.
- Interceptor do frontend agora anexa corretamente o JWT e tenta renovar o access token uma vez ao receber `401`.
- O painel administrativo exibe mensagens especificas para sessao expirada, falta de autorizacao e erros de cadastro.
- Erros de criacao e edicao de cursos, turmas e cupons agora aparecem dentro do formulario/modal correspondente, sem ocupar o feedback global do painel.
- Formulario de cursos, turmas e cupons agora identifica campos obrigatorios com `*`, marca campos opcionais e usa atributos de acessibilidade `required`/`aria-required`.
- Regras de compra agora exigem turma explicita para cursos migrados para `Course`, preservando o fluxo legado de produtos de curso ainda nao migrados.
- Testes de negocio adicionados para reserva de vagas, compra sem turma e turma pertencente a outro curso.
- Testes do frontend atualizados para o catalogo baseado em `CourseService` e para o checkout com turma explicita.
- Checkout agora possui cobertura para envio de `turmaId` e bloqueio de cursos sem turma selecionada.
- Webhooks de recusa, cancelamento e reembolso liberam vagas; reembolsos cancelam matriculas e sao idempotentes.
- O arquivamento logico usa `Course.IsActive` para cursos e `CourseClass.Status` (`scheduled`, `completed` ou `cancelled`) para turmas, sem exclusao de historico.
- Etapa 2 iniciada: criado endpoint publico paginado `GET /api/courses/catalog` com busca, categoria, cidade/local, periodo, disponibilidade e ordenacao por data, preco ou nome.
- Catalogo do frontend passou a consumir o endpoint paginado e exibir filtros, ordenacao, estados de carregamento/erro e navegacao entre paginas.
- Cards do catalogo agora exibem a proxima turma, data, local e vagas disponiveis; cursos sem turma elegivel mostram aviso de indisponibilidade.
- Detalhes do curso agora consideram somente turmas agendadas, futuras e com vagas para selecao e compra.

## Painel administrativo atual

O painel ja foi reformulado para trabalhar somente com cursos:

- Cadastro de cursos em modal;
- Lista de cursos cadastrados;
- Edicao e arquivamento;
- Cards de turmas;
- Modal com alunos matriculados;
- Cadastro e edicao de cupons;
- Gerenciamento de pedidos.

Arquivos:

```text
src\app\features\admin\admin.ts
src\app\features\admin\admin.html
src\app\features\admin\admin.scss
```

O painel carrega alunos pelo endpoint especifico da turma:

```http
GET /api/courses/{courseId}/classes/{classId}/students
```

## Proximos passos recomendados

### 1. Validar o backend

Abrir uma nova sessao em `E:\Projetos\PIM4API` e executar:

```powershell
dotnet build .\backend\backend.csproj --no-restore --nologo
dotnet test .\backend.Tests\backend.Tests.csproj --no-restore --nologo
```

Os testes novos estao em:

```text
backend.Tests\CourseDomainTests.cs
```

Eles cobrem:

- Criacao de turma sem produto legado;
- Relacao explicita entre curso, turma e matricula.

### 2. Confirmar migrations no banco

Verificar se a tabela `Courses` existe e se as migrations abaixo estao registradas:

```text
20260828150000_DeleteEquipmentMockData
20260902125852_IntroduceCoursesAndExplicitClasses
20260904090000_AllowCourseClassesWithoutLegacyProduct
```

Nao usar `docker compose down -v`, pois isso apaga o volume do PostgreSQL.

### 3. Migrar completamente o frontend para cursos

- Criar catalogo baseado em `CourseService`;
- Remover dependencias de data/local/instrutor diretamente de `Product`;
- Fazer o admin criar `Course` e `CourseClass` separadamente;
- Permitir varias turmas por curso;
- Atualizar detalhes, carrinho e checkout para usarem contratos de curso.

### 4. Corrigir o painel de turmas

- Carregar turmas reais por curso;
- Exibir um card por turma ou agrupar turmas dentro do card do curso;
- Consultar alunos pelo endpoint especifico da turma;
- Mostrar capacidade, vagas restantes e status;
- Impedir exclusao destrutiva quando houver pedidos ou matriculas.

### 5. Reforcar concorrencia de vagas

O `InventoryService` atualmente decrementa vagas na mesma transacao do pedido, mas ainda deve ser validado para compras simultaneas em PostgreSQL. Avaliar:

- Lock de linha com consulta especifica do PostgreSQL;
- Atualizacao condicional atomica;
- Teste de duas compras disputando a ultima vaga.

### 6. Adicionar testes de negocio

Cobrir:

- Curso com varias turmas;
- Compra com turma explicita;
- Compra sem turma;
- Turma de outro curso;
- Compra da ultima vaga;
- Duas compras concorrentes;
- Cancelamento liberando vaga;
- Webhook repetido;
- Reembolso cancelando matricula;
- Matricula vinculada a turma correta.

## Docker e configuracao

Arquivos:

```text
E:\Projetos\PIM4API\docker-compose.yml
E:\Projetos\PIM4Front\Dockerfile
E:\Projetos\PIM4Front\.dockerignore
```

O frontend esta em um projeto separado e o compose usa o contexto relativo `../PIM4Front`.

Portas usadas anteriormente:

```text
API:      http://localhost:5278
Frontend: http://localhost:4200
Postgres: localhost:5432
```

Dentro do Docker, a API usa `Host=postgres`. Fora do Docker, comandos do Entity Framework devem usar `Host=localhost`.

A senha do PostgreSQL e definida quando o volume e criado. Alterar o `.env` nao altera a senha de um volume existente.

## Importante sobre a nova sessao

Ao iniciar uma nova sessao, informar:

```text
Leia E:\Projetos\PIM4API\CONTEXTO-PRIMEIRA-ETAPA.md e continue a primeira etapa da separacao entre cursos, turmas e matriculas.
```

Comecar validando o backend e o banco. Nao recriar o trabalho ja feito e nao apagar volumes do Docker.

---

# Roadmap completo de profissionalizacao

Este roadmap registra as etapas seguintes do projeto, suas metas, escopo tecnico,
dependencias e criterios de conclusao. A implementacao deve seguir a ordem
recomendada, preservando compatibilidade com pedidos e matriculas existentes.

## Etapa 1 - Consolidacao do dominio de cursos

### Objetivo

Finalizar a separacao entre catalogo de cursos, turmas presenciais, pedidos e
matriculas. O modelo legado `Product` deve continuar apenas como camada de
compatibilidade durante a transicao.

### Entregas

- Confirmar e aplicar as migrations existentes;
- Garantir a existencia e integridade das tabelas `Courses` e `CourseClasses`;
- Migrar cursos legados de `Products` para `Courses`;
- Garantir varias turmas para um mesmo curso;
- Exigir turma explicita na compra de cursos;
- Vincular cada `OrderItem` a uma turma;
- Criar `Enrollment` somente depois do pagamento aprovado;
- Impedir matricula duplicada;
- Liberar vagas em cancelamento, recusa ou reembolso;
- Adicionar arquivamento logico de cursos e turmas;
- Impedir exclusao destrutiva de dados com historico financeiro.

Implementacao concluida no codigo e coberta por testes. A validacao final de
migrations, constraints e concorrencia no PostgreSQL real permanece bloqueada
ate que o Docker Engine/ambiente PostgreSQL esteja disponivel, sem apagar o
volume existente.

### Criterios de conclusao

- Nenhum pedido novo de curso e criado sem `TurmaId`;
- A turma selecionada pertence ao curso comprado;
- A capacidade nunca fica negativa;
- A quantidade de matriculas corresponde a quantidade paga;
- Webhooks repetidos nao duplicam matriculas;
- Cursos cancelados ou inativos nao aparecem no catalogo publico;
- Pedidos historicos continuam consultaveis.

## Etapa 2 - Catalogo e experiencia de compra

### Objetivo

Transformar o catalogo em uma experiencia profissional de cursos, sem
dependencia funcional do modelo legado de equipamentos.

### Backend

- Criar contratos publicos consistentes para `Course`;
- Adicionar filtros por categoria, cidade, periodo e disponibilidade;
- Adicionar ordenacao por preco, data e relevancia;
- Adicionar paginacao;
- Retornar somente cursos ativos e turmas abertas na consulta publica;
- Criar endpoint de detalhes da turma;
- Exibir quantidade de vagas e status da turma;
- Validar limite maximo de inscricoes por comprador;
- Padronizar respostas de erro e validacao.

Primeiro bloco implementado:

- `GET /api/courses/catalog` retorna somente cursos ativos com turmas futuras,
  agendadas e disponiveis por padrao;
- filtros de busca, categoria, cidade/local, periodo e disponibilidade;
- ordenacao por proxima turma, preco ou nome;
- paginação com tamanho limitado a 50 itens;
- o frontend usa `CourseService.getCatalog()` e preserva `CourseService.getAll()`
  para o painel administrativo.
- cards publicos exibem a proxima turma, local, data e quantidade de vagas;
- detalhes ocultam turmas passadas, canceladas ou lotadas da selecao de compra.

### Frontend

- Criar catalogo baseado em `CourseService`;
- Remover gradualmente `ProductService` das telas publicas;
- Exibir cards com imagem, categoria, preco e proxima turma;
- Criar filtros e busca;
- Mostrar todas as turmas disponiveis na pagina de detalhes;
- Permitir escolher local, data e horario;
- Mostrar estados de carregamento, erro e turma lotada;
- Preservar o carrinho entre sessoes;
- Impedir mistura de turmas diferentes do mesmo curso.

### Criterios de conclusao

- O usuario consegue encontrar um curso por busca ou filtro;
- O usuario visualiza detalhes completos antes de comprar;
- A turma escolhida permanece visivel no carrinho e checkout;
- O catalogo nao apresenta equipamentos;
- O layout funciona em desktop e mobile.

## Etapa 3 - Checkout, pagamento e confiabilidade

### Objetivo

Tornar o fluxo de compra seguro, idempotente e preparado para um gateway real
de pagamentos.

### Fluxo esperado

```text
Carrinho
  -> Validacao de curso e turma
  -> Reserva temporaria de vaga
  -> Criacao do pedido pending
  -> Pagamento no gateway
  -> Webhook autenticado
  -> Pedido paid
  -> Matricula criada
```

### Entregas

- Integrar um gateway real, mantendo interface abstrata para troca futura;
- Criar pagamento associado ao pedido;
- Validar assinatura dos webhooks;
- Registrar eventos recebidos do gateway;
- Garantir idempotencia por pedido, evento e `Idempotency-Key`;
- Definir expiracao para pedidos pendentes;
- Liberar automaticamente reservas expiradas;
- Diferenciar pagamento pendente, aprovado, recusado, cancelado e reembolsado;
- Criar fluxo de reembolso administrativo;
- Nao confiar em valores enviados pelo frontend;
- Recalcular preco, desconto e total no backend;
- Registrar moeda, parcelas, identificador externo e datas do pagamento.

### Concorrencia

O controle de vagas deve ser transacional. Avaliar e testar:

- Atualizacao condicional atomica de `AvailableSeats`;
- Lock de linha no PostgreSQL;
- Duas compras simultaneas disputando a ultima vaga;
- Rollback quando qualquer item do pedido falhar;
- Recuperacao em caso de timeout do gateway.

### Criterios de conclusao

- Uma vaga nao pode ser vendida duas vezes;
- Webhook duplicado nao altera o resultado final;
- O frontend nunca define o preco final;
- Cancelamento libera a reserva uma unica vez;
- Uma matricula so existe para pagamento aprovado.

## Etapa 4 - Area do aluno e pos-venda

### Objetivo

Criar uma experiencia completa para o aluno acompanhar suas compras e
matriculas.

### Backend

- Criar endpoint de matriculas do usuario autenticado;
- Consultar detalhes da turma matriculada;
- Exibir status da matricula;
- Permitir solicitar cancelamento dentro da politica definida;
- Criar comprovante ou certificado;
- Criar endpoint de progresso quando houver conteudo digital;
- Adicionar historico de pedidos e pagamentos;
- Separar dados administrativos de dados publicos.

### Frontend

- Criar dashboard do aluno;
- Criar pagina de minhas matriculas;
- Exibir curso, turma, data, horario, local e instrutor;
- Exibir status: ativa, concluida ou cancelada;
- Criar pagina de detalhes do pedido;
- Criar acesso a certificado;
- Exibir mensagens e instrucoes antes da aula;
- Criar notificacoes sobre alteracao de turma ou cancelamento.

### Criterios de conclusao

- O aluno consulta suas matriculas sem depender da lista administrativa;
- O aluno nao acessa dados de outros usuarios;
- O status exibido corresponde ao backend;
- O aluno consegue localizar facilmente local e horario da aula.

## Etapa 5 - Painel administrativo profissional

### Objetivo

Separar claramente as operacoes administrativas de cursos, turmas, alunos,
pedidos, pagamentos e relatorios.

### Cursos

- Criar, editar e arquivar cursos;
- Definir descricao, preco, categoria e imagem;
- Impedir exclusao de curso com historico;
- Exibir quantidade de turmas ativas;
- Exibir quantidade total de matriculados.

### Turmas

- Criar varias turmas por curso;
- Definir data inicial e final, horario, local e instrutor;
- Definir capacidade;
- Exibir vagas ocupadas e restantes;
- Alterar capacidade somente acima do numero reservado;
- Cancelar ou encerrar turma;
- Transferir aluno para outra turma com auditoria.

### Alunos

- Consultar alunos por turma;
- Consultar dados de contato;
- Alterar status da matricula;
- Exportar lista de presenca;
- Evitar cadastro manual duplicado;
- Registrar historico de alteracoes.

### Pedidos e pagamentos

- Filtrar por periodo, status e pagamento;
- Visualizar itens e turma de cada item;
- Processar cancelamento e reembolso;
- Consultar identificador do gateway;
- Exibir falhas de webhook.

### Relatorios

- Faturamento por periodo;
- Cursos mais vendidos;
- Ocupacao por turma;
- Taxa de cancelamento;
- Receita por categoria;
- Exportacao CSV.

### Criterios de conclusao

- O administrador nao precisa editar diretamente dados no banco;
- Operacoes destrutivas sao substituidas por arquivamento;
- Toda alteracao importante registra usuario e data;
- O painel funciona com dados reais de `Course`, `CourseClass` e `Enrollment`.

## Etapa 6 - Seguranca e controle de acesso

### Objetivo

Fortalecer autenticacao, autorizacao e protecao dos dados de alunos e pedidos.

### Entregas

- Revisar politicas JWT, emissor, audiencia e expiracao;
- Manter refresh token com rotacao e revogacao;
- Aplicar autorizacao por recurso, nao somente por role;
- Garantir que usuario so consulte os proprios pedidos e matriculas;
- Validar entrada e limites de tamanho;
- Proteger endpoints administrativos;
- Aplicar rate limiting em login, checkout e webhooks;
- Configurar CORS apenas para origens autorizadas em producao;
- Remover segredos de arquivos versionados;
- Usar HTTPS;
- Sanitizar logs para nao expor tokens, senhas ou dados sensiveis;
- Adicionar politica de senha e bloqueio por tentativas;
- Revisar vulnerabilidades de dependencias, incluindo o alerta do `Microsoft.OpenApi`.

### Criterios de conclusao

- Nao existe acesso horizontal entre usuarios;
- Endpoints administrativos recusam usuarios comuns;
- Segredos sao fornecidos por ambiente seguro;
- Webhooks nao podem ser forjados;
- Logs nao armazenam credenciais.

## Etapa 7 - Notificacoes e comunicacao

### Objetivo

Informar o aluno e o administrador sobre os eventos importantes do ciclo da
compra e da turma.

### Eventos

- Pedido criado;
- Pagamento aprovado;
- Pagamento recusado;
- Matricula confirmada;
- Turma alterada;
- Turma cancelada;
- Vencimento de reserva;
- Reembolso processado;
- Certificado liberado.

### Entregas

- Criar tabela de notificacoes;
- Criar servico de envio de e-mail;
- Criar templates de mensagens;
- Adicionar fila para tarefas assicronas;
- Evitar envio duplicado;
- Registrar sucesso e falha de entrega;
- Permitir preferencia de comunicacao do usuario.

## Etapa 8 - Qualidade, testes e observabilidade

### Objetivo

Garantir que o sistema possa evoluir sem regressao e que falhas sejam
identificadas rapidamente.

### Testes backend

- Testes unitarios de servicos;
- Testes de integracao com banco;
- Testes de controllers;
- Testes de autorizacao;
- Testes de webhook;
- Testes de concorrencia;
- Testes de migracao.

### Testes frontend

- Testes de servicos HTTP;
- Testes de componentes;
- Testes de formularios;
- Testes de carrinho;
- Testes de selecao de turma;
- Testes do checkout;
- Testes de rotas protegidas;

### Observabilidade

- Logs estruturados;
- Correlation ID por requisicao;
- Metricas de pedidos, pagamentos e matriculas;
- Rastreamento de erros;
- Health checks da API e do PostgreSQL;
- Endpoint de readiness e liveness;
- Monitoramento de tempo de resposta.

### Criterios de conclusao

- Fluxos criticos possuem testes automatizados;
- Falhas de pagamento podem ser rastreadas por pedido;
- O sistema informa quando banco ou servicos essenciais estao indisponiveis;
- O build e os testes executam no CI.

## Etapa 9 - Docker, ambientes e deploy

### Objetivo

Preparar ambientes consistentes de desenvolvimento, homologacao e producao.

### Entregas

- Separar configuracoes por ambiente;
- Remover URLs fixas do frontend;
- Usar variaveis para URL da API;
- Criar arquivos `.env.example` sem segredos reais;
- Configurar migrations de forma controlada em producao;
- Adicionar health check no Docker Compose;
- Fixar versoes de imagens;
- Usar multi-stage builds;
- Manter `.dockerignore` atualizado;
- Configurar backup do PostgreSQL;
- Documentar restauracao de backup;
- Definir politica de volumes;
- Nunca executar `docker compose down -v` em ambiente com dados importantes;
- Criar pipeline de build, teste e publicacao;
- Publicar imagens com tags versionadas.

### Criterios de conclusao

- O ambiente pode ser recriado sem dados mockados indevidos;
- API e frontend usam configuracao externa;
- O banco possui backup testado;
- O deploy nao depende de caminhos locais do Windows;
- O pipeline bloqueia publicacao quando os testes falham.

## Etapa 10 - Remocao controlada do legado

### Objetivo

Eliminar a dependencia de `Product` somente quando todos os pedidos antigos
estiverem preservados e o novo dominio estiver estavel.

### Ordem recomendada

1. Garantir que novos cursos sejam criados em `Courses`;
2. Fazer o catalogo publico usar `Course`;
3. Fazer o admin usar `Course` e `CourseClass`;
4. Fazer novos pedidos referenciarem curso e turma diretamente;
5. Migrar consultas de pedidos e relatorios;
6. Tornar `Product` somente leitura;
7. Criar rotina de auditoria de referencias;
8. Arquivar registros legados;
9. Remover endpoints legados apenas após periodo de compatibilidade;
10. Remover tabelas somente com backup e plano de rollback.

### Regra de seguranca

Nao remover `Products`, `OrderItems.ProdutoId` ou referencias antigas enquanto
existirem pedidos, pagamentos, turmas ou relatorios que dependam desses dados.

## Ordem de execucao recomendada agora

Para a proxima sessao, seguir esta sequencia:

1. Validar build e testes do backend;
2. Confirmar migrations e schema no PostgreSQL;
3. Corrigir concorrencia e consistencia de vagas;
4. Migrar catalogo publico para `CourseService`;
5. Ajustar painel administrativo para cursos e turmas reais;
6. Criar testes de compra com selecao explicita de turma;
7. Executar build e testes do frontend;
8. Recriar os containers sem apagar volumes;
9. Implementar area do aluno;
10. Implementar seguranca, notificacoes, observabilidade e CI.

## Checklist de retomada

- [ ] Abrir a sessao em `E:\Projetos\PIM4API`;
- [ ] Ler este documento inteiro;
- [ ] Nao usar `E:\Projetos\e-commerce`;
- [ ] Nao apagar volumes Docker;
- [ ] Confirmar estado do git antes de editar;
- [ ] Executar build e testes existentes;
- [ ] Consultar migrations antes de criar novas;
- [ ] Preservar alteracoes locais existentes;
- [ ] Implementar uma etapa por vez;
- [ ] Atualizar este documento quando uma etapa for concluida.
