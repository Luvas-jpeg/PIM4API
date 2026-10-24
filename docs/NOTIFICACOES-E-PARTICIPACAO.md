# Participação nas turmas e notificações locais Android

## Decisão final — 17/10/2026

O usuário escolheu notificações locais, sem Firebase. Os SDKs Firebase, o worker
de envio e as tabelas de dispositivos/tentativas foram retirados antes da migration
ser aplicada. A API, o site e o PostgreSQL permanecem na hospedagem escolhida.
O Android usa flutter_local_notifications e timezone para o agendamento local.

## Comportamento da participação

- Site e app mostram quanto falta para o início: dias, amanhã, hoje ou turma iniciada.
- Confirmar fica disponível nos sete dias (168 horas) anteriores ao início,
  até o horário de início, para matrícula ativa e turma agendada.
- A confirmação é registrada pela API e aparece no admin e na exportação CSV.
- Repetir a confirmação mantém a data/hora original.
- Confirmar não representa presença em aula, conclusão ou pagamento.
- Não responder mantém a matrícula e a vaga.
- Alterar início, cancelar turma ou transferir matrícula invalida a confirmação.
- Cursos EAD não possuem confirmação nem lembrete presencial.

## Contratos da API

POST /api/me/enrollments/{id}/participation exige JWT do próprio aluno:
200 retorna participation, 404 para matrícula inexistente/de outro usuário,
401 sem autenticação, 409 fora da janela ou com turma/matrícula indisponível.

GET /api/me/courses e GET /api/me/enrollments/{id} incluem:
- status: waiting, pending, confirmed, closed, unavailable ou not_applicable;
- opensAt: início menos sete dias;
- confirmedAt: data/hora efetiva, quando houver;
- canConfirm: autorização calculada no servidor.

A migration AddParticipationConfirmation adiciona apenas
ParticipationConfirmedAt e ParticipationStartDate, nullable, em Enrollments.
Nenhuma tabela existente é removida. Uma atualização condicional protege a
confirmação contra alteração simultânea da turma/data.

## Agendamento no celular

1. Entre no Android com sua conta de aluno.
2. Abra Perfil > Lembretes da sua turma > Ativar lembretes.
3. Autorize as notificações no Android.
4. O app consulta as matrículas e agenda o aviso para sete dias antes do início.
5. Se a turma já estiver na semana de início, agenda um único aviso próximo,
   após cerca de dez segundos, sem esperar por uma semana que já passou.
6. O texto inclui nome do curso, dia, horário local e local da turma.
7. Tocar no aviso abre a matrícula correspondente. Se necessário, o aluno faz
   login primeiro. Um aviso de outra conta não abre matrícula da conta atual.

O botão Testar notificação mostra um exemplo usando uma turma futura real ainda
não confirmada. Ele não modifica a matrícula nem substitui o lembrete agendado.

Ao consultar as matrículas, os detalhes ou voltar ao app, os lembretes são
sincronizados. Mudança de data/local atualiza o agendamento; cancelamento,
confirmação ou remoção da matrícula cancela o aviso. O histórico local impede
que atualizar a tela gere repetidamente um lembrete já disparado.

Preferências e histórico são separados por conta e persistidos em armazenamento
seguro. Sair da conta cancela os avisos locais; desativar lembretes também limpa
o agendamento. Reiniciar o Android pode reconstituir os avisos via boot receiver.

## Limites que precisam ser considerados

- O celular entrega um aviso já agendado mesmo sem a API naquele instante.
- A API não envia push. Uma mudança feita pelo admin só chega ao agendamento
  quando o aplicativo consultar novamente a API.
- Sem conexão, o app mantém os lembretes anteriormente salvos; não consegue
  descobrir cancelamentos ou novas datas.
- O agendamento Android é aproximado (inexactAllowWhileIdle), sem pedir permissão
  de alarme exato. Economia de bateria, restrições do fabricante, permissão negada
  ou encerramento forçado podem atrasar ou impedir a apresentação.
- O navegador não reproduz os alarmes Android; a prévia testa telas, API,
  contagem e confirmação.
- A entrega visual, reinício do aparelho e abertura a partir do aviso ainda
  precisam ser testados num Android físico ou emulador. Não há aparelho conectado
  nesta etapa.

## Compilação e operação

Não há projeto, JSON de serviço, server key ou credencial Firebase para configurar.

~~~powershell
cd D:/Projetos/PIM4Mobile
./scripts/Build-Android.ps1 -ApiBaseUrl https://api.seudominio.com/ -WebBaseUrl https://seudominio.com/
~~~

Em desenvolvimento, use as URLs HTTP locais de debug conforme o README.
Produção continua exigindo HTTPS e assinatura Android própria.

## Validação

Testes do backend cobrem prazo, acesso por proprietário, persistência, idempotência
e mudança de início. Testes Flutter usam dispositivo e armazenamento substitutos
para verificar agendamento, atualização, não repetição, cancelamento, permissão,
toque de outra conta e expiração de sessão sem travamento.

Referências do plugin utilizado:
- https://pub.dev/packages/flutter_local_notifications
- https://pub.dev/packages/timezone
