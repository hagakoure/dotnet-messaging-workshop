# Email Notification Service

> Production-ready микросервис для надёжной асинхронной отправки email-уведомлений на .NET 8.  
> Демонстрация event-driven архитектуры с паттернами Outbox, Idempotency и DLQ.

##  Архитектура

```
┌─────────────┐     ┌──────────────┐     ┌─────────────┐
│   Web API   │────▶│  RabbitMQ    │────▶│  Consumer   │
│  (Outbox)   │     │  ИЛИ Kafka   │     │(Idempotent) │
└─────────────┘     └──────────────┘     └─────────────┘
       │                                         │
       ▼                                         ▼
┌─────────────┐                          ┌─────────────┐
│ PostgreSQL  │                          │ PostgreSQL  │
│ (Outbox +   │                          │ (Idempotency│
│  Statuses)  │                          │  tracking)  │
└─────────────┘                          └─────────────┘
```

##  Ключевые возможности

### Надёжность доставки сообщений
- **Outbox Pattern** - атомарное сохранение бизнес-данных и события в одной транзакции
- **Идемпотентность консьюмера** - двухуровневая защита от дубликатов (check-then-act + unique constraint)
- **Retry Policy** - экспоненциальная задержка между попытками
- **Dead Letter Queue** - изоляция "отравленных" сообщений
- **Graceful Shutdown** - корректная остановка без потери данных

### Observability
- **OpenTelemetry** - распределённая трассировка (traces + metrics + logs)
- **Jaeger** - визуализация трейсов
- **Health Checks** - liveness/readiness пробы для Kubernetes

### Гибкость
- **Переключение брокеров** - RabbitMQ и Kafka через конфигурацию
- **Абстракция IMessagePublisher** - бизнес-логика не зависит от брокера (SOLID)
- **Central Package Management** - согласованные версии NuGet-пакетов

### Качество кода
- **Unit-тесты** - покрытие бизнес-логики с Moq + InMemory EF Core
- **FluentValidation** - валидация DTO на уровне middleware
- **Multi-stage Dockerfile** - оптимизированные образы (~200MB)

##  Быстрый старт

### Предварительные требования
- .NET 8 SDK
- Docker Desktop

### Запуск инфраструктуры
```bash
docker-compose up -d
```

Это поднимет:
- PostgreSQL (порт 5432)
- RabbitMQ + Management UI (порты 5672, 15672)
- Kafka (порт 9092)
- Jaeger UI (порт 16686)

### Запуск приложений
```bash
# Терминал 1: API
dotnet run --project Api

# Терминал 2: Consumer
dotnet run --project EmailService
```

### Переключение между брокерами
В `appsettings.Development.json` обоих проектов:
```json
"MessageBroker": {
  "Type": "rabbitmq"  // или "kafka"
}
```

##  API Endpoints

| Метод | Путь | Описание |
|-------|------|----------|
| POST | `/notifications/email` | Запрос отправки письма (202 Accepted) |
| GET | `/notifications/status/{correlationId}` | Проверка статуса |
| GET | `/health/live` | Liveness probe (Kubernetes) |
| GET | `/health/ready` | Readiness probe с деталями |

##  Тестирование

```bash
# Unit-тесты
dotnet test

# Интеграционное тестирование вручную
# 1. Запустить docker-compose
# 2. Запустить Api и EmailService
# 3. Отправить письмо через Swagger
# 4. Проверить логи и БД
```

##  Структура решения

```
dotnet-messaging-workshop/
├── Api/                          # Web API (Minimal API)
│   ├── Controllers/              # (опционально)
│   ├── Data/                     # EF Core DbContext + Entities
│   ├── Extensions/               # Database migration extensions
│   ├── HealthChecks/             # Кастомные health checks
│   ├── Options/                  # Configuration classes
│   ├── Services/                 # OutboxService, IMessagePublisher
│   └── Validators/               # FluentValidation
├── EmailService/                 # Background consumer
│   ├── Consumers/                # MassTransit consumers
│   └── Data/                     # Idempotency tracking
├── Shared/                       # Общие контракты (DTO, events)
├── UnitTests/                    # Unit-тесты
├── docker-compose.yml            # Инфраструктура
├── Directory.Packages.props      # Central Package Management
└── README.md
```

##  Технологии

| Категория | Технологии |
|-----------|-----------|
| Runtime | .NET 8, C# 12, Minimal API |
| Messaging | MassTransit 8.x (RabbitMQ + Kafka) |
| Database | Entity Framework Core 8 + PostgreSQL |
| Observability | OpenTelemetry + Jaeger |
| Testing | xUnit, Moq, FluentAssertions, InMemory EF Core |
| Validation | FluentValidation |
| Containerization | Docker, docker-compose |
| Package Management | Central Package Management |

##  Ключевые паттерны

### Outbox Pattern
Гарантирует, что бизнес-операция и публикация события происходят атомарно. Если приложение упадёт после сохранения в БД, но до публикации - фоновый `OutboxService` опубликует событие при следующем запуске.

### Idempotent Consumer
Двухуровневая защита от дубликатов:
1. **Быстрая проверка** через `AnyAsync` - покрывает 99% случаев
2. **Fallback** через обработку `unique constraint violation` (PostgreSQL 23505) - обрабатывает race conditions

### Dead Letter Queue
Сообщения, которые не удалось обработать после всех retry-попыток, перемещаются в DLQ для ручной обработки. Это предотвращает блокировку очереди "отравленными" сообщениями.

##  Конфигурация

### Переменные окружения

Создай файл `.env` в корне проекта (или скопируй из `.env.example`):

```bash
cp .env.example .env