# Inventário técnico — 17/10/2026

Gerado a partir dos fontes e dos metadados do PostgreSQL local. Não contém registros de alunos, senhas, tokens ou configurações secretas. A enumeração de métodos usa leitura estática; auxiliares internos e funções escritas como propriedades podem não aparecer.

## Endpoints presentes no OpenAPI local

| Método | Rota | Grupo |
| --- | --- | --- |
| GET | /health | backend |
| GET | /api/audit | Audit |
| POST | /api/Auth/cadastrar | Auth |
| POST | /api/Auth/login | Auth |
| POST | /api/Auth/refresh | Auth |
| POST | /api/Auth/logout | Auth |
| PUT | /api/Auth/perfil | Auth |
| GET | /api/courses | Courses |
| POST | /api/courses | Courses |
| GET | /api/courses/catalog | Courses |
| GET | /api/courses/catalog/options | Courses |
| GET | /api/courses/{id} | Courses |
| PUT | /api/courses/{id} | Courses |
| POST | /api/courses/{id}/archive | Courses |
| POST | /api/courses/{id}/restore | Courses |
| POST | /api/courses/{id}/interests | Courses |
| GET | /api/courses/{courseId}/classes | Courses |
| POST | /api/courses/{courseId}/classes | Courses |
| GET | /api/courses/{courseId}/classes/{classId} | Courses |
| PUT | /api/courses/{courseId}/classes/{classId} | Courses |
| GET | /api/courses/{courseId}/classes/{classId}/students | Courses |
| PATCH | /api/courses/{courseId}/classes/{classId}/students/{studentId}/status | Courses |
| POST | /api/courses/{courseId}/classes/{classId}/students/{studentId}/transfer | Courses |
| GET | /api/courses/{courseId}/modules | Courses |
| POST | /api/courses/{courseId}/modules | Courses |
| PUT | /api/courses/{courseId}/modules/{moduleId} | Courses |
| POST | /api/courses/{courseId}/modules/{moduleId}/lessons | Courses |
| PUT | /api/courses/{courseId}/modules/{moduleId}/lessons/{lessonId} | Courses |
| GET | /api/courses/{courseId}/assessments | Courses |
| POST | /api/courses/{courseId}/assessments | Courses |
| PUT | /api/courses/{courseId}/assessments/{assessmentId} | Courses |
| POST | /api/courses/{courseId}/assessments/{assessmentId}/questions | Courses |
| GET | /api/me | Me |
| GET | /api/me/courses | Me |
| POST | /api/me/enrollments/{enrollmentId}/participation | Me |
| GET | /api/me/courses/{courseId}/progress | Me |
| PUT | /api/me/courses/{courseId}/progress | Me |
| GET | /api/me/enrollments/{enrollmentId} | Me |
| POST | /api/me/courses/{courseId}/assessments/{assessmentId}/submit | Me |
| GET | /api/me/courses/{courseId}/certificate | Me |
| POST | /api/me/courses/{courseId}/certificate/issue | Me |
| POST | /api/Orders | Orders |
| GET | /api/Orders | Orders |
| GET | /api/Orders/my | Orders |
| GET | /api/Orders/{id} | Orders |
| POST | /api/Orders/{id}/cancel | Orders |
| GET | /api/Orders/{id}/payment | Orders |
| PUT | /api/Orders/{id}/status | Orders |
| POST | /api/Orders/{id}/refund | Orders |
| POST | /api/payments/webhook | Payments |
| GET | /api/payments/webhook-events | Payments |
| GET | /api/PromoCodes | PromoCodes |
| POST | /api/PromoCodes | PromoCodes |
| GET | /api/PromoCodes/{id} | PromoCodes |
| PUT | /api/PromoCodes/{id} | PromoCodes |
| DELETE | /api/PromoCodes/{id} | PromoCodes |
| GET | /api/PromoCodes/validate | PromoCodes |
| GET | /api/Students | Students |
| POST | /api/Students | Students |
| GET | /api/Students/{id} | Students |
| PUT | /api/Students/{id} | Students |
| DELETE | /api/Students/{id} | Students |

O acesso de cada rota depende dos atributos Authorize e da checagem de proprietário nos controllers. A ausência de security no OpenAPI não significa acesso público.

## Estrutura real do PostgreSQL

### AuditLogs

| Coluna | Tipo | Aceita nulo |
| --- | --- | --- |
| Id | bigint | NO |
| UserId | integer | YES |
| Action | text | NO |
| EntityType | text | NO |
| EntityId | text | NO |
| PreviousValue | text | YES |
| NewValue | text | YES |
| CreatedAt | timestamp with time zone | NO |

Restrições:
- FK_AuditLogs_Users_UserId: FOREIGN KEY ("UserId") REFERENCES "Users"("ID") ON DELETE SET NULL
- PK_AuditLogs: PRIMARY KEY ("Id")

Índices:
- IX_AuditLogs_EntityType_EntityId_CreatedAt: CREATE INDEX "IX_AuditLogs_EntityType_EntityId_CreatedAt" ON public."AuditLogs" USING btree ("EntityType", "EntityId", "CreatedAt")
- IX_AuditLogs_UserId: CREATE INDEX "IX_AuditLogs_UserId" ON public."AuditLogs" USING btree ("UserId")
- PK_AuditLogs: CREATE UNIQUE INDEX "PK_AuditLogs" ON public."AuditLogs" USING btree ("Id")

### CourseAssessmentAnswers

| Coluna | Tipo | Aceita nulo |
| --- | --- | --- |
| Id | integer | NO |
| AttemptId | integer | NO |
| QuestionId | integer | NO |
| SelectedOptionId | integer | NO |
| IsCorrect | boolean | NO |

Restrições:
- FK_CourseAssessmentAnswers_CourseAssessmentAttempts_AttemptId: FOREIGN KEY ("AttemptId") REFERENCES "CourseAssessmentAttempts"("Id") ON DELETE CASCADE
- FK_CourseAssessmentAnswers_CourseQuestionOptions_SelectedOptio~: FOREIGN KEY ("SelectedOptionId") REFERENCES "CourseQuestionOptions"("Id") ON DELETE CASCADE
- FK_CourseAssessmentAnswers_CourseQuestions_QuestionId: FOREIGN KEY ("QuestionId") REFERENCES "CourseQuestions"("Id") ON DELETE CASCADE
- PK_CourseAssessmentAnswers: PRIMARY KEY ("Id")

Índices:
- IX_CourseAssessmentAnswers_AttemptId: CREATE INDEX "IX_CourseAssessmentAnswers_AttemptId" ON public."CourseAssessmentAnswers" USING btree ("AttemptId")
- IX_CourseAssessmentAnswers_QuestionId: CREATE INDEX "IX_CourseAssessmentAnswers_QuestionId" ON public."CourseAssessmentAnswers" USING btree ("QuestionId")
- IX_CourseAssessmentAnswers_SelectedOptionId: CREATE INDEX "IX_CourseAssessmentAnswers_SelectedOptionId" ON public."CourseAssessmentAnswers" USING btree ("SelectedOptionId")
- PK_CourseAssessmentAnswers: CREATE UNIQUE INDEX "PK_CourseAssessmentAnswers" ON public."CourseAssessmentAnswers" USING btree ("Id")

### CourseAssessmentAttempts

| Coluna | Tipo | Aceita nulo |
| --- | --- | --- |
| Id | integer | NO |
| UserId | integer | NO |
| CourseId | integer | NO |
| AssessmentId | integer | NO |
| AttemptNumber | integer | NO |
| Score | integer | NO |
| Passed | boolean | NO |
| SubmittedAt | timestamp with time zone | NO |

Restrições:
- FK_CourseAssessmentAttempts_CourseAssessments_AssessmentId: FOREIGN KEY ("AssessmentId") REFERENCES "CourseAssessments"("Id") ON DELETE CASCADE
- FK_CourseAssessmentAttempts_Courses_CourseId: FOREIGN KEY ("CourseId") REFERENCES "Courses"("Id") ON DELETE CASCADE
- FK_CourseAssessmentAttempts_Users_UserId: FOREIGN KEY ("UserId") REFERENCES "Users"("ID") ON DELETE CASCADE
- PK_CourseAssessmentAttempts: PRIMARY KEY ("Id")

Índices:
- IX_CourseAssessmentAttempts_AssessmentId: CREATE INDEX "IX_CourseAssessmentAttempts_AssessmentId" ON public."CourseAssessmentAttempts" USING btree ("AssessmentId")
- IX_CourseAssessmentAttempts_CourseId: CREATE INDEX "IX_CourseAssessmentAttempts_CourseId" ON public."CourseAssessmentAttempts" USING btree ("CourseId")
- IX_CourseAssessmentAttempts_UserId_AssessmentId_AttemptNumber: CREATE UNIQUE INDEX "IX_CourseAssessmentAttempts_UserId_AssessmentId_AttemptNumber" ON public."CourseAssessmentAttempts" USING btree ("UserId", "AssessmentId", "AttemptNumber")
- PK_CourseAssessmentAttempts: CREATE UNIQUE INDEX "PK_CourseAssessmentAttempts" ON public."CourseAssessmentAttempts" USING btree ("Id")

### CourseAssessments

| Coluna | Tipo | Aceita nulo |
| --- | --- | --- |
| Id | integer | NO |
| CourseId | integer | NO |
| Title | text | NO |
| MinimumScore | integer | NO |
| MaxAttempts | integer | NO |
| IsActive | boolean | NO |

Restrições:
- FK_CourseAssessments_Courses_CourseId: FOREIGN KEY ("CourseId") REFERENCES "Courses"("Id") ON DELETE CASCADE
- PK_CourseAssessments: PRIMARY KEY ("Id")

Índices:
- IX_CourseAssessments_CourseId: CREATE INDEX "IX_CourseAssessments_CourseId" ON public."CourseAssessments" USING btree ("CourseId")
- PK_CourseAssessments: CREATE UNIQUE INDEX "PK_CourseAssessments" ON public."CourseAssessments" USING btree ("Id")

### CourseCertificates

| Coluna | Tipo | Aceita nulo |
| --- | --- | --- |
| Id | integer | NO |
| UserId | integer | NO |
| CourseId | integer | NO |
| ValidationCode | text | NO |
| IssuedAt | timestamp with time zone | NO |

Restrições:
- FK_CourseCertificates_Courses_CourseId: FOREIGN KEY ("CourseId") REFERENCES "Courses"("Id") ON DELETE CASCADE
- FK_CourseCertificates_Users_UserId: FOREIGN KEY ("UserId") REFERENCES "Users"("ID") ON DELETE CASCADE
- PK_CourseCertificates: PRIMARY KEY ("Id")

Índices:
- IX_CourseCertificates_CourseId: CREATE INDEX "IX_CourseCertificates_CourseId" ON public."CourseCertificates" USING btree ("CourseId")
- IX_CourseCertificates_UserId_CourseId: CREATE UNIQUE INDEX "IX_CourseCertificates_UserId_CourseId" ON public."CourseCertificates" USING btree ("UserId", "CourseId")
- IX_CourseCertificates_ValidationCode: CREATE UNIQUE INDEX "IX_CourseCertificates_ValidationCode" ON public."CourseCertificates" USING btree ("ValidationCode")
- PK_CourseCertificates: CREATE UNIQUE INDEX "PK_CourseCertificates" ON public."CourseCertificates" USING btree ("Id")

### CourseClasses

| Coluna | Tipo | Aceita nulo |
| --- | --- | --- |
| Id | integer | NO |
| DataRealizacao | timestamp with time zone | NO |
| Local | text | NO |
| VafasDisponiveis | integer | NO |
| Instructor | text | NO |
| AvailableSeats | integer | NO |
| Capacity | integer | NO |
| CourseId | integer | NO |
| EndDate | timestamp with time zone | YES |
| Status | text | NO |

Restrições:
- FK_CourseClasses_Courses_CourseId: FOREIGN KEY ("CourseId") REFERENCES "Courses"("Id") ON DELETE RESTRICT
- PK_CourseClasses: PRIMARY KEY ("Id")

Índices:
- IX_CourseClasses_CourseId: CREATE INDEX "IX_CourseClasses_CourseId" ON public."CourseClasses" USING btree ("CourseId")
- PK_CourseClasses: CREATE UNIQUE INDEX "PK_CourseClasses" ON public."CourseClasses" USING btree ("Id")

### CourseInterests

| Coluna | Tipo | Aceita nulo |
| --- | --- | --- |
| Id | integer | NO |
| CourseId | integer | NO |
| Name | text | NO |
| Email | text | NO |
| Phone | text | YES |
| CreatedAt | timestamp with time zone | NO |
| Status | text | NO |

Restrições:
- FK_CourseInterests_Courses_CourseId: FOREIGN KEY ("CourseId") REFERENCES "Courses"("Id") ON DELETE CASCADE
- PK_CourseInterests: PRIMARY KEY ("Id")

Índices:
- IX_CourseInterests_CourseId_Email: CREATE UNIQUE INDEX "IX_CourseInterests_CourseId_Email" ON public."CourseInterests" USING btree ("CourseId", "Email")
- PK_CourseInterests: CREATE UNIQUE INDEX "PK_CourseInterests" ON public."CourseInterests" USING btree ("Id")

### CourseLessons

| Coluna | Tipo | Aceita nulo |
| --- | --- | --- |
| Id | integer | NO |
| ModuleId | integer | NO |
| Title | text | NO |
| Description | text | NO |
| VideoUrl | text | NO |
| DurationMinutes | integer | NO |
| SortOrder | integer | NO |
| IsActive | boolean | NO |

Restrições:
- FK_CourseLessons_CourseModules_ModuleId: FOREIGN KEY ("ModuleId") REFERENCES "CourseModules"("Id") ON DELETE CASCADE
- PK_CourseLessons: PRIMARY KEY ("Id")

Índices:
- IX_CourseLessons_ModuleId: CREATE INDEX "IX_CourseLessons_ModuleId" ON public."CourseLessons" USING btree ("ModuleId")
- PK_CourseLessons: CREATE UNIQUE INDEX "PK_CourseLessons" ON public."CourseLessons" USING btree ("Id")

### CourseModules

| Coluna | Tipo | Aceita nulo |
| --- | --- | --- |
| Id | integer | NO |
| CourseId | integer | NO |
| Title | text | NO |
| SortOrder | integer | NO |
| IsActive | boolean | NO |

Restrições:
- FK_CourseModules_Courses_CourseId: FOREIGN KEY ("CourseId") REFERENCES "Courses"("Id") ON DELETE CASCADE
- PK_CourseModules: PRIMARY KEY ("Id")

Índices:
- IX_CourseModules_CourseId: CREATE INDEX "IX_CourseModules_CourseId" ON public."CourseModules" USING btree ("CourseId")
- PK_CourseModules: CREATE UNIQUE INDEX "PK_CourseModules" ON public."CourseModules" USING btree ("Id")

### CourseProgresses

| Coluna | Tipo | Aceita nulo |
| --- | --- | --- |
| Id | integer | NO |
| UserId | integer | NO |
| CourseId | integer | NO |
| Percent | double precision | NO |
| CompletedLessons | text | NO |
| LastSeenAt | timestamp with time zone | YES |
| CompletedAt | timestamp with time zone | YES |

Restrições:
- FK_CourseProgresses_Users_UserId: FOREIGN KEY ("UserId") REFERENCES "Users"("ID") ON DELETE CASCADE
- PK_CourseProgresses: PRIMARY KEY ("Id")

Índices:
- IX_CourseProgresses_UserId_CourseId: CREATE UNIQUE INDEX "IX_CourseProgresses_UserId_CourseId" ON public."CourseProgresses" USING btree ("UserId", "CourseId")
- PK_CourseProgresses: CREATE UNIQUE INDEX "PK_CourseProgresses" ON public."CourseProgresses" USING btree ("Id")

### CourseQuestionOptions

| Coluna | Tipo | Aceita nulo |
| --- | --- | --- |
| Id | integer | NO |
| QuestionId | integer | NO |
| Text | text | NO |
| IsCorrect | boolean | NO |
| SortOrder | integer | NO |

Restrições:
- FK_CourseQuestionOptions_CourseQuestions_QuestionId: FOREIGN KEY ("QuestionId") REFERENCES "CourseQuestions"("Id") ON DELETE CASCADE
- PK_CourseQuestionOptions: PRIMARY KEY ("Id")

Índices:
- IX_CourseQuestionOptions_QuestionId: CREATE INDEX "IX_CourseQuestionOptions_QuestionId" ON public."CourseQuestionOptions" USING btree ("QuestionId")
- PK_CourseQuestionOptions: CREATE UNIQUE INDEX "PK_CourseQuestionOptions" ON public."CourseQuestionOptions" USING btree ("Id")

### CourseQuestions

| Coluna | Tipo | Aceita nulo |
| --- | --- | --- |
| Id | integer | NO |
| AssessmentId | integer | NO |
| Statement | text | NO |
| SortOrder | integer | NO |
| IsActive | boolean | NO |

Restrições:
- FK_CourseQuestions_CourseAssessments_AssessmentId: FOREIGN KEY ("AssessmentId") REFERENCES "CourseAssessments"("Id") ON DELETE CASCADE
- PK_CourseQuestions: PRIMARY KEY ("Id")

Índices:
- IX_CourseQuestions_AssessmentId: CREATE INDEX "IX_CourseQuestions_AssessmentId" ON public."CourseQuestions" USING btree ("AssessmentId")
- PK_CourseQuestions: CREATE UNIQUE INDEX "PK_CourseQuestions" ON public."CourseQuestions" USING btree ("Id")

### Courses

| Coluna | Tipo | Aceita nulo |
| --- | --- | --- |
| Id | integer | NO |
| Nome | text | NO |
| Description | text | NO |
| Preco | numeric | NO |
| Image | text | NO |
| Category | text | NO |
| IsActive | boolean | NO |
| DeliveryMode | text | NO |
| WorkloadHours | integer | NO |

Restrições:
- PK_Courses: PRIMARY KEY ("Id")

Índices:
- PK_Courses: CREATE UNIQUE INDEX "PK_Courses" ON public."Courses" USING btree ("Id")

### Enrollments

| Coluna | Tipo | Aceita nulo |
| --- | --- | --- |
| Id | integer | NO |
| OrderId | integer | NO |
| ClassId | integer | YES |
| Status | text | NO |
| StudentId | integer | NO |
| EnrolledAt | timestamp with time zone | NO |
| CourseId | integer | YES |
| ParticipationConfirmedAt | timestamp with time zone | YES |
| ParticipationStartDate | timestamp with time zone | YES |

Restrições:
- FK_Enrollments_CourseClasses_ClassId: FOREIGN KEY ("ClassId") REFERENCES "CourseClasses"("Id") ON DELETE SET NULL
- FK_Enrollments_Courses_CourseId: FOREIGN KEY ("CourseId") REFERENCES "Courses"("Id") ON DELETE SET NULL
- FK_Enrollments_Orders_OrderId: FOREIGN KEY ("OrderId") REFERENCES "Orders"("Id") ON DELETE CASCADE
- FK_Enrollments_Students_StudentId: FOREIGN KEY ("StudentId") REFERENCES "Students"("Id") ON DELETE CASCADE
- PK_Inscricoes: PRIMARY KEY ("Id")

Índices:
- IX_Enrollments_ClassId: CREATE INDEX "IX_Enrollments_ClassId" ON public."Enrollments" USING btree ("ClassId")
- IX_Enrollments_CourseId: CREATE INDEX "IX_Enrollments_CourseId" ON public."Enrollments" USING btree ("CourseId")
- IX_Enrollments_OrderId: CREATE INDEX "IX_Enrollments_OrderId" ON public."Enrollments" USING btree ("OrderId")
- IX_Enrollments_StudentId: CREATE INDEX "IX_Enrollments_StudentId" ON public."Enrollments" USING btree ("StudentId")
- PK_Inscricoes: CREATE UNIQUE INDEX "PK_Inscricoes" ON public."Enrollments" USING btree ("Id")

### OrderItems

| Coluna | Tipo | Aceita nulo |
| --- | --- | --- |
| Id | integer | NO |
| PedidoId | integer | NO |
| TurmaId | integer | YES |
| Quantidade | integer | NO |
| PrecoUnitario | numeric | NO |
| CourseId | integer | YES |
| ItemType | text | NO |
| NameSnapshot | text | NO |

Restrições:
- FK_OrderItems_CourseClasses_TurmaId: FOREIGN KEY ("TurmaId") REFERENCES "CourseClasses"("Id")
- FK_OrderItems_Courses_CourseId: FOREIGN KEY ("CourseId") REFERENCES "Courses"("Id") ON DELETE RESTRICT
- FK_OrderItems_Orders_PedidoId: FOREIGN KEY ("PedidoId") REFERENCES "Orders"("Id") ON DELETE CASCADE
- PK_OrderItems: PRIMARY KEY ("Id")

Índices:
- IX_OrderItems_CourseId: CREATE INDEX "IX_OrderItems_CourseId" ON public."OrderItems" USING btree ("CourseId")
- IX_OrderItems_PedidoId: CREATE INDEX "IX_OrderItems_PedidoId" ON public."OrderItems" USING btree ("PedidoId")
- IX_OrderItems_TurmaId: CREATE INDEX "IX_OrderItems_TurmaId" ON public."OrderItems" USING btree ("TurmaId")
- PK_OrderItems: CREATE UNIQUE INDEX "PK_OrderItems" ON public."OrderItems" USING btree ("Id")

### Orders

| Coluna | Tipo | Aceita nulo |
| --- | --- | --- |
| Id | integer | NO |
| UsuarioId | integer | NO |
| DataPedido | timestamp with time zone | NO |
| Status | text | NO |
| Total | numeric | NO |
| ValorFrete | numeric | NO |
| Installments | integer | YES |
| PaymentMethod | text | NO |
| PromoCode | text | NO |
| PaymentStatus | text | NO |
| GatewayPaymentId | text | YES |
| PaidAt | timestamp with time zone | YES |
| IdempotencyKey | text | YES |

Restrições:
- FK_Orders_Users_UsuarioId: FOREIGN KEY ("UsuarioId") REFERENCES "Users"("ID") ON DELETE CASCADE
- PK_Orders: PRIMARY KEY ("Id")

Índices:
- IX_Orders_UsuarioId_IdempotencyKey: CREATE UNIQUE INDEX "IX_Orders_UsuarioId_IdempotencyKey" ON public."Orders" USING btree ("UsuarioId", "IdempotencyKey") WHERE ("IdempotencyKey" IS NOT NULL)
- PK_Orders: CREATE UNIQUE INDEX "PK_Orders" ON public."Orders" USING btree ("Id")

### PaymentWebhookEvents

| Coluna | Tipo | Aceita nulo |
| --- | --- | --- |
| Id | integer | NO |
| EventId | text | NO |
| OrderId | integer | NO |
| PaymentId | text | NO |
| Status | text | NO |
| ReceivedAt | timestamp with time zone | NO |
| ProcessedAt | timestamp with time zone | YES |

Restrições:
- FK_PaymentWebhookEvents_Orders_OrderId: FOREIGN KEY ("OrderId") REFERENCES "Orders"("Id") ON DELETE CASCADE
- PK_PaymentWebhookEvents: PRIMARY KEY ("Id")

Índices:
- IX_PaymentWebhookEvents_EventId: CREATE UNIQUE INDEX "IX_PaymentWebhookEvents_EventId" ON public."PaymentWebhookEvents" USING btree ("EventId")
- IX_PaymentWebhookEvents_OrderId: CREATE INDEX "IX_PaymentWebhookEvents_OrderId" ON public."PaymentWebhookEvents" USING btree ("OrderId")
- PK_PaymentWebhookEvents: CREATE UNIQUE INDEX "PK_PaymentWebhookEvents" ON public."PaymentWebhookEvents" USING btree ("Id")

### PromoCodes

| Coluna | Tipo | Aceita nulo |
| --- | --- | --- |
| Id | integer | NO |
| Code | text | NO |
| Discount | numeric | NO |
| DiscountType | text | NO |
| StartDate | text | NO |
| EndDate | text | NO |
| IsActive | boolean | NO |
| UsageLimit | integer | YES |
| UsageCount | integer | NO |

Restrições:
- PK_PromoCodes: PRIMARY KEY ("Id")

Índices:
- IX_PromoCodes_Code: CREATE UNIQUE INDEX "IX_PromoCodes_Code" ON public."PromoCodes" USING btree ("Code")
- PK_PromoCodes: CREATE UNIQUE INDEX "PK_PromoCodes" ON public."PromoCodes" USING btree ("Id")

### RefreshTokens

| Coluna | Tipo | Aceita nulo |
| --- | --- | --- |
| Id | integer | NO |
| Token | text | NO |
| UserId | integer | NO |
| ExpiresAt | timestamp with time zone | NO |
| CreatedAt | timestamp with time zone | NO |
| RevokedAt | timestamp with time zone | YES |
| ReplacedByToken | text | YES |

Restrições:
- FK_RefreshTokens_Users_UserId: FOREIGN KEY ("UserId") REFERENCES "Users"("ID") ON DELETE CASCADE
- PK_RefreshTokens: PRIMARY KEY ("Id")

Índices:
- IX_RefreshTokens_Token: CREATE UNIQUE INDEX "IX_RefreshTokens_Token" ON public."RefreshTokens" USING btree ("Token")
- IX_RefreshTokens_UserId: CREATE INDEX "IX_RefreshTokens_UserId" ON public."RefreshTokens" USING btree ("UserId")
- PK_RefreshTokens: CREATE UNIQUE INDEX "PK_RefreshTokens" ON public."RefreshTokens" USING btree ("Id")

### Students

| Coluna | Tipo | Aceita nulo |
| --- | --- | --- |
| Id | integer | NO |
| Name | text | NO |
| Email | text | NO |
| Phone | text | NO |
| CourseId | text | NO |
| CourseName | text | NO |
| EnrollmentDate | text | NO |
| Status | text | NO |
| UserId | integer | YES |

Restrições:
- FK_Students_Users_UserId: FOREIGN KEY ("UserId") REFERENCES "Users"("ID") ON DELETE SET NULL
- PK_Students: PRIMARY KEY ("Id")

Índices:
- IX_Students_UserId: CREATE INDEX "IX_Students_UserId" ON public."Students" USING btree ("UserId")
- PK_Students: CREATE UNIQUE INDEX "PK_Students" ON public."Students" USING btree ("Id")

### Users

| Coluna | Tipo | Aceita nulo |
| --- | --- | --- |
| ID | integer | NO |
| Nome | text | NO |
| Email | text | NO |
| SenhaHash | text | NO |
| Role | text | NO |
| Cpf | text | NO |
| Phone | text | NO |
| Street | text | NO |
| Number | text | NO |
| Complement | text | NO |
| Neighborhood | text | NO |
| City | text | NO |
| State | text | NO |
| ZipCode | text | NO |
| FailedLoginAttempts | integer | NO |
| LockoutEnd | timestamp with time zone | YES |

Restrições:
- PK_Users: PRIMARY KEY ("ID")

Índices:
- IX_Users_Email: CREATE UNIQUE INDEX "IX_Users_Email" ON public."Users" USING btree ("Email")
- PK_Users: CREATE UNIQUE INDEX "PK_Users" ON public."Users" USING btree ("ID")

### __EFMigrationsHistory

| Coluna | Tipo | Aceita nulo |
| --- | --- | --- |
| MigrationId | character varying | NO |
| ProductVersion | character varying | NO |

Restrições:
- PK___EFMigrationsHistory: PRIMARY KEY ("MigrationId")

Índices:
- PK___EFMigrationsHistory: CREATE UNIQUE INDEX "PK___EFMigrationsHistory" ON public."__EFMigrationsHistory" USING btree ("MigrationId")

## Migrations aplicadas

- 20260404160204_InitialCreate — EF 10.0.11
- 20260404161937_AddProductDetails — EF 10.0.11
- 20260503160000_AddCourseProductDetails — EF 10.0.11
- 20260504120000_AddStudentsAndPromoCodes — EF 10.0.11
- 20260522174500_SeedAdminUser — EF 10.0.11
- 20260526120000_AddUserRoles — EF 10.0.11
- 20260526123000_AddUserProfileFields — EF 10.0.11
- 20260526130000_AddUserAddressFields — EF 10.0.11
- 20260526133000_RenameInscricoesToEnrollments — EF 10.0.11
- 20260608150000_AddOrderPaymentFields — EF 10.0.11
- 20260815201619_AddRefreshTokenPaymentProgress — EF 10.0.11
- 20260828115348_AddOrderIdempotencyAndUniqueIndexes — EF 10.0.11
- 20260902125852_IntroduceCoursesAndExplicitClasses — EF 10.0.11
- 20260904090000_AllowCourseClassesWithoutLegacyProduct — EF 10.0.11
- 20260910165744_AddPaymentWebhookEvents — EF 10.0.11
- 20260915144829_AddAuditLogs — EF 10.0.11
- 20260921185324_AddAccountLockout — EF 10.0.11
- 20260922175450_LinkStudentsToUsers — EF 10.0.11
- 20260929152030_MigrateLegacyCoursesAndAddCourseInterests — EF 10.0.11
- 20260929175643_AddDirectCourseReferenceToOrderItems — EF 10.0.11
- 20261004135219_DecoupleCourseClassesFromProducts — EF 10.0.11
- 20261008160715_RemoveProducts — EF 10.0.11
- 20261017164427_AddParticipationConfirmation — EF 10.0.11

## Fontes e funções identificadas

### PIM4API

Git nesta pasta: sim. Arquivos de fonte inventariados: 96.

- backend/Controllers/AuditController.cs (46 linhas)
  - Get (linha 20)
- backend/Controllers/AuthController.cs (127 linhas)
  - Cadastrar (linha 22); Login (linha 44); Refresh (linha 57); Logout (linha 69); AtualizarPerfil (linha 83)
- backend/Controllers/CoursesController.cs (299 linhas)
  - GetAll (linha 22); GetCatalog (linha 25); GetCatalogOptions (linha 29); GetById (linha 33); Create (linha 41); Update (linha 51); Archive (linha 63); Restore (linha 68); RegisterInterest (linha 74); GetClasses (linha 85); GetClass (linha 89); GetClassStudents (linha 99); UpdateEnrollmentStatus (linha 110); TransferEnrollment (linha 130); CreateClass (linha 150); UpdateClass (linha 161); GetModules (linha 174); CreateModule (linha 184); UpdateModule (linha 194); CreateLesson (linha 207); UpdateLesson (linha 220); GetAssessments (linha 239); CreateAssessment (linha 249); UpdateAssessment (linha 259); CreateQuestion (linha 272)
- backend/Controllers/MeController.cs (643 linhas)
  - GetMe (linha 23); ListMyCourses (linha 40); ConfirmParticipation (linha 89); GetProgress (linha 106); GetEnrollment (linha 135); SubmitAssessment (linha 244); GetCertificateStatus (linha 353); IssueCertificate (linha 365); UpdateProgress (linha 411)
- backend/Controllers/OrdersController.cs (165 linhas)
  - CreateOrder (linha 23); GetMyOrders (linha 44); GetById (linha 64); Cancel (linha 75); GetPayment (linha 89); GetAllOrders (linha 112); UpdateStatus (linha 121); Refund (linha 135)
- backend/Controllers/PaymentsController.cs (127 linhas)
  - Webhook (linha 27); GetWebhookEvents (linha 85)
- backend/Controllers/PromoCodesController.cs (122 linhas)
  - GetAll (linha 21); GetById (linha 30); Validate (linha 43); Create (linha 57); Update (linha 76); Delete (linha 99)
- backend/Controllers/StudentsController.cs (104 linhas)
  - GetAll (linha 21); GetById (linha 29); Create (linha 42); Update (linha 55); Delete (linha 73)
- backend/Data/AppDbContext.cs (274 linhas)
- backend/Data/AppDbContextFactory.cs (61 linhas)
  - CreateDbContext (linha 8)
- backend/Data/DatabaseConfiguration.cs (36 linhas)
  - GetConnectionString (linha 8)
- backend/DTOs/AuthDTO.cs (51 linhas)
- backend/DTOs/AuthExtrasDTO.cs (28 linhas)
- backend/DTOs/CourseDTO.cs (193 linhas)
- backend/DTOs/CourseInterestDTO.cs (17 linhas)
- backend/DTOs/EnrollmentDTO.cs (104 linhas)
- backend/DTOs/OrderDTO.cs (28 linhas)
- backend/DTOs/ParticipationDTO.cs (10 linhas)
- backend/DTOs/ProgressDTO.cs (9 linhas)
- backend/DTOs/PromoCodeDTO.cs (28 linhas)
- backend/DTOs/StudentDTO.cs (28 linhas)
- backend/Migrations/20260404160204_InitialCreate.cs (230 linhas)
- backend/Migrations/20260404161937_AddProductDetails.cs (108 linhas)
- backend/Migrations/20260503160000_AddCourseProductDetails.cs (82 linhas)
- backend/Migrations/20260504120000_AddStudentsAndPromoCodes.cs (85 linhas)
- backend/Migrations/20260522174500_SeedAdminUser.cs (37 linhas)
- backend/Migrations/20260526120000_AddUserRoles.cs (36 linhas)
- backend/Migrations/20260526123000_AddUserProfileFields.cs (41 linhas)
- backend/Migrations/20260526130000_AddUserAddressFields.cs (35 linhas)
- backend/Migrations/20260526133000_RenameInscricoesToEnrollments.cs (190 linhas)
- backend/Migrations/20260608150000_AddOrderPaymentFields.cs (51 linhas)
- backend/Migrations/20260815201619_AddRefreshTokenPaymentProgress.cs (120 linhas)
- backend/Migrations/20260828115348_AddOrderIdempotencyAndUniqueIndexes.cs (69 linhas)
- backend/Migrations/20260828150000_DeleteEquipmentMockData.cs (34 linhas)
- backend/Migrations/20260902125852_IntroduceCoursesAndExplicitClasses.cs (164 linhas)
- backend/Migrations/20260904090000_AllowCourseClassesWithoutLegacyProduct.cs (41 linhas)
- backend/Migrations/20260910165744_AddPaymentWebhookEvents.cs (59 linhas)
- backend/Migrations/20260915144829_AddAuditLogs.cs (59 linhas)
- backend/Migrations/20260916090000_AddEadCoursesContent.cs (173 linhas)
- backend/Migrations/20260916103000_AddEadAssessments.cs (103 linhas)
- backend/Migrations/20260916110000_AddEadAssessmentAttempts.cs (123 linhas)
- backend/Migrations/20260916113000_AddCourseCertificates.cs (65 linhas)
- backend/Migrations/20260921185324_AddAccountLockout.cs (447 linhas)
- backend/Migrations/20260922175450_LinkStudentsToUsers.cs (79 linhas)
- backend/Migrations/20260929152030_MigrateLegacyCoursesAndAddCourseInterests.cs (71 linhas)
- backend/Migrations/20260929175643_AddDirectCourseReferenceToOrderItems.cs (58 linhas)
- backend/Migrations/20261004135219_DecoupleCourseClassesFromProducts.cs (126 linhas)
- backend/Migrations/20261008160715_RemoveProducts.cs (103 linhas)
- backend/Migrations/20261017164427_AddParticipationConfirmation.cs (40 linhas)
- backend/Models/AuditLog.cs (16 linhas)
- backend/Models/Course.cs (23 linhas)
- backend/Models/CourseAssessment.cs (15 linhas)
- backend/Models/CourseAssessmentAnswer.cs (15 linhas)
- backend/Models/CourseAssessmentAttempt.cs (19 linhas)
- backend/Models/CourseCertificate.cs (14 linhas)
- backend/Models/CourseClass.cs (26 linhas)
- backend/Models/CourseInterest.cs (15 linhas)
- backend/Models/CourseLesson.cs (16 linhas)
- backend/Models/CourseModule.cs (14 linhas)
- backend/Models/CourseProgress.cs (19 linhas)
- backend/Models/CourseQuestion.cs (14 linhas)
- backend/Models/CourseQuestionOption.cs (13 linhas)
- backend/Models/Enrollment.cs (20 linhas)
- backend/Models/Order.cs (26 linhas)
- backend/Models/OrderItem.cs (20 linhas)
- backend/Models/PaymentWebhookEvent.cs (15 linhas)
- backend/Models/PromoCode.cs (16 linhas)
- backend/Models/RefreshToken.cs (20 linhas)
- backend/Models/Student.cs (19 linhas)
- backend/Models/User.cs (25 linhas)
- backend/Program.cs (204 linhas)
- backend/Services/AuditService.cs (38 linhas)
  - Add (linha 14)
- backend/Services/AuthService.cs (349 linhas)
  - RegisterAsync (linha 23); LoginAsync (linha 73); RefreshAsync (linha 141); LogoutAsync (linha 200); UpdateProfileAsync (linha 224)
- backend/Services/AuthServiceResult.cs (28 linhas)
  - Ok (linha 9); Fail (linha 18)
- backend/Services/CourseService.cs (1171 linhas)
  - GetAllAsync (linha 18); GetCatalogAsync (linha 38); GetCatalogOptionsAsync (linha 124); GetByIdAsync (linha 162); RegisterInterestAsync (linha 177); CreateAsync (linha 210); UpdateAsync (linha 226); SetActiveAsync (linha 256); GetClassesAsync (linha 277); GetClassAsync (linha 293); GetClassStudentsAsync (linha 305); UpdateEnrollmentStatusAsync (linha 324); TransferEnrollmentAsync (linha 389); GetModulesAsync (linha 481); CreateModuleAsync (linha 500); UpdateModuleAsync (linha 542); CreateLessonAsync (linha 575); UpdateLessonAsync (linha 607); GetAssessmentsAsync (linha 658); CreateAssessmentAsync (linha 674); UpdateAssessmentAsync (linha 708); CreateQuestionAsync (linha 739); CreateClassAsync (linha 771); UpdateClassAsync (linha 811)
- backend/Services/EnrollmentService.cs (46 linhas)
  - CreateEnrollmentsForCourseAsync (linha 9)
- backend/Services/InventoryService.cs (77 linhas)
  - ValidateAndReserveAsync (linha 11)
- backend/Services/LoginResponse.cs (12 linhas)
- backend/Services/OrderResponses.cs (50 linhas)
- backend/Services/OrderService.cs (553 linhas)
  - CreateAsync (linha 37); GetMyOrdersAsync (linha 147); GetByIdAsync (linha 206); CancelAsync (linha 225); ExpirePendingOrdersAsync (linha 249); RefundAsync (linha 276); ProcessPaymentWebhookAsync (linha 323); ProcessPaymentWebhookAsync (linha 332); GetAllAsync (linha 451); UpdateStatusAsync (linha 490)
- backend/Services/ParticipationService.cs (59 linhas)
  - Describe (linha 10); ConfirmAsync (linha 25)
- backend/Services/PendingOrderExpirationService.cs (58 linhas)
- backend/Services/PromoCodeService.cs (262 linhas)
  - GetAllAsync (linha 22); GetByIdAsync (linha 30); ValidateAsync (linha 37); CreateAsync (linha 54); UpdateAsync (linha 79); DeleteAsync (linha 109); ApplyDiscountAsync (linha 123)
- backend/Services/ServiceResult.cs (18 linhas)
  - Ok (linha 7); Fail (linha 12)
- backend/Services/StudentService.cs (229 linhas)
  - GetAllAsync (linha 25); GetByIdAsync (linha 32); CreateAsync (linha 41); UpdateAsync (linha 72); DeleteAsync (linha 135)
- backend/Services/TokenService.cs (69 linhas)
  - GetAccessTokenLifetime (linha 17); GenerateToken (linha 28); GetAccessTokenLifetimeSeconds (linha 63)
- backend.Tests/AuthorizationPolicyTests.cs (50 linhas)
  - AdministrativeActionsRequireAdminRole (linha 19); AdministrativeControllersRequireAdminRole (linha 31)
- backend.Tests/AuthSecurityTests.cs (128 linhas)
  - RefreshRotatesHashedTokenAndRejectsReuse (linha 16); RefreshMigratesLegacyPlainTextTokenToHash (linha 42); CreateAsync (linha 74); AddRefreshTokenAsync (linha 96); CreateService (linha 107); DisposeAsync (linha 121)
- backend.Tests/CourseDomainTests.cs (715 linhas)
  - CatalogDoesNotAdvertiseSeatsAfterAClassHasStarted (linha 14); CreatedClassesReferenceCoursesWithoutALegacyProductRelation (linha 43); EnrollmentUsesTheExplicitCourseClassRelation (linha 82); CoursePurchaseReservesSeatsFromTheSelectedClass (linha 155); CoursePurchaseMustProvideAnExplicitClass (linha 190); EadCoursePurchaseDoesNotRequireAClass (linha 212); EadCoursePurchaseRejectsAProvidedClass (linha 234); CoursePurchaseRejectsAClassFromAnotherCourse (linha 265); PublicCatalogReturnsOnlyActiveCoursesWithAvailableFutureClasses (linha 294); EadCoursesArePreservedButHiddenWhileTheFeatureIsDisabled (linha 345); CourseCanBeArchivedAndRestoredWithoutDeletingItsHistory (linha 384); EnrollmentStatusChangesUpdateClassAvailability (linha 415); ActiveEnrollmentCanBeTransferredBetweenClasses (linha 474); CoursePurchaseRejectsCompletedClasses (linha 549); CoursePurchaseRejectsMoreThanMaximumQuantity (linha 579); NewOrdersRejectArchivedCourses (linha 609); CatalogOptionsIncludeOnlyAvailableActiveCourseData (linha 621); CourseWithClassesCannotBeDeleted (linha 652); ClassCannotBeSavedWithoutAValidCourse (linha 669); CreateAsync (linha 694); DisposeAsync (linha 707)
- backend.Tests/DatabaseConfigurationTests.cs (69 linhas)
  - ExistingConnectionStringTakesPrecedence (linha 10); RenderPropertiesPreserveSpecialCharactersInPassword (linha 17); MissingDatabaseConfigurationFailsWithoutExposingSecrets (linha 29); InvalidPortIsRejected (linha 39); InvalidSslModeIsRejected (linha 46)
- backend.Tests/MeAuthorizationTests.cs (178 linhas)
  - UserCanReadOwnEnrollmentButNotAnotherUsersEnrollment (linha 16); AnotherUserCannotReadCourseProgressOrCertificateStatus (linha 30); CancelledEnrollmentDoesNotGrantCourseAccess (linha 43); CreateAsync (linha 82); CreateController (linha 142); DisposeAsync (linha 171)
- backend.Tests/OrderPaymentFlowTests.cs (416 linhas)
  - CreatingOrderDoesNotCreateEnrollmentBeforePayment (linha 14); ApprovedPaymentCreatesEnrollmentOnlyOnce (linha 29); ApprovedEadPaymentCreatesEnrollmentWithoutClass (linha 48); CancellingPendingOrderReleasesReservedStock (linha 72); RefundingPaidOrderCancelsEnrollmentsAndReleasesStockOnlyOnce (linha 87); AdministrativeRefundIsRestrictedToPaidOrdersAndIsIdempotent (linha 106); RefusedPaymentReleasesReservedStock (linha 134); CreatingCourseOrderIgnoresClientProvidedShipping (linha 149); ExpiringPendingOrderReleasesReservationOnlyOnce (linha 164); UserCannotReadAnotherUsersOrder (linha 185); UserCannotCancelAnotherUsersOrder (linha 200); OrderPreservesPurchasedCourseNameAndPriceAfterCatalogChanges (linha 218); SplitItemsCannotBypassCourseQuantityLimit (linha 240); LaterEventDoesNotRegressApprovedPayment (linha 259); DifferentFailureEventsReleaseSeatsOnlyOnce (linha 276); ApprovalAfterExpirationDoesNotEnrollWithoutReservedSeats (linha 289); RefundBeforeApprovalIsRejected (linha 306); EventCannotBeReusedForAnotherOrder (linha 318); CreateAsync (linha 355); CreateOrderService (linha 399); DisposeAsync (linha 408)
- backend.Tests/ParticipationTests.cs (147 linhas)
  - ConfirmationWindowHasExactBoundaries (linha 19); InactiveEnrollmentOrClassCannotConfirm (linha 32); EadHasNoAttendanceConfirmation (linha 41); ConfirmationIsOwnedPersistentAndIdempotent (linha 49); DateChangeRequiresNewConfirmation (linha 68); TooEarlyOrStartedCannotWriteConfirmation (linha 82); StudentCancellationAndReactivationDoNotRestoreOldConfirmation (linha 95); GetUtcNow (linha 118); CreateAsync (linha 127); DisposeAsync (linha 144)
- backend.Tests/PaymentWebhookSecurityTests.cs (58 linhas)
  - WebhookRejectsRequestWhenSecretIsNotConfigured (linha 12); WebhookRejectsMissingOrInvalidSignature (linha 26)
- backend.Tests/PromoCodeServiceTests.cs (39 linhas)
  - AppliesPercentageDiscountAndIncrementsUsage (linha 11)
- backend.Tests/StudentServiceTests.cs (125 linhas)
  - CreateRejectsDuplicateStudentForSameCourseAndEmail (linha 14); DeleteRejectsStudentWithEnrollments (linha 39); CreateAsync (linha 104); DisposeAsync (linha 117)

### PIM4Front

Git nesta pasta: sim. Arquivos de fonte inventariados: 58.

- e2e/enrollment-flow.spec.ts (249 linhas)
- playwright.config.ts (30 linhas)
- src/app/app.config.ts (14 linhas)
- src/app/app.routes.ts (66 linhas)
- src/app/app.spec.ts (34 linhas)
- src/app/app.ts (46 linhas)
  - onWindowScroll (linha 23); onWindowMouseMove (linha 36); isAdmin (linha 42)
- src/app/core/config/api.config.ts (4 linhas)
- src/app/core/guards/admin.guard.ts (14 linhas)
- src/app/core/guards/auth.guard.ts (17 linhas)
- src/app/core/interceptors/auth.interceptor.ts (42 linhas)
- src/app/core/models/audit.models.ts (12 linhas)
- src/app/core/models/auth.models.ts (50 linhas)
- src/app/core/models/course.models.ts (156 linhas)
- src/app/core/models/enrollment.models.ts (114 linhas)
- src/app/core/models/order.models.ts (50 linhas)
- src/app/core/models/payment.models.ts (10 linhas)
- src/app/core/models/promo-codes.models.ts (23 linhas)
- src/app/core/models/student.models.ts (32 linhas)
- src/app/core/services/audit.service.ts (17 linhas)
  - getAll (linha 11)
- src/app/core/services/auth.service.ts (89 linhas)
  - login (linha 17); register (linha 23); updateProfile (linha 30); logout (linha 36); getToken (linha 43); getRefreshToken (linha 47); refresh (linha 51); isAuthenticated (linha 65); isAdmin (linha 69); setSession (linha 73); setUser (linha 79); getStoredUser (linha 84)
- src/app/core/services/cart.service.ts (82 linhas)
  - add (linha 21); addCourse (linha 25); updateQuantity (linha 47); remove (linha 56); clear (linha 60); setItems (linha 62); getStoredItems (linha 67)
- src/app/core/services/course.service.ts (177 linhas)
  - getAll (linha 28); getCatalog (linha 34); getCatalogOptions (linha 51); getById (linha 55); create (linha 59); update (linha 63); archive (linha 67); restore (linha 71); getClasses (linha 75); createClass (linha 79); getModules (linha 91); createModule (linha 95); updateModule (linha 99); createLesson (linha 103); getAssessments (linha 119); createAssessment (linha 123); getClassStudents (linha 149)
- src/app/core/services/enrollment.service.ts (58 linhas)
  - confirmParticipation (linha 19); getMine (linha 23); getById (linha 27); getProgress (linha 31); updateProgress (linha 35); getCertificateStatus (linha 50); issueCertificate (linha 54)
- src/app/core/services/order.service.ts (43 linhas)
  - create (linha 11); getMyOrders (linha 17); getById (linha 21); getAll (linha 25); cancel (linha 29); updateStatus (linha 33); refund (linha 40)
- src/app/core/services/payment.service.ts (17 linhas)
  - getWebhookEvents (linha 11)
- src/app/core/services/promo-code.service.ts (33 linhas)
  - getAll (linha 11); create (linha 15); update (linha 19); delete (linha 23); validate (linha 27)
- src/app/core/services/student.service.ts (27 linhas)
  - getAll (linha 11); create (linha 15); update (linha 19); delete (linha 23)
- src/app/features/account/account.spec.ts (77 linhas)
- src/app/features/account/account.ts (154 linhas)
  - formatOrderDate (linha 49); cancelOrder (linha 56); paymentStatusLabel (linha 75); loadOrders (linha 87); save (linha 95); logout (linha 115); fillForm (linha 120); normalizeRequest (linha 142)
- src/app/features/admin/admin.spec.ts (74 linhas)
- src/app/features/admin/admin.ts (126 linhas)
  - setTab (linha 60); setCourses (linha 66); setOrders (linha 71); setPromoCodes (linha 75); handleNotification (linha 79); loadAdminData (linha 84)
- src/app/features/admin/admin.types.ts (8 linhas)
- src/app/features/admin/components/admin-audit/admin-audit.ts (139 linhas)
  - setSearch (linha 48); setActionFilter (linha 52); setEntityFilter (linha 56); auditActionLabel (linha 60); auditEntityLabel (linha 76); formatAuditValue (linha 88); formatAuditDate (linha 103); exportAuditCsv (linha 113)
- src/app/features/admin/components/admin-classes/admin-classes.ts (292 linhas)
  - openClassForm (linha 39); closeForm (linha 59); saveClass (linha 65); openClassModal (linha 111); closeClassModal (linha 121); studentsForCourse (linha 127); participationLabel (linha 131); exportClassStudents (linha 136); updateStudentStatus (linha 167); transferStudent (linha 197); toNumber (linha 232); studentStatusLabel (linha 236); patchClassSeats (linha 246); toDateTimeLocal (linha 277); slugify (linha 283)
- src/app/features/admin/components/admin-coupons/admin-coupons.ts (135 linhas)
  - formatPrice (linha 43); openCreateForm (linha 47); closeForm (linha 53); resetForm (linha 58); save (linha 72); editCoupon (linha 110); deleteCoupon (linha 125)
- src/app/features/admin/components/admin-courses/admin-courses.ts (164 linhas)
  - formatPrice (linha 42); openCreateForm (linha 46); closeForm (linha 52); resetForm (linha 57); editCourse (linha 70); save (linha 84); toggleCourseActive (linha 128)
- src/app/features/admin/components/admin-dashboard/admin-dashboard.ts (311 linhas)
  - setPeriod (linha 249); formatPrice (linha 253); formatPercent (linha 257); orderStatusLabel (linha 261); exportReports (linha 272); formatDayLabel (linha 300); formatShortDate (linha 307)
- src/app/features/admin/components/admin-ead-content/admin-ead-content.ts (370 linhas)
  - ngOnChanges (linha 86); selectCourse (linha 98); selectModule (linha 106); saveModule (linha 111); saveLesson (linha 148); saveAssessment (linha 197); saveQuestion (linha 240); correctOptionText (linha 309); patchCourse (linha 316); patchModule (linha 325); patchAssessment (linha 341); syncAssessmentForm (linha 357); sortModules (linha 366)
- src/app/features/admin/components/admin-orders/admin-orders.ts (195 linhas)
  - setOrderSearch (linha 54); setOrderStatusFilter (linha 58); setPaymentStatusFilter (linha 62); formatPrice (linha 66); formatDate (linha 70); paymentMethodLabel (linha 82); orderStatusLabel (linha 92); paymentStatusLabel (linha 103); updateOrderStatus (linha 115); refundOrder (linha 127); exportOrdersCsv (linha 139); openDetails (linha 167); closeDetails (linha 172); itemTotal (linha 177); loadWebhookEvents (linha 181)
- src/app/features/admin/components/admin-students/admin-students.ts (77 linhas)
  - setSearch (linha 51); setStatusFilter (linha 55); studentStatusLabel (linha 59); openStudent (linha 69); closeStudent (linha 73)
- src/app/features/auth/login/login.spec.ts (129 linhas)
- src/app/features/auth/login/login.ts (113 linhas)
  - setMode (linha 45); submitLogin (linha 51); submitRegister (linha 73); navigateAfterAuth (linha 107)
- src/app/features/cart/cart.spec.ts (140 linhas)
- src/app/features/cart/cart.ts (93 linhas)
  - itemSubtotal (linha 28); increase (linha 32); decrease (linha 40); updateQuantity (linha 44); remove (linha 57); clear (linha 61); imageUrl (linha 65); setFallbackImage (linha 69); formatPrice (linha 79); stockLabel (linha 83); availableQuantity (linha 89)
- src/app/features/checkout/checkout.spec.ts (197 linhas)
- src/app/features/checkout/checkout.ts (263 linhas)
  - formatPrice (linha 99); applyCoupon (linha 103); removeCoupon (linha 132); openAddressModal (linha 138); closeAddressModal (linha 143); saveAddress (linha 147); submit (linha 191); fillAddressForm (linha 244)
- src/app/features/course-detail/course-detail.ts (168 linhas)
  - imageUrl (linha 61); setFallbackImage (linha 65); decreaseQuantity (linha 70); increaseQuantity (linha 76); selectClass (linha 85); classStatusLabel (linha 93); classPeriod (linha 106); addToCart (linha 122); isEad (linha 158); availableQuantity (linha 162)
- src/app/features/enrollment-detail/enrollment-detail.ts (301 linhas)
  - formatDate (linha 113); statusLabel (linha 120); isLessonCompleted (linha 132); totalLessons (linha 136); completedLessons (linha 141); toggleLesson (linha 145); selectedOption (linha 177); selectOption (linha 181); remainingAttempts (linha 194); submitAssessment (linha 202); issueCertificate (linha 251); loadProgress (linha 272); loadCertificateStatus (linha 286); refreshCertificateStatus (linha 295)
- src/app/features/home/home.spec.ts (160 linhas)
- src/app/features/home/home.ts (183 linhas)
  - updateSearch (linha 53); updateCategory (linha 59); updateCity (linha 65); updateStartDate (linha 69); updateEndDate (linha 73); applyFilters (linha 77); clearFilters (linha 82); updateSort (linha 100); changePage (linha 106); imageUrl (linha 112); setFallbackImage (linha 116); formatPrice (linha 126); availabilityLabel (linha 142); hasAvailability (linha 151); loadCourses (linha 155)
- src/app/features/landing/landing.spec.ts (45 linhas)
- src/app/features/landing/landing.ts (78 linhas)
  - ngAfterViewInit (linha 39); imageUrl (linha 60); setFallbackImage (linha 64); formatPrice (linha 74)
- src/app/features/order-detail/order-detail.ts (62 linhas)
  - formatDate (linha 40); statusLabel (linha 48)
- src/app/features/student/student.spec.ts (93 linhas)
- src/app/features/student/student.ts (60 linhas)
  - load (linha 40); statusLabel (linha 52); count (linha 56)
- src/app/shared/enrollment-schedule/enrollment-schedule.spec.ts (84 linhas)
- src/app/shared/enrollment-schedule/enrollment-schedule.ts (91 linhas)
  - confirm (linha 78)
- src/main.ts (11 linhas)

### PIM4Mobile

Git nesta pasta: não encontrado. Arquivos de fonte inventariados: 20.

- lib/core/api_client.dart (291 linhas)
  - toString (linha 14); Function (linha 32); Function (linha 33); Function (linha 34); Function (linha 35); _storageAction (linha 44); _validSession (linha 50); restore (linha 62); _save (linha 80); _expire (linha 92); _send (linha 101); login (linha 152); _refresh (linha 162); _refreshOnce (linha 178); _authenticated (linha 189); confirmParticipation (linha 216); enrollments (linha 229); enrollment (linha 236); orders (linha 245); order (linha 247); updateProfile (linha 257); logout (linha 269); dispose (linha 286)
- lib/core/app_config.dart (37 linhas)
- lib/core/course_reminders.dart (344 linhas)
  - read (linha 12); write (linha 13); read (linha 19); write (linha 21); Function (linha 48); initialize (linha 60); _notify (linha 96); _enqueue (linha 100); _sessionChanged (linha 115); _onTap (linha 160); _saveLedger (linha 177); _sync (linha 183); activate (linha 241); deactivate (linha 263); preview (linha 283); didChangeAppLifecycleState (linha 317); dispose (linha 332)
- lib/core/formatters.dart (29 linhas)
  - textOf (linha 2); dateOf (linha 5); moneyOf (linha 11); statusOf (linha 16)
- lib/core/reminder_device.dart (158 linhas)
  - initialize (linha 78); requestPermission (linha 79); hasPermission (linha 80); pendingIds (linha 81); schedule (linha 82); cancel (linha 83); cancelAll (linha 84); preview (linha 85); initialize (linha 95); requestPermission (linha 127); hasPermission (linha 130); pendingIds (linha 133); schedule (linha 136); cancel (linha 146); cancelAll (linha 148); preview (linha 150)
- lib/core/session_store.dart (20 linhas)
  - read (linha 4); write (linha 5); clear (linha 6); read (linha 14); write (linha 16); clear (linha 18)
- lib/features/detail_pages.dart (155 linhas)
  - build (linha 13); build (linha 99)
- lib/features/login_page.dart (165 linhas)
  - _login (linha 19); dispose (linha 35); build (linha 42)
- lib/features/profile_page.dart (222 linhas)
  - initState (linha 35); dispose (linha 45); _save (linha 52); _logout (linha 76); _field (linha 112); build (linha 148)
- lib/features/student_home.dart (319 linhas)
  - build (linha 22); build (linha 76); build (linha 259)
- lib/main.dart (37 linhas)
  - main (linha 8)
- lib/student_app.dart (132 linhas)
  - initState (linha 23); _openEnrollment (linha 30); _sessionChanged (linha 47); dispose (linha 60); build (linha 67)
- lib/ui/enrollment_schedule.dart (185 linhas)
  - courseStartMessage (linha 7); initState (linha 50); didUpdateWidget (linha 58); dispose (linha 68); _confirm (linha 73); build (linha 96)
- lib/ui/reminder_settings.dart (65 linhas)
  - build (linha 9)
- lib/ui/shared.dart (177 linhas)
  - openSite (linha 7); build (linha 31); build (linha 55); initState (linha 94); _reload (linha 99); build (linha 109); build (linha 159)
- test/api_client_test.dart (156 linhas)
  - main (linha 11)
- test/course_reminders_test.dart (300 linhas)
  - read (linha 14); write (linha 16); Function (linha 27); initialize (linha 29); requestPermission (linha 34); hasPermission (linha 36); pendingIds (linha 38); schedule (linha 40); cancel (linha 46); cancelAll (linha 51); preview (linha 57); main (linha 61); enrollment (linha 65)
- test/enrollment_schedule_test.dart (162 linhas)
  - main (linha 11); item (linha 15); apiFor (linha 43)
- test/real_api_smoke_test.dart (67 linhas)
  - main (linha 9)
- test/widget_test.dart (138 linhas)
  - read (linha 14); write (linha 16); clear (linha 18); session (linha 20); main (linha 54)

### PIM4Desktop

Git nesta pasta: sim. Arquivos de fonte inventariados: 9.

- App.xaml (9 linhas)
- App.xaml.cs (32 linhas)
- AssemblyInfo.cs (11 linhas)
- Configuration/AppConfiguration.cs (37 linhas)
- MainWindow.xaml (325 linhas)
- MainWindow.xaml.cs (188 linhas)
- Models/ApiModels.cs (154 linhas)
- Services/ApiClient.cs (172 linhas)
  - LoginAsync (linha 30); GetCoursesAsync (linha 48); GetOrdersAsync (linha 51); GetStudentsAsync (linha 54); GetPromoCodesAsync (linha 57); GetAuditLogsAsync (linha 60); CreateCourseAsync (linha 63); SetCourseActiveAsync (linha 66); CreateClassAsync (linha 69); CreatePromoCodeAsync (linha 72); LogoutAsync (linha 75); ClearSession (linha 87); Dispose (linha 169)
- ViewModels/MainViewModel.cs (201 linhas)
  - LoginAsync (linha 55); RefreshAsync (linha 72); CreateCourseAsync (linha 74); SetCourseActiveAsync (linha 80); CreateClassAsync (linha 86); CreatePromoCodeAsync (linha 92); LogoutAsync (linha 98); Dispose (linha 198)

