# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first to leverage Docker layer caching on restore.
# Restore the API project directly (not the .slnx): the solution also references
# the test project, which isn't part of the runtime image and isn't copied here.
# Restoring the API pulls in its transitive refs (Domain, Application, Infrastructure).
COPY src/TwitterClone.Domain/TwitterClone.Domain.csproj src/TwitterClone.Domain/
COPY src/TwitterClone.Application/TwitterClone.Application.csproj src/TwitterClone.Application/
COPY src/TwitterClone.Infrastructure/TwitterClone.Infrastructure.csproj src/TwitterClone.Infrastructure/
COPY src/TwitterClone.Api/TwitterClone.Api.csproj src/TwitterClone.Api/
RUN dotnet restore src/TwitterClone.Api/TwitterClone.Api.csproj

# Copy the rest and publish (restore is cached above).
COPY . .
RUN dotnet publish src/TwitterClone.Api/TwitterClone.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Render injects PORT at runtime; Program.cs binds to it. 8080 is the local default.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "TwitterClone.Api.dll"]
