# API real da plataforma de cursos

Este documento define a API como produto central para tres clientes:

- Site/Web: venda de cursos e checkout.
- Desktop Admin: cadastro de cursos, acompanhamento de compras, alunos e cupons.
- Mobile Flutter: area do aluno depois da compra aprovada.

O contrato alvo esta em `backend/openapi.yaml`.

## Objetivo da API

A API deve ser a fonte unica de dados para:

- autenticacao de aluno e admin;
- catalogo publico de cursos;
- checkout e pagamento;
- liberacao automatica do curso apos pagamento aprovado;
- area do aluno no Flutter;
- painel administrativo no desktop.

## Divisao dos modulos

### Auth

Endpoints principais:

- `POST /api/auth/register`
- `POST /api/auth/login`
- `POST /api/auth/refresh`
- `POST /api/auth/logout`
- `GET /api/me`
- `PUT /api/me`

Falta implementar:

- refresh token persistido no banco;
- logout com revogacao do refresh token;
- recuperacao de senha;
- confirmacao de e-mail, se for necessario;
- padrao unico de roles: `student` e `admin`.

### Catalogo publico

Endpoints principais:

- `GET /api/courses`
- `GET /api/courses/{courseId}`

Falta implementar:

- separar `Course` de produto generico;
- filtros por categoria, busca e status publicado;
- paginacao;
- campos claros para data, local, instrutor, vagas e imagem;
- ocultar cursos `draft` e `archived` no site.

### Checkout e compras

Endpoints principais:

- `POST /api/orders`
- `GET /api/orders/my`
- `GET /api/orders/{orderId}`

Falta implementar:

- separar `orderStatus` de `paymentStatus`;
- idempotencia no checkout para evitar compra duplicada;
- salvar `gatewayPaymentId`;
- salvar dados de Pix quando aplicavel;
- liberar matricula somente quando `paymentStatus = paid`;
- emitir recibo/comprovante, se o projeto precisar.

### Pagamentos

Endpoint principal:

- `POST /api/payments/webhook`

Falta implementar:

- escolher gateway de pagamento;
- validar assinatura do webhook;
- tratar eventos repetidos;
- atualizar compra para `paid`, `refused`, `cancelled` ou `refunded`;
- criar matricula do aluno apos pagamento aprovado.

Gateways comuns no Brasil:

- Mercado Pago;
- Pagar.me;
- Asaas;
- Stripe, se fizer sentido para cartao internacional.

### Area do aluno no Flutter

Endpoints principais:

- `GET /api/me/courses`
- `GET /api/me/courses/{courseId}`
- `GET /api/me/courses/{courseId}/progress`
- `PUT /api/me/courses/{courseId}/progress`

Falta implementar:

- tabela de progresso do aluno;
- lista de aulas/materiais do curso, caso os cursos tenham conteudo online;
- controle de acesso por matricula;
- certificado, se houver conclusao;
- download seguro de arquivos, se houver materiais.

### Desktop Admin

Endpoints principais:

- `GET /api/admin/courses`
- `POST /api/admin/courses`
- `PUT /api/admin/courses/{courseId}`
- `DELETE /api/admin/courses/{courseId}`
- `GET /api/admin/orders`
- `GET /api/admin/orders/{orderId}`
- `PUT /api/admin/orders/{orderId}/status`
- `GET /api/admin/students`
- `GET /api/admin/promocodes`
- `POST /api/admin/promocodes`

Falta implementar:

- autorizacao obrigatoria por role admin;
- logs de acoes administrativas;
- filtros por data, status, aluno e curso;
- exportacao CSV/PDF, se o administrativo precisar.

## Modelo de dados recomendado

Entidades principais:

- `User`
- `RefreshToken`
- `Course`
- `CourseLesson`, se houver aulas online;
- `Order`
- `OrderItem`
- `Payment`
- `Enrollment`
- `CourseProgress`
- `PromoCode`
- `AdminAuditLog`

Estados recomendados:

- `OrderStatus`: `pending`, `processing`, `completed`, `cancelled`
- `PaymentStatus`: `pending`, `paid`, `refused`, `cancelled`, `refunded`
- `CourseStatus`: `draft`, `published`, `archived`
- `EnrollmentStatus`: `active`, `completed`, `cancelled`

## Padroes tecnicos

Recomendado:

- JSON em camelCase;
- datas em ISO 8601;
- dinheiro como `decimal` no backend;
- erros no formato `{ code, message, details }`;
- paginacao em toda lista administrativa;
- JWT curto com refresh token;
- secrets fora do `appsettings.json`;
- CORS restrito aos dominios reais do site/admin.

## Hospedagem recomendada

## Caminho gratuito para comecar sem DevOps

Para aprender e validar o projeto, use servicos gerenciados. A ideia e evitar VPS no inicio, porque VPS exige configurar Linux, Docker, firewall, HTTPS, backup, logs e atualizacoes de seguranca manualmente.

Stack gratuita/recomendada para primeiro deploy:

- Site/Web: Vercel, Netlify ou Cloudflare Pages.
- API .NET: Render Web Service Free ou Railway Free/Trial com Docker.
- Banco PostgreSQL: Supabase Free ou Neon Free.
- Imagens/materiais: Supabase Storage Free ou Cloudflare R2.
- Dominio: pode comecar usando os dominios gratuitos das plataformas.

Minha sugestao para voce comecar:

1. Site na Vercel.
2. Banco no Supabase Free.
3. API no Render Free usando Docker.
4. Uploads no Supabase Storage.
5. Mobile Flutter apontando para a URL publica da API.
6. Desktop Admin apontando para a mesma URL publica da API.

Limites importantes:

- Render Free para API pode "dormir" apos inatividade e demorar para acordar.
- Render Postgres Free expira depois de 30 dias, entao nao e bom para dados reais.
- Supabase Free tem limite de banco e pode pausar depois de inatividade.
- Vercel Hobby e gratuito para projetos pessoais, mas tem limites de uso.
- Railway tem trial/creditos gratuitos, mas nao deve ser tratado como hospedagem gratuita permanente.

Para producao real com clientes pagando, o ideal e sair do gratuito pelo menos no banco. O primeiro upgrade mais importante e banco com backup automatico.

### API .NET

Opcoes boas:

- Azure App Service: melhor alinhamento com .NET e ambiente Microsoft.
- Render Web Service: simples para subir API via Docker/Git.
- Railway: simples para projetos pequenos, mas ASP.NET Core exige Dockerfile.
- Fly.io ou DigitalOcean App Platform: boas opcoes com Docker.
- VPS: Hostinger VPS, Contabo, Hetzner ou DigitalOcean Droplet, se quiser mais controle e menor custo.

Minha recomendacao inicial:

- Para simplicidade: Render ou Railway.
- Para stack profissional .NET: Azure App Service.
- Para custo/controle: VPS com Docker Compose.

Referencias:

- Azure App Service para ASP.NET Core: https://learn.microsoft.com/aspnet/core/host-and-deploy/azure-apps/
- Render Web Services: https://render.com/docs/web-services
- Railway ASP.NET Core: https://docs.railway.com/guides/aspnet-core

### Banco PostgreSQL

Opcoes boas:

- Neon Postgres;
- Supabase Postgres;
- Railway Postgres;
- Render PostgreSQL;
- Azure Database for PostgreSQL;
- banco no proprio VPS, apenas se aceitar gerenciar backup e seguranca.

Minha recomendacao inicial:

- Neon ou Supabase para comecar rapido.
- Azure Database for PostgreSQL se a API ficar no Azure.

### Site/Web

Opcoes boas:

- Vercel, se o frontend for Next.js/React;
- Netlify, se for React/Vite ou site estatico;
- Cloudflare Pages;
- Render Static Site;
- hospedagem propria no mesmo VPS, se quiser centralizar tudo.

Minha recomendacao inicial:

- Vercel para frontend moderno com preview por branch.
- Netlify ou Cloudflare Pages para site estatico/Vite.

Referencias:

- Vercel Deployments: https://vercel.com/docs/deployments/overview
- Netlify Deploys: https://docs.netlify.com/deploy/create-deploys/

### Arquivos, imagens e materiais dos cursos

Opcoes boas:

- Cloudflare R2;
- AWS S3;
- Azure Blob Storage;
- Supabase Storage.

Recomendacao:

- Evitar salvar arquivos dentro da API.
- Salvar arquivos em storage externo e gravar apenas a URL/metadados no banco.
- Para materiais pagos, usar URLs assinadas/temporarias.

### Mobile Flutter

O app Flutter nao e hospedado como site. Ele e distribuido por:

- Google Play Store;
- Apple App Store;
- alternativa interna: APK direto, apenas para testes/uso privado.

O app deve consumir a API publica em HTTPS.

### Desktop Admin

O desktop tambem nao precisa ser hospedado como site. Ele deve ser distribuido por:

- instalador `.exe`;
- Microsoft Store, se fizer sentido;
- atualizador automatico, se o projeto crescer.

O desktop deve consumir a mesma API via HTTPS e login admin.

## Ambientes

Criar tres ambientes:

- Local: desenvolvimento na maquina.
- Staging: testes antes de publicar.
- Production: usuarios reais.

Variaveis por ambiente:

- `ConnectionStrings__DefaultConnection`
- `Jwt__Secret`
- `Jwt__Issuer`
- `Jwt__Audience`
- `Payment__Provider`
- `Payment__WebhookSecret`
- `Storage__Provider`
- `Storage__Bucket`
- `Cors__AllowedOrigins`

## Checklist para tornar a API real

1. Atualizar controllers para bater com `backend/openapi.yaml`.
2. Criar entidades de `Course`, `Payment`, `RefreshToken` e `CourseProgress`.
3. Implementar endpoints `/api/me`.
4. Implementar refresh token e logout.
5. Implementar area do aluno.
6. Implementar webhook de pagamento.
7. Separar status do pedido e status do pagamento.
8. Padronizar erros.
9. Adicionar paginacao e filtros.
10. Configurar CORS para dominios reais.
11. Configurar HTTPS em producao.
12. Configurar deploy automatico pelo Git.
13. Configurar backup do banco.
14. Configurar logs e monitoramento.
15. Gerar SDKs/clients a partir do OpenAPI para Web, Flutter e Desktop.

## Ordem recomendada de implementacao

1. Contrato e rotas: ajustar API real ao `openapi.yaml`.
2. Admin Desktop: CRUD de cursos e listagem de compras.
3. Site: catalogo, checkout e criacao de pedido.
4. Pagamento: gateway e webhook.
5. Flutter: login e cursos liberados.
6. Progresso/certificado: se fizer parte do produto.
