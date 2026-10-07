FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["BlazeTAK/BlazeTAK.Client/BlazeTAK.Client.csproj", "BlazeTAK/BlazeTAK.Client/"]
RUN dotnet restore "BlazeTAK/BlazeTAK.Client/BlazeTAK.Client.csproj"
COPY . .
WORKDIR "/src/BlazeTAK/BlazeTAK.Client"
RUN dotnet build "./BlazeTAK.Client.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./BlazeTAK.Client.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BlazeTAK.Client.dll"]
