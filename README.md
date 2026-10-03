# FiapCloudGames.BuildingBlocks

Pacote privado dos componentes técnicos compartilhados entre as APIs do FIAP
Cloud Games.

O pacote não contém entidades, repositories, `DbContext`, migrations ou regras
de negócio.

## Funcionalidades

- validação de JWT por Authority/JWKS e `token_use`;
- somente tokens `user` e `service`, sem token exchange;
- `ICallerContext` com tipo do token, `client_id`, scopes e usuário da borda;
- autenticação obrigatória por padrão, com `AllowAnonymous` explícito;
- policies por tipo de token, cliente e scopes;
- cliente Client Credentials com cache de token;
- tratamento global de exceções com `IExceptionHandler`;
- respostas `application/problem+json` padronizadas;
- validação MVC padronizada;
- correlation ID seguro e propagado na resposta;
- logging estruturado de respostas 4xx;
- health checks;
- configuração de trace e span nos logs.

A versão atual cobre HTTP, autenticação e observabilidade básica. Recursos
adicionais só entram quando forem compartilháveis e não carregarem regras de
domínio. As decisões de RabbitMQ, Kubernetes e topologia da plataforma ficam na
[documentação central](https://github.com/MarinsPedro/fcg-microservices-docs).

## Instalação

Consulte [Consumo do pacote privado](docs/package-consumption.md) para configurar
o GitHub Packages sem gravar tokens no repositório.

```xml
<PackageReference Include="FiapCloudGames.BuildingBlocks" Version="0.2.0" />
```

## Arquitetura do pacote

Building Blocks não é um bounded context e, por isso, não possui camadas
`Domain`, `Application`, `Infrastructure` e `Presentation`. O equivalente à
Clean Architecture neste pacote é o isolamento entre módulos técnicos:

```text
Authentication ──> Exceptions
Web ──────────────> Exceptions + Observability
Hosting            sem dependências internas
Observability      sem dependências internas
```

Todos continuam no mesmo assembly e no mesmo NuGet. Testes de arquitetura
fiscalizam a direção dessas dependências, a ausência de persistência e a
correspondência entre pastas e namespaces.

Consulte [Arquitetura do código](docs/architecture/code-architecture.md).

## Configuração da API

```csharp
using FiapCloudGames.BuildingBlocks.Authentication;
using FiapCloudGames.BuildingBlocks.Hosting;
using FiapCloudGames.BuildingBlocks.Web;

var builder = WebApplication.CreateBuilder(args);

builder.AddFiapServiceDefaults();

builder.Services.AddControllers();
builder.Services.AddFiapApiDefaults();
builder.Services.AddFiapServiceAuthentication(builder.Configuration);

// Somente quando a aplicação precisar chamar outra API.
builder.Services.AddFiapServiceTokenProvider(builder.Configuration);

var app = builder.Build();

app.UseFiapApiDefaults();
app.UseAuthentication();
app.UseAuthorization();

app.MapFiapHealthChecks().AllowAnonymous();
app.MapControllers();

await app.RunAsync();
```

`UseFiapApiDefaults` deve ser chamado antes de autenticação e autorização para
padronizar também as respostas `401` e `403` sem corpo.

## Configuração

```json
{
  "Authentication": {
    "Authority": "https://identity.internal",
    "Issuer": "fiap-cloud-games-identity",
    "Audience": "orders-api",
    "AllowedTokenUses": [
      "service"
    ],
    "RequireHttpsMetadata": true,
    "ClockSkew": "00:00:30"
  },
  "ServiceAuthentication": {
    "TokenEndpoint": "https://identity.internal/connect/token",
    "ClientId": "orders-api",
    "ClientSecret": "fornecido-por-secret-ou-user-secrets",
    "RequireHttps": true,
    "RefreshBeforeExpiration": "00:00:30"
  }
}
```

APIs internas aceitam apenas `service` por padrão. Um BFF ou API Gateway que
valida tokens externos deve configurar `"AllowedTokenUses": ["user"]`.
Tokens `user` nunca são encaminhados às APIs internas.

O `ClientSecret` nunca deve ser commitado. Em desenvolvimento, use user-secrets;
nos ambientes de execução, use o mecanismo de secrets disponível.

## Policies técnicas

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        "ProcessPayments",
        policy => policy
            .RequireAuthenticatedUser()
            .RequireServiceToken()
            .RequireClients("orders-api")
            .RequireScopes("payments.process"));
});
```

```csharp
[Authorize(Policy = "ProcessPayments")]
[HttpPost("/internal/payments")]
public IActionResult ProcessPayment() => Ok();
```

## Obtenção de token técnico

```csharp
var token = await tokenProvider.GetAccessTokenAsync(
    audience: "payments-api",
    scopes: "payments.process",
    cancellationToken);
```

O provider normaliza os scopes e reutiliza o token até próximo da expiração.
Ele nunca registra o token ou o client secret.

## Usuário nas chamadas internas

O BFF ou Gateway obtém o usuário do `sub` do token `user`, remove qualquer
identificador de usuário fornecido externamente e cria um novo contrato para a
API interna:

```http
POST /internal/orders
Authorization: Bearer <token-service-do-bff>
Content-Type: application/json

{
  "userId": "47de4c37-34ab-4084-a293-e46229c34aad",
  "gameId": "80fdc742-e7c1-4564-a269-8d52784bec44"
}
```

O `userId` pode ficar no body ou na rota conforme o recurso. Query string fica
reservada a filtros. A API confia nesse valor somente depois de validar o token
`service`, a audience, o scope e o `client_id` autorizado.

O pacote não lê `userId` do body, rota ou query. Esse valor pertence ao contrato
do endpoint. Em tokens `service`, `ICallerContext.UserId` é sempre `null`.

## Desenvolvimento

```powershell
dotnet restore --configfile NuGet.Config
dotnet build --no-restore
dotnet test --no-build --no-restore
dotnet format FiapCloudGames.BuildingBlocks.slnx --verify-no-changes --no-restore
dotnet pack src/FiapCloudGames.BuildingBlocks -c Release
```

## Documentação

- [Arquitetura do código](docs/architecture/code-architecture.md)
- [Consumo do pacote](docs/package-consumption.md)
- [Documentação da plataforma](https://github.com/MarinsPedro/fcg-microservices-docs)
- [Decisão arquitetural sobre o pacote](https://github.com/MarinsPedro/fcg-microservices-docs/blob/main/decisions/ADR-005-building-blocks-package.md)
