# Multi-stage build: compile with the full SDK image, run on the much
# smaller ASP.NET runtime-only image (SDK image is ~800MB+, runtime-only
# is a fraction of that -- no reason to ship the compiler in production).

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY *.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "NuclearOutagePlatform.dll"]
