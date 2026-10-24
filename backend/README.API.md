# Backend (.NET) - API

> Documentacao principal atualizada: [README do PIM4API](../README.md).
> Os contratos alvo e orientacoes abaixo sao complementares e podem incluir
> recursos planejados. Configuracao atual, Docker, testes e pendencias estao
> no README principal.

API central da plataforma de cursos.

Ela deve atender tres clientes:

- Site/Web: catalogo, compra de cursos e checkout.
- Desktop Admin: cadastro de cursos, compras, alunos e cupons.
- Mobile Flutter: area do aluno depois da compra aprovada.

## Contrato

O contrato alvo da API esta em:

- `backend/openapi.yaml`

O documento de arquitetura, pendencias e hospedagem esta em:

- `docs/API_REAL_PRODUCAO.md`

## Stack recomendada

- .NET
- ASP.NET Core Web API
- EF Core
- PostgreSQL
- OpenAPI para gerar clientes/SDKs
- Docker para deploy

## Rodando localmente

1. Configure `ConnectionStrings__DefaultConnection`.
2. Configure `Jwt__Secret`, `Jwt__Issuer` e `Jwt__Audience`.
3. Rode `dotnet restore`.
4. Rode `dotnet ef database update`.
5. Rode `dotnet run --project backend/backend.csproj`.

## Observacao importante

O `openapi.yaml` descreve a API alvo para o produto real. Alguns endpoints ainda precisam ser implementados no backend atual.

## Participação e lembretes Android

Confirmar participação, estados da matrícula e envio agendado Firebase estão descritos em
[Notificações e participação](../docs/NOTIFICACOES-E-PARTICIPACAO.md).
