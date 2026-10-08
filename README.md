# Music Discovery — playlists e recomendação com a API do Spotify

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat&logo=dotnet&logoColor=white)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-Web%20API-512BD4?style=flat&logo=dotnet&logoColor=white)
![Blazor](https://img.shields.io/badge/Blazor-Server-512BD4?style=flat&logo=blazor&logoColor=white)
![SQLite](https://img.shields.io/badge/SQLite-EF%20Core%208-003B57?style=flat&logo=sqlite&logoColor=white)
![Spotify](https://img.shields.io/badge/Spotify-OAuth%20PKCE-1DB954?style=flat&logo=spotify&logoColor=white)

API e um Blazor Server que entram na conta Spotify, sincronizam playlists e músicas curtidas, e ranqueiam faixas pela similaridade das audio features. O banco no código é só SQLite. Não há provider PostgreSQL, apesar do comentário no `Program.cs`.

| Decisão | No código |
|---|---|
| Login | Authorization Code + PKCE em `GET /api/auth/login` e callback em `GET /api/auth/callback`. Não há client secret no `appsettings`. |
| Tokens da Spotify | Access e refresh são gravados com Data Protection (`TokenProtector`), não em texto puro. |
| Sessão da API | Depois do callback a API emite um JWT próprio (`AppTokenService`, HMAC-SHA256). |
| Recomendação | `RecommendationEngine` tira o centróide das features da playlist e ordena candidatos por cosseno. |

O `PlaylistsController` avisa uma limitação real: `Track` não tem `UserId`. `suggest-organization` lê todas as faixas com audio features, não só as do usuário logado.

## Stack

- .NET 8 (`net8.0`)
- API: ASP.NET Core, EF Core 8.0.8, SQLite, JWT Bearer, Swagger em Development
- Web: Blazor Server (`AddServerSideBlazor`)
- Spotify Web API, escopos `playlist-read-private`, `playlist-modify-private`, `user-library-read`, `user-top-read`

## Estrutura

```
music-discovery/
├── MusicDiscovery.sln
├── MusicDiscovery.Api/
│   ├── Program.cs
│   ├── appsettings.json
│   ├── appsettings.Development.json.example
│   ├── Controllers/     AuthController, PlaylistsController
│   ├── Data/AppDbContext.cs
│   ├── Models/Entities.cs
│   └── Services/        auth Spotify, sync, recomendação, organizer, JWT
└── MusicDiscovery.Web/
    ├── Program.cs
    ├── Pages/Dashboard.razor
    ├── Pages/_Host.cshtml
    └── Shared/          App.razor, MainLayout.razor
```

Entidades: `AppUser`, `Playlist`, `Track`, `PlaylistTrack`, `AudioFeatures`.

Endpoints de playlist (exigem o JWT da API):

- `GET /api/playlists`
- `GET /api/playlists/{playlistId}/recommendations?take=15`
- `GET /api/playlists/suggest-organization?clusters=4`

Não há pasta `Migrations`. O `Program.cs` não chama `EnsureCreated` nem `Migrate`.

## Como rodar

Pré-requisitos: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) e um app no [Spotify Developer Dashboard](https://developer.spotify.com/dashboard).

```bash
git clone https://github.com/gabrielteramae/music-discovery.git
cd music-discovery
cp MusicDiscovery.Api/appsettings.Development.json.example MusicDiscovery.Api/appsettings.Development.json
```

No exemplo, preencha `Spotify:ClientId` e troque `Jwt:SigningKey`. O redirect configurado é `https://localhost:7050/api/auth/callback`. Cadastre essa URL no dashboard da Spotify. O arquivo de Development está no `.gitignore`.

O repositório não traz `launchSettings.json`. Suba API e Blazor nas portas que o `appsettings` já espera (`BlazorClientUrl` é `https://localhost:7100`; o `HttpClient` do Blazor usa `https://localhost:7050/`):

```bash
dotnet dev-certs https --trust
dotnet ef migrations add Initial --project MusicDiscovery.Api
dotnet ef database update --project MusicDiscovery.Api
dotnet run --project MusicDiscovery.Api --urls "https://localhost:7050"
```

O pacote `Microsoft.EntityFrameworkCore.Design` está no csproj; as migrations acima não existem no git e precisam ser geradas na máquina. Sem elas, o `SaveChanges` do login não tem schema.

Em outro terminal:

```bash
dotnet run --project MusicDiscovery.Web --urls "https://localhost:7100"
```

O login começa em `https://localhost:7050/api/auth/login`. O callback sincroniza a biblioteca (`SyncUserLibraryAsync`) e devolve o JWT no corpo da resposta.

O `Dashboard.razor` chama `api/playlists/suggest-organization` sem header `Authorization`. Esse endpoint está com `[Authorize]`, então a página não autentica sozinha.

Swagger da API só aparece em Development.

---

© 2026 Gabriel Teramae Chan
