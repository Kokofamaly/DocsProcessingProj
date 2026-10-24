# Платформа асинхронной обработки документов

Распределённое приложение на .NET Aspire: пользователь загружает документ через веб-интерфейс, документ сохраняется в хранилище, асинхронно обрабатывается Azure Function, а по завершении публикуется событие для Notification и Audit сервисов.

## Архитектура

| Компонент | Назначение |
|---|---|
| `AspireApp.AppHost` | Запускает все сервисы, контейнеры и эмуляторы |
| `AspireApp.ApiService` | Document API, авторизация (cookie), DLQ-эндпоинт |
| `AspireApp.Functions` | Обработчик документов по триггеру очереди |
| `AspireApp.NotificationService` | Подписка на топик, сохранение уведомлений, `GET /notifications` |
| `AspireApp.AuditService` | Подписка на топик, запись журнала событий в таблицу `AuditLogs` |
| `AspireApp.ServiceDefaults` | OpenTelemetry, health checks, service discovery |
| `CommonData` | Общие модели, `AppDbContext`, имена ресурсов |
| `AspireApp.UnitTests` | Юнит-тесты (xUnit, Moq, EF InMemory) |
| `frontend` | React + TypeScript (Vite) |

## Технологии

- .NET 10
- ASP.NET Core Minimal API
- Aspire
- Azure Functions
- Azure Service Bus (эмулятор)
- Azure Blob Storage (Azurite)
- PostgreSQL
- EF Core
- React
- TypeScript
- TanStack Query
- OpenTelemetry

## Что нужно установить

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Docker Desktop (запущенный): в контейнерах работают PostgreSQL, Azurite и эмулятор Service Bus
- Node.js (для фронтенда)
- Azure Functions Core Tools, если Aspire попросит его при запуске Functions

## Запуск

1. Запустите Docker Desktop.
2. Запустите проект `AspireApp.AppHost` из IDE или командой:
    ```
    aspire run AspireApp
    ```
    или
   ```
   dotnet run --project AspireApp/AspireApp.AppHost
   ```
3. В консоли должна появиться ссылка Aspire Dashboard. Откройте ее. Дождитесь, пока все ресурсы получат статус `Running` / `Healthy`. Первый запуск долгий: скачиваются образы, эмулятор Service Bus стартует 1–3 минуты.
4. **Откройте фронтенд по адресу `http://localhost:5000`.**

> Ссылку на фронтенд из дашборда (`*.dev.localhost`) использовать не рекомендуется: браузер может не сохранить cookie авторизации, и вход «не запомнится».

## Учётные записи

| Роль | Логин | Пароль |
|---|---|---|
| Администратор | `AdminUser` | `12345678` |
| Обычный пользователь | создаётся через экран регистрации | |

Администратор создаётся при старте API. Только администратор видит `GET /dlq`. Обычные пользователи видят и перезапускают только свои документы.

## Как проверить сценарии

1. **Успешная обработка.** Зарегистрируйтесь, загрузите любой файл. Статус изменится `Pending` → `Processing` → `Completed`.
2. **Ошибка и retry.** Загрузите файл, в имени которого есть `error` (например, `error-contract.pdf`). Сообщение будет доставлено 3 раза, после чего документ получит статус `Failed`, а сообщение попадёт в Dead Letter Queue.
3. **DLQ.** Войдите под администратором и откройте `GET /dlq` (адрес API виден в дашборде). Эндпоинт показывает до 10 сообщений из dead-letter очереди.
4. **Reprocess.** Нажмите `Reprocess` у документа: статус сбросится в `Pending`, в очередь уйдёт новое сообщение, цикл обработки запустится заново.
5. **Уведомления.** `GET /notifications` в Notification Service (адрес в дашборде) возвращает список вида `<имя файла> was processed.`
6. **Аудит.** Каждое событие `DocumentProcessed` записывается в таблицу `AuditLogs` (`DocumentId`, `EventType`, `Timestamp`) и дублируется в лог. Таблицу можно посмотреть в PostgreSQL, логи в дашборде.

## Наблюдаемость

Логи, трейсы и метрики всех сервисов идут в Aspire Dashboard через OpenTelemetry (`ServiceDefaults`):

- **Structured / Console**: логи по ресурсам;
- **Traces**: цепочка запросов, в том числе сообщения Service Bus;
- **Metrics**: HTTP-метрики и метрики рантайма.

## Тесты

Юнит-тесты не требуют Docker и Aspire:

```
dotnet test AspireApp/AspireApp.Tests
```

Покрыты: `DocumentService`, `DocumentProcessor`, `NotificationProcessor`, `AuditProcessor`. Внешние сервисы (Service Bus, Blob) подменены через Moq, база данных EF InMemory.

## Docker

Для API, Notification, Audit и фронтенда есть `Dockerfile`. Полный запуск с эмуляторами выполняется через Aspire AppHost, отдельные образы без инфраструктуры не заработают. Azure Functions запускается через Aspire и в отдельный образ не вынесена.

.NET-сервисы собираются из корня решения (им нужен проект `CommonData`):

```
docker build -f AspireApp/AspireApp.ApiService/Dockerfile -t docs-api .
docker build -f AspireApp/AspireApp.NotificationService/Dockerfile -t docs-notification .
docker build -f AspireApp/AspireApp.AuditService/Dockerfile -t docs-audit .
```

Фронтенд (адрес API вшивается при сборке):

```
docker build -f AspireApp/frontend/Dockerfile --build-arg VITE_API_URL=http://localhost:5398 -t docs-frontend AspireApp/frontend
```

## Обработка ошибок

- **Retry.** `MaxDeliveryCount = 3` у очереди Service Bus. Порог `Failed` в `DocumentProcessor` равен этому же числу.
- **DLQ.** Сообщения, не обработанные за 3 попытки, попадают в dead-letter очередь, доступную через `GET /dlq`.
- **Reprocess.** Сброс статуса в `Pending` и повторная публикация сообщения. Доступно только владельцу документа.

## Авторизация

Cookie-аутентификация ASP.NET Core: регистрация и вход (`/register`, `/login`, `/logout`, `/me`), пароли хранятся хэшем (`PasswordHasher`). Все эндпоинты закрыты fallback-политикой, открыты только вход и регистрация. Роль `Admin` нужна для `/dlq`.

## Известные ограничения

- В режиме Development cookie выдаётся с `SameSite=None; Secure`, чтобы работать при открытии фронта по разным адресам. В Production используется `Lax`.
- Логин и пароль администратора заданы в коде. В реальном проекте они не будут видны в репозитории, а должны храниться в user secrets, или чем-то аналогичном.