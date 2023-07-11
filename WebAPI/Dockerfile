FROM mcr.microsoft.com/dotnet/sdk:7.0
COPY bin/Release/net7.0/publish/ App/
EXPOSE 8080
WORKDIR /App
ENTRYPOINT ["dotnet", "WebAPI.dll"]
