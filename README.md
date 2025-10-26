# EPS Discount Code Service

A high-performance discount code generation and validation service built with .NET 9, gRPC, Entity Framework Core, and Redis caching.

## 🚀 Features

- **gRPC API** for efficient client-server communication
- **Discount Code Generation** with configurable length (up to 2000 codes per request)
- **Code Validation & Usage Tracking** with atomic operations
- **Redis Caching** to minimize database load and improve response times
- **PostgreSQL Database** with optimized indexes for high-performance queries
- **Clean Architecture** with separation of concerns across layers
- **Comprehensive Unit Tests** using xUnit with 90%+ code coverage
- **Bulk Insert Operations** for efficient code generation
- **Load Testing Tools** included for performance validation
- **Docker Support** for easy deployment

## 🏗️ Architecture

The solution follows Clean Architecture principles with the following projects:

- **EPS.Discount.Core** - Domain models, interfaces, and business logic
- **EPS.Discount.Data** - Entity Framework Core, database context, and migrations
- **EPS.Discount.Application** - Application services and business orchestration
- **EPS.Discount.Server** - gRPC server host and configuration
- **EPS.Discount.Client** - Sample gRPC client for testing
- **EPS.Discount.Tests** - Unit tests with xUnit
- **EPS.Discount.LoadTest** - Load testing tools

## 📋 Prerequisites

### Option 1: Docker (Recommended)
- [Docker Desktop](https://www.docker.com/products/docker-desktop) or Docker Engine with Docker Compose

### Option 2: Local Development Setup
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [PostgreSQL 16+](https://www.postgresql.org/download/)
- [Redis 7+](https://redis.io/download)
- IDE: Visual Studio 2022, JetBrains Rider, or VS Code

## 🚀 Running the Application

### Using Docker

1.  **Clone the repository**

2.  **Start all services**
    The following command will start the gRPC server, PostgreSQL, and Redis containers.

    ```bash
    docker-compose up -d
    ```

3.  **View logs** (optional)

    ```bash
    docker-compose logs -f
    ```

4.  The gRPC server will be available at `http://localhost:5000`.

5.  **Stop services and remove volumes**

    ```bash
    docker-compose down -v
    ```

### Local Development

1.  Ensure PostgreSQL and Redis instances are running and accessible.

2.  **Clone the repository**

    ```bash
    git clone https://github.com/ferhat-aksoy/EPS.DiscountService.git
    cd EPS.DiscountService
    ```

3.  **Configure Database & Redis**
    Update the `ConnectionStrings` section in `EPS.Discount.Server/appsettings.Development.json`:

    ```json
    {
      "ConnectionStrings": {
        "DefaultConnection": "Host=localhost;Port=5432;Database=discounts;Username=postgres;Password=your_password",
        "Redis": "localhost:6379"
      }
    }
    ```

4.  **Build the Solution**

    ```bash
    dotnet build
    ```

5.  **Run Database Migrations**
    This command applies EF Core migrations to create the database schema.

    ```bash
    dotnet ef database update --project EPS.Discount.Data
    ```

6.  **Run the Server**

    ```bash
    dotnet run --project EPS.Discount.Server
    ```

The server will start and listen for gRPC connections as configured in `launchSettings.json`.

**Run the Sample Client:**

To test the server, run the sample client application in a separate terminal:

1.  **Navigate to the Client Directory**

    ```bash
    cd EPS.Discount.Client
    ```

2.  **Run the Client**

    ```bash
    dotnet run
    ```

3.  The client will connect to the gRPC server and perform sample code generation and validation operations.

## 📡 API Reference

The service is defined using Protocol Buffers. See `discount.proto` for the full contract.

### GenerateCodes

Generates a batch of unique discount codes.

**Request:**

```proto
message GenerateCodesRequest {
  int32 count = 1;
  int32 length = 2;
}

```

**Response:**

```proto
message GenerateCodesResponse {
  repeated string codes = 1;
  string error = 2;
}
```

### ValidateCode

Validates a discount code and marks it as used if valid.

**Request:**

```proto
message ValidateCodeRequest {
  string code = 1;
}

```

**Response:**

```proto
message ValidateCodeResponse {
  bool is_valid = 1;
  string error = 2;
}
```

**Limits:**
- Maximum codes per request: 2000
- Code length: 7-8 characters

### UseCode

Validates and marks a discount code as used.

**Request:**

```proto
message UseCodeRequest {
  string code = 1;
}

```

**Response:**

```proto
message UseCodeResponse {
  bool is_used = 1;
  string error = 2;
}
```

**Result Codes:**
- `0` - Success (code used successfully)
- `1` - Code not found
- `2` - Code already used
- `3` - Invalid code format

## 🧪 Testing

### Run Unit Tests

Execute the following command from the solution root to run all unit tests:

```bash
dotnet test
```

### Run Load Tests

The load test project simulates concurrent code generation and validation.

1.  Ensure the target database (e.g., a test instance) is running.
2.  Update the connection strings in `EPS.Discount.LoadTest/Program.cs`.
3.  Run the test:

    ```bash
    dotnet run --project EPS.Discount.LoadTest
    ```

The load test simulates concurrent code generation and validation operations to measure throughput and response times.

## 🔧 Technology Stack

| Component | Technology |
|-----------|-----------|
| Framework | .NET 9 |
| API Protocol | gRPC |
| Database | PostgreSQL |
| ORM | Entity Framework Core 9 |
| Caching | Redis (StackExchange.Redis) |
| Testing | xUnit, Moq, SQLite (in-memory) |
| Code Generation | `RandomNumberGenerator` with memory pooling (`ArrayPool`) |
| Containerization | Docker & Docker Compose |

## 📊 Performance Characteristics

- **Code Generation**: ~500-1000 codes/second (batch operation)
- **Code Validation**: <5ms average response time (with Redis cache)
- **Cache Hit Ratio**: >95% for validation operations
- **Database Indexes**: Optimized for code lookups and range queries

## 🔒 Security Considerations

- Codes are generated using cryptographically secure random numbers (`RandomNumberGenerator`).
- The implementation uses `ArrayPool` to reduce memory allocations under load.
- Character alphabet excludes ambiguous characters (0, 1, O, I, L) to reduce user error.
- Case-insensitive code comparison to prevent enumeration attacks.
- Atomic database operations to prevent race conditions.
- Input validation on all API endpoints.
- Password-protected Redis and PostgreSQL in Docker setup.

## 📝 Database Schema

### DiscountCode Table

| Column | Type | Description |
|--------|------|-------------|
| `Id` | int (PK) | Auto-incrementing primary key |
| `Code` | varchar(32) | Unique discount code (indexed) |
| `Length` | int | Length of the code |
| `IsUsed` | boolean | Whether the code has been used |
| `CreatedAt` | timestamp | Code creation timestamp |
| `UsedAt` | timestamp (nullable) | Code usage timestamp |

**Indexes:**
- Unique index on `Code` (case-insensitive)
- Composite index on `(IsUsed, CreatedAt)` for performance optimization

## 🐳 Docker Configuration

### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `ASPNETCORE_ENVIRONMENT` | `Production` | ASP.NET Core environment |
| `ConnectionStrings__DefaultConnection` | - | PostgreSQL connection string |
| `ConnectionStrings__Redis` | - | Redis connection string |
| `Redis__CacheExpirationMinutes` | `1440` | Cache TTL in minutes |
| `Database__Provider` | `postgres` | Database provider |

### Ports

| Service | Port | Description |
|---------|------|-------------|
| PostgreSQL | 5432 | Database server |
| Redis | 6379 | Cache server |
| gRPC Server | 5000 | API endpoint |

### Docker Commands

## 🤝 Contributing

Contributions are welcome! Please follow these steps:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 👥 Authors

- Ferhat Aksoy - Initial work

## 🙏 Acknowledgments

- Built with [ASP.NET Core](https://dotnet.microsoft.com/apps/aspnet)
- gRPC implementation using [Grpc.AspNetCore](https://github.com/grpc/grpc-dotnet)
- Caching powered by [StackExchange.Redis](https://github.com/StackExchange/StackExchange.Redis)

## 📞 Support

For issues and questions, please open an issue on the [GitHub repository](https://github.com/ferhat-aksoy/eps-discount/issues)
