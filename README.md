# FiapCloudGames.BuildingBlocks

Repositório privado dos componentes técnicos compartilhados entre as APIs do
FIAP Cloud Games.

## Escopo inicial

- autenticação técnica entre serviços;
- tratamento padronizado de exceções e `ProblemDetails`;
- health checks;
- correlation ID;
- observabilidade básica;
- configurações comuns do ASP.NET Core.

RabbitMQ, MassTransit e Kubernetes não fazem parte da primeira etapa da
migração.

## Estrutura

```text
src/FiapCloudGames.BuildingBlocks/
tests/FiapCloudGames.BuildingBlocks.Tests/
docs/architecture/
```

## Comandos

```powershell
dotnet restore
dotnet build --no-restore
dotnet test --no-build
dotnet pack src/FiapCloudGames.BuildingBlocks -c Release
```

## Documentação

- [Decisão dos building blocks](docs/architecture/shared-building-blocks.md)
- [Roteiro de migração](docs/architecture/migration-roadmap.md)
- [Arquitetura-alvo](docs/architecture/target-microservices.md)
- [Autenticação-alvo](docs/architecture/target-authentication.md)
