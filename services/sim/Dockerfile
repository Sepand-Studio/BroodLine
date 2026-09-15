FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Both trees. A context scoped to services/sim/ cannot see engine/ and the
# restore fails with a path error that reads like a typo.
COPY engine/ engine/
COPY services/sim/ services/sim/
RUN dotnet publish services/sim/Broodline.Sim.Service.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Broodline.Sim.Service.dll"]
