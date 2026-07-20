# MusicDiscovery

![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-8.0-purple)
![Blazor](https://img.shields.io/badge/Blazor-Server-blue)
![Entity Framework](https://img.shields.io/badge/EF%20Core-8.0-blueviolet)
![SQLite](https://img.shields.io/badge/SQLite-Dev-lightgrey)

Aplicação Full-Stack para integração com o Spotify e gerenciamento de descobertas musicais.

## Sobre

Uma solução estruturada que combina uma Web API robusta em .NET com uma interface interativa em Blazor. O backend gerencia a autenticação OAuth 2.0 com PKCE junto ao Spotify e o acesso a dados via Entity Framework Core, enquanto o frontend consome os serviços de forma fluida.

## Funcionalidades

- Integração completa com a API do Spotify via OAuth 2.0 + PKCE
- Listagem e comparação de playlists
- Gerenciamento de dados com Entity Framework Core
- Documentação interativa da API com Swagger / OpenAPI

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
git clone [https://github.com/gabrielteramae/music-discovery.git](https://github.com/gabrielteramae/music-discovery.git)
cd music-discovery

# configure Spotify:ClientId em appsettings.Development.json

dotnet restore
dotnet build

# API
cd MusicDiscovery.Api && dotnet ef database update && dotnet run

# Blazor (outro terminal)
cd MusicDiscovery.Web && dotnet run

© 2026 Gabriel Teramae Chan
