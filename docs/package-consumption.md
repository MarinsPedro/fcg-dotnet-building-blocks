# Consumo do pacote privado

## Fonte

O pacote será publicado no GitHub Packages:

```text
https://nuget.pkg.github.com/MarinsPedro/index.json
```

## Configuração local

Crie um Personal Access Token com permissão mínima `read:packages`. Não coloque
o token no `NuGet.Config` versionado.

Registre a fonte no perfil do desenvolvedor:

```powershell
dotnet nuget add source `
  https://nuget.pkg.github.com/MarinsPedro/index.json `
  --name github `
  --username MarinsPedro `
  --password SEU_TOKEN `
  --store-password-in-clear-text
```

O armazenamento em texto claro ocorre no arquivo de configuração do perfil do
desenvolvedor, fora do repositório. Em máquinas compartilhadas, prefira um
credential provider ou secret temporário.

## Referência no serviço

No `Directory.Packages.props`:

```xml
<ItemGroup>
  <PackageVersion
      Include="FiapCloudGames.BuildingBlocks"
      Version="0.3.0" />
</ItemGroup>
```

No `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="FiapCloudGames.BuildingBlocks" />
</ItemGroup>
```

Não use versões flutuantes como `0.*`.

## Alternar entre projeto local e pacote publicado

Os serviços do workspace mantêm a flag no próprio `Directory.Build.props`. No
ambiente acadêmico atual, ela assume `true` quando não é informada para compilar
diretamente o checkout irmão do Building Blocks. Com `false`, os `.csproj` usam
o pacote publicado definido em `Directory.Packages.props`.

```powershell
# Desenvolvimento no workspace: usa o projeto local por padrão.
dotnet build

# Pacote publicado: desabilita explicitamente a referência local.
dotnet build -p:UseLocalBuildingBlocks=false
```

A saída do build local deve exibir a mensagem `Usando
FiapCloudGames.BuildingBlocks local` e o caminho resolvido. Além disso, a lista
de dependências do projeto mostra uma referência de projeto, e não o pacote.
Alterar apenas o `appsettings.json` não funciona para esse propósito: a escolha
entre NuGet e projeto local ocorre durante o MSBuild, antes da aplicação iniciar.

## GitHub Actions dos serviços consumidores

O workflow consumidor precisa de `packages: read`. O `setup-dotnet` pode
configurar a fonte autenticada com o `GITHUB_TOKEN`. Como o checkout irmão não
existe no runner isolado, o workflow também deve definir
`UseLocalBuildingBlocks=false`:

```yaml
permissions:
  contents: read
  packages: read

env:
  UseLocalBuildingBlocks: false

steps:
  - uses: actions/checkout@v7

  - uses: actions/setup-dotnet@v6
    with:
      dotnet-version: 10.0.401
      source-url: https://nuget.pkg.github.com/MarinsPedro/index.json
    env:
      NUGET_AUTH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
```

Se o pacote não herdar acesso para o repositório consumidor, conceda acesso ao
repositório nas configurações do package no GitHub. Não crie tokens de escrita
para pipelines que apenas restauram pacotes.

## Publicação

O workflow `publish-package.yml` publica somente tags no formato:

```text
v1.2.3
```

Exemplo para a primeira versão:

```powershell
git tag v0.1.0
git push origin v0.1.0
```

Criar a tag é uma decisão explícita de release; commits comuns e pull requests
executam build, testes e pack, mas não publicam o pacote.
