# Arquitetura do código do Building Blocks

O Building Blocks segue o mesmo nível de disciplina arquitetural das
aplicações, mas não replica suas camadas. Ele não é um bounded context, não
executa casos de uso e não possui domínio próprio. Portanto, criar projetos
`Domain`, `Application`, `Infrastructure` e `Presentation` aqui produziria
camadas artificiais.

A unidade de organização é o módulo técnico. Inicialmente todos os módulos
permanecem em um único projeto, assembly e pacote NuGet.

## Estrutura

```text
src/FiapCloudGames.BuildingBlocks/
  Authentication/   validação e obtenção de tokens, caller e policies
  Exceptions/       categorias de falha comuns às aplicações
  Hosting/          defaults do host e health checks
  Observability/    correlação e contexto observável
  Web/              pipeline ASP.NET Core e Problem Details

tests/
  FiapCloudGames.BuildingBlocks.Tests/
  FiapCloudGames.BuildingBlocks.ArchitectureTests/
```

RabbitMQ será adicionado em `Messaging` somente depois das decisões específicas
de cliente, retry, dead-letter e outbox/inbox. Manifests Kubernetes continuam
fora do NuGet.

## Dependências internas

```text
Exceptions <── Authentication
     ↑
     └──────── Web ──> Observability

Hosting       (independente)
Observability (independente)
```

| Módulo | Pode depender internamente de | Não contém |
|---|---|---|
| Authentication | Exceptions | autorização de negócio e usuários persistidos |
| Exceptions | nenhum | exceções específicas de um domínio |
| Hosting | nenhum | pipeline HTTP específico ou regra de aplicação |
| Observability | nenhum | dados sensíveis ou decisão de negócio |
| Web | Exceptions e Observability | Controllers e contratos de endpoints |

Instalar o pacote não ativa todos os módulos. Cada host chama explicitamente as
extensions que utiliza.

## Regras obrigatórias

- existe um único projeto de produção e um único `PackageId`;
- namespaces acompanham a pasta do módulo;
- nenhuma referência a EF Core, Npgsql, MongoDB ou Dapper;
- nenhuma entidade, repository, `DbContext`, migration ou Controller público;
- nenhuma referência a projetos de microsserviços;
- nenhum evento ou DTO específico de Identity, Catalog, Promotions, Orders,
  Payments, Library ou Notifications;
- APIs públicas existentes permanecem compatíveis dentro da mesma linha de
  versão;
- implementações são `internal` quando o consumidor necessita apenas da
  abstração ou extension method;
- tokens, senhas, client secrets e chaves nunca entram em logs.

Essas regras são verificadas por
`FiapCloudGames.BuildingBlocks.ArchitectureTests`.

## Onde implementar uma mudança

| Mudança | Local |
|---|---|
| claim, validação JWT, policy ou client credentials | Authentication |
| categoria de falha comum e independente de transporte | Exceptions |
| configuração comum do processo/host | Hosting |
| correlação, tracing, métricas ou logging técnico | Observability |
| Problem Details, validação MVC ou middleware HTTP | Web |
| regra de negócio | serviço proprietário, nunca neste pacote |
| contrato HTTP específico | Presentation do serviço proprietário |
| evento de integração | contrato do serviço produtor |
| EF Core, repository ou migration | Infrastructure do serviço proprietário |
| manifests e recursos Kubernetes | repositório da aplicação ou `fcg-platform` |

## API pública e encapsulamento

São públicos somente os tipos que os serviços consumidores precisam usar:
interfaces, opções, constantes, modelos de erro seguros e extension methods.
Handlers, middleware, validadores e providers concretos permanecem `internal`
sempre que forem detalhes de implementação.

Alterar namespace, assinatura, semântica ou valor padrão de um tipo público
exige avaliar compatibilidade e versionamento semântico. Funcionalidades novas e
compatíveis incrementam a versão minor enquanto o pacote estiver em `0.x`;
quebras deliberadas exigem uma nova linha de versão e roteiro de migração.

## Testes

- testes unitários cobrem comportamento de cada módulo;
- testes de arquitetura fiscalizam estrutura e fronteiras;
- testes de integração do serviço consumidor confirmam o pipeline real;
- o CI executa restore, format, build, tests e pack;
- a publicação ocorre somente por tag semântica.

Comandos locais:

```powershell
dotnet restore FiapCloudGames.BuildingBlocks.slnx --configfile NuGet.Config
dotnet format FiapCloudGames.BuildingBlocks.slnx --verify-no-changes --no-restore
dotnet build FiapCloudGames.BuildingBlocks.slnx --no-restore
dotnet test FiapCloudGames.BuildingBlocks.slnx --no-build --no-restore
dotnet pack src/FiapCloudGames.BuildingBlocks -c Release --no-restore
```

## Quando separar o NuGet

A separação deve ocorrer por necessidade comprovada, não para imitar as camadas
de um microsserviço. Sinais relevantes incluem ciclos de versão independentes,
dependências pesadas que poucos consumidores utilizam, suporte a Workers sem
ASP.NET Core ou propriedade por equipes diferentes.
