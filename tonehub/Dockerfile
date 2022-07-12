FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["tonehub/tonehub.csproj", "tonehub/"]
RUN dotnet restore "tonehub/tonehub.csproj"
COPY . .
WORKDIR "/src/tonehub"
RUN dotnet build "tonehub.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "tonehub.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "tonehub.dll"]
