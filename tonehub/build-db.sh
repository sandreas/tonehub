#!/usr/bin/env sh
rm -f Migrations/*.cs var/db/tonehub.db
dotnet ef migrations add InitialCreate