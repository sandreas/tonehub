#!/usr/bin/env sh
rm -f Migrations/*.cs var/db/app.db
dotnet ef migrations add InitialCreate \
 && dotnet ef database update