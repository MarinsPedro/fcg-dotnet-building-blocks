# Building blocks compartilhados entre microsserviços

## Status deste documento

Este documento descreve a estratégia planejada para compartilhar recursos
técnicos entre os microsserviços da Fase 2.

A decisão inicial é manter **um único pacote NuGet**, organizado internamente
por módulos. A separação em vários pacotes será feita apenas se surgir uma
necessidade concreta.

Este repositório e pacote não representam um microsserviço. Eles não possuem
banco de dados, API própria ou deploy em produção.

## Decisão arquitetural

Será criado um repositório separado:

```text
Repositório: fcg-dotnet-building-blocks
Solution:    FiapCloudGames.BuildingBlocks.sln
Pacote:      FiapCloudGames.BuildingBlocks
```

Cada API ou Worker instalará somente esse pacote:

```xml
<PackageReference
    Include="FiapCloudGames.BuildingBlocks"
    Version="1.0.0" />
```

O pacote poderá conter apenas comportamentos técnicos comuns. Entidades,
contratos e regras de negócio continuarão pertencendo aos respectivos
microsserviços.

## Por que começar com um único pacote

Para o tamanho atual do projeto, um pacote oferece:

- publicação e versionamento mais simples;
- menor quantidade de pipelines;
- instalação padronizada em todas as aplicações;
- menos decisões prematuras sobre a divisão das bibliotecas;
- facilidade para mover os componentes comuns do projeto atual;
- possibilidade de separar os módulos posteriormente sem redesenhar tudo.

O custo aceito é que uma aplicação pode receber algumas dependências que não
utiliza diretamente. Esse custo é aceitável enquanto o pacote permanecer
pequeno e com finalidade exclusivamente técnica.

## Estrutura proposta

```text
fcg-dotnet-building-blocks/
├── FiapCloudGames.BuildingBlocks.sln
├── Directory.Build.props
├── Directory.Packages.props
├── README.md
├── src/
│   └── FiapCloudGames.BuildingBlocks/
│       ├── Authentication/
│       ├── Exceptions/
│       ├── Hosting/
│       ├── Messaging/
│       ├── Observability/
│       ├── Web/
│       └── FiapCloudGames.BuildingBlocks.csproj
└── tests/
    └── FiapCloudGames.BuildingBlocks.Tests/
```

Mesmo existindo um único projeto e um único NuGet, cada responsabilidade deve
ter seu próprio namespace.

```csharp
using FiapCloudGames.BuildingBlocks.Authentication;
using FiapCloudGames.BuildingBlocks.Hosting;
using FiapCloudGames.BuildingBlocks.Messaging;
using FiapCloudGames.BuildingBlocks.Observability;
using FiapCloudGames.BuildingBlocks.Web;
```

Essa separação interna permite dividir o pacote no futuro sem misturar as
responsabilidades desde o início.

## Módulos internos

### Authentication

Responsável pela autenticação técnica entre aplicações:

- configuração do JWT Bearer;
- validação de issuer, audience, assinatura e expiração;
- validação de `token_use=user` ou `token_use=service`;
- leitura de `client_id`, scopes e usuário somente na borda;
- autenticação obrigatória por padrão;
- registro de policies por tipo de token, cliente e scopes;
- obtenção e cache de access tokens técnicos;
- abstração para identificar a aplicação chamadora;
- respostas padronizadas de `401` e `403`.

Uma abstração comum pode representar o chamador:

```csharp
public interface ICallerContext
{
    string? TokenUse { get; }
    string? ClientId { get; }
    Guid? UserId { get; }
    IReadOnlyCollection<string> Scopes { get; }
    bool IsUser { get; }
    bool IsService { get; }
    bool IsAuthenticated { get; }
}
```

O nome `ICallerContext` é preferível a `ICurrentUserContext`, porque o chamador
pode ser um BFF, Gateway, API ou Worker, e não necessariamente um usuário.
Em APIs internas, tokens `service` nunca produzem `UserId`; o identificador do
usuário chega pelo contrato específico do endpoint, quando necessário.

O módulo não decide se um pedido pode ser alterado ou se um pagamento pode ser
processado. Essas decisões continuam no domínio responsável.

### Exceptions

Pode conter exceções técnicas e de aplicação que tenham a mesma semântica em
todos os serviços:

```text
AppException
ConflictException
ForbiddenException
NotFoundException
ValidationException
```

Essas exceções devem representar categorias de erro. Exceções específicas de
negócio permanecem no serviço, por exemplo:

```text
OrderAlreadyPaidException
GameUnavailableException
EntitlementAlreadyExistsException
```

### Hosting

Reúne a configuração padrão de inicialização:

- health checks;
- informações de nome e versão do serviço;
- configuração resiliente de `HttpClient`;
- valores padrão de timeouts;
- service discovery, quando for adotado;
- registro simplificado dos demais módulos.

Exemplo:

```csharp
builder.AddFiapServiceDefaults();
```

### Observability

Padroniza a observabilidade das aplicações:

- OpenTelemetry;
- traces;
- métricas;
- logs estruturados;
- `TraceId` e `CorrelationId`;
- propagação de correlação em HTTP e RabbitMQ;
- remoção de dados sensíveis dos logs.

O módulo nunca deve registrar:

- access token;
- refresh token;
- client secret;
- senha;
- chave privada;
- dados completos de cartão.

### Web

Contém recursos comuns às APIs ASP.NET Core:

- tratamento global de exceções;
- conversão de erros para `ProblemDetails`;
- configuração de controllers;
- configuração JSON;
- validação de entrada;
- middleware de correlation ID;
- logs padronizados de erros HTTP;
- extensions para OpenAPI.

Exemplo:

```csharp
builder.Services.AddFiapApiDefaults();

var app = builder.Build();

app.UseExceptionHandler();
app.UseFiapCorrelationId();
```

### Messaging, evolução futura

Quando RabbitMQ entrar no escopo, este módulo poderá padronizar a integração
com o broker e o MassTransit:

- configuração do broker;
- convenções para filas e exchanges;
- serialização;
- correlation e causation IDs;
- retry;
- dead-letter queue;
- filtros de consumo e publicação;
- observabilidade;
- configuração de outbox e inbox.

O módulo fornece a infraestrutura para publicar e consumir mensagens, mas não
é dono dos eventos de negócio.

Por exemplo, ele pode conter:

```csharp
public interface IIntegrationEventPublisher
{
    Task PublishAsync<T>(
        T message,
        CancellationToken cancellationToken = default);
}
```

Ele não deve conter eventos como:

```text
OrderPlaced
PaymentApproved
EntitlementGranted
UserRegistered
```

Cada evento pertence ao serviço que o produz e deve ser versionado junto de
seu contrato de integração.

Na primeira etapa de migração, esse módulo não será implementado.

## Configuração em uma API

Uma API poderá habilitar os módulos necessários no `Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddFiapServiceDefaults();

builder.Services.AddFiapApiDefaults();

builder.Services.AddFiapServiceAuthentication(
    builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseFiapCorrelationId();
app.UseAuthentication();
app.UseAuthorization();

app.MapFiapHealthChecks();
app.MapControllers();

app.Run();
```

Instalar o pacote não ativa todas as funcionalidades automaticamente. Cada
aplicação chama apenas os métodos de configuração que precisa.

Em uma evolução futura, um Worker sem endpoints HTTP poderá usar:

```csharp
builder.AddFiapServiceDefaults();
builder.Services.AddFiapServiceAuthentication(configuration);
builder.Services.AddFiapMessaging(configuration);
```

## O que pode ser compartilhado

Um componente pode entrar no pacote quando atender a estes critérios:

1. é técnico, e não uma regra de negócio;
2. possui o mesmo comportamento em vários serviços;
3. tem uma interface pequena e estável;
4. não depende de entidades de um domínio específico;
5. pode ser atualizado sem exigir o deploy simultâneo de todos os serviços;
6. sua centralização reduz duplicação relevante ou risco de inconsistência.

Exemplos adequados:

- autenticação de aplicação;
- serialização e validação de tokens;
- health checks;
- telemetria;
- correlation ID;
- `ProblemDetails`;
- convenções técnicas do RabbitMQ;
- abstração de relógio;
- abstração de geração de identificadores.

## O que não pode ser compartilhado

Não devem entrar no pacote:

- entidades como `User`, `Game`, `Order`, `Payment` e `Entitlement`;
- value objects específicos de um domínio;
- repositories;
- `DbContext` e migrations;
- regras de preço, promoção, compra ou pagamento;
- DTOs específicos dos endpoints;
- handlers de casos de uso;
- policies de autorização de negócio;
- todos os contratos de integração em um único assembly;
- configurações particulares de apenas um serviço.

Uma estrutura como esta deve ser evitada:

```text
FiapCloudGames.Common
├── User.cs
├── Game.cs
├── Order.cs
├── Payment.cs
├── OrderRepository.cs
└── PaymentRules.cs
```

Ela obrigaria diferentes serviços a evoluírem e serem publicados em conjunto,
criando um monólito distribuído.

## Persistência não será compartilhada inicialmente

O pacote inicial não deve referenciar EF Core nem Npgsql.

Cada microsserviço será responsável por:

- seu `DbContext`;
- suas entidades persistidas;
- suas migrations;
- seus repositories;
- seu banco ou schema;
- configurações específicas de índices e constraints.

Se no futuro houver convenções de persistência comprovadamente idênticas em
vários serviços, elas poderão ser avaliadas separadamente.

## Contratos de integração

Os contratos de eventos não entram no pacote geral de building blocks.

Inicialmente, o contrato pode permanecer no repositório do serviço produtor.
Quando consumidores em outros repositórios precisarem compilá-lo, há duas
opções:

1. publicar um pacote pequeno pertencente ao produtor, como
   `FiapCloudGames.Payments.IntegrationEvents`;
2. manter um schema versionado em AsyncAPI, JSON Schema ou Protobuf.

Essa necessidade não obriga a criar esses pacotes agora. A decisão será tomada
quando o primeiro contrato precisar ser consumido entre repositórios.

## Recursos comuns que não pertencem ao NuGet

Nem toda padronização deve ser implementada em código compartilhado.

| Responsabilidade | Local correto |
|---|---|
| CORS e rate limit externos | API Gateway |
| TLS, certificados e WAF | Gateway e infraestrutura |
| Secrets | Kubernetes Secret ou cofre |
| Usuários e emissão de tokens | Identity |
| Coleta central de telemetria | OpenTelemetry Collector |
| Filas, exchanges e credenciais | RabbitMQ e infraestrutura |
| Estrutura inicial de um serviço | Template de repositório |
| Pipelines de build e deploy | Templates de CI/CD |

## Template de microsserviço

Além do NuGet, poderá ser criado futuramente um template:

```powershell
dotnet new fcg-service --name FiapCloudGames.Orders
```

O template pode gerar:

```text
src/
├── FiapCloudGames.Orders.Api/
├── FiapCloudGames.Orders.Application/
├── FiapCloudGames.Orders.Domain/
├── FiapCloudGames.Orders.Infrastructure/
└── FiapCloudGames.Orders.Worker/

tests/
├── FiapCloudGames.Orders.UnitTests/
└── FiapCloudGames.Orders.IntegrationTests/
```

Também pode incluir `Dockerfile`, arquivos de configuração, pipeline e a
referência ao pacote `FiapCloudGames.BuildingBlocks`.

O template apenas cria a estrutura inicial. Ele não gera dependência de
execução entre os microsserviços.

## Versionamento e distribuição

O pacote deve ser publicado em um feed NuGet privado por uma pipeline. O feed
exato ainda pode ser escolhido entre GitHub Packages, Azure Artifacts ou outra
solução acessível ao projeto.

Será utilizado versionamento semântico:

```text
1.0.1 -> correção compatível
1.1.0 -> nova funcionalidade compatível
2.0.0 -> alteração incompatível
```

Cada repositório deve fixar explicitamente a versão utilizada, preferencialmente
no seu `Directory.Packages.props`:

```xml
<ItemGroup>
  <PackageVersion
      Include="FiapCloudGames.BuildingBlocks"
      Version="1.0.0" />
</ItemGroup>
```

Não devem ser utilizadas versões flutuantes como `1.*`. Um serviço atualiza o
pacote quando estiver pronto e não precisa ser implantado junto com os demais.

## Testes do pacote

O repositório deve possuir testes para os comportamentos compartilhados:

- conversão de exceções para `ProblemDetails`;
- propagação de correlation ID;
- validação de issuer e audience;
- validação de `client_id` e scopes;
- expiração e cache de tokens técnicos;
- remoção de dados sensíveis dos logs;
- propagação de contexto em mensagens;
- retry e comportamento de dead-letter;
- registro correto das extensions de configuração.

Os testes de regras de negócio continuam nos repositórios dos serviços.

## Migração dos componentes atuais

A extração deve ser incremental:

1. identificar componentes técnicos já usados por mais de uma aplicação;
2. remover dependências de domínio desses componentes;
3. mover autenticação, erros HTTP e observabilidade para os namespaces do novo
   pacote;
4. publicar a primeira versão no feed privado;
5. instalar a versão em um microsserviço piloto;
6. validar logs, autenticação, health checks e tratamento de erros;
7. adotar o pacote nos demais serviços, um de cada vez.

Mapeamento inicial sugerido:

| Componente atual | Módulo de destino |
|---|---|
| Tratamento global de exceções | `Web` |
| Fábrica de `ProblemDetails` | `Web` |
| Configuração de controllers e JSON | `Web` |
| Logging de erros HTTP | `Web` ou `Observability` |
| Contexto do chamador autenticado | `Authentication` |
| Validação de JWT técnico | `Authentication` |
| Health checks | `Hosting` |
| OpenTelemetry e correlação | `Observability` |
| Convenções do MassTransit | `Messaging` |

Componentes que ainda são usados por apenas uma aplicação podem permanecer
locais até existir uma segunda necessidade real.

## Quando dividir o pacote

Não existe necessidade de separar o NuGet agora. A divisão será considerada
quando ocorrer pelo menos uma destas situações:

- Workers recebem muitas dependências exclusivas de ASP.NET Core;
- nem todos os serviços usam RabbitMQ ou MassTransit;
- autenticação e mensageria passam a ter ciclos de versão diferentes;
- uma mudança pequena exige atualizar dependências sem relação com ela;
- o pacote fica pesado ou difícil de compreender;
- módulos passam a ser mantidos por equipes diferentes.

Como os namespaces já estarão separados, a evolução poderá ser:

```text
FiapCloudGames.BuildingBlocks
                  |
                  +-> FiapCloudGames.ServiceDefaults
                  +-> FiapCloudGames.AspNetCore
                  +-> FiapCloudGames.ServiceAuthentication
                  +-> FiapCloudGames.Messaging
```

Essa divisão futura é uma opção, não um requisito da Fase 2.

## Decisões pendentes que não bloqueiam a implementação

- provedor do feed NuGet privado;
- versão inicial `0.x` ou `1.0.0`;
- ferramenta exata de CI/CD;
- adoção imediata ou futura de um template `dotnet new`;
- formato de distribuição dos contratos de eventos entre repositórios.

## Resumo

O modelo inicial será:

```text
Um repositório de building blocks.

Uma solution.

Um projeto principal.

Um pacote NuGet privado.

Vários namespaces técnicos internos.

Nenhuma entidade ou regra de negócio compartilhada.

Separação em novos pacotes somente quando houver necessidade comprovada.
```
