## 🛠️ Stack

| Camada | Tecnologia |
|---|---|
| Backend | ASP.NET Core 8 Web API |
| Frontend | Blazor Server |
| Dados | Entity Framework Core 8 + SQLite (dev) / PostgreSQL (prod) |
| Autenticação | OAuth 2.0 Authorization Code + PKCE (Spotify) |
| Docs da API | Swagger / OpenAPI |

## 🚀 Rodando localmente

Pré-requisitos: [.NET 8 SDK](https://dotnet.microsoft.com/download) e uma conta no [Spotify Developer Dashboard](https://developer.spotify.com/dashboard).

```bash
git clone https://github.com/gabrielteramae/music-discovery.git
cd music-discovery

# configure Spotify:ClientId em appsettings.Development.json

dotnet restore
dotnet build

# API
cd MusicDiscovery.Api && dotnet ef database update && dotnet run

# Blazor (outro terminal)
cd MusicDiscovery.Web && dotnet run
```

## 🗺️ Roadmap

- [ ] Job de sincronização automática (playlists + audio features)
- [ ] Emissão de JWT próprio pós-login
- [ ] Criptografia de tokens da Spotify em repouso
- [ ] Telas Blazor completas (login, listagem, comparação de playlists)

---

© 2026 Gabriel Teramae Chan
