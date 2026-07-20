# MusicDiscovery

App full C# (ASP.NET Core + Blazor) pra descobrir e organizar playlists usando a Spotify Web API.

## O que já tá pronto

- **Modelo de dados** (`MusicDiscovery.Api/Models/Entities.cs` + `Data/AppDbContext.cs`): User, Playlist, Track, AudioFeatures, com EF Core.
- **Autenticação Spotify (OAuth Authorization Code + PKCE)**: `Services/SpotifyAuthService.cs` + `Controllers/AuthController.cs`.
- **Cliente da Spotify Web API**: `Services/SpotifyApiService.cs` — playlists, tracks, liked songs, audio features, recomendações.
- **Motor de recomendação próprio**: `Services/RecommendationEngine.cs` — cosine similarity ponderada sobre o vetor de audio features, comparando candidatos com o centróide de uma playlist.
- **Organizador automático de playlists**: `Services/PlaylistOrganizerService.cs` — k-means simplificado que agrupa "Liked Songs" por vibe (energia/valência/dançabilidade/BPM) e sugere nomes de playlist.
- **Front Blazor Server**: `MusicDiscovery.Web` com uma página inicial já consumindo `/api/playlists/suggest-organization`.
- **Sincronização** (`Services/SyncService.cs`): puxa playlists, liked songs e audio features da Spotify e persiste no banco (upsert idempotente, não duplica em re-sync).
- **JWT próprio da API** (`Services/AppTokenService.cs`): emitido no fim do `AuthController.Callback`, valida via `Microsoft.AspNetCore.Authentication.JwtBearer`.
- **Criptografia dos tokens da Spotify em repouso** (`Services/AppTokenService.cs` → `TokenProtector`), usando o Data Protection API nativo do ASP.NET Core.
- **`PlaylistsController` protegido** com `[Authorize]` e consultas escopadas pelo usuário autenticado (via claim do JWT).

## O que falta (próximos passos, nessa ordem sugerida)

1. **Criar o app na Spotify** em https://developer.spotify.com/dashboard, pegar o `ClientId` e colocar em `appsettings.Development.json` (não versionado). Registrar o Redirect URI: `https://localhost:7050/api/auth/callback`.
2. **Trocar a `Jwt:SigningKey` do `appsettings.json`** por uma chave gerada de verdade antes de rodar (hoje é só um placeholder de exemplo) — use `dotnet user-secrets` em dev.
3. **`LikedTrack(UserId, TrackId)`**: hoje `Track` não tem vínculo direto com usuário, só `Playlist` tem. Funciona pra um usuário só rodando local, mas precisa dessa entidade pra isolar dados corretamente entre múltiplos usuários (ver comentário em `PlaylistsController.SuggestOrganization`).
4. **Refresh automático de token**: usar `ISpotifyAuthService.RefreshTokenAsync` quando `TokenExpiresAt` estiver perto de vencer, em vez de forçar novo login.
5. Completar as páginas Blazor: tela de login (redirecionar pra `/api/auth/login`), guardar o JWT recebido, lista de playlists, tela de recomendações por playlist.

## Como rodar localmente

Esse ambiente aqui (sandbox) não tem o SDK do .NET nem acesso ao NuGet, então o projeto não foi compilado — só escrito. Localmente, com o SDK do .NET 8 instalado:

```bash
cd MusicDiscovery
dotnet restore
dotnet build

# roda a API (porta 7050, conforme appsettings)
cd MusicDiscovery.Api
dotnet run

# em outro terminal, roda o Blazor (porta 7100)
cd ../MusicDiscovery.Web
dotnet run
```

A primeira execução da API cria o banco SQLite automaticamente na pasta do projeto — mas ainda precisa rodar as migrations do EF Core:

```bash
cd MusicDiscovery.Api
dotnet tool install --global dotnet-ef   # se ainda não tiver
dotnet ef migrations add InitialCreate
dotnet ef database update
```

## Stack

- ASP.NET Core 8 Web API + Blazor Server (net8.0)
- EF Core 8 + SQLite (dev) — troca fácil pra Postgres em produção
- Spotify Web API (OAuth 2.0 Authorization Code + PKCE)
- Swagger/OpenAPI habilitado em dev (`/swagger`)
