# Recomendações de cursos com ML.NET

A API usa Microsoft.ML 5.0.1 e K-Means para agrupar cursos por conteúdo. A preparação extrai vetores TF-IDF de nome, categoria e descrição e os normaliza com L2. O modelo é treinado com `Fit` e atribui os grupos com `Transform`.

## Endpoint

`GET /api/courses/{id}/recommendations`, público, retorna o método, estado do modelo, identificação do conjunto de entrada, data de treinamento, quantidade de cursos/grupos e até três sugestões.

- Usa cursos presenciais ativos, no máximo os primeiros 200 IDs, sem atributos pessoais dos alunos.
- Exige ao menos quatro cursos e dois textos distintos; sem isso responde `insufficient-data` e lista vazia.
- K varia de dois a três grupos, com semente 42; vocabulário máximo de 2.048 termos.
- Prioriza candidatos do mesmo grupo e depois similaridade de cosseno; limiar inicial de 0,05.
- Exclui o curso atual e candidatos sem turma futura agendada com vagas.
- Guarda representações/grupos em memória; mudanças no texto ou na composição do catálogo provocam novo treinamento. Preço e disponibilidade são consultados novamente, sem treinamento por si só.
- Não persiste modelo em disco nem usa um serviço externo de ML. Reiniciar a API limpa o cache.

`modelVersion` é um resumo do hash da entrada de treinamento, não uma medida de desempenho. O agrupamento é do catálogo atual; não representa previsão de compra nem recomendação personalizada para cada aluno. Os testes sintéticos não demonstram relevância pedagógica em produção.

Quando `items` está vazio, `emptyReason` explica a causa: `no-available-courses` indica que não há outros cursos com turmas futuras agendadas e vagas; `no-similar-courses` indica que há candidatos disponíveis, mas nenhum atingiu o limiar de similaridade. `eligibleCourseCount` informa quantos outros cursos disponíveis foram avaliados. O estado `ready` descreve o modelo treinado, não garante recomendações. O frontend mantém a seção visível e exibe uma mensagem para listas vazias; versões antigas da API continuam com uma mensagem genérica compatível.

## Arquivos

- `backend/Services/CourseRecommendationEngine.cs`: preparação, treinamento, cache e ordenação.
- `backend/Controllers/CourseRecommendationsController.cs`: consulta pública e filtros do catálogo.
- `backend.Tests/CourseRecommendationTests.cs`: treinamento, ordenação, invalidação do cache e dados insuficientes.
- `backend.Tests/CourseRecommendationEndpointTests.cs`: filtro de cursos arquivados, EAD, indisponíveis e curso atual.

Não há migration nova nem necessidade de credencial de ML. A biblioteca é restaurada pelo NuGet e o CI existente compila e testa o recomendador. Nenhuma recomendação concede matrícula ou altera o pedido; checkout e confirmação de pagamento preservam suas regras.

## Demonstração

Preparar cursos com temas e descrições variados e candidatos com turmas futuras e vagas. Consultar o endpoint, abrir “Cursos semelhantes” no frontend e mostrar o treinamento e os testes. A avaliação de relevância com professores/instrutores continua pendente; não afirmar acurácia ou aumento de vendas sem medi-los.

Um ranking de cursos mais vendidos seria uma agregação dos pedidos pagos e não comprovaria Machine Learning. O algoritmo de agrupamento foi escolhido para apresentar aprendizado não supervisionado com uma biblioteca de ML, sem depender de histórico suficiente de compras.

O [documento acadêmico conjunto](../../PIM4Front/docs/REQUISITOS-ACADEMICOS.md) explica também acessibilidade/VLibras, relações étnico-raciais e afrodescendência, empreendedorismo em TI, Cloud Computing e DevOps.

Referências: [tutorial K-Means do ML.NET](https://learn.microsoft.com/en-us/dotnet/machine-learning/tutorials/iris-clustering), [Microsoft.ML 5.0.1](https://www.nuget.org/packages/Microsoft.ML/5.0.1).
