FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["BankIdDemo.Backend/BankIdDemo.Backend.csproj", "BankIdDemo.Backend/"]
RUN dotnet restore "BankIdDemo.Backend/BankIdDemo.Backend.csproj"
COPY . .
WORKDIR "/src/BankIdDemo.Backend"
RUN dotnet build "BankIdDemo.Backend.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "BankIdDemo.Backend.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BankIdDemo.Backend.dll"]
