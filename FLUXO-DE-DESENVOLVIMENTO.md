# Fluxo de desenvolvimento - Plataforma de cursos

## Objetivo do projeto

O projeto e uma plataforma de cursos presenciais com tres experiencias
principais: web, mobile e desktop/admin. A API deve ser compartilhada entre
todos os clientes.

- Backend ASP.NET Core/.NET 10;
- Entity Framework Core 10;
- PostgreSQL 17;
- Frontend Angular 21;
- Mobile Flutter, ainda a ser criado;
- Desktop administrativo, ainda a ser definido;
- Docker Compose para PostgreSQL, API e frontend.

O sistema deixou de ser um ecommerce de equipamentos medicos. O escopo atual e
uma plataforma de cursos, separando cursos presenciais e cursos EAD. Nao ha
previsao de cursos hibridos neste momento.

## Caminhos dos projetos

```text
Backend:  E:\Projetos\PIM4API
Frontend: E:\Projetos\PIM4Front
Mobile:   a definir
Desktop:  a definir
```

O antigo caminho `E:\Projetos\e-commerce` nao existe mais. Uma sessao antiga do terminal tentou iniciar nesse diretorio e apresentou `os error 267`. Esse erro significa que o diretorio de trabalho inicial e invalido; nao indica problema no Docker Engine.

## Experiencias esperadas por plataforma

### Web

A experiencia web deve continuar sendo o principal site publico da plataforma.
Ela deve permitir:

- listar cursos presenciais e EAD;
- buscar e filtrar cursos;
- visualizar turmas, data, horario, local, professor/instrutor e vagas;
- selecionar turma e comprar, quando o curso for presencial;
- comprar curso EAD sem selecao de turma presencial;
- acessar area do aluno com pedidos, matriculas e detalhes das turmas;
- acessar o painel administrativo enquanto o desktop/admin dedicado ainda nao
  existir.

### Mobile

O aplicativo mobile deve ser focado no estudante. A ideia inicial e permitir:

- consultar cursos comprados ou matriculas ativas;
- visualizar cursos presenciais com dia, horario, local, professor/instrutor e
  status da matricula;
- acompanhar pedidos e status de pagamento;
- acessar avisos e instrucoes antes da aula presencial;
- acessar aulas online/conteudos digitais de cursos EAD;
- futuramente acessar certificado/comprovante, se essa regra for definida.

O mobile nao deve priorizar administracao. Administracao em celular tende a
ficar limitada e deve ser evitada, exceto para consultas simples.

### Desktop/Admin

O desktop deve ser pensado como uma experiencia administrativa dedicada. A ideia
inicial e concentrar:

- dashboard de vendas;
- relatorios administrativos;
- gerenciamento de cursos;
- criacao e edicao de turmas;
- gerenciamento de alunos e matriculas;
- transferencia de aluno entre turmas;
- criacao e gestao de cupons;
- gerenciamento de pedidos, pagamentos, reembolsos e eventos de webhook;
- auditoria administrativa.

Enquanto o desktop dedicado nao existir, o painel administrativo web continua
sendo a implementacao principal dessas funcionalidades.

## Arquitetura de dominio desejada

```text
Course
  |
  +-- CourseClass           (somente presencial)
  |
  +-- CourseModule          (somente EAD)
  |     |
  |     +-- CourseLesson
  |
  +-- Enrollment
        +-- Student
        +-- Order
```

Um curso presencial pode possuir varias turmas. Cada turma possui data,
horario, local, instrutor, capacidade e vagas disponiveis. O comprador deve
selecionar uma turma antes de concluir o pedido presencial.

Um curso EAD nao possui turma presencial. Ele deve possuir conteudo digital em
modulos e aulas, e a matricula fica vinculada diretamente ao curso.

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

### Regra consolidada de acesso

- O usuario continua com a role global `Cliente`; nao existe uma role
  `Student` obrigatoria.
- O pagamento aprovado cria ou reutiliza um registro `Student` vinculado ao
  `User.ID` por `Students.UserId` e cria a `Enrollment` do curso comprado.
- A area do aluno e os endpoints de progresso, avaliacao e certificado
  autorizam o acesso pela cadeia `User -> Student -> Enrollment`, sempre
  limitada ao curso da matricula e ao seu status.
- O e-mail permanece apenas como dado de contato e compatibilidade. Ele nao e
  mais usado como chave de autorizacao.
- A primeira migration cria `UserId` como nullable para preservar alunos
  legados ou cadastros administrativos sem conta; novas compras sempre
  preenchem esse campo.

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
- Detalhes da turma selecionada agora exibem periodo completo, local, instrutor, capacidade, vagas restantes e status de inscricoes abertas.
- Checkout de turmas agora rejeita classes concluidas ou fora do periodo de inscricoes, e a tela de detalhes limita a quantidade ao total de vagas restantes com feedback local.

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
Leia E:\Projetos\PIM4API\FLUXO-DE-DESENVOLVIMENTO.md e continue o fluxo de desenvolvimento registrado.
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
- a turma selecionada permanece detalhada antes da adicao ao carrinho, com periodo e status visiveis.
- a quantidade solicitada nao pode ultrapassar as vagas restantes e classes concluidas ou iniciadas nao podem ser compradas.
- catalogo permite filtrar por intervalo de datas diretamente na tela inicial, usando os parametros `startDate` e `endDate` da API.
- filtros de categoria e local agora usam `GET /api/courses/catalog/options`, retornando opcoes de todo o catalogo ativo e disponivel, independentemente da pagina atual.
- Limite maximo de 5 inscricoes por curso/pedido foi aplicado no backend e refletido na pagina de detalhes, sem permitir quantidade acima do limite ou das vagas restantes.
- Ordenacao por relevancia foi adicionada para buscas, priorizando correspondencias exatas e iniciais no nome do curso.
- Carrinho impede misturar duas turmas diferentes do mesmo curso e aplica o limite de 5 inscricoes tambem nas alteracoes de quantidade.
- Testes frontend adicionados para filtros, carrinho, limite de inscricoes e bloqueio de mistura de turmas.

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

Status: **Etapa 2 concluida**. O proximo trabalho deve iniciar a Etapa 3,
com foco em checkout, pagamento e confiabilidade.

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

Primeiro bloco implementado:

- total do pedido continua sendo calculado no backend a partir dos produtos e
  do desconto validado; o frete informado pelo frontend nao e mais confiado;
- webhook de pagamento agora valida assinatura HMAC-SHA256 usando o corpo bruto
  da requisicao e comparacao em tempo constante;
- payloads invalidos ou assinaturas ausentes/incorretas sao rejeitados antes do
  processamento do pedido;
- eventos de webhook agora sao persistidos em `PaymentWebhookEvents`, com
  `EventId` unico e processamento idempotente;
- pedidos pendentes com mais de 30 minutos sao expirados automaticamente por um
  servico em segundo plano, liberando as vagas reservadas uma unica vez;
- administradores podem reembolsar pedidos pagos por `POST
  /api/Orders/{id}/refund`; o fluxo e idempotente, cancela matriculas e libera
  vagas sem repetir a liberacao;
- a integracao externa com InfinityPay e PicPay permanece separada e pendente
  do recebimento das credenciais, contratos e URLs oficiais fornecidos pelo
  cliente; nenhum formato de API de pagamento deve ser presumido;
- teste adicionado para garantir que um frete adulterado pelo cliente nao altere
  o total de um pedido de cursos.
- O fluxo atual de checkout foi ajustado somente para cursos: frete e endereco de
  entrega foram removidos da compra nova; produtos legados de equipamento
  permanecem apenas para historico e sao rejeitados em novos pedidos.

Status da Etapa 3: **parcialmente concluida, aguardando definicoes do cliente
para a integracao externa de pagamentos**.

A parte interna de checkout e confiabilidade esta implementada. A etapa nao
pode ser considerada totalmente concluida enquanto o cliente nao fornecer e
confirmar:

- qual provedor sera usado em cada forma de pagamento: InfinityPay, PicPay ou
  ambos;
- documentacao e versao oficial das APIs contratadas;
- credenciais de sandbox e producao, armazenadas fora do codigo;
- endpoint, formato, autenticacao e assinatura dos webhooks;
- fluxo de criacao, consulta, cancelamento e reembolso no provedor;
- identificadores externos, moeda, parcelas e estados oficiais de pagamento;
- URLs publicas de retorno e webhook;
- regras de conciliacao e comportamento em caso de timeout ou divergencia.

Essas informacoes sao necessarias para implementar a integracao real sem
inventar endpoints, headers, payloads ou regras que possam causar pagamentos
incorretos. Enquanto aguardamos o cliente, o sistema permanece funcional com
pedidos internos `pending`, processamento simulado por webhook autenticado e
reembolso interno administrativo.

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

Primeiro bloco implementado:

- `GET /api/me/courses` agora retorna as matriculas do usuario autenticado
  com curso, turma, pedido, instrutor, local, datas e status;
- a consulta usa a relacao explicita `CourseClass.Course`, com fallback apenas
  para dados legados;
- a area Minha Conta passou a exibir as matriculas do aluno e seus dados de
  turma, sem expor matriculas de outros usuarios.
- o historico de pedidos e pagamentos foi adicionado a area do aluno, usando
  `GET /api/Orders/my`;
- pedidos ainda pendentes podem ser cancelados pelo proprio aluno, com a mesma
  liberacao transacional de vagas do backend.
- foi criada a rota protegida `/conta/pedido/:id`, que consulta
  `GET /api/Orders/{id}` e exibe detalhes do pedido, itens, pagamento e total;
- o backend continua filtrando o detalhe pelo usuario autenticado, impedindo o
  acesso de um aluno ao pedido de outra pessoa.
- foi criada a rota protegida `/conta/matricula/:id`, com detalhes da turma e
  instrucoes basicas antes da aula;
- `GET /api/me/enrollments/{id}` valida que a matricula pertence ao usuario
  autenticado antes de retornar os dados.
- a tela de detalhes da matricula exibe avisos contextuais para matriculas
  ativas, turmas iniciadas, encerradas ou canceladas, sem criar notificacoes
  ficticias no backend.
- a area do aluno passou a exibir um resumo com matriculas ativas, concluidas,
  canceladas e pedidos pendentes;
- cada matricula agora possui acesso direto aos detalhes da matricula e ao
  pedido de origem.

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

Status: **Etapa 4 parcialmente concluida**.

Entregas concluidas:

- area autenticada do aluno com matriculas, pedidos e resumo;
- detalhes protegidos de matricula e pedido;
- cancelamento de pedidos pendentes;
- exibicao de turma, data, local, instrutor e instrucoes antes da aula;
- filtros de acesso por usuario autenticado no backend.

Pendencias para validacao com o cliente:

- **Politica de cancelamento de matricula:** depende da definicao de prazo,
  elegibilidade, percentual de reembolso, taxa administrativa e regra para
  turmas ja iniciadas. Sem essas decisoes, o sistema nao deve permitir
  cancelamento direto de matricula paga.
- **Certificado:** depende da definicao de presenca minima, conclusao,
  responsavel pela liberacao, formato e codigo de validacao. Por isso, ainda
  nao existe emissao ou download de certificado.
- **Notificacoes persistentes:** depende da escolha dos canais (email, SMS,
  WhatsApp ou notificacao interna), eventos obrigatorios, templates e
  provedor. A interface exibe avisos baseados no estado atual, mas ainda nao
  existe uma central de notificacoes ou envio externo.

As pendencias nao bloqueiam o inicio da Etapa 5, pois os recursos dependem de
regras comerciais e fornecedores que ainda precisam ser aprovados pelo cliente.

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

Primeiro bloco implementado:

- a aba administrativa de pedidos agora permite buscar por numero, cliente ou
  curso;
- foram adicionados filtros por status do pedido e status do pagamento;
- os status exibidos no painel foram alinhados aos valores reais persistidos
  pelo backend (`pending`, `processing`, `completed`, `cancelled`);
- o painel exibe o cliente, o status do pagamento e permite iniciar o
  reembolso de pedidos pagos.
- os indicadores de pedidos ativos e o grafico de status passaram a usar os
  valores internos persistidos, mantendo a traducao apenas na interface;
- os filtros foram organizados no formulario administrativo sem ultrapassar o
  limite maximo de CSS do build do frontend.

Validacao deste bloco:

- build do frontend concluido com sucesso;
- 36 testes do frontend aprovados;
- permanecem apenas avisos pre-existentes de orcamento CSS em outras telas e
  o aviso informativo do proprio `admin.scss` no limite de 8,00 kB.

Segundo bloco implementado:

- cursos podem ser arquivados e restaurados pelo painel administrativo;
- o arquivamento usa `IsActive` e nao remove cursos, turmas ou historico;
- cursos arquivados deixam de aparecer no catalogo publico;
- o painel administrativo consulta cursos ativos e arquivados para permitir
  gerenciamento completo;
- a API recebeu as operacoes protegidas
  `POST /api/courses/{id}/archive` e
  `POST /api/courses/{id}/restore`.

Validacao deste segundo bloco:

- build do frontend concluido com sucesso;
- 36 testes do frontend aprovados;
- 20 testes do backend aprovados;
- nenhuma migration foi necessaria, pois o campo `IsActive` ja existia.

Terceiro bloco implementado:

- o painel de turmas permite consultar os alunos matriculados e alterar o
  status individual da matricula;
- a API administrativa recebeu
  `PATCH /api/courses/{courseId}/classes/{classId}/students/{studentId}/status`;
- os status aceitos sao `active`, `completed` e `cancelled`;
- cancelamentos liberam uma vaga e reativacoes reservam uma vaga novamente;
- alterar uma matricula para concluida preserva a ocupacao historica da turma;
- a reativacao e bloqueada quando a turma esta cancelada, concluida ou sem
  vagas.

Validacao deste terceiro bloco:

- 36 testes do frontend aprovados;
- 21 testes do backend aprovados;
- a capacidade da turma permanece consistente nas transicoes entre
  matricula ativa, concluida e cancelada.

Quarto bloco implementado:

- o modal de alunos da turma agora permite exportar a lista de presenca em
  CSV;
- o arquivo inclui nome, e-mail, telefone, data da matricula e status;
- o nome do arquivo identifica o curso, a turma e a data da exportacao;
- a exportacao ocorre somente quando existem alunos carregados para a turma.

Validacao deste quarto bloco:

- build do frontend concluido com sucesso;
- 36 testes do frontend aprovados;
- a exportacao foi mantida somente no frontend, sem alterar o contrato da
  API ou o banco de dados.

Quinto bloco implementado:

- o dashboard administrativo passou a exibir taxa de ocupacao das turmas;
- foi adicionada a taxa de cancelamento dos pedidos;
- foi criado um ranking de ocupacao por turma;
- foi criado um resumo de receita por categoria de curso;
- o dashboard permite exportar um relatorio CSV com esses indicadores.

Validacao deste quinto bloco:

- build do frontend concluido com sucesso;
- 36 testes do frontend aprovados;
- os relatorios usam os dados ja carregados pelo painel e nao alteram o
  contrato da API ou o banco de dados.

Sexto bloco implementado:

- o painel administrativo do frontend foi refatorado em componentes dedicados
  por mini tela: dashboard, cursos, turmas, cupons, pedidos e auditoria;
- o componente principal `admin` ficou responsavel apenas por carregar dados,
  controlar abas e receber notificacoes dos componentes filhos;
- foi criada a transferencia administrativa de aluno entre turmas do mesmo
  curso, com ajuste de vagas da turma origem e destino;
- a transferencia aceita somente matriculas ativas e rejeita turma destino
  concluida, cancelada ou sem vagas;
- a API recebeu
  `POST /api/courses/{courseId}/classes/{classId}/students/{studentId}/transfer`;
- a transferencia registra auditoria com a acao `transferred`;
- a aba de pedidos passou a abrir um modal de detalhes com cliente, itens,
  metodo de pagamento, parcelas, total e identificador do gateway;
- a API recebeu consulta administrativa de eventos de webhook em
  `GET /api/payments/webhook-events`, com filtro opcional por `orderId`;
- o detalhe administrativo do pedido exibe os eventos de webhook registrados
  para aquele pedido.

Validacao deste sexto bloco:

- build do backend concluido com sucesso;
- 22 testes do backend aprovados;
- build do frontend concluido com sucesso;
- 36 testes do frontend aprovados;
- permanecem apenas avisos pre-existentes de budget CSS em algumas telas.

Setimo bloco implementado:

- o cadastro manual de alunos no backend passou a bloquear duplicidade por
  e-mail e curso;
- e-mails de alunos sao normalizados para minusculas e status de matricula sao
  normalizados antes de persistir;
- criacao, edicao e remocao manual de aluno agora registram auditoria;
- remocao manual de aluno com matriculas vinculadas foi bloqueada para
  preservar historico academico e financeiro;
- testes de negocio foram adicionados para cadastro duplicado e bloqueio de
  remocao de aluno com matricula;
- o painel administrativo recebeu uma aba `Alunos`, com consulta geral,
  busca por nome, e-mail, telefone ou curso e filtro por status.

Validacao deste setimo bloco:

- build do backend concluido com sucesso;
- 24 testes do backend aprovados;
- build do frontend concluido com sucesso;
- 36 testes do frontend aprovados;
- permanecem apenas avisos pre-existentes de budget CSS em algumas telas.

Oitavo bloco implementado:

- a auditoria administrativa passou a ter busca textual, filtro por acao,
  filtro por entidade e exportacao CSV dos registros filtrados;
- a aba de pedidos passou a exportar CSV dos pedidos filtrados;
- a aba de alunos passou a abrir um detalhe administrativo simples do aluno,
  com contato, curso, status e data de matricula;
- o painel administrativo foi revisado para cobrir as operacoes principais de
  cursos, turmas, alunos, pedidos, pagamentos, relatorios e auditoria.

Validacao deste oitavo bloco:

- build do backend concluido com sucesso;
- 24 testes do backend aprovados;
- build do frontend concluido com sucesso;
- 36 testes do frontend aprovados;
- permanecem apenas avisos pre-existentes de budget CSS em algumas telas e o
  alerta de vulnerabilidade do pacote `Microsoft.OpenApi`.

Status: **Etapa 5 concluida**.

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

## Etapa 5.5 - Separacao entre cursos EAD e presenciais

### Objetivo

Criar a base tecnica para dois fluxos de curso:

- **Presencial:** precisa de turma, data, local, instrutor, capacidade e vagas.
- **EAD:** nao possui turma presencial; possui modulos, aulas online e pode ser
  comprado sem `TurmaId`.

Nao existe curso hibrido nesta etapa.

### Backend implementado

- `Course` recebeu `DeliveryMode` (`presencial` ou `ead`) e `WorkloadHours`;
- criadas as entidades `CourseModule` e `CourseLesson`;
- `Enrollment` agora aceita `ClassId` nullable e possui `CourseId` para
  matriculas EAD;
- `CourseService` valida modalidade, bloqueia turmas para cursos EAD e gerencia
  modulos/aulas;
- criacao/edicao de curso sincroniza um `Product` legado de tipo `course`,
  mantendo o checkout atual funcional enquanto `OrderItem` ainda usa
  `ProdutoId`;
- `CoursesController` recebeu endpoints para modulos e aulas EAD;
- `InventoryService` exige turma somente para presencial e rejeita turma em EAD;
- `EnrollmentService` cria matriculas EAD com `CourseId` e `ClassId = null`;
- `OrderService` nao reserva nem devolve estoque para curso EAD;
- `MeController` consulta matriculas por `Course` direto, mantendo fallback
  legado.

Migration criada:

```text
backend\Migrations\20260916090000_AddEadCoursesContent.cs
```

### Frontend implementado

- `Course` agora possui `deliveryMode`, `workloadHours`, `modules` e `lessons`;
- `CourseService` consome endpoints de modulos e aulas;
- cadastro administrativo de cursos permite escolher `Presencial` ou `EAD`;
- aba `Turmas` bloqueia criacao de turma para EAD;
- criada aba administrativa `Conteudo EAD` para cadastrar modulos e aulas;
- detalhes do curso permitem comprar EAD sem turma;
- carrinho e checkout exibem EAD como acesso online;
- checkout exige turma somente para curso presencial;
- catalogo diferencia cursos `EAD` e `Presencial`.

### Testes e validacao

- Backend build concluido com sucesso;
- Backend tests: 27 aprovados;
- Frontend build concluido com sucesso;
- Frontend tests: 36 aprovados.

Testes adicionados:

- compra de curso EAD sem turma e sem reserva de estoque;
- rejeicao de compra EAD com turma informada;
- pagamento aprovado de EAD criando matricula com `CourseId` e `ClassId` nulo.

Avisos conhecidos:

- `Microsoft.OpenApi 2.0.0` possui alerta de vulnerabilidade alto;
- alguns arquivos SCSS continuam acima do budget de warning, mas abaixo do
  limite de erro.

### Pendencias futuras

- Criar area do estudante para assistir aulas EAD;
- modelar progresso por aula/modulo de forma mais completa;
- evoluir avaliacao EAD com banco de questoes mais completo;
- evoluir certificado EAD com PDF, layout visual e validacao publica do codigo;
- remover gradualmente a dependencia de `Product` para novos cursos, mantendo
  compatibilidade historica;
- atualizar o `AppDbContextModelSnapshot` por migration gerada pelo EF quando o
  ambiente de migrations estiver estabilizado.

Status: **Etapa 5.5 concluida como base funcional**.

### Incremento da area do estudante EAD

Foi adicionada uma primeira experiencia do aluno para cursos EAD:

- `GET /api/me/enrollments/{id}` agora retorna `DeliveryMode`,
  `WorkloadHours` e os modulos/aulas ativos quando a matricula pertence a um
  curso EAD;
- `GET /api/me/courses/{courseId}/progress` e
  `PUT /api/me/courses/{courseId}/progress` passaram a validar se o usuario
  autenticado possui matricula ativa no curso antes de consultar ou atualizar
  progresso;
- a atualizacao de progresso valida se a aula pertence ao curso matriculado;
- a area Minha Conta diferencia matriculas EAD e presenciais;
- a tela de detalhes da matricula EAD funciona como uma sala simples de aulas,
  exibindo modulos, aulas, link do video e botao para marcar aula como
  concluida;
- o percentual de progresso e calculado a partir das aulas concluidas pela
  interface e persistido no backend.

Validacao deste incremento:

- backend build concluido com sucesso;
- backend tests: 27 aprovados;
- frontend build concluido com sucesso;
- frontend tests: 36 aprovados.

Ainda nao foi implementado:

- tela mobile Flutter para assistir aulas.

### Incremento de avaliacao administrativa EAD

Foi adicionada a base administrativa para montar provas de cursos EAD:

- novas entidades `CourseAssessment`, `CourseQuestion` e
  `CourseQuestionOption`;
- nova migration manual
  `backend\Migrations\20260916103000_AddEadAssessments.cs`;
- `Course` agora retorna avaliacoes vinculadas no contrato administrativo;
- `CourseService` permite criar/editar avaliacao e cadastrar questoes com
  alternativas;
- a validacao exige:
  - titulo da avaliacao;
  - nota minima entre 0 e 100;
  - ao menos uma tentativa;
  - questao com enunciado;
  - ao menos duas alternativas;
  - exatamente uma alternativa correta;
- `CoursesController` recebeu endpoints administrativos:

```http
GET    /api/courses/{courseId}/assessments
POST   /api/courses/{courseId}/assessments
PUT    /api/courses/{courseId}/assessments/{assessmentId}
POST   /api/courses/{courseId}/assessments/{assessmentId}/questions
```

- a aba `Conteudo EAD` do admin agora permite:
  - configurar titulo da avaliacao;
  - definir nota minima;
  - definir numero maximo de tentativas;
  - cadastrar questoes;
  - cadastrar alternativas e marcar a correta.

Validacao deste incremento:

- backend build concluido com sucesso;
- backend tests: 27 aprovados;
- frontend build concluido com sucesso;
- frontend tests: 36 aprovados.

### Incremento de submissao da avaliacao pelo aluno

Foi implementada a primeira versao funcional da prova EAD na area do aluno:

- novas entidades `CourseAssessmentAttempt` e `CourseAssessmentAnswer`;
- nova migration manual
  `backend\Migrations\20260916110000_AddEadAssessmentAttempts.cs`;
- `GET /api/me/enrollments/{id}` retorna avaliacoes ativas do curso EAD sem
  expor o gabarito;
- novo endpoint autenticado:

```http
POST /api/me/courses/{courseId}/assessments/{assessmentId}/submit
```

- a submissao valida:
  - usuario matriculado no curso;
  - avaliacao ativa e pertencente ao curso;
  - limite de tentativas;
  - todas as questoes respondidas;
  - alternativas pertencentes as respectivas questoes;
- o backend calcula a nota percentual, define aprovado/reprovado e registra a
  tentativa com as respostas;
- a tela de detalhe da matricula EAD exibe a avaliacao, permite escolher
  alternativas e mostra nota, aprovacao e tentativas restantes apos o envio.

Validacao deste incremento:

- backend build concluido com sucesso;
- backend tests: 27 aprovados;
- frontend build concluido com sucesso;
- frontend tests: 36 aprovados.

Ainda nao foi implementado:

- tela mobile Flutter para assistir aulas e responder avaliacao.

### Incremento de certificado EAD

Foi implementada a primeira versao de emissao de certificado para cursos EAD:

- nova entidade `CourseCertificate`;
- nova migration manual
  `backend\Migrations\20260916113000_AddCourseCertificates.cs`;
- `GET /api/me/enrollments/{id}` retorna o certificado ja emitido, quando
  existir;
- novos endpoints autenticados:

```http
GET  /api/me/courses/{courseId}/certificate
POST /api/me/courses/{courseId}/certificate/issue
```

- a liberacao do certificado exige:
  - usuario matriculado no curso;
  - curso na modalidade EAD;
  - progresso de 100%;
  - aprovacao em todas as avaliacoes ativas do curso;
- a emissao e idempotente por aluno e curso, evitando certificado duplicado;
- o certificado registra aluno, curso, carga horaria, data de emissao e codigo
  de validacao;
- a tela de detalhe da matricula EAD mostra o status de liberacao, permite
  emitir o certificado quando os requisitos forem cumpridos e exibe os dados do
  certificado emitido.

Validacao deste incremento:

- backend tests: 27 aprovados;
- frontend build concluido com sucesso;
- frontend tests: 36 aprovados;
- permanecem apenas os avisos conhecidos de budget SCSS e a vulnerabilidade do
  pacote `Microsoft.OpenApi`.

Ainda nao foi implementado:

- download/geracao visual de PDF do certificado;
- endpoint publico para validar o codigo do certificado;
- tela mobile Flutter para assistir aulas, responder avaliacao e consultar
  certificado.

## Etapa 6 - Seguranca e controle de acesso

### Objetivo

Fortalecer autenticacao, autorizacao e protecao dos dados de alunos e pedidos.

### Implementacao em andamento

- JWT valida assinatura, emissor, audiencia, validade e usa tolerancia de relogio
  de um minuto; segredo precisa ter pelo menos 32 bytes e expiracao maior que
  zero e limitada a 24 horas.
- Refresh tokens continuam com rotacao e revogacao, e agora sao armazenados
  como hash SHA-256 no banco. Tokens antigos ainda validos sao convertidos para
  hash na primeira renovacao, para preservar as sessoes durante a transicao.
- Login bloqueia temporariamente a conta por 15 minutos apos cinco senhas
  incorretas; o cadastro exige senha de 12 a 128 caracteres com maiuscula,
  minuscula e numero.
- Rate limiting por IP protege cadastro/login/refresh e webhook; checkout limita
  por usuario autenticado, com fallback para IP.
- CORS usa origens configuradas; producao falha ao iniciar sem uma lista
  explicita. HTTPS e HSTS ficam ativos fora do ambiente de desenvolvimento.
- O corpo HTTP tem limite de 1 MB e os servicos de autenticacao/perfil rejeitam
  campos fora dos limites definidos.
- Migration `AddAccountLockout` adiciona os campos do bloqueio; ainda nao foi
  aplicada ao banco.
- `Microsoft.AspNetCore.OpenApi` foi atualizado para 10.0.12 para resolver a
  dependencia vulneravel `Microsoft.OpenApi` 2.0.0.

### Revisoes pendentes

- Revisar acesso horizontal em todos os recursos e testar roles administrativas.
- Confirmar que segredos reais nao estao versionados e que logs nao registram
  tokens, senhas ou dados pessoais.
- Avaliar a concorrencia na rotacao de refresh tokens e definir o tratamento das
  sessoes existentes apos habilitar armazenamento por hash.
- Validar o pipeline de CI e executar os testes de seguranca antes de concluir
  a etapa.

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
