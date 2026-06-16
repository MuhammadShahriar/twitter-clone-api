# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution + project files first to leverage Docker layer caching on restore.
COPY twitter-clone-api.slnx ./
COPY src/TwitterClone.Domain/TwitterClone.Domain.csproj src/TwitterClone.Domain/
COPY src/TwitterClone.Application/TwitterClone.Application.csproj src/TwitterClone.Application/
COPY src/TwitterClone.Infrastructure/TwitterClone.Infrastructure.csproj src/TwitterClone.Infrastructure/
COPY src/TwitterClone.Api/TwitterClone.Api.csproj src/TwitterClone.Api/
RUN dotnet restore twitter-clone-api.slnx

# Copy the rest and publish.
COPY . .
RUN dotnet publish src/TwitterClone.Api/TwitterClone.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Render injects PORT at runtime; Program.cs binds to it. 8080 is the local default.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "TwitterClone.Api.dll"]
