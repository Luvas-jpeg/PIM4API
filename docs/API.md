# Documentacao da API

## Visao geral

API ASP.NET Core para a plataforma de cursos. Ela atende:

- Site Web: catalogo, autenticacao, pedidos e pagamento.
- Aplicativo Mobile: cursos liberados e progresso.
- Administracao: produtos, pedidos, alunos e cupons.

Base local:

```text
http://localhost:5000/api
```

Todas as datas retornadas pela API usam UTC e os valores monetarios usam
numeros decimais.

## Autenticacao

As rotas protegidas usam JWT:

```http
Authorization: Bearer <access-token>
```

### Cadastrar usuario

```http
POST /auth/cadastrar
Content-Type: application/json
```

```json
{
  "nome": "Aluno Teste",
  "email": "aluno@example.com",
  "senha": "senha-segura",
  "cpf": "52998224725",
  "phone": "(11) 99999-9999"
}
```

### Login

```http
POST /auth/login
Content-Type: application/json
```

```json
{
  "email": "aluno@example.com",
  "senha": "senha-segura"
}
```

Retorna `accessToken`, `refreshToken`, `expiresIn` e os dados do usuario.

### Renovar token

```http
POST /auth/refresh
Content-Type: application/json
```

```json
{
  "refreshToken": "<refresh-token>"
}
```

### Encerrar sessao

```http
POST /auth/logout
Authorization: Bearer <access-token>
```

### Atualizar perfil

```http
PUT /auth/perfil
Authorization: Bearer <access-token>
Content-Type: application/json
```

## Perfil e area do aluno

### Obter usuario autenticado

```http
GET /me
Authorization: Bearer <access-token>
```

### Listar cursos do aluno

```http
GET /me/courses
Authorization: Bearer <access-token>
```

O curso deve estar associado a uma matricula do usuario.

### Consultar progresso

```http
GET /me/courses/{courseId}/progress
Authorization: Bearer <access-token>
```

### Atualizar progresso

```http
PUT /me/courses/{courseId}/progress
Authorization: Bearer <access-token>
Content-Type: application/json
```

```json
{
  "percent": 45,
  "completedLessonId": 12
}
```

O campo `percent` deve estar entre `0` e `100`.

## Catalogo de produtos e cursos

Os cursos ainda sao representados pela entidade `Product`. Para listar somente
cursos, use `tipo=course`.

### Listar produtos

```http
GET /products
GET /products?tipo=course
```

### Obter produto

```http
GET /products/{id}
```

### Criar produto

Requer a role `Admin`.

```http
POST /products
Authorization: Bearer <admin-token>
Content-Type: application/json
```

```json
{
  "nome": "Curso de Primeiros Socorros",
  "preco": 450.00,
  "tipoProduto": "course",
  "estoque": 20,
  "description": "Curso introdutorio de primeiros socorros.",
  "image": "https://example.com/curso.jpg",
  "category": "Saude",
  "date": "15/04/2026",
  "location": "Online",
  "instructor": "Instrutor Teste"
}
```

As propriedades aceitas para atualizacao sao as mesmas da criacao. A remocao
usa `DELETE /products/{id}` e tambem requer `Admin`.

## Pedidos

Todas as rotas de pedidos exigem autenticacao.

### Criar pedido

```http
POST /orders
Authorization: Bearer <access-token>
Content-Type: application/json
```

```json
{
  "itens": [
    {
      "produtoId": 5,
      "quantidade": 1
    }
  ],
  "valorFrete": 0,
  "paymentMethod": "pix",
  "installments": null,
  "promoCode": "MEDICO10"
}
```

Formas de pagamento aceitas atualmente:

- `pix`
- `credit_card`
- `debit_card`

O pedido e criado com `paymentStatus=pending`. O estoque/vagas e reservado,
mas a matricula ainda nao e criada.

### Listar meus pedidos

```http
GET /orders/my
Authorization: Bearer <access-token>
```

### Consultar pedido

```http
GET /orders/{orderId}
Authorization: Bearer <access-token>
```

O usuario somente pode consultar os proprios pedidos.

### Consultar pagamento do pedido

```http
GET /orders/{orderId}/payment
Authorization: Bearer <access-token>
```

### Cancelar pedido pendente

```http
POST /orders/{orderId}/cancel
Authorization: Bearer <access-token>
```

Pedidos pagos, concluidos ou ja cancelados nao podem ser cancelados por esta
rota.

## Pagamentos

### Webhook do gateway

```http
POST /payments/webhook
Content-Type: application/json
X-Webhook-Signature: <assinatura>
```

```json
{
  "eventId": "event-123",
  "paymentId": "payment-123",
  "orderId": 10,
  "status": "paid"
}
```

Status aceitos:

- `paid`
- `refused`
- `cancelled`
- `refunded`

Quando o status for `paid`, a API:

1. Atualiza o pagamento.
2. Marca o pedido como `processing`.
3. Cria a matricula do curso.
4. Libera o acesso na area do aluno.

Eventos repetidos de pagamento aprovado nao criam matriculas duplicadas.
A assinatura deve ser configurada conforme o gateway escolhido em
`Payments:WebhookSecret`.

## Cupons

### Validar cupom

```http
GET /promocodes/validate?code=MEDICO10
```

### Rotas administrativas

Requerem a role `Admin`:

```http
GET    /promocodes
GET    /promocodes/{id}
POST   /promocodes
PUT    /promocodes/{id}
DELETE /promocodes/{id}
```

### Usar cupom

```http
POST /promocodes/{id}/use
Authorization: Bearer <access-token>
```

No fluxo normal de compra, o cupom e aplicado durante a criacao do pedido.

## Alunos

Todas as rotas exigem a role `Admin`:

```http
GET    /students
GET    /students/{id}
POST   /students
PUT    /students/{id}
DELETE /students/{id}
```

## Respostas e erros

Sucesso:

- `200 OK`: consulta ou atualizacao concluida.
- `201 Created`: recurso criado.
- `204 No Content`: operacao concluida sem corpo.

Erros mais comuns:

- `400 Bad Request`: dados invalidos ou regra de negocio violada.
- `401 Unauthorized`: token ausente, invalido ou expirado.
- `403 Forbidden`: usuario sem a role necessaria.
- `404 Not Found`: recurso inexistente ou inacessivel.
- `409 Conflict`: dados duplicados, como e-mail ou cupom.

Formato atual:

```json
{
  "message": "Descricao do erro"
}
```

## Fluxo recomendado para Web e Mobile

```text
login
  -> guardar accessToken e refreshToken
  -> listar /products?tipo=course
  -> criar /orders
  -> consultar /orders/{id}/payment
  -> aguardar webhook paid
  -> listar /me/courses
  -> atualizar progresso
```

O cliente deve renovar o access token em `/auth/refresh` quando ele expirar.

## Diferencas em relacao ao contrato OpenAPI alvo

O arquivo `backend/openapi.yaml` descreve uma versao futura. As rotas
implementadas atualmente usam:

| Contrato alvo | Implementacao atual |
|---|---|
| `/auth/register` | `/auth/cadastrar` |
| `PUT /me` | `PUT /auth/perfil` |
| `/courses` | `/products?tipo=course` |
| `/admin/courses` | `/products` com role `Admin` |
| `/admin/orders` | `/orders` com role `Admin` |
| `/admin/students` | `/students` com role `Admin` |
| `/admin/promocodes` | `/promocodes` com role `Admin` |

Ao desenvolver os clientes, use as rotas implementadas acima ou altere o
backend para seguir o contrato OpenAPI antes de gerar SDKs.

## Execucao e testes

Executar a API:

```bash
dotnet run --project backend/backend.csproj
```

Executar os testes automatizados:

```bash
dotnet test backend.Tests/backend.Tests.csproj
```

O projeto de testes valida criacao de pedido, pagamento aprovado, matricula
idempotente, cancelamento com devolucao de estoque e aplicacao de cupom.
