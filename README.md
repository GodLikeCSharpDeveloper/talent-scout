# TalentScout

ASP.NET Core 10 API для рекрутинга: кандидаты → проверка требований → первое сообщение → анализ ответа → приглашение → бронирование интервью. Расширение браузера в этот проект пока не входит.

## Запуск

Нужен Docker с Linux containers.

```powershell
Copy-Item .env.example .env
# Заполнить POSTGRES_PASSWORD и API_KEY в .env.
# API_KEY: случайная строка длиной минимум 32 символа.
docker compose up --build -d
```

API: `http://localhost:5262`. PostgreSQL хранит данные в именованном томе и не публикует порт на хост. API автоматически применяет миграции перед началом обработки запросов через `MigrateAsync`; отдельная команда или сервис не нужны. `docker compose down` сохраняет данные.

Каждый запрос к API и `/openapi/v1.json` требует заголовок `X-Api-Key`. `/health` доступен без ключа и использует стандартный ASP.NET Core Health Checks.

## Структура и архитектура

```text
TalentScout.Api/
  Features/
    Campaigns/       # Контроллер, хендлеры, запросы, валидация
    Candidates/
    Pipeline/
    Conversations/
    Interviews/
    Audit/
  Integrations/     # LinkedIn, AI, scheduling
  Infrastructure/   # HTTP, безопасность, регистрация API
  Shared/           # Валидация и преобразование ожидаемых ошибок
TalentScout.Data/
  Entities/         # Все сущности и статусы
  Persistence/      # AppDbContext, настройки, UnitOfWork, design-time factory
    Migrations/
  Repositories/
    Contracts/      # Интерфейсы репозиториев и IUnitOfWork
  ReadModels/       # Независимые проекции запросов
  Exceptions/       # Ошибки хранения
  DependencyInjection.cs
TalentScout.Api.Tests/
docs/openapi.json
```

В каждой фиче находятся тонкий контроллер, папки `Handlers`, `Requests` и при необходимости `Responses`, API-контракты. Сущности находятся в отдельной библиотеке `TalentScout.Data`. Контроллеры не используют DbContext: они вызывают хендлеры и возвращают `Ardalis.Result<T>`. Атрибут библиотеки `[TranslateResultToActionResult]` преобразует результат в HTTP-ответ; `ResultConvention` задаёт коды и формат Problem Details. Ожидаемые ошибки преобразуются в Problem Details с кодами 400/401/404/409/502/503; неожиданные исключения обрабатываются централизованно. Создание кампании возвращает 201 и Location; остальные успешные операции — 200. Общая инфраструктура ограничена хранением, конфигурацией и обработкой ошибок. Три независимых фоновых воркера оценивают профили, отправляют сообщения и анализируют ответы. Каждый использует отдельный запрос очереди; расписание, scopes и обработка ошибок вынесены в `PipelineExecutionService`. HTTP-команды не выбирают действие по текущему статусу: каждая выполняет только указанную операцию. Хендлеры внедряются через конструкторы контроллеров; HTTP-методы принимают только маршрут, query/body и CancellationToken. Доступ к данным выполняется через шесть специализированных репозиториев: интерфейсы находятся в `TalentScout.Data/Repositories/Contracts`, реализации EF Core — в `TalentScout.Data/Repositories`. Хендлеры и worker не зависят от DbContext и не выполняют LINQ-запросов к базе. `IUnitOfWork` сохраняет изменения и аудит общей транзакцией; атомарный захват отправки и SQL для лимитов инкапсулированы в репозитории сообщений. Репозитории возвращают готовые сущности и DTO, не раскрывают IQueryable.

`Candidate` — уникальный профиль. `Enrollment` — участие кандидата в конкретной кампании: статус, оценка и факты отправки. Один профиль может участвовать в разных кампаниях, при этом их статусы не смешиваются. Возраст и другие демографические параметры не используются как поля фильтрации.

## Контракты API

OpenAPI генерируется из приложения по `/openapi/v1.json`; снимок для просмотра и импорта сохранён в `docs/openapi.json`. После изменения маршрутов снимок следует обновить из запущенного API.

| Метод | Путь | Действие |
|---|---|---|
| POST / GET | `/api/campaigns` | Создать / получить кампании |
| GET | `/api/campaigns/{id}` | Кампания |
| PUT | `/api/campaigns/{id}/status` | `Active`, `Paused`, `Stopped` |
| POST | `/api/campaigns/{id}/search` | Добор новых участников кампании, с пропуском существующих |
| POST / GET | `/api/candidates` | Импорт / список профилей |
| GET | `/api/candidates/{id}` | Профиль |
| POST / GET | `/api/campaigns/{id}/candidates` | Включение / ранжированный список участников |
| GET | `/api/enrollments/{id}` | Прочитать текущее состояние участия |
| GET | `/api/campaigns/{id}/search-batches` | Партии поиска и причины остановки |
| PUT | `/api/campaigns/{id}/approval-settings` | Настроить автоподтверждение |
| POST | `/api/campaigns/{id}/outreach/start` | Подтвердить и запустить выбранных |
| POST | `/api/campaigns/{id}/outreach/exclude` | Исключить выбранных до начала рассылки |
| POST | `/api/enrollments/{id}/screen-profile` | Оценить профиль, применить настройку автоподтверждения |
| POST | `/api/enrollments/{id}/send-introduction` | Только подготовить и отправить первое сообщение |
| POST | `/api/enrollments/{id}/analyze-reply` | Только проанализировать следующий необработанный ответ |
| POST | `/api/enrollments/{id}/send-interview-invitation` | Только подготовить и отправить приглашение |
| POST | `/api/enrollments/{id}/review` | Решение рекрутера для `NeedsReview` |
| POST | `/api/enrollments/{id}/messages/replies` | Принять нормализованный ответ |
| GET | `/api/enrollments/{id}/messages` | История сообщений и анализ |
| POST | `/api/messages/{id}/reconcile` | Ручная сверка неизвестного результата отправки |
| POST | `/api/interviews/bookings` | Принять нормализованное бронирование |
| GET | `/api/interviews` | Интервью, опционально `campaignId` |
| GET | `/api/campaigns/{id}/events` | Журнал действий |

Списки принимают `page` (от 1) и `pageSize` (1–100). Участники кампании также фильтруются по `status`. Поиск принимает `limit` (1–100) и `cursor`, возвращает `nextCursor`. Оценки и confidence — числа от 0 до 1. Enum в JSON — строки. Ошибки — Problem Details: 400 валидация, 401 ключ, 404 ресурс, 409 состояние/конкуренция/конфликт ID, 502 неверный ответ адаптера, 503 провайдер не настроен или неизвестен результат отправки.

Пример создания кампании:

```json
{
  "name": "Backend developers",
  "jobTitle": ".NET developer",
  "description": "Разработка API продукта",
  "requirements": "C#, ASP.NET Core, PostgreSQL; минимум 3 года опыта",
  "searchQuery": ".NET developer",
  "location": "Europe",
  "recruiter": "Recruiter name",
  "interviewUrl": "https://calendly.com/your-team/interview",
  "minimumFitScore": 0.7
}
```

Последовательность работы:

1. Создать кампанию в `Draft`, задать критерии и ссылку интервью.
2. Выполнить поиск или импортировать профиль, затем включить его в кампанию. Полученный `enrollmentId` используется во всех командах ниже.
3. Перевести кампанию в `Active` через `{ "status": "Active" }`.
4. `POST /api/enrollments/{id}/screen-profile` — AI оценивает профиль. Результат: `Screened` или `Rejected`. Сообщения этот endpoint никогда не отправляет.
5. `POST /api/enrollments/{id}/send-introduction` — для одобренного `Screened` с начатой рассылкой AI готовит первое сообщение, оно сохраняется в очереди и отправляется через LinkedIn при доступном лимите. Никаких анализа ответов и приглашений этот endpoint не выполняет. Результат содержит `messageId`, `kind`, `delivery`, `status`, `content`, `sentAt`. `delivery: Pending` означает, что сообщение ожидает отправки; `Sent` подтверждает отправку. `Sending`/`Unknown` автоматически не повторяются.
6. Принять ответ через `POST /api/enrollments/{id}/messages/replies`: `{ "externalMessageId": "stable-provider-id", "content": "Да, интересно" }`. Endpoint только сохраняет ответ.
7. `POST /api/enrollments/{id}/analyze-reply` — AI анализирует следующий необработанный ответ. Результат содержит ID ответа, решение, confidence, объяснение и статус `Interested`, `NotInterested` или `NeedsReview`. Приглашение не отправляется. Если новых ответов нет, возвращается 404.
8. `POST /api/enrollments/{id}/send-interview-invitation` — отдельная отправка приглашения только для `Interested`, после первого сообщения и при отсутствии необработанных ответов. После отправки — `InterviewInvitationSent`.
9. Записать бронирование через `POST /api/interviews/bookings` с `enrollmentId`, `provider`, `externalBookingId`, `startsAt`, `endsAt`. Статус станет `InterviewBooked`.

Команды принимают пустое тело. Вызов команды вне допустимого состояния возвращает 409 и не выполняет другие этапы вместо неё. Повтор отправки возвращает уже сохранённое сообщение без дублирования. Текущий статус можно прочитать через `GET /api/enrollments/{id}`. Фоновые этапы выполняют `ProfileScreeningWorker`, `MessageSendingWorker` и `ReplyAnalysisWorker`; HTTP-команды сохраняют явную семантику.

Ручное решение: `{ "decision": "Interested", "reason": "Подтверждено рекрутером" }`. Неоднозначные ответы автоматически не уточняются: они направляются на ручное рассмотрение, как допускает ТЗ.

## Интеграции: что уже работает и что нужно подключить

Бизнес-процесс, хранение и HTTP API реализованы. OpenAI подключён через официальный SDK и включается при наличии ключа. Без ключа AI отвечает 503. Для LinkedIn добавлен адаптер Unipile v1: поиск Classic, профили, сообщения/InMail, запросы на добавление и webhooks. Настройка и новые маршруты описаны в [docs/unipile.md](docs/unipile.md). Без ключа Unipile интеграция отвечает 503. Тестовые ответы используются только в тестах и локальной симуляции.

* `ILinkedInGateway`: поиск с пагинацией и отправка сообщения со стабильным `IdempotencyKey`. Адаптер переводит реальные ID профилей и сообщений в эти контракты. Входящие ответы приводятся адаптером к нормализованному API приёма ответов; это может быть push или polling выбранного провайдера. Сам API пока не опрашивает LinkedIn.
* `IRecruitmentAi`: оценка обязательных критериев и score, персонализированное первое сообщение, семантическая классификация переписки. Первое сообщение должно выяснять интерес и не содержать приглашение на интервью. Профили и тексты кандидатов необходимо передавать модели как недоверенные данные, применять structured output и не давать модели доступ к операциям отправки. Низкий confidence всегда ведёт в `NeedsReview`.
* `IInterviewScheduler`: выбор ссылки для конкретной кампании/рекрутера. Сейчас используется URL кампании либо `Scheduling:DefaultInterviewUrl`.

`/api/interviews/bookings` **не является нативным webhook Calendly**. Будущий Calendly-адаптер должен проверить подпись и повторную доставку события, сопоставить бронирование с `EnrollmentId` и вызвать обработчик нормализованного события. Поля API key и signing key предусмотрены в конфигурации, но до подключения адаптера не используются. Аналогично нормализованный endpoint ответов не принимает произвольные native webhook payloads LinkedIn.

Для подключения другого провайдера реализовать нужный интерфейс, заменить регистрацию в регистрации `Infrastructure/DependencyInjection.cs` и добавить допустимое значение `Provider` в startup validation. После подключения обоих адаптеров можно включить `Pipeline:WorkerEnabled`. Worker обрабатывает уже импортированных участников и принятые ответы, по одному этапу за итерацию. Поиск запускается явно через API. Неизвестное имя провайдера приводит к ошибке запуска.

## Состояния, дубли и сбои

* Кампания: `Draft → Active ↔ Paused → Stopped`. Из `Draft` можно сразу перейти в `Stopped`; остановленную кампанию нельзя перезапустить.
* Участник: `Found → Screened → AwaitingReply → Interested → InterviewInvitationSent → InterviewBooked`. Ветви: `Rejected`, `NotInterested`, `NeedsReview`. Факт первого сообщения также фиксируется в истории отправок и аудите.
* Уникальные индексы защищают профиль, участие в кампании, исходящее сообщение каждого типа, внешний ID ответа и бронирование. Tracking-параметры URL LinkedIn удаляются при импорте.
* Повторный импорт существующего профиля возвращает существующую запись; это снимок, автоматического обновления профиля нет. Повторное включение в кампанию возвращает тот же ID. Повтор входящего события с тем же ID и содержимым возвращает существующую запись; другое содержимое с тем же ID — 409.
* Конкурирующие запросы могут получить 409: перечитать ресурс и повторить безопасный запрос. Для исходящих сообщений сначала сохраняется уникальное сообщение, затем атомарно захватывается право отправки.
* База хранит очереди и результаты. После таймаута или падения состояние `Sending`/`Unknown` не повторяется автоматически: результат внешнего вызова может быть успешным. Для сверки есть `reconcile` с `wasSent`, `externalMessageId` при успешной отправке и `reason`. Сверка разрешена не раньше чем через пять минут после попытки. `wasSent=false` вручную разрешает повтор с прежним idempotency key; перед этим нужно убедиться у провайдера, что исходная попытка не завершится позже.
* По умолчанию между отправками одной кампании минимум 60 секунд, максимум 50 попыток в день по UTC. Лимиты и захват отправки хранятся в PostgreSQL и работают между экземплярами API. Эти значения — настройки приложения, а не утверждение о разрешённых лимитах LinkedIn.
* Перед отправкой повторно проверяются кампания, актуальный статус, наличие необработанных ответов и бронирования. Уже выполняющийся внешний вызов остановка кампании отменить гарантированно не может.
* Автоматических follow-up сообщений при молчании кандидата нет. Бронирование поддерживает одно интервью на участника кампании; переносы и отмены нужно будет добавить вместе с контрактом календарного провайдера.

## Настройки и секреты

Живые обновления SignalR, редактирование кампаний и автодополнение локаций описаны в [docs/workspace-api.md](docs/workspace-api.md). После обновления пересоберите API; новая миграция применится при старте.

Конфигурация разделена по назначению:

* `appsettings.json` — общие несекретные настройки: логирование, AllowedHosts, параметры БД, выбор LinkedIn-провайдера, модель AI, порог confidence, интервалы и лимиты worker. Эти параметры можно менять без правки C#. Пустых ключей, паролей и connection string здесь нет. Отдельный `appsettings.Development.json` не нужен, пока нет отличающихся настроек разработки.
* Типизированные `DatabaseOptions`, `AiOptions`, `PipelineOptions` и остальные Options — контракт настроек каждого модуля и запасные значения, если параметр вообще не задан конфигурацией. Рабочие значения берутся из `appsettings.json` и переопределений окружения, а не требуют редактирования классов.
* `.env` — значения конкретного Docker-окружения: пароли, ключи, адреса интеграций и переопределения настроек AI/worker. Compose переводит имена из `.env.example` в ключи .NET с разделителем `__`, например `AI_API_KEY` → `Ai__ApiKey`. Само приложение `.env` не читает. Compose явно задаёт дефолты передаваемых параметров, чтобы уже существующие `.env` без новых переменных продолжали работать.
* Для запуска без Docker — переменные окружения .NET или User Secrets. User Secrets подключаются стандартным `WebApplication.CreateBuilder` только в Development; идентификатор задан в проекте API, значения хранятся вне репозитория. В deployment используются переменные окружения / хранилище секретов инфраструктуры.

Приоритет: аргументы командной строки → переменные окружения → User Secrets (Development) → `appsettings*.json` → дефолты Options. Дополнительного загрузчика `.env` и отдельного глобального конфига нет. Настройки привязываются в модуле, который их использует. API-ключ и настройки интеграций/worker проверяются при старте.

`.env` и локальные settings исключены из Git и Docker build context. Не помещать настоящие ключи в `appsettings*.json`; файл `appsettings.Local.json` автоматически не загружается. При изменении настроек перезапускайте приложение.

При запуске через Docker меняйте параметры в `.env`:

| Переменная `.env` | Настройка .NET | По умолчанию |
|---|---|---|
| `AI_MODEL` | `Ai:Model` | `gpt-4.1-mini` |
| `AI_MINIMUM_INTEREST_CONFIDENCE` | `Ai:MinimumInterestConfidence` | `0.8` |
| `PIPELINE_WORKER_ENABLED` | `Pipeline:WorkerEnabled` | `false` |
| `PIPELINE_POLL_INTERVAL_SECONDS` | `Pipeline:PollIntervalSeconds` | `30` |
| `PIPELINE_BATCH_SIZE` | `Pipeline:BatchSize` | `10` |
| `PIPELINE_MINIMUM_SEND_INTERVAL_SECONDS` | `Pipeline:MinimumSendIntervalSeconds` | `60` |
| `PIPELINE_MAXIMUM_DAILY_MESSAGES_PER_CAMPAIGN` | `Pipeline:MaximumDailyMessagesPerCampaign` | `50` |

Например, `AI_MINIMUM_INTEREST_CONFIDENCE=0.9` повышает порог уверенности, а `PIPELINE_MAXIMUM_DAILY_MESSAGES_PER_CAMPAIGN=20` ограничивает кампанию двадцатью попытками отправки в сутки. Затем выполните `docker compose up -d`: Compose пересоздаст API с новым окружением. Простой `docker compose restart` не подхватывает изменения `.env`. Для изменения самого `appsettings.json`, включённого в Docker-образ, нужна пересборка: `docker compose up --build -d`. Параметры, которые Compose передаёт через environment, имеют приоритет над JSON. Вне Docker их можно менять непосредственно в JSON или переопределять переменными .NET, например `Pipeline__BatchSize`.

Настройки БД задаются отдельными полями `Database`; connection string строится через Npgsql builder, поэтому спецсимволы пароля обрабатываются корректно. Для существующих окружений и тестов поддерживается приоритетная `ConnectionStrings:Database`.

В локальном Compose PostgreSQL использует пароль, поэтому `Database__GssEncryptionMode=Disable` отключает ненужную попытку GSSAPI/Kerberos и сообщение об отсутствующей `libgssapi_krb5.so.2` в Linux-образе .NET. Это не меняет настройки SSL/TLS. Вне Compose сохраняется стандартный `Prefer`; параметр можно задать через `Database:GssEncryptionMode`. При использовании полной `ConnectionStrings:Database` режим задаётся в самой строке: `GSS Encryption Mode=Disable` для окружения без Kerberos.

Для локального запуска API без Compose (нужен доступный PostgreSQL):

```powershell
dotnet user-secrets set "Security:ApiKey" "<случайный-ключ-минимум-32-символа>" --project TalentScout.Api
dotnet user-secrets set "Database:Password" "<пароль-локальной-БД>" --project TalentScout.Api
dotnet user-secrets set "Ai:ApiKey" "<ключ-OpenAI>" --project TalentScout.Api
# При необходимости: Database:Host, Database:Port, Ai:Model, LinkedIn:* и Pipeline:*.
dotnet run --project TalentScout.Api --launch-profile http
```

Шаблоны в угловых скобках заменяются своими значениями. Ключ OpenAI можно не задавать, если AI не используется. При запуске без профиля явно задайте `ASPNETCORE_ENVIRONMENT=Development`, чтобы подключились User Secrets. EF design-time factory в проекте Data использует env, `appsettings.json` из output и дефолты `DatabaseOptions`, но не User Secrets API: для `dotnet ef` передайте `Database__Password` и прочие параметры БД либо `ConnectionStrings__Database` в окружении процесса.

Ключ API — общий серверный ключ для MVP, без пользовательских ролей и разделения организаций. Для будущего публичного frontend/расширения потребуется пользовательская авторизация: серверный ключ в расширение встраивать нельзя. Compose публикует API только на loopback; внешнее размещение требует настроенного HTTPS входа.

## Разработка и проверка

Нужны .NET 10 SDK или совместимый SDK и Docker для тестов.

```powershell
dotnet build TalentScout.Api/TalentScout.Api.csproj
dotnet test TalentScout.Api.Tests/TalentScout.Api.Tests.csproj

dotnet tool restore
dotnet ef migrations add MigrationName --project TalentScout.Data/TalentScout.Data.csproj --startup-project TalentScout.Api/TalentScout.Api.csproj --output-dir Persistence/Migrations
```

Интеграционные тесты запускают временный PostgreSQL через Testcontainers, используют настоящие HTTP endpoints через ASP.NET Core TestServer и тестовые адаптеры LinkedIn/AI. Проверяются положительный путь до бронирования, отрицательные и неоднозначные ответы, ручное решение, дубли, конкурирующие отправки, остановка кампании, интервалы и дневной лимит, неизвестный результат отправки, валидация, авторизация и OpenAPI. Доступ к реальным сервисам эти проверки не подтверждают.

При запуске вне Compose настроить `Security__ApiKey` и параметры БД, затем выполнить `dotnet run --project TalentScout.Api`. Миграции применятся автоматически. `Program` — обычный класс с `Main`; этот тип также используется интеграционными тестами. Для EF CLI предусмотрен `DesignTimeDbContextFactory`, поэтому генерация миграций не запускает API и не применяет миграции к базе.

Используемые механизмы: [ASP.NET Core OpenAPI](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/openapi?view=aspnetcore-10.0), [Npgsql EF Core 10](https://www.npgsql.org/efcore/release-notes/10.0.html).

Результаты: пакеты `Ardalis.Result`, `Ardalis.Result.AspNetCore`, `Ardalis.Result.FluentValidation` версии 10.1.0. Самописных `Result<T>`, `Error` и `ToActionResult` нет. Хендлеры напрямую возвращают результаты библиотеки; неожиданные исключения обрабатываются стандартным exception handler. HTTP-запросы, пагинация, профили и ответы внешних провайдеров проверяются отдельными FluentValidation-валидаторами. Ошибки запросов переводятся через `Invalid(validation.AsErrors())`; некорректные ответы AI и LinkedIn отклоняются до обновления состояния. После валидации отдельно выполняется нормализация текста и URL.

`TalentScout.Api` ссылается на `TalentScout.Data`; у Data нет обратной ссылки на API, контроллеры или HTTP-контракты. Сущности, репозитории, контекст, настройки БД и миграции полностью принадлежат библиотеке. Data возвращает независимые read models, которые хендлеры переводят в API responses. Входная точка регистрирует данные через `AddData` и автоматически применяет миграции через `MigrateDataAsync`. Идентификатор существующей миграции сохранён.


Интеграции LinkedIn и AI имеют одинаковую структуру: `Abstractions` — интерфейсы, `Models` — отдельный файл на каждый входной/выходной тип, `Options` — настройки, `Providers/Disabled` — текущие реализации, `Validators` — FluentValidation. Регистрация и startup validation выполняются в `AddLinkedInIntegration` и `AddAiIntegration`; новые провайдеры добавляются в соответствующий модуль.


## Подтверждение кандидатов и запуск рассылки

`autoApproveCandidates` — настройка кампании, по умолчанию `false`. Её можно передать при создании или изменить через `PUT /api/campaigns/{id}/approval-settings`:

```json
{ "autoApproveCandidates": false }
```

Каждый поиск создаёт отдельную партию `searchBatchId`. `limit` означает число НОВЫХ участников этой кампании, а не число профилей на первой странице. Уже участвующие пропускаются независимо от статуса; поиск продолжает пагинацию. Ограничения: `maxProfilesToScan` (по умолчанию 1000, максимум 10000) и 100 страниц. Ответ содержит только новые `enrollmentIds`, счётчики, `nextCursor` и причину остановки. Возможные причины: `TargetReached`, `ResultsExhausted`, `ScanLimitReached`, `PageLimitReached`, `RepeatedCursor`. Кандидат из общей базы, ещё не участвующий в кампании, считается новым участником. Старая запись участия и её партия не меняются. Один профиль не может дважды попасть в кампанию даже при конкурирующих поисках: добавление участия использует PostgreSQL ON CONFLICT.

Запрос поиска:

```json
{ "limit": 20, "maxProfilesToScan": 1000 }
```

Список партий: `GET /api/campaigns/{id}/search-batches`. Результаты конкретной партии: `GET /api/campaigns/{id}/candidates?searchBatchId={batchId}`. Для списка ожидающих решения добавить `status=Screened&approvalStatus=Pending`. В ответах кандидатов доступны `searchBatchId`, `approvalStatus`, `approvedAt`, `outreachStartedAt`. Неудавшийся поиск сохраняет партию со статусом `Failed` и уже импортированных участников; ошибка не отменяет предыдущие страницы.

В ручном режиме новый кандидат проходит AI-проверку и остаётся `Screened` + `Pending`. Для оценки профиля достаточно Draft или Active; worker тоже может оценивать профили в Draft. Оценка не означает разрешение на отправку. Пользователь выбирает подходящих и один раз вызывает:

`POST /api/campaigns/{id}/outreach/start`

```json
{ "enrollmentIds": ["id-первого-участника", "id-второго-участника"] }
```

Команда подтверждает и запускает только выбранных Screened-кандидатов, активирует Draft-кампанию, возвращает новые и уже запущенные ID. Она не отправляет сообщения синхронно: далее их отправляет включённый worker либо явная команда `send-introduction`. При одном некорректном участнике весь выбор отклоняется без частичного запуска. Paused сначала нужно возобновить; Stopped не перезапускается.

Для удаления из списка рассмотрения: `POST /api/campaigns/{id}/outreach/exclude` с таким же массивом `enrollmentIds`. Исключённые сохраняются в кампании с `approvalStatus=Excluded`, поэтому следующий поиск их тоже пропустит. Исключать уже запущенную коммуникацию этой командой нельзя.

При `autoApproveCandidates=true` worker подхватывает всех подходящих Screened-кандидатов, включая найденных до включения настройки. Разрешение проверяется непосредственно перед отправкой; при первой отправке сохраняется `Automatic` и активируется Draft. Отключение блокирует ещё не отправленные автоматические сообщения. Ручной `Approved` остаётся действующим, `Excluded` всегда блокирует отправку. Анализ полученных ответов не зависит от галочки первого сообщения.

Проверка одобрения и запуска есть и в хендлере, и в атомарной транзакции отправки в репозитории. Обойти ручное подтверждение прямым вызовом send-introduction нельзя. Новая миграция сохраняет разрешение для уже начатых до обновления диалогов, а кандидатов без первого сообщения оставляет на подтверждение.

## Подтверждение ссылки на интервью

Второй этап независим от первого: `autoApproveInterviewInvitations` по умолчанию `false`. Когда AI или ручная проверка установили `Interested`, отправка ссылки ожидает решения. `POST /api/campaigns/{id}/interview-invitations/approve` подтверждает выбранных; `/reject` запрещает отправку, в том числе при включённом автоматическом режиме. Тело обоих запросов: `{ "enrollmentIds": ["uuid"] }`. Повторное подтверждение может отменить отказ. Команды сохраняют решение, отправку выполняет worker или `send-interview-invitation`.

`PUT /api/campaigns/{id}/approval-settings` принимает одну или обе галочки. Пропущенная настройка сохраняет значение. Включение второй галочки подхватывает уже ожидающих Interested-кандидатов; отключение останавливает неподтверждённые ссылки, даже если сообщение уже подготовлено. Ручное подтверждение сохраняется. Отправленное сообщение отозвать нельзя. В момент атомарного захвата отправки повторно проверяются актуальные настройки кампании.

Состояние решения: `invitationApprovalStatus` (`Pending`, `Approved`, `Rejected`, `Automatic`), время — `invitationApprovedAt`. Очередь: `GET /api/campaigns/{id}/candidates?status=Interested&invitationApprovalStatus=Pending`. Чужие ID, неподходящий статус или необработанные ответы отклоняются до сохранения всего выбора. Ссылка берётся из настроек кампании/планирования; интеграцию календаря этот этап не создаёт.

Основная цепочка: поиск → проверка → подтверждение первого контакта → сообщение → получение и анализ ответа → подтверждение ссылки → отправка ссылки → регистрация бронирования. Помимо GET остаются приём ответов и бронирований, ручные решения и разбор неопределённых отправок. Для реального запуска нужен ключ OpenAI и адаптер LinkedIn.

Снимок `docs/openapi.json` обновляется из запущенного API; сохранённый снимок пока не обновлён после добавления Unipile. Подписки webhook регистрируются автоматически при старте настроенной интеграции, до запуска воркеров; ручного endpoint регистрации нет. Настройки и последовательность запуска описаны в [docs/unipile.md](docs/unipile.md).


## OpenAI и системный промпт из UI

Официальный пакет [OpenAI .NET SDK](https://github.com/openai/openai-dotnet) версии 2.15.0 зарегистрирован за `IRecruitmentAi`. Для запуска достаточно указать `AI_API_KEY` в `.env`; `AI_MODEL` по умолчанию `gpt-4.1-mini`. Вне Compose: `Ai__ApiKey` и при необходимости `Ai__Model` либо эквивалентные ключи User Secrets. Ключ остаётся на сервере. `Ai:Provider` и `Ai:BaseUrl` больше не нужны. Несекретные модель и порог confidence указаны в `appsettings.json`; для Docker переопределяются через `AI_MODEL` и `AI_MINIMUM_INTEREST_CONFIDENCE` в `.env`. Пустой API-ключ в JSON не хранится.

Системный промпт хранится отдельно для каждой кампании в БД, не в appsettings. Его можно задать в `POST /api/campaigns` полем `systemPrompt` или обновить из UI:

```http
PUT /api/campaigns/{id}/ai-settings
X-Api-Key: <ключ самого TalentScout API>
Content-Type: application/json

{ "systemPrompt": "Пиши коротко українською. Оцінюй практичний досвід ASP.NET Core." }
```

Текущий промпт возвращает `GET /api/campaigns/{id}`. Пустая строка удаляет пользовательские инструкции; базовые инструкции этапов остаются. Максимум 20000 символов. Изменения действуют для последующих AI-вызовов без перезапуска, существующие оценки и уже подготовленные сообщения не пересоздаются. Для остановленной кампании редактирование возвращает 409.

Адаптер выполняет три операции через Chat Completions: оценку профиля, создание первого сообщения и классификацию ответа. Промпт кампании передаётся системным сообщением, данные профиля/истории — сериализованным user-сообщением. Каждая операция использует strict JSON schema; отказ, незавершённый или некорректный ответ отклоняется с 502. Rate limit, временные ошибки и таймауты дают 503. Подробности ошибки провайдера не возвращаются клиенту. SDK использует стандартные повторы временных ошибок и сетевой таймаут 60 секунд на попытку; отмена запроса передаётся дальше. Сохранение completion у провайдера выключено (`store=false`). Галочки подтверждений по-прежнему контролируют отправку сообщений.

`dotnet test` проверяет реальный SDK по HTTP с локальным Kestrel-сервером и PostgreSQL для API. Для независимого прогона тех же SDK-тестов через Postman CLI (1.75+), без логина и ключа OpenAI:

```powershell
# Терминал 1, из корня репозитория:
postman simulation run postman/simulations/openai.sim.yaml --port 15963 --no-history --output ndjson

# Терминал 2:
$env:TALENTSCOUT_OPENAI_SIMULATION_URL = 'http://127.0.0.1:15963/openai'
dotnet test TalentScout.Api.Tests/TalentScout.Api.Tests.csproj --filter FullyQualifiedName~OpenAiTests
Remove-Item Env:TALENTSCOUT_OPENAI_SIMULATION_URL
```

Симуляция отвечает только на локальные запросы и не обращается к OpenAI. Сценарии ошибок выбираются синтетическим именем модели в тестах; это механизм тестов, не production-настройка. Эти проверки не подтверждают доступность модели для реального ключа или качество рекрутинговых решений.
